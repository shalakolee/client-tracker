using SQLite;

namespace ClientTracker.Models;

public class AuditLog
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public int EntityId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
