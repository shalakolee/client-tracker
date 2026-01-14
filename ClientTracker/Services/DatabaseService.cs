using System.Linq;
using ClientTracker.Models;
using ContactModel = ClientTracker.Models.Contact;
using SQLite;
using SQLitePCL;

namespace ClientTracker.Services;

public class DatabaseService
{
    private SQLiteAsyncConnection _db;
    private readonly string _dbPath;
    private bool _initialized;

    public DatabaseService()
    {
        Batteries_V2.Init();
        _dbPath = GetProjectDatabasePath();
        _db = new SQLiteAsyncConnection(_dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache, false);
    }

    public string DatabasePath => _dbPath;

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
        return client;
    }

    public async Task UpdateClientAsync(Client client)
    {
        await EnsureInitializedAsync();
        client.UpdatedUtc = DateTime.UtcNow;
        await _db.UpdateAsync(client);
        await LogAuditAsync(nameof(Client), client.Id, "update", $"Name={client.Name}");
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
    }

    public async Task DeleteContactAsync(ContactModel contact)
    {
        await EnsureInitializedAsync();
        var now = DateTime.UtcNow;
        await _db.ExecuteAsync("UPDATE Contact SET IsDeleted = 1, DeletedUtc = ?, UpdatedUtc = ? WHERE Id = ?", now, now, contact.Id);
        await LogAuditAsync(nameof(ContactModel), contact.Id, "delete", $"Soft deleted contact {contact.Id}");
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
