using ClientTracker.ViewModels;
using System.Globalization;

namespace ClientTracker.Models;

public class PaymentEditItem : ViewModelBase
{
    private DateTime _paymentDate;
    private decimal _amount;
    private string _amountText = string.Empty;
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
        set
        {
            if (SetProperty(ref _amount, value))
            {
                var formatted = _amount.ToString("0.00", CultureInfo.CurrentCulture);
                if (_amountText != formatted)
                {
                    _amountText = formatted;
                    OnPropertyChanged(nameof(AmountText));
                }
            }
        }
    }

    public string AmountText
    {
        get => string.IsNullOrWhiteSpace(_amountText)
            ? Amount.ToString("0.00", CultureInfo.CurrentCulture)
            : _amountText;
        set
        {
            if (SetProperty(ref _amountText, value))
            {
                if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed))
                {
                    Amount = parsed;
                }
            }
        }
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
