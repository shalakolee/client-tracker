namespace ClientTracker.Models;

public class PaymentScheduleItem : ViewModels.ViewModelBase
{
    private bool _isPaid;

    public int PaymentId { get; set; }
    public DateTime PayDate { get; set; }
    public DateTime PaymentDate { get; set; }
    public int PaymentNumber { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal SaleAmount { get; set; }
    public decimal PaymentAmount { get; set; }
    public decimal Commission { get; set; }
    public DateTime? PaidDateUtc { get; set; }

    public bool IsPaid
    {
        get => _isPaid;
        set => SetProperty(ref _isPaid, value);
    }
}
