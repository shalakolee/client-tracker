namespace ClientTracker.Models;

public class ClientListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LocationLine { get; set; } = string.Empty;
    public int TotalSalesCount { get; set; }
    public decimal TotalSalesAmount { get; set; }
    public int UpcomingPaymentCount { get; set; }
    public decimal UpcomingCommissionAmount { get; set; }
}
