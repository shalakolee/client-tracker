using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using ClientTracker.Models;
using ClientTracker.Services;
using Microsoft.Maui.Controls;

namespace ClientTracker.ViewModels;

public class CommissionPlansViewModel : ViewModelBase
{
    private readonly DatabaseService _database;
    private readonly LocalizationResourceManager _localization;
    private string _statusMessage = string.Empty;

    public CommissionPlansViewModel(DatabaseService database, LocalizationResourceManager localization)
    {
        _database = database;
        _localization = localization;
        Plans = new ObservableCollection<CommissionPlanListItem>();
        RefreshCommand = new Command(async () => await LoadAsync());
        AddPlanCommand = new Command(async () => await Shell.Current.GoToAsync("commission-plan-edit"));
        EditPlanCommand = new Command<CommissionPlanListItem>(async item =>
        {
            if (item is null)
            {
                return;
            }

            await Shell.Current.GoToAsync("commission-plan-edit", new Dictionary<string, object>
            {
                ["planId"] = item.Id
            });
        });
    }

    public ObservableCollection<CommissionPlanListItem> Plans { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public Command RefreshCommand { get; }
    public Command AddPlanCommand { get; }
    public Command<CommissionPlanListItem> EditPlanCommand { get; }

    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            StatusMessage = string.Empty;
            Plans.Clear();
            var plans = await _database.GetCommissionPlansAsync();
            foreach (var plan in plans)
            {
                Plans.Add(new CommissionPlanListItem
                {
                    Id = plan.Id,
                    Name = plan.Name,
                    IsDefault = plan.IsDefault,
                    ClientSummary = BuildClientSummary(plan),
                    CommissionSummary = BuildCommissionSummary(plan)
                });
            }

            if (Plans.Count == 0)
            {
                StatusMessage = _localization["CommissionPlan_Empty"];
            }
        });
    }

    private string BuildClientSummary(CommissionPlan plan)
    {
        var schedule = BuildScheduleSummary(plan.ClientScheduleMode, plan.ClientPaymentCount, plan.ClientOffsetsJson, plan.ClientMonthlyDaysJson);
        return string.Format(_localization["CommissionPlan_ClientSummary"], schedule);
    }

    private string BuildCommissionSummary(CommissionPlan plan)
    {
        var schedule = BuildScheduleSummary(plan.CommissionScheduleMode, plan.CommissionPaymentCount, plan.CommissionOffsetsJson, plan.CommissionMonthlyDaysJson);
        return string.Format(_localization["CommissionPlan_CommissionSummary"], schedule);
    }

    private string BuildScheduleSummary(ScheduleMode mode, int count, string? offsetsJson, string? daysJson)
    {
        return mode == ScheduleMode.Offsets
            ? BuildOffsetsSummary(ParseList(offsetsJson))
            : BuildMonthlySummary(ParseList(daysJson), count);
    }

    private string BuildOffsetsSummary(IReadOnlyList<int> offsets)
    {
        if (offsets.Count == 0)
        {
            return _localization["CommissionPlan_NoOffsets"];
        }

        var offsetsLabel = string.Join(", ", offsets);
        return string.Format(_localization["CommissionPlan_OffsetsFormat"], offsetsLabel);
    }

    private string BuildMonthlySummary(IReadOnlyList<int> days, int count)
    {
        if (days.Count == 0)
        {
            return _localization["CommissionPlan_NoDays"];
        }

        var dayLabels = days.Select(day => day == 0 ? _localization["CommissionPlan_LastDay"] : day.ToString());
        var daysLabel = string.Join(", ", dayLabels);
        if (count > 0 && count != days.Count)
        {
            return string.Format(_localization["CommissionPlan_DaysCountFormat"], daysLabel, count);
        }

        return string.Format(_localization["CommissionPlan_DaysFormat"], daysLabel);
    }

    private static List<int> ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<int>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>();
        }
        catch
        {
            return new List<int>();
        }
    }
}

public class CommissionPlanListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public string ClientSummary { get; set; } = string.Empty;
    public string CommissionSummary { get; set; } = string.Empty;
}
