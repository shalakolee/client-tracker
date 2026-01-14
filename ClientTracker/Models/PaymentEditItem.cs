using ClientTracker.ViewModels;

namespace ClientTracker.Models;

public class PaymentEditItem : ViewModelBase
{
    private DateTime _paymentDate;
    private decimal _amount;
    private bool _isPaid;
    private decimal _commission;
    private DateTime _payDate;

    public int PaymentId { get; set; }
    public int SaleId { get; set; }
    public int PaymentNumber { get; set; }

    public DateTime PaymentDate
    {
        get => _paymentDate;
        set => SetProperty(ref _paymentDate, value);
    }

    public decimal Amount
    {
        get => _amount;
        set => SetProperty(ref _amount, value);
    }

    public decimal Commission
    {
        get => _commission;
        set => SetProperty(ref _commission, value);
    }

    public DateTime PayDate
    {
        get => _payDate;
        set => SetProperty(ref _payDate, value);
    }

    public bool IsPaid
    {
        get => _isPaid;
        set => SetProperty(ref _isPaid, value);
    }
}
