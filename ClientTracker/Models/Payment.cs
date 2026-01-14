using SQLite;

namespace ClientTracker.Models;

public class Payment
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SaleId { get; set; }

    public DateTime PaymentDate { get; set; }

    [Indexed]
    public DateTime PayDate { get; set; }

    public decimal Amount { get; set; }

    public decimal Commission { get; set; }

    public bool IsPaid { get; set; }

    public DateTime? PaidDateUtc { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }

    public DateTime? DeletedUtc { get; set; }
}
