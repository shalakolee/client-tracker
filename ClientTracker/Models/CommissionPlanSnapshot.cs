namespace ClientTracker.Models;

public class CommissionPlanSnapshot
{
    public string Name { get; set; } = string.Empty;
    public ScheduleMode ClientScheduleMode { get; set; }
    public int ClientPaymentCount { get; set; }
    public List<int> ClientOffsets { get; set; } = new();
    public List<int> ClientMonthlyDays { get; set; } = new();
    public ScheduleMode CommissionScheduleMode { get; set; }
    public int CommissionPaymentCount { get; set; }
    public List<int> CommissionOffsets { get; set; } = new();
    public List<int> CommissionMonthlyDays { get; set; } = new();
    public CommissionDayHandling CommissionDayHandling { get; set; }
}
