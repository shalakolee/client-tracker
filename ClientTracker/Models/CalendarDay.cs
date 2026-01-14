using ClientTracker.ViewModels;

namespace ClientTracker.Models;

public class CalendarDay : ViewModelBase
{
    private bool _isSelected;

    public DateTime Date { get; set; }
    public string DayLabel { get; set; } = string.Empty;
    public bool IsCurrentMonth { get; set; }
    public decimal CommissionTotal { get; set; }
    public int PaymentCount { get; set; }
    public bool IsPayDate { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
