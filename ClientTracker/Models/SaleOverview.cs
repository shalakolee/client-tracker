namespace ClientTracker.Models;

public class SaleOverview
{
    public int SaleId { get; set; }
    public DateTime SaleDate { get; set; }
    public decimal Amount { get; set; }
    public decimal CommissionPercent { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
}
