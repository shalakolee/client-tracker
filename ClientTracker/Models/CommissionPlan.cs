using SQLite;

namespace ClientTracker.Models;

public enum ScheduleMode
{
    Offsets = 0,
    MonthlyDays = 1
}

public enum CommissionDayHandling
{
    SameDay = 0,
    NextPayout = 1
}

public class CommissionPlan
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public ScheduleMode ClientScheduleMode { get; set; }

    public int ClientPaymentCount { get; set; }

    public string ClientOffsetsJson { get; set; } = "[]";

    public string ClientMonthlyDaysJson { get; set; } = "[]";

    public ScheduleMode CommissionScheduleMode { get; set; }

    public int CommissionPaymentCount { get; set; }

    public string CommissionOffsetsJson { get; set; } = "[]";

    public string CommissionMonthlyDaysJson { get; set; } = "[]";

    public CommissionDayHandling CommissionDayHandling { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }

    public DateTime? DeletedUtc { get; set; }
}
