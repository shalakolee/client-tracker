using System.Globalization;
using Microsoft.Data.Sqlite;
using MySqlConnector;

static string? GetArg(string[] args, string name)
{
    var index = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
    if (index < 0 || index + 1 >= args.Length)
    {
        return null;
    }

    return args[index + 1];
}

static string GetDefaultSqlitePath()
{
    var baseDir = AppContext.BaseDirectory;
    var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
    return Path.Combine(repoRoot, "ClientTracker", "data", "client-tracker.db3");
}

static DateTime? ReadDateTime(object? value, bool assumeUtc)
{
    if (value is null || value is DBNull)
    {
        return null;
    }

    if (value is DateTime dt)
    {
        return assumeUtc ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt;
    }

    if (value is long l)
    {
        // Heuristics:
        // - sqlite-net often stores DateTime as TEXT, but legacy values may be ticks or epoch.
        // - ticks since 0001-01-01 are very large (>= 10^14 in modern dates)
        if (l >= 100_000_000_000_000)
        {
            var fromTicks = new DateTime(l, assumeUtc ? DateTimeKind.Utc : DateTimeKind.Unspecified);
            return fromTicks;
        }

        // epoch milliseconds
        if (l >= 1_000_000_000_000)
        {
            var dto = DateTimeOffset.FromUnixTimeMilliseconds(l);
            return assumeUtc ? dto.UtcDateTime : dto.DateTime;
        }

        // epoch seconds
        if (l >= 1_000_000_000)
        {
            var dto = DateTimeOffset.FromUnixTimeSeconds(l);
            return assumeUtc ? dto.UtcDateTime : dto.DateTime;
        }

        return new DateTime(l, assumeUtc ? DateTimeKind.Utc : DateTimeKind.Unspecified);
    }

    if (value is double d)
    {
        var l2 = (long)d;
        return ReadDateTime(l2, assumeUtc);
    }

    if (value is string s)
    {
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return assumeUtc ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc) : parsed;
        }

        if (DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
        {
            return assumeUtc ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc) : parsed;
        }

        if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var asLong))
        {
            return ReadDateTime(asLong, assumeUtc);
        }
    }

    throw new InvalidOperationException($"Unsupported DateTime value: {value} ({value.GetType().FullName})");
}

static DateTime ReadDateTimeOrThrow(object? value, bool assumeUtc)
    => ReadDateTime(value, assumeUtc) ?? throw new InvalidOperationException("Expected DateTime but value was null");

static string ReadString(object? value) => value is null or DBNull ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
static int ReadInt(object? value) => value is null or DBNull ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
static bool ReadBool(object? value) => ReadInt(value) != 0;
static decimal ReadDecimal(object? value) => value is null or DBNull ? 0m : Convert.ToDecimal(value, CultureInfo.InvariantCulture);

var sqlitePath = GetArg(args, "--sqlite") ?? GetDefaultSqlitePath();
var mysqlConnectionString = GetArg(args, "--mysql") ?? Environment.GetEnvironmentVariable("CLIENTTRACKER_MYSQL_CONNECTION");

if (string.IsNullOrWhiteSpace(mysqlConnectionString))
{
    Console.Error.WriteLine("Missing MySQL connection string. Provide --mysql or set CLIENTTRACKER_MYSQL_CONNECTION.");
    Environment.Exit(2);
    return;
}

if (!File.Exists(sqlitePath))
{
    Console.Error.WriteLine($"SQLite file not found: {sqlitePath}");
    Environment.Exit(2);
    return;
}

Console.WriteLine($"SQLite: {sqlitePath}");
Console.WriteLine("MySQL:  (connection string provided via args/env)");

var sqliteConnectionString = new SqliteConnectionStringBuilder { DataSource = sqlitePath, Mode = SqliteOpenMode.ReadOnly }.ToString();
await using var sqlite = new SqliteConnection(sqliteConnectionString);
await sqlite.OpenAsync();

await using var mysql = new MySqlConnection(mysqlConnectionString);
await mysql.OpenAsync();

await using var tx = await mysql.BeginTransactionAsync();

static async Task ExecAsync(MySqlConnection conn, MySqlTransaction tx, string sql)
{
    await using var cmd = new MySqlCommand(sql, conn, tx);
    await cmd.ExecuteNonQueryAsync();
}

await ExecAsync(mysql, tx, "SET sql_mode='STRICT_TRANS_TABLES,NO_ENGINE_SUBSTITUTION';");
await ExecAsync(mysql, tx, "SET time_zone = '+00:00';");

// Schema (uses the same table names as the existing SQLite DB for simpler migration)
await ExecAsync(mysql, tx, """
CREATE TABLE IF NOT EXISTS Client (
  Id INT NOT NULL AUTO_INCREMENT,
  Name VARCHAR(255) NOT NULL,
  AddressLine1 VARCHAR(255) NOT NULL,
  AddressLine2 VARCHAR(255) NOT NULL,
  City VARCHAR(255) NOT NULL,
  StateProvince VARCHAR(255) NOT NULL,
  PostalCode VARCHAR(64) NOT NULL,
  Country VARCHAR(255) NOT NULL,
  TaxId VARCHAR(255) NOT NULL,
  ContactName VARCHAR(255) NOT NULL,
  ContactEmail VARCHAR(255) NOT NULL,
  ContactPhone VARCHAR(255) NOT NULL,
  CreatedUtc DATETIME(6) NOT NULL,
  UpdatedUtc DATETIME(6) NOT NULL,
  IsDeleted TINYINT(1) NOT NULL DEFAULT 0,
  DeletedUtc DATETIME(6) NULL,
  PRIMARY KEY (Id),
  INDEX IX_Client_Name (Name),
  INDEX IX_Client_IsDeleted (IsDeleted)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

await ExecAsync(mysql, tx, """
CREATE TABLE IF NOT EXISTS Contact (
  Id INT NOT NULL AUTO_INCREMENT,
  ClientId INT NOT NULL,
  Name VARCHAR(255) NOT NULL,
  Email VARCHAR(255) NOT NULL,
  Phone VARCHAR(255) NOT NULL,
  Notes TEXT NOT NULL,
  CreatedUtc DATETIME(6) NOT NULL,
  UpdatedUtc DATETIME(6) NOT NULL,
  IsDeleted TINYINT(1) NOT NULL DEFAULT 0,
  DeletedUtc DATETIME(6) NULL,
  PRIMARY KEY (Id),
  INDEX IX_Contact_ClientId (ClientId),
  INDEX IX_Contact_IsDeleted (IsDeleted),
  CONSTRAINT FK_Contact_Client FOREIGN KEY (ClientId) REFERENCES Client(Id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

await ExecAsync(mysql, tx, """
CREATE TABLE IF NOT EXISTS Sale (
  Id INT NOT NULL AUTO_INCREMENT,
  ClientId INT NOT NULL,
  ContactId INT NOT NULL DEFAULT 0,
  InvoiceNumber VARCHAR(255) NOT NULL,
  SaleDate DATE NOT NULL,
  Amount DECIMAL(18,2) NOT NULL,
  CommissionPercent DECIMAL(9,4) NOT NULL,
  CreatedUtc DATETIME(6) NOT NULL,
  UpdatedUtc DATETIME(6) NOT NULL,
  IsDeleted TINYINT(1) NOT NULL DEFAULT 0,
  DeletedUtc DATETIME(6) NULL,
  PRIMARY KEY (Id),
  INDEX IX_Sale_ClientId (ClientId),
  INDEX IX_Sale_IsDeleted (IsDeleted),
  CONSTRAINT FK_Sale_Client FOREIGN KEY (ClientId) REFERENCES Client(Id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

await ExecAsync(mysql, tx, """
CREATE TABLE IF NOT EXISTS Payment (
  Id INT NOT NULL AUTO_INCREMENT,
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
  INDEX IX_Payment_IsDeleted (IsDeleted),
  CONSTRAINT FK_Payment_Sale FOREIGN KEY (SaleId) REFERENCES Sale(Id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

await ExecAsync(mysql, tx, """
CREATE TABLE IF NOT EXISTS AuditLog (
  Id INT NOT NULL AUTO_INCREMENT,
  EntityType VARCHAR(128) NOT NULL,
  EntityId INT NOT NULL,
  Action VARCHAR(64) NOT NULL,
  Details TEXT NOT NULL,
  TimestampUtc DATETIME(6) NOT NULL,
  PRIMARY KEY (Id),
  INDEX IX_AuditLog_Entity (EntityType, EntityId),
  INDEX IX_AuditLog_TimestampUtc (TimestampUtc)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

// Clear existing rows (optional; keeps this migrator idempotent by replacing data)
await ExecAsync(mysql, tx, "SET FOREIGN_KEY_CHECKS=0;");
await ExecAsync(mysql, tx, "TRUNCATE TABLE AuditLog;");
await ExecAsync(mysql, tx, "TRUNCATE TABLE Payment;");
await ExecAsync(mysql, tx, "TRUNCATE TABLE Sale;");
await ExecAsync(mysql, tx, "TRUNCATE TABLE Contact;");
await ExecAsync(mysql, tx, "TRUNCATE TABLE Client;");
await ExecAsync(mysql, tx, "SET FOREIGN_KEY_CHECKS=1;");

static async Task<int> ScalarCountAsync(SqliteConnection sqlite, string table)
{
    await using var cmd = sqlite.CreateCommand();
    cmd.CommandText = $"SELECT COUNT(*) FROM {table}";
    var result = await cmd.ExecuteScalarAsync();
    return Convert.ToInt32(result, CultureInfo.InvariantCulture);
}

Console.WriteLine($"Export counts: Client={await ScalarCountAsync(sqlite, "Client")}, Contact={await ScalarCountAsync(sqlite, "Contact")}, Sale={await ScalarCountAsync(sqlite, "Sale")}, Payment={await ScalarCountAsync(sqlite, "Payment")}, AuditLog={await ScalarCountAsync(sqlite, "AuditLog")}");

static async IAsyncEnumerable<SqliteDataReader> QueryRowsAsync(SqliteConnection sqlite, string sql)
{
    var cmd = sqlite.CreateCommand();
    cmd.CommandText = sql;
    var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        yield return reader;
    }
    await reader.DisposeAsync();
    await cmd.DisposeAsync();
}

// Client
{
    const string insertSql = """
INSERT INTO Client (Id, Name, AddressLine1, AddressLine2, City, StateProvince, PostalCode, Country, TaxId, ContactName, ContactEmail, ContactPhone, CreatedUtc, UpdatedUtc, IsDeleted, DeletedUtc)
VALUES (@Id, @Name, @AddressLine1, @AddressLine2, @City, @StateProvince, @PostalCode, @Country, @TaxId, @ContactName, @ContactEmail, @ContactPhone, @CreatedUtc, @UpdatedUtc, @IsDeleted, @DeletedUtc);
""";
    await using var insert = new MySqlCommand(insertSql, mysql, tx);
    insert.Parameters.Add("@Id", MySqlDbType.Int32);
    insert.Parameters.Add("@Name", MySqlDbType.VarChar);
    insert.Parameters.Add("@AddressLine1", MySqlDbType.VarChar);
    insert.Parameters.Add("@AddressLine2", MySqlDbType.VarChar);
    insert.Parameters.Add("@City", MySqlDbType.VarChar);
    insert.Parameters.Add("@StateProvince", MySqlDbType.VarChar);
    insert.Parameters.Add("@PostalCode", MySqlDbType.VarChar);
    insert.Parameters.Add("@Country", MySqlDbType.VarChar);
    insert.Parameters.Add("@TaxId", MySqlDbType.VarChar);
    insert.Parameters.Add("@ContactName", MySqlDbType.VarChar);
    insert.Parameters.Add("@ContactEmail", MySqlDbType.VarChar);
    insert.Parameters.Add("@ContactPhone", MySqlDbType.VarChar);
    insert.Parameters.Add("@CreatedUtc", MySqlDbType.DateTime);
    insert.Parameters.Add("@UpdatedUtc", MySqlDbType.DateTime);
    insert.Parameters.Add("@IsDeleted", MySqlDbType.Bool);
    insert.Parameters.Add("@DeletedUtc", MySqlDbType.DateTime);

    await foreach (var row in QueryRowsAsync(sqlite, "SELECT * FROM Client"))
    {
        insert.Parameters["@Id"].Value = ReadInt(row["Id"]);
        insert.Parameters["@Name"].Value = ReadString(row["Name"]);
        insert.Parameters["@AddressLine1"].Value = ReadString(row["AddressLine1"]);
        insert.Parameters["@AddressLine2"].Value = ReadString(row["AddressLine2"]);
        insert.Parameters["@City"].Value = ReadString(row["City"]);
        insert.Parameters["@StateProvince"].Value = ReadString(row["StateProvince"]);
        insert.Parameters["@PostalCode"].Value = ReadString(row["PostalCode"]);
        insert.Parameters["@Country"].Value = ReadString(row["Country"]);
        insert.Parameters["@TaxId"].Value = ReadString(row["TaxId"]);
        insert.Parameters["@ContactName"].Value = ReadString(row["ContactName"]);
        insert.Parameters["@ContactEmail"].Value = ReadString(row["ContactEmail"]);
        insert.Parameters["@ContactPhone"].Value = ReadString(row["ContactPhone"]);
        insert.Parameters["@CreatedUtc"].Value = ReadDateTimeOrThrow(row["CreatedUtc"], assumeUtc: true);
        insert.Parameters["@UpdatedUtc"].Value = ReadDateTimeOrThrow(row["UpdatedUtc"], assumeUtc: true);
        insert.Parameters["@IsDeleted"].Value = ReadBool(row["IsDeleted"]);
        insert.Parameters["@DeletedUtc"].Value = ReadDateTime(row["DeletedUtc"], assumeUtc: true) ?? (object)DBNull.Value;

        await insert.ExecuteNonQueryAsync();
    }
}

// Contact
{
    const string insertSql = """
INSERT INTO Contact (Id, ClientId, Name, Email, Phone, Notes, CreatedUtc, UpdatedUtc, IsDeleted, DeletedUtc)
VALUES (@Id, @ClientId, @Name, @Email, @Phone, @Notes, @CreatedUtc, @UpdatedUtc, @IsDeleted, @DeletedUtc);
""";
    await using var insert = new MySqlCommand(insertSql, mysql, tx);
    insert.Parameters.Add("@Id", MySqlDbType.Int32);
    insert.Parameters.Add("@ClientId", MySqlDbType.Int32);
    insert.Parameters.Add("@Name", MySqlDbType.VarChar);
    insert.Parameters.Add("@Email", MySqlDbType.VarChar);
    insert.Parameters.Add("@Phone", MySqlDbType.VarChar);
    insert.Parameters.Add("@Notes", MySqlDbType.Text);
    insert.Parameters.Add("@CreatedUtc", MySqlDbType.DateTime);
    insert.Parameters.Add("@UpdatedUtc", MySqlDbType.DateTime);
    insert.Parameters.Add("@IsDeleted", MySqlDbType.Bool);
    insert.Parameters.Add("@DeletedUtc", MySqlDbType.DateTime);

    await foreach (var row in QueryRowsAsync(sqlite, "SELECT * FROM Contact"))
    {
        insert.Parameters["@Id"].Value = ReadInt(row["Id"]);
        insert.Parameters["@ClientId"].Value = ReadInt(row["ClientId"]);
        insert.Parameters["@Name"].Value = ReadString(row["Name"]);
        insert.Parameters["@Email"].Value = ReadString(row["Email"]);
        insert.Parameters["@Phone"].Value = ReadString(row["Phone"]);
        insert.Parameters["@Notes"].Value = ReadString(row["Notes"]);
        insert.Parameters["@CreatedUtc"].Value = ReadDateTimeOrThrow(row["CreatedUtc"], assumeUtc: true);
        insert.Parameters["@UpdatedUtc"].Value = ReadDateTimeOrThrow(row["UpdatedUtc"], assumeUtc: true);
        insert.Parameters["@IsDeleted"].Value = ReadBool(row["IsDeleted"]);
        insert.Parameters["@DeletedUtc"].Value = ReadDateTime(row["DeletedUtc"], assumeUtc: true) ?? (object)DBNull.Value;

        await insert.ExecuteNonQueryAsync();
    }
}

// Sale
{
    const string insertSql = """
INSERT INTO Sale (Id, ClientId, ContactId, InvoiceNumber, SaleDate, Amount, CommissionPercent, CreatedUtc, UpdatedUtc, IsDeleted, DeletedUtc)
VALUES (@Id, @ClientId, @ContactId, @InvoiceNumber, @SaleDate, @Amount, @CommissionPercent, @CreatedUtc, @UpdatedUtc, @IsDeleted, @DeletedUtc);
""";
    await using var insert = new MySqlCommand(insertSql, mysql, tx);
    insert.Parameters.Add("@Id", MySqlDbType.Int32);
    insert.Parameters.Add("@ClientId", MySqlDbType.Int32);
    insert.Parameters.Add("@ContactId", MySqlDbType.Int32);
    insert.Parameters.Add("@InvoiceNumber", MySqlDbType.VarChar);
    insert.Parameters.Add("@SaleDate", MySqlDbType.Date);
    insert.Parameters.Add("@Amount", MySqlDbType.NewDecimal);
    insert.Parameters.Add("@CommissionPercent", MySqlDbType.NewDecimal);
    insert.Parameters.Add("@CreatedUtc", MySqlDbType.DateTime);
    insert.Parameters.Add("@UpdatedUtc", MySqlDbType.DateTime);
    insert.Parameters.Add("@IsDeleted", MySqlDbType.Bool);
    insert.Parameters.Add("@DeletedUtc", MySqlDbType.DateTime);

    await foreach (var row in QueryRowsAsync(sqlite, "SELECT * FROM Sale"))
    {
        insert.Parameters["@Id"].Value = ReadInt(row["Id"]);
        insert.Parameters["@ClientId"].Value = ReadInt(row["ClientId"]);
        insert.Parameters["@ContactId"].Value = ReadInt(row["ContactId"]);
        insert.Parameters["@InvoiceNumber"].Value = ReadString(row["InvoiceNumber"]);
        insert.Parameters["@SaleDate"].Value = ReadDateTimeOrThrow(row["SaleDate"], assumeUtc: false).Date;
        insert.Parameters["@Amount"].Value = ReadDecimal(row["Amount"]);
        insert.Parameters["@CommissionPercent"].Value = ReadDecimal(row["CommissionPercent"]);
        insert.Parameters["@CreatedUtc"].Value = ReadDateTimeOrThrow(row["CreatedUtc"], assumeUtc: true);
        insert.Parameters["@UpdatedUtc"].Value = ReadDateTimeOrThrow(row["UpdatedUtc"], assumeUtc: true);
        insert.Parameters["@IsDeleted"].Value = ReadBool(row["IsDeleted"]);
        insert.Parameters["@DeletedUtc"].Value = ReadDateTime(row["DeletedUtc"], assumeUtc: true) ?? (object)DBNull.Value;

        await insert.ExecuteNonQueryAsync();
    }
}

// Payment
{
    const string insertSql = """
INSERT INTO Payment (Id, SaleId, PaymentDate, PayDate, Amount, Commission, IsPaid, PaidDateUtc, CreatedUtc, UpdatedUtc, IsDeleted, DeletedUtc)
VALUES (@Id, @SaleId, @PaymentDate, @PayDate, @Amount, @Commission, @IsPaid, @PaidDateUtc, @CreatedUtc, @UpdatedUtc, @IsDeleted, @DeletedUtc);
""";
    await using var insert = new MySqlCommand(insertSql, mysql, tx);
    insert.Parameters.Add("@Id", MySqlDbType.Int32);
    insert.Parameters.Add("@SaleId", MySqlDbType.Int32);
    insert.Parameters.Add("@PaymentDate", MySqlDbType.Date);
    insert.Parameters.Add("@PayDate", MySqlDbType.Date);
    insert.Parameters.Add("@Amount", MySqlDbType.NewDecimal);
    insert.Parameters.Add("@Commission", MySqlDbType.NewDecimal);
    insert.Parameters.Add("@IsPaid", MySqlDbType.Bool);
    insert.Parameters.Add("@PaidDateUtc", MySqlDbType.DateTime);
    insert.Parameters.Add("@CreatedUtc", MySqlDbType.DateTime);
    insert.Parameters.Add("@UpdatedUtc", MySqlDbType.DateTime);
    insert.Parameters.Add("@IsDeleted", MySqlDbType.Bool);
    insert.Parameters.Add("@DeletedUtc", MySqlDbType.DateTime);

    await foreach (var row in QueryRowsAsync(sqlite, "SELECT * FROM Payment"))
    {
        insert.Parameters["@Id"].Value = ReadInt(row["Id"]);
        insert.Parameters["@SaleId"].Value = ReadInt(row["SaleId"]);
        insert.Parameters["@PaymentDate"].Value = ReadDateTimeOrThrow(row["PaymentDate"], assumeUtc: false).Date;
        insert.Parameters["@PayDate"].Value = ReadDateTimeOrThrow(row["PayDate"], assumeUtc: false).Date;
        insert.Parameters["@Amount"].Value = ReadDecimal(row["Amount"]);
        insert.Parameters["@Commission"].Value = ReadDecimal(row["Commission"]);
        insert.Parameters["@IsPaid"].Value = ReadBool(row["IsPaid"]);
        insert.Parameters["@PaidDateUtc"].Value = ReadDateTime(row["PaidDateUtc"], assumeUtc: true) ?? (object)DBNull.Value;
        insert.Parameters["@CreatedUtc"].Value = ReadDateTimeOrThrow(row["CreatedUtc"], assumeUtc: true);
        insert.Parameters["@UpdatedUtc"].Value = ReadDateTimeOrThrow(row["UpdatedUtc"], assumeUtc: true);
        insert.Parameters["@IsDeleted"].Value = ReadBool(row["IsDeleted"]);
        insert.Parameters["@DeletedUtc"].Value = ReadDateTime(row["DeletedUtc"], assumeUtc: true) ?? (object)DBNull.Value;

        await insert.ExecuteNonQueryAsync();
    }
}

// AuditLog
{
    const string insertSql = """
INSERT INTO AuditLog (Id, EntityType, EntityId, Action, Details, TimestampUtc)
VALUES (@Id, @EntityType, @EntityId, @Action, @Details, @TimestampUtc);
""";
    await using var insert = new MySqlCommand(insertSql, mysql, tx);
    insert.Parameters.Add("@Id", MySqlDbType.Int32);
    insert.Parameters.Add("@EntityType", MySqlDbType.VarChar);
    insert.Parameters.Add("@EntityId", MySqlDbType.Int32);
    insert.Parameters.Add("@Action", MySqlDbType.VarChar);
    insert.Parameters.Add("@Details", MySqlDbType.Text);
    insert.Parameters.Add("@TimestampUtc", MySqlDbType.DateTime);

    await foreach (var row in QueryRowsAsync(sqlite, "SELECT * FROM AuditLog"))
    {
        insert.Parameters["@Id"].Value = ReadInt(row["Id"]);
        insert.Parameters["@EntityType"].Value = ReadString(row["EntityType"]);
        insert.Parameters["@EntityId"].Value = ReadInt(row["EntityId"]);
        insert.Parameters["@Action"].Value = ReadString(row["Action"]);
        insert.Parameters["@Details"].Value = ReadString(row["Details"]);
        insert.Parameters["@TimestampUtc"].Value = ReadDateTimeOrThrow(row["TimestampUtc"], assumeUtc: true);

        await insert.ExecuteNonQueryAsync();
    }
}

await tx.CommitAsync();
Console.WriteLine("Migration complete.");
