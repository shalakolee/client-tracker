using System;
using System.Collections.Generic;
using ClientTracker.ViewModels;

namespace ClientTracker.Models;

public class PayDateGroup : ViewModelBase
{
    private DateTime _payDate;
    private decimal _totalCommission;
    private int _paymentCount;
    private IReadOnlyList<PaymentScheduleItem> _payments = Array.Empty<PaymentScheduleItem>();
    private bool _isExpanded;

    public DateTime PayDate
    {
        get => _payDate;
        set => SetProperty(ref _payDate, value);
    }

    public decimal TotalCommission
    {
        get => _totalCommission;
        set => SetProperty(ref _totalCommission, value);
    }

    public int PaymentCount
    {
        get => _paymentCount;
        set => SetProperty(ref _paymentCount, value);
    }

    public IReadOnlyList<PaymentScheduleItem> Payments
    {
        get => _payments;
        set => SetProperty(ref _payments, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
}
