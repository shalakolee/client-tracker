using System.Linq;
using ClientTracker.Models;
using ContactModel = ClientTracker.Models.Contact;
using MySqlConnector;
using SQLite;
using SQLitePCL;

namespace ClientTracker.Services;

public class DatabaseService
{
    private SQLiteAsyncConnection _db;
    private readonly string _dbPath;
    private bool _initialized;
    private bool _remoteSettingsLoaded;
    private DatabaseConnectionSettings? _remoteSettings;
    private string? _remoteConnectionString;
    private bool _remoteEnabled;
    private bool _remoteInitialSyncAttempted;
    private readonly SemaphoreSlim _remoteGate = new(1, 1);
    private readonly object _remotePushScheduleGate = new();
    private CancellationTokenSource? _remotePushCts;

    public DatabaseService()
    {
        Batteries_V2.Init();
        _dbPath = GetProjectDatabasePath();
        _db = new SQLiteAsyncConnection(_dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache, false);
    }

    public string DatabasePath => _remoteEnabled && _remoteSettings is not null
        ? _remoteSettings.GetDisplayName()
        : _dbPath;

    private static string GetProjectDatabasePath()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var dataDir = Path.Combine(projectRoot, "data");
        Directory.CreateDirectory(dataDir);
        return Path.Combine(dataDir, "client-tracker.db3");
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized)
        {
            return;
        }

        await EnsureRemoteSettingsAsync();
        await EnableForeignKeysAsync();
        await _db.CreateTableAsync<Client>();
        await _db.CreateTableAsync<Sale>();
        await _db.CreateTableAsync<ContactModel>();
        await _db.CreateTableAsync<Payment>();
        await _db.CreateTableAsync<AuditLog>();
        await EnsureClientSchemaAsync();
        await EnsureContactSchemaAsync();
        await EnsureSaleSchemaAsync();
        await EnsurePaymentSchemaAsync();
        await CleanupOrphanedRecordsAsync();
        await EnsureForeignKeyConstraintsAsync();
        await EnsureIndexesAsync();
        await EnsurePaymentsForExistingSalesAsync();
        _initialized = true;

        if (_remoteEnabled && !_remoteInitialSyncAttempted)
        {
            _remoteInitialSyncAttempted = true;
            try
            {
                await SyncFromRemoteMySqlAsync();
            }
            catch (Exception ex)
            {
                StartupLog.Write(ex, "RemoteMySql.InitialSync");
            }
        }
    }

    private async Task EnsureRemoteSettingsAsync()
    {
        if (_remoteSettingsLoaded)
        {
            return;
        }

        await ReloadRemoteSettingsAsync();
        _remoteSettingsLoaded = true;
    }

    public async Task ReloadRemoteSettingsAsync()
    {
        _remoteSettings = await DatabaseConnectionSettings.LoadAsync();
        _remoteConnectionString = _remoteSettings.BuildConnectionString();
        _remoteEnabled = _remoteSettings.UseRemoteMySql && !string.IsNullOrWhiteSpace(_remoteConnectionString);
    }

    public async Task<bool> TestRemoteMySqlConnectionAsync()
    {
        await EnsureRemoteSettingsAsync();
        if (string.IsNullOrWhiteSpace(_remoteConnectionString))
        {
            return false;
        }

        try
        {
            await _remoteGate.WaitAsync();
            await using var conn = new MySqlConnection(_remoteConnectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand("SELECT 1", conn);
            _ = await cmd.ExecuteScalarAsync();
            return true;
        }
        catch (Exception ex)
        {
            StartupLog.Write(ex, "RemoteMySql.TestConnection");
            return false;
        }
        finally
        {
            _remoteGate.Release();
        }
    }

    private void QueueRemotePush()
    {
        if (!_remoteEnabled)
        {
            return;
        }

        lock (_remotePushScheduleGate)
        {
            _remotePushCts?.Cancel();
            _remotePushCts?.Dispose();
            _remotePushCts = new CancellationTokenSource();
            var token = _remotePushCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(1500, token);
                    await PushLocalDataToRemoteMySqlAsync();
                }
                catch (OperationCanceledException)
                {
                    // ignored
                }
                catch (Exception ex)
                {
                    StartupLog.Write(ex, "RemoteMySql.ScheduledPush");
                }
            }, token);
        }
    }

    public async Task PushLocalDataToRemoteMySqlAsync(bool overwriteRemote = false)
    {
        await EnsureInitializedAsync();
        await EnsureRemoteSettingsAsync();
        if (string.IsNullOrWhiteSpace(_remoteConnectionString))
        {
            return;
        }

        await _remoteGate.WaitAsync();
        try
        {
            var clients = await _db.Table<Client>().ToListAsync();
            var contacts = await _db.Table<ContactModel>().ToListAsync();
            var sales = await _db.Table<Sale>().ToListAsync();
            var payments = await _db.Table<Payment>().ToListAsync();
            var audits = await _db.Table<AuditLog>().ToListAsync();

            await using var mysql = new MySqlConnection(_remoteConnectionString);
            await mysql.OpenAsync();
            await using var tx = await mysql.BeginTransactionAsync();

            await EnsureMySqlSchemaAsync(mysql, tx);
            if (overwriteRemote)
            {
                await ExecMySqlAsync(mysql, tx, "SET FOREIGN_KEY_CHECKS=0;");
                await ExecMySqlAsync(mysql, tx, "TRUNCATE TABLE AuditLog;");
                await ExecMySqlAsync(mysql, tx, "TRUNCATE TABLE Payment;");
                await ExecMySqlAsync(mysql, tx, "TRUNCATE TABLE Sale;");
                await ExecMySqlAsync(mysql, tx, "TRUNCATE TABLE Contact;");
                await ExecMySqlAsync(mysql, tx, "TRUNCATE TABLE Client;");
                await ExecMySqlAsync(mysql, tx, "SET FOREIGN_KEY_CHECKS=1;");

                await InsertClientsAsync(mysql, tx, clients, upsert: false);
                await InsertContactsAsync(mysql, tx, contacts, upsert: false);
                await InsertSalesAsync(mysql, tx, sales, upsert: false);
                await InsertPaymentsAsync(mysql, tx, payments, upsert: false);
                await InsertAuditLogsAsync(mysql, tx, audits, upsert: false);
            }
            else
            {
                await InsertClientsAsync(mysql, tx, clients, upsert: true);
                await InsertContactsAsync(mysql, tx, contacts, upsert: true);
                await InsertSalesAsync(mysql, tx, sales, upsert: true);
                await InsertPaymentsAsync(mysql, tx, payments, upsert: true);
                await InsertAuditLogsAsync(mysql, tx, audits, upsert: true);
            }

            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            StartupLog.Write(ex, "RemoteMySql.PushLocalData");
            throw;
        }
        finally
        {
            _remoteGate.Release();
        }
    }

    public async Task SyncFromRemoteMySqlAsync(bool replaceLocal = false)
    {
        await EnsureInitializedAsync();
        await EnsureRemoteSettingsAsync();
        if (string.IsNullOrWhiteSpace(_remoteConnectionString))
        {
            return;
        }

        await _remoteGate.WaitAsync();
        try
        {
            await using var mysql = new MySqlConnection(_remoteConnectionString);
            await mysql.OpenAsync();

            var clients = await QueryClientsAsync(mysql);
            var contacts = await QueryContactsAsync(mysql);
            var sales = await QuerySalesAsync(mysql);
            var payments = await QueryPaymentsAsync(mysql);
            var audits = await QueryAuditLogsAsync(mysql);

            await _db.ExecuteAsync("PRAGMA foreign_keys = OFF");
            if (replaceLocal)
            {
                await _db.ExecuteAsync("DELETE FROM AuditLog");
                await _db.ExecuteAsync("DELETE FROM Payment");
                await _db.ExecuteAsync("DELETE FROM Sale");
                await _db.ExecuteAsync("DELETE FROM Contact");
                await _db.ExecuteAsync("DELETE FROM Client");

                await _db.InsertAllAsync(clients);
                await _db.InsertAllAsync(contacts);
                await _db.InsertAllAsync(sales);
                await _db.InsertAllAsync(payments);
                await _db.InsertAllAsync(audits);
            }
            else
            {
                await _db.RunInTransactionAsync(conn =>
                {
                    foreach (var client in clients)
                    {
                        conn.InsertOrReplace(client);
                    }

                    foreach (var contact in contacts)
                    {
                        conn.InsertOrReplace(contact);
                    }

                    foreach (var sale in sales)
                    {
                        conn.InsertOrReplace(sale);
                    }

                    foreach (var payment in payments)
                    {
                        conn.InsertOrReplace(payment);
                    }

                    foreach (var audit in audits)
                    {
                        conn.InsertOrReplace(audit);
                    }
                });
            }

            await _db.ExecuteAsync("PRAGMA foreign_keys = ON");

            _initialized = false;
            await EnsureInitializedAsync();
        }
        catch (Exception ex)
        {
            StartupLog.Write(ex, "RemoteMySql.SyncFromRemote");
            throw;
        }
        finally
        {
            _remoteGate.Release();
        }
    }

    private static async Task ExecMySqlAsync(MySqlConnection conn, MySqlTransaction tx, string sql)
    {
        await using var cmd = new MySqlCommand(sql, conn, tx);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task EnsureMySqlSchemaAsync(MySqlConnection conn, MySqlTransaction tx)
    {
        await ExecMySqlAsync(conn, tx, """
            CREATE TABLE IF NOT EXISTS Client (
                Id INT NOT NULL,
                Name VARCHAR(255) NOT NULL,
                AddressLine1 VARCHAR(255) NOT NULL DEFAULT '',
                AddressLine2 VARCHAR(255) NOT NULL DEFAULT '',
                City VARCHAR(128) NOT NULL DEFAULT '',
                StateProvince VARCHAR(128) NOT NULL DEFAULT '',
                PostalCode VARCHAR(64) NOT NULL DEFAULT '',
                Country VARCHAR(128) NOT NULL DEFAULT '',
                TaxId VARCHAR(128) NOT NULL DEFAULT '',
                ContactName VARCHAR(255) NOT NULL DEFAULT '',
                ContactEmail VARCHAR(255) NOT NULL DEFAULT '',
                ContactPhone VARCHAR(255) NOT NULL DEFAULT '',
                CreatedUtc DATETIME(6) NOT NULL,
                UpdatedUtc DATETIME(6) NOT NULL,
                IsDeleted TINYINT(1) NOT NULL DEFAULT 0,
                DeletedUtc DATETIME(6) NULL,
                PRIMARY KEY (Id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            """);

        await ExecMySqlAsync(conn, tx, """
            CREATE TABLE IF NOT EXISTS Contact (
                Id INT NOT NULL,
                ClientId INT NOT NULL,
                Name VARCHAR(255) NOT NULL,
                Email VARCHAR(255) NOT NULL DEFAULT '',
                Phone VARCHAR(255) NOT NULL DEFAULT '',
                Notes TEXT NOT NULL,
                CreatedUtc DATETIME(6) NOT NULL,
                UpdatedUtc DATETIME(6) NOT NULL,
                IsDeleted TINYINT(1) NOT NULL DEFAULT 0,
                DeletedUtc DATETIME(6) NULL,
                PRIMARY KEY (Id),
                INDEX IX_Contact_ClientId (ClientId),
                CONSTRAINT FK_Contact_Client FOREIGN KEY (ClientId) REFERENCES Client(Id) ON DELETE RESTRICT ON UPDATE RESTRICT
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            """);

        await ExecMySqlAsync(conn, tx, """
            CREATE TABLE IF NOT EXISTS Sale (
                Id INT NOT NULL,
                ClientId INT NOT NULL,
                ContactId INT NOT NULL DEFAULT 0,
                InvoiceNumber VARCHAR(255) NOT NULL DEFAULT '',
                SaleDate DATE NOT NULL,
                Amount DECIMAL(18,2) NOT NULL,
                CommissionPercent DECIMAL(18,2) NOT NULL,
                CreatedUtc DATETIME(6) NOT NULL,
                UpdatedUtc DATETIME(6) NOT NULL,
                IsDeleted TINYINT(1) NOT NULL DEFAULT 0,
                DeletedUtc DATETIME(6) NULL,
                PRIMARY KEY (Id),
                INDEX IX_Sale_ClientId (ClientId),
                INDEX IX_Sale_ContactId (ContactId),
                CONSTRAINT FK_Sale_Client FOREIGN KEY (ClientId) REFERENCES Client(Id) ON DELETE RESTRICT ON UPDATE RESTRICT,
                CONSTRAINT FK_Sale_Contact FOREIGN KEY (ContactId) REFERENCES Contact(Id) ON DELETE RESTRICT ON UPDATE RESTRICT
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            """);

        await ExecMySqlAsync(conn, tx, """
            CREATE TABLE IF NOT EXISTS Payment (
                Id INT NOT NULL,
                SaleId INT NOT NULL,
                PaymentDate DATE NOT NULL,
                PayDate DATE NOT NULL,
                Amount DECIMAL(18,2) NOT NULL,
                Commission DECIMAL(18,2) NOT NULL,
                IsPaid TINYINT(1) NOT NULL DEFAULT 0,
                PaidDateUtc DATETIME(6) NULL,
                CreatedUtc DATETIME(6) NOT NULL,
                UpdatedUtc DATETIME(6) NOT NULL,
                IsDeleted TINYINT(1) NOT NULL DEFAULT 0,
                DeletedUtc DATETIME(6) NULL,
                PRIMARY KEY (Id),
                INDEX IX_Payment_SaleId (SaleId),
                INDEX IX_Payment_PayDate (PayDate),
                CONSTRAINT FK_Payment_Sale FOREIGN KEY (SaleId) REFERENCES Sale(Id) ON DELETE RESTRICT ON UPDATE RESTRICT
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            """);

        await ExecMySqlAsync(conn, tx, """
            CREATE TABLE IF NOT EXISTS AuditLog (
                Id INT NOT NULL,
                EntityType VARCHAR(255) NOT NULL,
                EntityId INT NOT NULL,
                Action VARCHAR(64) NOT NULL,
                Details TEXT NOT NULL,
                TimestampUtc DATETIME(6) NOT NULL,
                PRIMARY KEY (Id),
                INDEX IX_AuditLog_Entity (EntityType, EntityId)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            """);
    }

    private static async Task InsertClientsAsync(MySqlConnection conn, MySqlTransaction tx, IReadOnlyCollection<Client> clients, bool upsert)
    {
        if (clients.Count == 0)
        {
            return;
        }

        var sql = """
            INSERT INTO Client
                (Id, Name, AddressLine1, AddressLine2, City, StateProvince, PostalCode, Country, TaxId, ContactName, ContactEmail, ContactPhone, CreatedUtc, UpdatedUtc, IsDeleted, DeletedUtc)
            VALUES
                (@Id, @Name, @AddressLine1, @AddressLine2, @City, @StateProvince, @PostalCode, @Country, @TaxId, @ContactName, @ContactEmail, @ContactPhone, @CreatedUtc, @UpdatedUtc, @IsDeleted, @DeletedUtc)
            """;
        if (upsert)
        {
            sql += """
                ON DUPLICATE KEY UPDATE
                    Name=VALUES(Name),
                    AddressLine1=VALUES(AddressLine1),
                    AddressLine2=VALUES(AddressLine2),
                    City=VALUES(City),
                    StateProvince=VALUES(StateProvince),
                    PostalCode=VALUES(PostalCode),
                    Country=VALUES(Country),
                    TaxId=VALUES(TaxId),
                    ContactName=VALUES(ContactName),
                    ContactEmail=VALUES(ContactEmail),
                    ContactPhone=VALUES(ContactPhone),
                    CreatedUtc=VALUES(CreatedUtc),
                    UpdatedUtc=VALUES(UpdatedUtc),
                    IsDeleted=VALUES(IsDeleted),
                    DeletedUtc=VALUES(DeletedUtc);
                """;
        }

        await using var cmd = new MySqlCommand(sql, conn, tx);
        cmd.Parameters.Add("@Id", MySqlDbType.Int32);
        cmd.Parameters.Add("@Name", MySqlDbType.VarChar);
        cmd.Parameters.Add("@AddressLine1", MySqlDbType.VarChar);
        cmd.Parameters.Add("@AddressLine2", MySqlDbType.VarChar);
        cmd.Parameters.Add("@City", MySqlDbType.VarChar);
        cmd.Parameters.Add("@StateProvince", MySqlDbType.VarChar);
        cmd.Parameters.Add("@PostalCode", MySqlDbType.VarChar);
        cmd.Parameters.Add("@Country", MySqlDbType.VarChar);
        cmd.Parameters.Add("@TaxId", MySqlDbType.VarChar);
        cmd.Parameters.Add("@ContactName", MySqlDbType.VarChar);
        cmd.Parameters.Add("@ContactEmail", MySqlDbType.VarChar);
        cmd.Parameters.Add("@ContactPhone", MySqlDbType.VarChar);
        cmd.Parameters.Add("@CreatedUtc", MySqlDbType.DateTime);
        cmd.Parameters.Add("@UpdatedUtc", MySqlDbType.DateTime);
        cmd.Parameters.Add("@IsDeleted", MySqlDbType.Bool);
        cmd.Parameters.Add("@DeletedUtc", MySqlDbType.DateTime);

        foreach (var c in clients)
        {
            cmd.Parameters["@Id"].Value = c.Id;
            cmd.Parameters["@Name"].Value = c.Name;
            cmd.Parameters["@AddressLine1"].Value = c.AddressLine1 ?? string.Empty;
            cmd.Parameters["@AddressLine2"].Value = c.AddressLine2 ?? string.Empty;
            cmd.Parameters["@City"].Value = c.City ?? string.Empty;
            cmd.Parameters["@StateProvince"].Value = c.StateProvince ?? string.Empty;
            cmd.Parameters["@PostalCode"].Value = c.PostalCode ?? string.Empty;
            cmd.Parameters["@Country"].Value = c.Country ?? string.Empty;
            cmd.Parameters["@TaxId"].Value = c.TaxId ?? string.Empty;
            cmd.Parameters["@ContactName"].Value = c.ContactName ?? string.Empty;
            cmd.Parameters["@ContactEmail"].Value = c.ContactEmail ?? string.Empty;
            cmd.Parameters["@ContactPhone"].Value = c.ContactPhone ?? string.Empty;
            cmd.Parameters["@CreatedUtc"].Value = c.CreatedUtc;
            cmd.Parameters["@UpdatedUtc"].Value = c.UpdatedUtc;
            cmd.Parameters["@IsDeleted"].Value = c.IsDeleted;
            cmd.Parameters["@DeletedUtc"].Value = c.DeletedUtc ?? (object)DBNull.Value;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task InsertContactsAsync(MySqlConnection conn, MySqlTransaction tx, IReadOnlyCollection<ContactModel> contacts, bool upsert)
    {
        if (contacts.Count == 0)
        {
            return;
        }

        var sql = """
            INSERT INTO Contact
                (Id, ClientId, Name, Email, Phone, Notes, CreatedUtc, UpdatedUtc, IsDeleted, DeletedUtc)
            VALUES
                (@Id, @ClientId, @Name, @Email, @Phone, @Notes, @CreatedUtc, @UpdatedUtc, @IsDeleted, @DeletedUtc)
            """;
        if (upsert)
        {
            sql += """
                ON DUPLICATE KEY UPDATE
                    ClientId=VALUES(ClientId),
                    Name=VALUES(Name),
                    Email=VALUES(Email),
                    Phone=VALUES(Phone),
                    Notes=VALUES(Notes),
                    CreatedUtc=VALUES(CreatedUtc),
                    UpdatedUtc=VALUES(UpdatedUtc),
                    IsDeleted=VALUES(IsDeleted),
                    DeletedUtc=VALUES(DeletedUtc);
                """;
        }

        await using var cmd = new MySqlCommand(sql, conn, tx);
        cmd.Parameters.Add("@Id", MySqlDbType.Int32);
        cmd.Parameters.Add("@ClientId", MySqlDbType.Int32);
        cmd.Parameters.Add("@Name", MySqlDbType.VarChar);
        cmd.Parameters.Add("@Email", MySqlDbType.VarChar);
        cmd.Parameters.Add("@Phone", MySqlDbType.VarChar);
        cmd.Parameters.Add("@Notes", MySqlDbType.Text);
        cmd.Parameters.Add("@CreatedUtc", MySqlDbType.DateTime);
        cmd.Parameters.Add("@UpdatedUtc", MySqlDbType.DateTime);
        cmd.Parameters.Add("@IsDeleted", MySqlDbType.Bool);
        cmd.Parameters.Add("@DeletedUtc", MySqlDbType.DateTime);

        foreach (var c in contacts)
        {
            cmd.Parameters["@Id"].Value = c.Id;
            cmd.Parameters["@ClientId"].Value = c.ClientId;
            cmd.Parameters["@Name"].Value = c.Name;
            cmd.Parameters["@Email"].Value = c.Email ?? string.Empty;
            cmd.Parameters["@Phone"].Value = c.Phone ?? string.Empty;
            cmd.Parameters["@Notes"].Value = c.Notes ?? string.Empty;
            cmd.Parameters["@CreatedUtc"].Value = c.CreatedUtc;
            cmd.Parameters["@UpdatedUtc"].Value = c.UpdatedUtc;
            cmd.Parameters["@IsDeleted"].Value = c.IsDeleted;
            cmd.Parameters["@DeletedUtc"].Value = c.DeletedUtc ?? (object)DBNull.Value;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task InsertSalesAsync(MySqlConnection conn, MySqlTransaction tx, IReadOnlyCollection<Sale> sales, bool upsert)
    {
        if (sales.Count == 0)
        {
            return;
        }

        var sql = """
            INSERT INTO Sale
                (Id, ClientId, ContactId, InvoiceNumber, SaleDate, Amount, CommissionPercent, CreatedUtc, UpdatedUtc, IsDeleted, DeletedUtc)
            VALUES
                (@Id, @ClientId, @ContactId, @InvoiceNumber, @SaleDate, @Amount, @CommissionPercent, @CreatedUtc, @UpdatedUtc, @IsDeleted, @DeletedUtc)
            """;
        if (upsert)
        {
            sql += """
                ON DUPLICATE KEY UPDATE
                    ClientId=VALUES(ClientId),
                    ContactId=VALUES(ContactId),
                    InvoiceNumber=VALUES(InvoiceNumber),
                    SaleDate=VALUES(SaleDate),
                    Amount=VALUES(Amount),
                    CommissionPercent=VALUES(CommissionPercent),
                    CreatedUtc=VALUES(CreatedUtc),
                    UpdatedUtc=VALUES(UpdatedUtc),
                    IsDeleted=VALUES(IsDeleted),
                    DeletedUtc=VALUES(DeletedUtc);
                """;
        }

        await using var cmd = new MySqlCommand(sql, conn, tx);
        cmd.Parameters.Add("@Id", MySqlDbType.Int32);
        cmd.Parameters.Add("@ClientId", MySqlDbType.Int32);
        cmd.Parameters.Add("@ContactId", MySqlDbType.Int32);
        cmd.Parameters.Add("@InvoiceNumber", MySqlDbType.VarChar);
        cmd.Parameters.Add("@SaleDate", MySqlDbType.Date);
        cmd.Parameters.Add("@Amount", MySqlDbType.NewDecimal);
        cmd.Parameters.Add("@CommissionPercent", MySqlDbType.NewDecimal);
        cmd.Parameters.Add("@CreatedUtc", MySqlDbType.DateTime);
        cmd.Parameters.Add("@UpdatedUtc", MySqlDbType.DateTime);
        cmd.Parameters.Add("@IsDeleted", MySqlDbType.Bool);
        cmd.Parameters.Add("@DeletedUtc", MySqlDbType.DateTime);

        foreach (var s in sales)
        {
            cmd.Parameters["@Id"].Value = s.Id;
            cmd.Parameters["@ClientId"].Value = s.ClientId;
            cmd.Parameters["@ContactId"].Value = s.ContactId;
            cmd.Parameters["@InvoiceNumber"].Value = s.InvoiceNumber ?? string.Empty;
            cmd.Parameters["@SaleDate"].Value = s.SaleDate.Date;
            cmd.Parameters["@Amount"].Value = s.Amount;
            cmd.Parameters["@CommissionPercent"].Value = s.CommissionPercent;
            cmd.Parameters["@CreatedUtc"].Value = s.CreatedUtc;
            cmd.Parameters["@UpdatedUtc"].Value = s.UpdatedUtc;
            cmd.Parameters["@IsDeleted"].Value = s.IsDeleted;
            cmd.Parameters["@DeletedUtc"].Value = s.DeletedUtc ?? (object)DBNull.Value;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task InsertPaymentsAsync(MySqlConnection conn, MySqlTransaction tx, IReadOnlyCollection<Payment> payments, bool upsert)
    {
        if (payments.Count == 0)
        {
            return;
        }

        var sql = """
            INSERT INTO Payment
                (Id, SaleId, PaymentDate, PayDate, Amount, Commission, IsPaid, PaidDateUtc, CreatedUtc, UpdatedUtc, IsDeleted, DeletedUtc)
            VALUES
                (@Id, @SaleId, @PaymentDate, @PayDate, @Amount, @Commission, @IsPaid, @PaidDateUtc, @CreatedUtc, @UpdatedUtc, @IsDeleted, @DeletedUtc)
            """;
        if (upsert)
        {
            sql += """
                ON DUPLICATE KEY UPDATE
                    SaleId=VALUES(SaleId),
                    PaymentDate=VALUES(PaymentDate),
                    PayDate=VALUES(PayDate),
                    Amount=VALUES(Amount),
                    Commission=VALUES(Commission),
                    IsPaid=VALUES(IsPaid),
                    PaidDateUtc=VALUES(PaidDateUtc),
                    CreatedUtc=VALUES(CreatedUtc),
                    UpdatedUtc=VALUES(UpdatedUtc),
                    IsDeleted=VALUES(IsDeleted),
                    DeletedUtc=VALUES(DeletedUtc);
                """;
        }

        await using var cmd = new MySqlCommand(sql, conn, tx);
        cmd.Parameters.Add("@Id", MySqlDbType.Int32);
        cmd.Parameters.Add("@SaleId", MySqlDbType.Int32);
        cmd.Parameters.Add("@PaymentDate", MySqlDbType.Date);
        cmd.Parameters.Add("@PayDate", MySqlDbType.Date);
        cmd.Parameters.Add("@Amount", MySqlDbType.NewDecimal);
        cmd.Parameters.Add("@Commission", MySqlDbType.NewDecimal);
        cmd.Parameters.Add("@IsPaid", MySqlDbType.Bool);
        cmd.Parameters.Add("@PaidDateUtc", MySqlDbType.DateTime);
        cmd.Parameters.Add("@CreatedUtc", MySqlDbType.DateTime);
        cmd.Parameters.Add("@UpdatedUtc", MySqlDbType.DateTime);
        cmd.Parameters.Add("@IsDeleted", MySqlDbType.Bool);
        cmd.Parameters.Add("@DeletedUtc", MySqlDbType.DateTime);

        foreach (var p in payments)
        {
            cmd.Parameters["@Id"].Value = p.Id;
            cmd.Parameters["@SaleId"].Value = p.SaleId;
            cmd.Parameters["@PaymentDate"].Value = p.PaymentDate.Date;
            cmd.Parameters["@PayDate"].Value = p.PayDate.Date;
            cmd.Parameters["@Amount"].Value = p.Amount;
            cmd.Parameters["@Commission"].Value = p.Commission;
            cmd.Parameters["@IsPaid"].Value = p.IsPaid;
            cmd.Parameters["@PaidDateUtc"].Value = p.PaidDateUtc ?? (object)DBNull.Value;
            cmd.Parameters["@CreatedUtc"].Value = p.CreatedUtc;
            cmd.Parameters["@UpdatedUtc"].Value = p.UpdatedUtc;
            cmd.Parameters["@IsDeleted"].Value = p.IsDeleted;
            cmd.Parameters["@DeletedUtc"].Value = p.DeletedUtc ?? (object)DBNull.Value;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task InsertAuditLogsAsync(MySqlConnection conn, MySqlTransaction tx, IReadOnlyCollection<AuditLog> audits, bool upsert)
    {
        if (audits.Count == 0)
        {
            return;
        }

        var sql = """
            INSERT INTO AuditLog
                (Id, EntityType, EntityId, Action, Details, TimestampUtc)
            VALUES
                (@Id, @EntityType, @EntityId, @Action, @Details, @TimestampUtc)
            """;
        if (upsert)
        {
            sql += """
                ON DUPLICATE KEY UPDATE
                    EntityType=VALUES(EntityType),
                    EntityId=VALUES(EntityId),
                    Action=VALUES(Action),
                    Details=VALUES(Details),
                    TimestampUtc=VALUES(TimestampUtc);
                """;
        }

        await using var cmd = new MySqlCommand(sql, conn, tx);
        cmd.Parameters.Add("@Id", MySqlDbType.Int32);
        cmd.Parameters.Add("@EntityType", MySqlDbType.VarChar);
        cmd.Parameters.Add("@EntityId", MySqlDbType.Int32);
        cmd.Parameters.Add("@Action", MySqlDbType.VarChar);
        cmd.Parameters.Add("@Details", MySqlDbType.Text);
        cmd.Parameters.Add("@TimestampUtc", MySqlDbType.DateTime);

        foreach (var a in audits)
        {
            cmd.Parameters["@Id"].Value = a.Id;
            cmd.Parameters["@EntityType"].Value = a.EntityType ?? string.Empty;
            cmd.Parameters["@EntityId"].Value = a.EntityId;
            cmd.Parameters["@Action"].Value = a.Action ?? string.Empty;
            cmd.Parameters["@Details"].Value = a.Details ?? string.Empty;
            cmd.Parameters["@TimestampUtc"].Value = a.TimestampUtc;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task<List<Client>> QueryClientsAsync(MySqlConnection conn)
    {
        const string sql = """
            SELECT Id, Name, AddressLine1, AddressLine2, City, StateProvince, PostalCode, Country, TaxId, ContactName, ContactEmail, ContactPhone, CreatedUtc, UpdatedUtc, IsDeleted, DeletedUtc
            FROM Client
            """;

        var list = new List<Client>();
        await using var cmd = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Client
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                AddressLine1 = reader.GetString(2),
                AddressLine2 = reader.GetString(3),
                City = reader.GetString(4),
                StateProvince = reader.GetString(5),
                PostalCode = reader.GetString(6),
                Country = reader.GetString(7),
                TaxId = reader.GetString(8),
                ContactName = reader.GetString(9),
                ContactEmail = reader.GetString(10),
                ContactPhone = reader.GetString(11),
                CreatedUtc = reader.GetDateTime(12),
                UpdatedUtc = reader.GetDateTime(13),
                IsDeleted = reader.GetBoolean(14),
                DeletedUtc = reader.IsDBNull(15) ? null : reader.GetDateTime(15)
            });
        }

        return list;
    }

    private static async Task<List<ContactModel>> QueryContactsAsync(MySqlConnection conn)
    {
        const string sql = """
            SELECT Id, ClientId, Name, Email, Phone, Notes, CreatedUtc, UpdatedUtc, IsDeleted, DeletedUtc
            FROM Contact
            """;

        var list = new List<ContactModel>();
        await using var cmd = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new ContactModel
            {
                Id = reader.GetInt32(0),
                ClientId = reader.GetInt32(1),
                Name = reader.GetString(2),
                Email = reader.GetString(3),
                Phone = reader.GetString(4),
                Notes = reader.GetString(5),
                CreatedUtc = reader.GetDateTime(6),
                UpdatedUtc = reader.GetDateTime(7),
                IsDeleted = reader.GetBoolean(8),
                DeletedUtc = reader.IsDBNull(9) ? null : reader.GetDateTime(9)
            });
        }

        return list;
    }

    private static async Task<List<Sale>> QuerySalesAsync(MySqlConnection conn)
    {
        const string sql = """
            SELECT Id, ClientId, ContactId, InvoiceNumber, SaleDate, Amount, CommissionPercent, CreatedUtc, UpdatedUtc, IsDeleted, DeletedUtc
            FROM Sale
            """;

        var list = new List<Sale>();
        await using var cmd = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Sale
            {
                Id = reader.GetInt32(0),
                ClientId = reader.GetInt32(1),
                ContactId = reader.GetInt32(2),
                InvoiceNumber = reader.GetString(3),
                SaleDate = reader.GetDateTime(4),
                Amount = reader.GetDecimal(5),
                CommissionPercent = reader.GetDecimal(6),
                CreatedUtc = reader.GetDateTime(7),
                UpdatedUtc = reader.GetDateTime(8),
                IsDeleted = reader.GetBoolean(9),
                DeletedUtc = reader.IsDBNull(10) ? null : reader.GetDateTime(10)
            });
        }

        return list;
    }

    private static async Task<List<Payment>> QueryPaymentsAsync(MySqlConnection conn)
    {
        const string sql = """
            SELECT Id, SaleId, PaymentDate, PayDate, Amount, Commission, IsPaid, PaidDateUtc, CreatedUtc, UpdatedUtc, IsDeleted, DeletedUtc
            FROM Payment
            """;

        var list = new List<Payment>();
        await using var cmd = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Payment
            {
                Id = reader.GetInt32(0),
                SaleId = reader.GetInt32(1),
                PaymentDate = reader.GetDateTime(2),
                PayDate = reader.GetDateTime(3),
                Amount = reader.GetDecimal(4),
                Commission = reader.GetDecimal(5),
                IsPaid = reader.GetBoolean(6),
                PaidDateUtc = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                CreatedUtc = reader.GetDateTime(8),
                UpdatedUtc = reader.GetDateTime(9),
                IsDeleted = reader.GetBoolean(10),
                DeletedUtc = reader.IsDBNull(11) ? null : reader.GetDateTime(11)
            });
        }

        return list;
    }

    private static async Task<List<AuditLog>> QueryAuditLogsAsync(MySqlConnection conn)
    {
        const string sql = """
            SELECT Id, EntityType, EntityId, Action, Details, TimestampUtc
            FROM AuditLog
            """;

        var list = new List<AuditLog>();
        await using var cmd = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new AuditLog
            {
                Id = reader.GetInt32(0),
                EntityType = reader.GetString(1),
                EntityId = reader.GetInt32(2),
                Action = reader.GetString(3),
                Details = reader.GetString(4),
                TimestampUtc = reader.GetDateTime(5)
            });
        }

        return list;
    }

    private sealed class TableInfo
    {
        public string name { get; set; } = string.Empty;
    }

    private sealed class ForeignKeyInfo
    {
        public string table { get; set; } = string.Empty;
        public string from { get; set; } = string.Empty;
    }

    private Task EnableForeignKeysAsync()
    {
        return _db.ExecuteAsync("PRAGMA foreign_keys = ON");
    }

    private async Task EnsureClientSchemaAsync()
    {
        var columns = await _db.QueryAsync<TableInfo>("PRAGMA table_info(Client)");
        var existing = new HashSet<string>(columns.Select(c => c.name), StringComparer.OrdinalIgnoreCase);
        var needed = new[]
        {
            ("TaxId", "TEXT"),
            ("AddressLine1", "TEXT"),
            ("AddressLine2", "TEXT"),
            ("City", "TEXT"),
            ("StateProvince", "TEXT"),
            ("PostalCode", "TEXT"),
            ("Country", "TEXT"),
            ("ContactName", "TEXT"),
            ("ContactEmail", "TEXT"),
            ("ContactPhone", "TEXT"),
            ("CreatedUtc", "TEXT"),
            ("UpdatedUtc", "TEXT"),
            ("IsDeleted", "INTEGER"),
            ("DeletedUtc", "TEXT")
        };

        foreach (var (name, type) in needed)
        {
            if (!existing.Contains(name))
            {
                await _db.ExecuteAsync($"ALTER TABLE Client ADD COLUMN {name} {type}");
            }
        }

        var now = DateTime.UtcNow;
        await _db.ExecuteAsync("UPDATE Client SET IsDeleted = 0 WHERE IsDeleted IS NULL");
        await _db.ExecuteAsync("UPDATE Client SET CreatedUtc = ? WHERE CreatedUtc IS NULL", now);
        await _db.ExecuteAsync("UPDATE Client SET UpdatedUtc = ? WHERE UpdatedUtc IS NULL", now);
        if (existing.Contains("BusinessAddress"))
        {
            await _db.ExecuteAsync("""
                UPDATE Client
                SET AddressLine1 = BusinessAddress
                WHERE (AddressLine1 IS NULL OR AddressLine1 = '')
                  AND BusinessAddress IS NOT NULL
                  AND BusinessAddress <> ''
                """);
        }
    }

    private async Task EnsureSaleSchemaAsync()
    {
        var columns = await _db.QueryAsync<TableInfo>("PRAGMA table_info(Sale)");
        var existing = new HashSet<string>(columns.Select(c => c.name), StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains("ContactId"))
        {
            await _db.ExecuteAsync("ALTER TABLE Sale ADD COLUMN ContactId INTEGER");
        }

        if (!existing.Contains("InvoiceNumber"))
        {
            await _db.ExecuteAsync("ALTER TABLE Sale ADD COLUMN InvoiceNumber TEXT");
        }

        var needed = new[]
        {
            ("CreatedUtc", "TEXT"),
            ("UpdatedUtc", "TEXT"),
            ("IsDeleted", "INTEGER"),
            ("DeletedUtc", "TEXT")
        };

        foreach (var (name, type) in needed)
        {
            if (!existing.Contains(name))
            {
                await _db.ExecuteAsync($"ALTER TABLE Sale ADD COLUMN {name} {type}");
            }
        }

        var now = DateTime.UtcNow;
        await _db.ExecuteAsync("UPDATE Sale SET IsDeleted = 0 WHERE IsDeleted IS NULL");
        await _db.ExecuteAsync("UPDATE Sale SET CreatedUtc = ? WHERE CreatedUtc IS NULL", now);
        await _db.ExecuteAsync("UPDATE Sale SET UpdatedUtc = ? WHERE UpdatedUtc IS NULL", now);
    }

    private async Task EnsureContactSchemaAsync()
    {
        var columns = await _db.QueryAsync<TableInfo>("PRAGMA table_info(Contact)");
        var existing = new HashSet<string>(columns.Select(c => c.name), StringComparer.OrdinalIgnoreCase);
        var needed = new[]
        {
            ("Notes", "TEXT"),
            ("CreatedUtc", "TEXT"),
            ("UpdatedUtc", "TEXT"),
            ("IsDeleted", "INTEGER"),
            ("DeletedUtc", "TEXT")
        };

        foreach (var (name, type) in needed)
        {
            if (!existing.Contains(name))
            {
                await _db.ExecuteAsync($"ALTER TABLE Contact ADD COLUMN {name} {type}");
            }
        }

        var now = DateTime.UtcNow;
        await _db.ExecuteAsync("UPDATE Contact SET IsDeleted = 0 WHERE IsDeleted IS NULL");
        await _db.ExecuteAsync("UPDATE Contact SET CreatedUtc = ? WHERE CreatedUtc IS NULL", now);
        await _db.ExecuteAsync("UPDATE Contact SET UpdatedUtc = ? WHERE UpdatedUtc IS NULL", now);
        await _db.ExecuteAsync("UPDATE Contact SET Notes = '' WHERE Notes IS NULL");
    }

    private async Task EnsurePaymentSchemaAsync()
    {
        var columns = await _db.QueryAsync<TableInfo>("PRAGMA table_info(Payment)");
        var existing = new HashSet<string>(columns.Select(c => c.name), StringComparer.OrdinalIgnoreCase);
        var needed = new[]
        {
            ("CreatedUtc", "TEXT"),
            ("UpdatedUtc", "TEXT"),
            ("IsDeleted", "INTEGER"),
            ("DeletedUtc", "TEXT")
        };

        foreach (var (name, type) in needed)
        {
            if (!existing.Contains(name))
            {
                await _db.ExecuteAsync($"ALTER TABLE Payment ADD COLUMN {name} {type}");
            }
        }

        var now = DateTime.UtcNow;
        await _db.ExecuteAsync("UPDATE Payment SET IsDeleted = 0 WHERE IsDeleted IS NULL");
        await _db.ExecuteAsync("UPDATE Payment SET CreatedUtc = ? WHERE CreatedUtc IS NULL", now);
        await _db.ExecuteAsync("UPDATE Payment SET UpdatedUtc = ? WHERE UpdatedUtc IS NULL", now);
    }

    private async Task CleanupOrphanedRecordsAsync()
    {
        await _db.ExecuteAsync("DELETE FROM Sale WHERE ClientId NOT IN (SELECT Id FROM Client)");
        await EnsureContactsForSalesAsync();
        await _db.ExecuteAsync("DELETE FROM Contact WHERE ClientId NOT IN (SELECT Id FROM Client)");
        await _db.ExecuteAsync("DELETE FROM Payment WHERE SaleId NOT IN (SELECT Id FROM Sale)");
    }

    private async Task EnsureContactsForSalesAsync()
    {
        var sales = await _db.Table<Sale>().Where(s => !s.IsDeleted).ToListAsync();
        if (sales.Count == 0)
        {
            return;
        }

        var contacts = await _db.Table<ContactModel>().Where(c => !c.IsDeleted).ToListAsync();
        var contactsById = contacts.ToDictionary(c => c.Id);
        var fallbackByClient = contacts
            .GroupBy(c => c.ClientId)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Name).First());

        foreach (var sale in sales)
        {
            if (sale.ContactId > 0 && contactsById.ContainsKey(sale.ContactId))
            {
                continue;
            }

            if (!fallbackByClient.TryGetValue(sale.ClientId, out var contact))
            {
                contact = new ContactModel
                {
                    ClientId = sale.ClientId,
                    Name = "Primary Contact"
                };

                await _db.InsertAsync(contact);
                contactsById[contact.Id] = contact;
                fallbackByClient[sale.ClientId] = contact;
            }

            sale.ContactId = contact.Id;
            await _db.UpdateAsync(sale);
        }
    }

    private async Task EnsureForeignKeyConstraintsAsync()
    {
        if (!await HasForeignKeyAsync("Contact", "ClientId", "Client"))
        {
            await RebuildContactTableAsync();
        }

        if (!await HasForeignKeyAsync("Sale", "ClientId", "Client") ||
            !await HasForeignKeyAsync("Sale", "ContactId", "Contact"))
        {
            await RebuildSaleTableAsync();
        }

        if (!await HasForeignKeyAsync("Payment", "SaleId", "Sale"))
        {
            await RebuildPaymentTableAsync();
        }
    }

    private async Task<bool> HasForeignKeyAsync(string tableName, string columnName, string referenceTable)
    {
        var foreignKeys = await _db.QueryAsync<ForeignKeyInfo>($"PRAGMA foreign_key_list({tableName})");
        return foreignKeys.Any(fk =>
            string.Equals(fk.from, columnName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(fk.table, referenceTable, StringComparison.OrdinalIgnoreCase));
    }

    private async Task RebuildContactTableAsync()
    {
        await _db.ExecuteAsync("ALTER TABLE Contact RENAME TO Contact_legacy");
        await _db.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS Contact (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ClientId INTEGER NOT NULL,
                Name TEXT NOT NULL DEFAULT '',
                Email TEXT NOT NULL DEFAULT '',
                Phone TEXT NOT NULL DEFAULT '',
                Notes TEXT NOT NULL DEFAULT '',
                CreatedUtc TEXT NULL,
                UpdatedUtc TEXT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0,
                DeletedUtc TEXT NULL,
                FOREIGN KEY (ClientId) REFERENCES Client(Id) ON DELETE CASCADE
            )
            """);

        await _db.ExecuteAsync("""
            INSERT INTO Contact (Id, ClientId, Name, Email, Phone, Notes, CreatedUtc, UpdatedUtc, IsDeleted, DeletedUtc)
            SELECT Id, ClientId, COALESCE(Name, ''), COALESCE(Email, ''), COALESCE(Phone, ''), COALESCE(Notes, ''),
                   CreatedUtc, UpdatedUtc, COALESCE(IsDeleted, 0), DeletedUtc
            FROM Contact_legacy
            WHERE ClientId IN (SELECT Id FROM Client)
            """);
        await _db.ExecuteAsync("DROP TABLE Contact_legacy");
    }

    private async Task RebuildSaleTableAsync()
    {
        await _db.ExecuteAsync("ALTER TABLE Sale RENAME TO Sale_legacy");
        await _db.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS Sale (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ClientId INTEGER NOT NULL,
                ContactId INTEGER NOT NULL,
                InvoiceNumber TEXT NOT NULL DEFAULT '',
                SaleDate TEXT NOT NULL,
                Amount REAL NOT NULL,
                CommissionPercent REAL NOT NULL,
                CreatedUtc TEXT NULL,
                UpdatedUtc TEXT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0,
                DeletedUtc TEXT NULL,
                FOREIGN KEY (ClientId) REFERENCES Client(Id) ON DELETE CASCADE,
                FOREIGN KEY (ContactId) REFERENCES Contact(Id) ON DELETE RESTRICT
            )
            """);

        await _db.ExecuteAsync("""
            INSERT INTO Sale (Id, ClientId, ContactId, InvoiceNumber, SaleDate, Amount, CommissionPercent, CreatedUtc, UpdatedUtc, IsDeleted, DeletedUtc)
            SELECT Id, ClientId, ContactId, COALESCE(InvoiceNumber, ''), SaleDate, Amount, CommissionPercent,
                   CreatedUtc, UpdatedUtc, COALESCE(IsDeleted, 0), DeletedUtc
            FROM Sale_legacy
            WHERE ClientId IN (SELECT Id FROM Client)
              AND ContactId IN (SELECT Id FROM Contact)
            """);
        await _db.ExecuteAsync("DROP TABLE Sale_legacy");
    }

    private async Task RebuildPaymentTableAsync()
    {
        await _db.ExecuteAsync("ALTER TABLE Payment RENAME TO Payment_legacy");
        await _db.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS Payment (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SaleId INTEGER NOT NULL,
                PaymentDate TEXT NOT NULL,
                PayDate TEXT NOT NULL,
                Amount REAL NOT NULL,
                Commission REAL NOT NULL,
                IsPaid INTEGER NOT NULL DEFAULT 0,
                PaidDateUtc TEXT NULL,
                CreatedUtc TEXT NULL,
                UpdatedUtc TEXT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0,
                DeletedUtc TEXT NULL,
                FOREIGN KEY (SaleId) REFERENCES Sale(Id) ON DELETE CASCADE
            )
            """);

        await _db.ExecuteAsync("""
            INSERT INTO Payment (Id, SaleId, PaymentDate, PayDate, Amount, Commission, IsPaid, PaidDateUtc, CreatedUtc, UpdatedUtc, IsDeleted, DeletedUtc)
            SELECT Id, SaleId, PaymentDate, PayDate, Amount, Commission, IsPaid, PaidDateUtc,
                   CreatedUtc, UpdatedUtc, COALESCE(IsDeleted, 0), DeletedUtc
            FROM Payment_legacy
            WHERE SaleId IN (SELECT Id FROM Sale)
            """);
        await _db.ExecuteAsync("DROP TABLE Payment_legacy");
    }

    private async Task EnsureIndexesAsync()
    {
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Client_Name ON Client(Name)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Sale_ClientId ON Sale(ClientId)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Sale_ContactId ON Sale(ContactId)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Contact_ClientId ON Contact(ClientId)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Payment_SaleId ON Payment(SaleId)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Payment_PayDate ON Payment(PayDate)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Client_IsDeleted ON Client(IsDeleted)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Contact_IsDeleted ON Contact(IsDeleted)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Sale_IsDeleted ON Sale(IsDeleted)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Payment_IsDeleted ON Payment(IsDeleted)");
    }

    private Task LogAuditAsync(string entityType, int entityId, string action, string details)
    {
        return _db.InsertAsync(new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Details = details,
            TimestampUtc = DateTime.UtcNow
        });
    }

    public async Task<int> GetActiveClientCountAsync()
    {
        await EnsureInitializedAsync();
        return await _db.Table<Client>().Where(c => !c.IsDeleted).CountAsync();
    }

    private async Task EnsurePaymentsForExistingSalesAsync()
    {
        var sales = await _db.Table<Sale>().Where(s => !s.IsDeleted).ToListAsync();
        foreach (var sale in sales)
        {
            var existingCount = await _db.Table<Payment>().Where(p => p.SaleId == sale.Id && !p.IsDeleted).CountAsync();
            if (existingCount == 0)
            {
                var now = DateTime.UtcNow;
                var payments = BuildPaymentsForSale(sale, Array.Empty<Payment>(), now);
                await _db.InsertAllAsync(payments);
            }
        }
    }

    public async Task<List<Client>> GetClientsAsync(string? search = null)
    {
        await EnsureInitializedAsync();
        var query = _db.Table<Client>().Where(c => !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim();
            query = query.Where(c =>
                c.Name.Contains(normalized) ||
                c.City.Contains(normalized) ||
                c.StateProvince.Contains(normalized) ||
                c.Country.Contains(normalized) ||
                c.TaxId.Contains(normalized));
        }

        return await query.OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<Client?> GetClientByIdAsync(int clientId)
    {
        await EnsureInitializedAsync();
        return await _db.Table<Client>().Where(c => c.Id == clientId && !c.IsDeleted).FirstOrDefaultAsync();
    }

    public async Task<ContactModel?> GetContactByIdAsync(int contactId)
    {
        await EnsureInitializedAsync();
        return await _db.Table<ContactModel>().Where(c => c.Id == contactId && !c.IsDeleted).FirstOrDefaultAsync();
    }

    public async Task<Client> AddClientAsync(string name)
    {
        await EnsureInitializedAsync();
        return await AddClientAsync(new Client { Name = name.Trim() });
    }

    public async Task<Client> AddClientAsync(Client client)
    {
        await EnsureInitializedAsync();
        var now = DateTime.UtcNow;
        client.Name = client.Name.Trim();
        client.TaxId = client.TaxId.Trim();
        client.AddressLine1 = client.AddressLine1.Trim();
        client.AddressLine2 = client.AddressLine2.Trim();
        client.City = client.City.Trim();
        client.StateProvince = client.StateProvince.Trim();
        client.PostalCode = client.PostalCode.Trim();
        client.Country = client.Country.Trim();
        client.ContactName = client.ContactName.Trim();
        client.ContactEmail = client.ContactEmail.Trim();
        client.ContactPhone = client.ContactPhone.Trim();
        client.CreatedUtc = now;
        client.UpdatedUtc = now;
        client.IsDeleted = false;
        await _db.InsertAsync(client);
        await LogAuditAsync(nameof(Client), client.Id, "create", $"Name={client.Name}");
        QueueRemotePush();
        return client;
    }

    public async Task UpdateClientAsync(Client client)
    {
        await EnsureInitializedAsync();
        client.UpdatedUtc = DateTime.UtcNow;
        await _db.UpdateAsync(client);
        await LogAuditAsync(nameof(Client), client.Id, "update", $"Name={client.Name}");
        QueueRemotePush();
    }

    public async Task DeleteClientAsync(Client client)
    {
        await EnsureInitializedAsync();
        var now = DateTime.UtcNow;
        await _db.ExecuteAsync("UPDATE Client SET IsDeleted = 1, DeletedUtc = ?, UpdatedUtc = ? WHERE Id = ?", now, now, client.Id);
        await _db.ExecuteAsync("UPDATE Contact SET IsDeleted = 1, DeletedUtc = ?, UpdatedUtc = ? WHERE ClientId = ?", now, now, client.Id);
        await _db.ExecuteAsync("UPDATE Sale SET IsDeleted = 1, DeletedUtc = ?, UpdatedUtc = ? WHERE ClientId = ?", now, now, client.Id);
        await _db.ExecuteAsync("""
            UPDATE Payment
            SET IsDeleted = 1, DeletedUtc = ?, UpdatedUtc = ?
            WHERE SaleId IN (SELECT Id FROM Sale WHERE ClientId = ?)
            """, now, now, client.Id);
        await LogAuditAsync(nameof(Client), client.Id, "delete", $"Soft deleted client {client.Id}");
        QueueRemotePush();
    }

    public async Task<List<Sale>> GetSalesForClientAsync(int clientId)
    {
        await EnsureInitializedAsync();
        return await _db.Table<Sale>()
            .Where(s => s.ClientId == clientId && !s.IsDeleted)
            .OrderByDescending(s => s.SaleDate)
            .ToListAsync();
    }

    public async Task<int> GetClientCountAsync(string? search = null)
    {
        await EnsureInitializedAsync();
        var query = _db.Table<Client>().Where(c => !c.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim();
            query = query.Where(c =>
                c.Name.Contains(normalized) ||
                c.City.Contains(normalized) ||
                c.StateProvince.Contains(normalized) ||
                c.Country.Contains(normalized) ||
                c.TaxId.Contains(normalized));
        }

        return await query.CountAsync();
    }

    public async Task<List<Client>> GetClientsPageAsync(string? search, int page, int pageSize)
    {
        await EnsureInitializedAsync();
        var query = _db.Table<Client>().Where(c => !c.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim();
            query = query.Where(c =>
                c.Name.Contains(normalized) ||
                c.City.Contains(normalized) ||
                c.StateProvince.Contains(normalized) ||
                c.Country.Contains(normalized) ||
                c.TaxId.Contains(normalized));
        }

        return await query.OrderBy(c => c.Name)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<List<Sale>> GetAllSalesAsync()
    {
        await EnsureInitializedAsync();
        return await _db.Table<Sale>()
            .Where(s => !s.IsDeleted)
            .OrderByDescending(s => s.SaleDate)
            .ToListAsync();
    }

    public async Task<List<Sale>> GetSalesForClientIdsAsync(IReadOnlyCollection<int> clientIds)
    {
        await EnsureInitializedAsync();
        if (clientIds.Count == 0)
        {
            return new List<Sale>();
        }

        var idList = string.Join(",", clientIds);
        var sql = $"SELECT * FROM Sale WHERE ClientId IN ({idList}) AND IsDeleted = 0";
        return await _db.QueryAsync<Sale>(sql);
    }

    public async Task<Sale?> GetSaleByIdAsync(int saleId)
    {
        await EnsureInitializedAsync();
        return await _db.Table<Sale>().Where(s => s.Id == saleId && !s.IsDeleted).FirstOrDefaultAsync();
    }

    public async Task<List<SaleOverview>> GetSalesOverviewAsync()
    {
        await EnsureInitializedAsync();
        const string sql = """
            SELECT
                s.Id AS SaleId,
                s.SaleDate AS SaleDate,
                s.Amount AS Amount,
                s.CommissionPercent AS CommissionPercent,
                s.InvoiceNumber AS InvoiceNumber,
                c.Name AS ClientName,
                co.Name AS ContactName
            FROM Sale s
            INNER JOIN Client c ON s.ClientId = c.Id
            INNER JOIN Contact co ON s.ContactId = co.Id
            WHERE s.IsDeleted = 0
              AND c.IsDeleted = 0
              AND co.IsDeleted = 0
            ORDER BY s.SaleDate DESC
            """;

        return await _db.QueryAsync<SaleOverview>(sql);
    }

    public async Task<Sale> AddSaleAsync(Sale sale)
    {
        await EnsureInitializedAsync();
        var now = DateTime.UtcNow;
        sale.CreatedUtc = now;
        sale.UpdatedUtc = now;
        sale.IsDeleted = false;
        await _db.InsertAsync(sale);
        await UpsertPaymentsForSaleAsync(sale);
        await LogAuditAsync(nameof(Sale), sale.Id, "create", $"ClientId={sale.ClientId}, ContactId={sale.ContactId}, Invoice={sale.InvoiceNumber}");
        QueueRemotePush();
        return sale;
    }

    public async Task UpdateSaleAsync(Sale sale)
    {
        await EnsureInitializedAsync();
        await UpdateSaleAsync(sale, true);
    }

    public async Task UpdateSaleAsync(Sale sale, bool regeneratePayments)
    {
        await EnsureInitializedAsync();
        sale.UpdatedUtc = DateTime.UtcNow;
        await _db.UpdateAsync(sale);
        if (regeneratePayments)
        {
            await UpsertPaymentsForSaleAsync(sale);
        }

        await LogAuditAsync(nameof(Sale), sale.Id, "update", $"Invoice={sale.InvoiceNumber}");
        QueueRemotePush();
    }

    public async Task<List<ContactModel>> GetContactsForClientAsync(int clientId)
    {
        await EnsureInitializedAsync();
        return await _db.Table<ContactModel>()
            .Where(c => c.ClientId == clientId && !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<ContactModel> AddContactAsync(ContactModel contact)
    {
        await EnsureInitializedAsync();
        var now = DateTime.UtcNow;
        contact.Name = contact.Name?.Trim() ?? string.Empty;
        contact.Email = contact.Email?.Trim() ?? string.Empty;
        contact.Phone = contact.Phone?.Trim() ?? string.Empty;
        contact.Notes = contact.Notes?.Trim() ?? string.Empty;
        contact.CreatedUtc = now;
        contact.UpdatedUtc = now;
        contact.IsDeleted = false;
        await _db.InsertAsync(contact);
        await LogAuditAsync(nameof(ContactModel), contact.Id, "create", $"ClientId={contact.ClientId}, Name={contact.Name}");
        QueueRemotePush();
        return contact;
    }

    public async Task UpdateContactAsync(ContactModel contact)
    {
        await EnsureInitializedAsync();
        contact.Name = contact.Name?.Trim() ?? string.Empty;
        contact.Email = contact.Email?.Trim() ?? string.Empty;
        contact.Phone = contact.Phone?.Trim() ?? string.Empty;
        contact.Notes = contact.Notes?.Trim() ?? string.Empty;
        contact.UpdatedUtc = DateTime.UtcNow;
        await _db.UpdateAsync(contact);
        await LogAuditAsync(nameof(ContactModel), contact.Id, "update", $"Name={contact.Name}");
        QueueRemotePush();
    }

    public async Task DeleteContactAsync(ContactModel contact)
    {
        await EnsureInitializedAsync();
        var now = DateTime.UtcNow;
        await _db.ExecuteAsync("UPDATE Contact SET IsDeleted = 1, DeletedUtc = ?, UpdatedUtc = ? WHERE Id = ?", now, now, contact.Id);
        await LogAuditAsync(nameof(ContactModel), contact.Id, "delete", $"Soft deleted contact {contact.Id}");
        QueueRemotePush();
    }

    public async Task<bool> ContactHasSalesAsync(int contactId)
    {
        await EnsureInitializedAsync();
        return await _db.Table<Sale>().Where(s => s.ContactId == contactId && !s.IsDeleted).CountAsync() > 0;
    }

    public async Task<List<ContactModel>> GetAllContactsAsync()
    {
        await EnsureInitializedAsync();
        return await _db.Table<ContactModel>()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<List<ContactOverview>> GetContactsOverviewAsync(string? search = null)
    {
        await EnsureInitializedAsync();
        const string sql = """
            SELECT
                co.Id AS ContactId,
                co.ClientId AS ClientId,
                c.Name AS ClientName,
                co.Name AS Name,
                co.Email AS Email,
                co.Phone AS Phone,
                co.Notes AS Notes
            FROM Contact co
            INNER JOIN Client c ON co.ClientId = c.Id
            WHERE co.IsDeleted = 0
              AND c.IsDeleted = 0
            ORDER BY co.Name
            """;

        var contacts = await _db.QueryAsync<ContactOverview>(sql);
        if (string.IsNullOrWhiteSpace(search))
        {
            return contacts;
        }

        var term = search.Trim();
        return contacts.Where(c =>
                c.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                c.Email.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                c.Phone.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                c.Notes.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                c.ClientName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task DeleteSaleAsync(Sale sale)
    {
        await EnsureInitializedAsync();
        var now = DateTime.UtcNow;
        await _db.ExecuteAsync("UPDATE Sale SET IsDeleted = 1, DeletedUtc = ?, UpdatedUtc = ? WHERE Id = ?", now, now, sale.Id);
        await _db.ExecuteAsync("UPDATE Payment SET IsDeleted = 1, DeletedUtc = ?, UpdatedUtc = ? WHERE SaleId = ?", now, now, sale.Id);
        await LogAuditAsync(nameof(Sale), sale.Id, "delete", $"Soft deleted sale {sale.Id}");
        QueueRemotePush();
    }

    public async Task<List<Payment>> GetPaymentsForMonthAsync(DateTime month)
    {
        await EnsureInitializedAsync();
        var start = new DateTime(month.Year, month.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        return await _db.Table<Payment>()
            .Where(p => p.PayDate >= start && p.PayDate <= end && !p.IsDeleted)
            .OrderBy(p => p.PayDate)
            .ThenBy(p => p.PaymentDate)
            .ToListAsync();
    }

    public async Task<List<Payment>> GetPaymentsBetweenAsync(DateTime start, DateTime end)
    {
        await EnsureInitializedAsync();
        var sql = """
            SELECT p.* FROM Payment p
            INNER JOIN Sale s ON p.SaleId = s.Id
            WHERE p.IsDeleted = 0
              AND s.IsDeleted = 0
              AND p.PayDate >= ?
              AND p.PayDate <= ?
            ORDER BY p.PayDate, p.PaymentDate
            """;

        return await _db.QueryAsync<Payment>(sql, start.Date, end.Date);
    }

    public async Task<List<Payment>> GetAllPaymentsAsync()
    {
        await EnsureInitializedAsync();
        const string sql = """
            SELECT p.* FROM Payment p
            INNER JOIN Sale s ON p.SaleId = s.Id
            WHERE p.IsDeleted = 0
              AND s.IsDeleted = 0
            ORDER BY p.PayDate, p.PaymentDate
            """;

        return await _db.QueryAsync<Payment>(sql);
    }

    public async Task<List<Payment>> GetPaymentsForClientIdsBetweenAsync(IReadOnlyCollection<int> clientIds, DateTime start, DateTime end)
    {
        await EnsureInitializedAsync();
        if (clientIds.Count == 0)
        {
            return new List<Payment>();
        }

        var idList = string.Join(",", clientIds);
        var sql = $"""
            SELECT p.* FROM Payment p
            INNER JOIN Sale s ON p.SaleId = s.Id
            WHERE s.ClientId IN ({idList})
              AND p.IsDeleted = 0
              AND s.IsDeleted = 0
              AND p.PayDate >= ?
              AND p.PayDate <= ?
            ORDER BY p.PayDate, p.PaymentDate
            """;

        return await _db.QueryAsync<Payment>(sql, start.Date, end.Date);
    }

    public async Task UpdatePaymentAsync(Payment payment)
    {
        await EnsureInitializedAsync();
        var existing = await _db.Table<Payment>().Where(p => p.Id == payment.Id && !p.IsDeleted).FirstOrDefaultAsync();
        if (existing is null)
        {
            return;
        }

        existing.IsPaid = payment.IsPaid;
        existing.PaidDateUtc = payment.PaidDateUtc;
        existing.UpdatedUtc = DateTime.UtcNow;
        await _db.UpdateAsync(existing);
        await LogAuditAsync(nameof(Payment), existing.Id, "update", $"IsPaid={existing.IsPaid}");
        QueueRemotePush();
    }

    public async Task<List<Payment>> GetPaymentsForSaleAsync(int saleId)
    {
        await EnsureInitializedAsync();
        return await _db.Table<Payment>()
            .Where(p => p.SaleId == saleId && !p.IsDeleted)
            .OrderBy(p => p.PaymentDate)
            .ToListAsync();
    }

    public async Task<List<Payment>> GetPaymentsForSaleIdsAsync(IReadOnlyCollection<int> saleIds)
    {
        await EnsureInitializedAsync();
        if (saleIds.Count == 0)
        {
            return new List<Payment>();
        }

        var idList = string.Join(",", saleIds);
        var sql = $"SELECT * FROM Payment WHERE SaleId IN ({idList}) AND IsDeleted = 0";
        return await _db.QueryAsync<Payment>(sql);
    }

    public async Task<int> GetPaymentCountAsync()
    {
        await EnsureInitializedAsync();
        return await _db.Table<Payment>().Where(p => !p.IsDeleted).CountAsync();
    }

    public async Task<(DateTime? MinPayDate, DateTime? MaxPayDate)> GetPaymentPayDateRangeAsync()
    {
        await EnsureInitializedAsync();
        var payments = await _db.Table<Payment>()
            .Where(p => !p.IsDeleted)
            .ToListAsync();

        if (payments.Count == 0)
        {
            return (null, null);
        }

        return (payments.Min(p => p.PayDate), payments.Max(p => p.PayDate));
    }

    public async Task UpdatePaymentDetailsAsync(Payment payment)
    {
        await EnsureInitializedAsync();
        var existing = await _db.Table<Payment>().Where(p => p.Id == payment.Id && !p.IsDeleted).FirstOrDefaultAsync();
        if (existing is null)
        {
            return;
        }

        existing.PaymentDate = payment.PaymentDate.Date;
        existing.PayDate = payment.PayDate.Date;
        existing.Amount = payment.Amount;
        existing.Commission = payment.Commission;
        existing.IsPaid = payment.IsPaid;
        existing.PaidDateUtc = payment.IsPaid ? (payment.PaidDateUtc ?? DateTime.UtcNow) : null;
        existing.UpdatedUtc = DateTime.UtcNow;

        await _db.UpdateAsync(existing);
        await LogAuditAsync(nameof(Payment), existing.Id, "update", $"PaymentDate={existing.PaymentDate:O}");
        QueueRemotePush();
    }

    private async Task UpsertPaymentsForSaleAsync(Sale sale)
    {
        await EnsureSalePaymentIntegrityAsync(sale.Id);
        var existing = await _db.Table<Payment>().Where(p => p.SaleId == sale.Id && !p.IsDeleted).ToListAsync();
        var now = DateTime.UtcNow;
        await _db.ExecuteAsync("UPDATE Payment SET IsDeleted = 1, DeletedUtc = ?, UpdatedUtc = ? WHERE SaleId = ?", now, now, sale.Id);

        var payments = BuildPaymentsForSale(sale, existing, now);
        await _db.InsertAllAsync(payments);
    }

    private static IEnumerable<Payment> BuildPaymentsForSale(Sale sale, IEnumerable<Payment> existing, DateTime now)
    {
        var existingByDate = existing
            .GroupBy(p => p.PaymentDate.Date)
            .ToDictionary(g => g.Key, g => g.First());
        var paymentDates = new[]
        {
            sale.SaleDate.Date.AddDays(25),
            sale.SaleDate.Date.AddDays(30),
            sale.SaleDate.Date.AddDays(35)
        };

        foreach (var paymentDate in paymentDates)
        {
            var payDate = GetCommissionPayDate(paymentDate);
            var paymentAmount = sale.Amount / 3m;
            var commission = decimal.Round(paymentAmount * (sale.CommissionPercent / 100m), 2, MidpointRounding.AwayFromZero);

            existingByDate.TryGetValue(paymentDate.Date, out var existingPayment);

            yield return new Payment
            {
                SaleId = sale.Id,
                PaymentDate = paymentDate.Date,
                PayDate = payDate,
                Amount = paymentAmount,
                Commission = commission,
                IsPaid = existingPayment?.IsPaid ?? false,
                PaidDateUtc = existingPayment?.PaidDateUtc,
                CreatedUtc = now,
                UpdatedUtc = now,
                IsDeleted = false
            };
        }
    }

    private static DateTime GetCommissionPayDate(DateTime paymentDate)
    {
        if (paymentDate.Day <= 15)
        {
            return new DateTime(paymentDate.Year, paymentDate.Month, 15);
        }

        var lastDay = DateTime.DaysInMonth(paymentDate.Year, paymentDate.Month);
        return new DateTime(paymentDate.Year, paymentDate.Month, lastDay);
    }

    private async Task EnsureSalePaymentIntegrityAsync(int saleId)
    {
        await _db.ExecuteAsync("""
            DELETE FROM Payment
            WHERE SaleId = ?
              AND (PaymentDate IS NULL OR PayDate IS NULL)
            """, saleId);

        await _db.ExecuteAsync("""
            DELETE FROM Payment
            WHERE SaleId = ?
              AND (
                CAST(strftime('%Y', PaymentDate) AS INTEGER) < 2000 OR
                CAST(strftime('%Y', PayDate) AS INTEGER) < 2000 OR
                CAST(strftime('%Y', PaymentDate) AS INTEGER) > 2100 OR
                CAST(strftime('%Y', PayDate) AS INTEGER) > 2100
              )
            """, saleId);

        await _db.ExecuteAsync("""
            DELETE FROM Payment
            WHERE Id IN (
                SELECT Id FROM (
                    SELECT Id,
                           ROW_NUMBER() OVER (PARTITION BY SaleId, date(PaymentDate) ORDER BY Id) AS rn
                    FROM Payment
                    WHERE SaleId = ?
                ) WHERE rn > 1
            )
            """, saleId);
    }
}
