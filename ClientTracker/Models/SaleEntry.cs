namespace ClientTracker.Models;

public class SaleEntry
{
    public int Id { get; set; }
    public DateTime SaleDate { get; set; }
    public decimal Amount { get; set; }
    public decimal CommissionPercent { get; set; }
    public int ContactId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public int PaidPaymentCount { get; set; }
    public int TotalPaymentCount { get; set; }
    public double PaymentProgress { get; set; }
}
