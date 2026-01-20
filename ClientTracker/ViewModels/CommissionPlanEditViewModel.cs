using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using ClientTracker.Models;
using ClientTracker.Services;
using Microsoft.Maui.Controls;

namespace ClientTracker.ViewModels;

public class CommissionPlanEditViewModel : ViewModelBase, IQueryAttributable
{
    private readonly DatabaseService _database;
    private readonly LocalizationResourceManager _localization;
    private int _planId;
    private string _name = string.Empty;
    private bool _isDefault;
    private ScheduleModeOption? _selectedClientMode;
    private ScheduleModeOption? _selectedCommissionMode;
    private DayHandlingOption? _selectedDayHandling;
    private string _statusMessage = string.Empty;
    private string _pageTitle = string.Empty;
    private string _clientOffsetsSummary = string.Empty;
    private string _clientDaysSummary = string.Empty;
    private string _commissionOffsetsSummary = string.Empty;
    private string _commissionDaysSummary = string.Empty;
    private bool _isSelectionDialogOpen;
    private string _selectionDialogTitle = string.Empty;
    private ObservableCollection<SelectableOption> _selectionDialogOptions = new();
    private List<int> _selectionBackup = new();
    private SelectionDialogType _selectionDialogType;

    public CommissionPlanEditViewModel(DatabaseService database, LocalizationResourceManager localization)
    {
        _database = database;
        _localization = localization;
        ClientModes = new List<ScheduleModeOption>
        {
            new(ScheduleMode.Offsets, "CommissionPlan_ModeOffsets"),
            new(ScheduleMode.MonthlyDays, "CommissionPlan_ModeMonthly")
        };
        CommissionModes = new List<ScheduleModeOption>
        {
            new(ScheduleMode.Offsets, "CommissionPlan_ModeOffsets"),
            new(ScheduleMode.MonthlyDays, "CommissionPlan_ModeMonthly")
        };
        DayHandlingOptions = new List<DayHandlingOption>
        {
            new(CommissionDayHandling.SameDay, "CommissionPlan_SameDay"),
            new(CommissionDayHandling.NextPayout, "CommissionPlan_NextPayout")
        };

        ClientOffsetOptions = BuildOffsetOptions();
        CommissionOffsetOptions = BuildOffsetOptions();
        ClientDayOptions = BuildDayOptions();
        CommissionDayOptions = BuildDayOptions();
        ToggleOptionCommand = new Command<SelectableOption>(option =>
        {
            if (option is null)
            {
                return;
            }

            option.IsSelected = !option.IsSelected;
        });

        OpenClientOffsetsDialogCommand = new Command(() => OpenSelectionDialog(SelectionDialogType.ClientOffsets, ClientOffsetOptions, _localization["CommissionPlan_OffsetsLabel"]));
        OpenClientDaysDialogCommand = new Command(() => OpenSelectionDialog(SelectionDialogType.ClientDays, ClientDayOptions, _localization["CommissionPlan_DaysLabel"]));
        OpenCommissionOffsetsDialogCommand = new Command(() => OpenSelectionDialog(SelectionDialogType.CommissionOffsets, CommissionOffsetOptions, _localization["CommissionPlan_OffsetsLabel"]));
        OpenCommissionDaysDialogCommand = new Command(() => OpenSelectionDialog(SelectionDialogType.CommissionDays, CommissionDayOptions, _localization["CommissionPlan_DaysLabel"]));
        ApplySelectionDialogCommand = new Command(CloseSelectionDialog);
        CancelSelectionDialogCommand = new Command(CancelSelectionDialog);

        SaveCommand = new Command(async () => await SaveAsync());

        WireSelectionOptions(ClientOffsetOptions, UpdateClientOffsetsSummary);
        WireSelectionOptions(ClientDayOptions, UpdateClientDaysSummary);
        WireSelectionOptions(CommissionOffsetOptions, UpdateCommissionOffsetsSummary);
        WireSelectionOptions(CommissionDayOptions, UpdateCommissionDaysSummary);
    }

    public List<ScheduleModeOption> ClientModes { get; }
    public List<ScheduleModeOption> CommissionModes { get; }
    public List<DayHandlingOption> DayHandlingOptions { get; }
    public ObservableCollection<SelectableOption> ClientOffsetOptions { get; }
    public ObservableCollection<SelectableOption> CommissionOffsetOptions { get; }
    public ObservableCollection<SelectableOption> ClientDayOptions { get; }
    public ObservableCollection<SelectableOption> CommissionDayOptions { get; }

    public string PageTitle
    {
        get => _pageTitle;
        private set => SetProperty(ref _pageTitle, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool IsDefault
    {
        get => _isDefault;
        set => SetProperty(ref _isDefault, value);
    }

    public ScheduleModeOption? SelectedClientMode
    {
        get => _selectedClientMode;
        set
        {
            if (SetProperty(ref _selectedClientMode, value))
            {
                OnPropertyChanged(nameof(IsClientOffsetsMode));
                OnPropertyChanged(nameof(IsClientMonthlyMode));
            }
        }
    }

    public ScheduleModeOption? SelectedCommissionMode
    {
        get => _selectedCommissionMode;
        set
        {
            if (SetProperty(ref _selectedCommissionMode, value))
            {
                OnPropertyChanged(nameof(IsCommissionOffsetsMode));
                OnPropertyChanged(nameof(IsCommissionMonthlyMode));
            }
        }
    }

    public DayHandlingOption? SelectedDayHandling
    {
        get => _selectedDayHandling;
        set => SetProperty(ref _selectedDayHandling, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string ClientOffsetsSummary
    {
        get => _clientOffsetsSummary;
        private set => SetProperty(ref _clientOffsetsSummary, value);
    }

    public string ClientDaysSummary
    {
        get => _clientDaysSummary;
        private set => SetProperty(ref _clientDaysSummary, value);
    }

    public string CommissionOffsetsSummary
    {
        get => _commissionOffsetsSummary;
        private set => SetProperty(ref _commissionOffsetsSummary, value);
    }

    public string CommissionDaysSummary
    {
        get => _commissionDaysSummary;
        private set => SetProperty(ref _commissionDaysSummary, value);
    }

    public bool IsSelectionDialogOpen
    {
        get => _isSelectionDialogOpen;
        private set => SetProperty(ref _isSelectionDialogOpen, value);
    }

    public string SelectionDialogTitle
    {
        get => _selectionDialogTitle;
        private set => SetProperty(ref _selectionDialogTitle, value);
    }

    public ObservableCollection<SelectableOption> SelectionDialogOptions
    {
        get => _selectionDialogOptions;
        private set => SetProperty(ref _selectionDialogOptions, value);
    }

    public bool IsClientOffsetsMode => SelectedClientMode?.Mode == ScheduleMode.Offsets;
    public bool IsClientMonthlyMode => SelectedClientMode?.Mode == ScheduleMode.MonthlyDays;
    public bool IsCommissionOffsetsMode => SelectedCommissionMode?.Mode == ScheduleMode.Offsets;
    public bool IsCommissionMonthlyMode => SelectedCommissionMode?.Mode == ScheduleMode.MonthlyDays;

    public Command SaveCommand { get; }
    public Command<SelectableOption> ToggleOptionCommand { get; }
    public Command OpenClientOffsetsDialogCommand { get; }
    public Command OpenClientDaysDialogCommand { get; }
    public Command OpenCommissionOffsetsDialogCommand { get; }
    public Command OpenCommissionDaysDialogCommand { get; }
    public Command ApplySelectionDialogCommand { get; }
    public Command CancelSelectionDialogCommand { get; }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("planId", out var value) && value is int id)
        {
            _planId = id;
        }

        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        StatusMessage = string.Empty;
        SelectedClientMode = ClientModes.First();
        SelectedCommissionMode = CommissionModes.First();
        SelectedDayHandling = DayHandlingOptions.First();
        if (_planId <= 0)
        {
            PageTitle = _localization["CommissionPlan_CreateTitle"];
            Name = string.Empty;
            IsDefault = false;
            SetSelections(ClientOffsetOptions, new[] { 25, 30, 35 });
            SetSelections(ClientDayOptions, Array.Empty<int>());
            SetSelections(CommissionOffsetOptions, Array.Empty<int>());
            SetSelections(CommissionDayOptions, new[] { 15, 0 });
            UpdateSelectionSummaries();
            return;
        }

        PageTitle = _localization["CommissionPlan_EditTitle"];
        var plan = await _database.GetCommissionPlanByIdAsync(_planId);
        if (plan is null)
        {
            StatusMessage = _localization["CommissionPlan_NotFound"];
            return;
        }

        Name = plan.Name;
        IsDefault = plan.IsDefault;
        SelectedClientMode = ClientModes.FirstOrDefault(m => m.Mode == plan.ClientScheduleMode) ?? ClientModes.First();
        SelectedCommissionMode = CommissionModes.FirstOrDefault(m => m.Mode == plan.CommissionScheduleMode) ?? CommissionModes.First();
        SelectedDayHandling = DayHandlingOptions.FirstOrDefault(o => o.Handling == plan.CommissionDayHandling) ?? DayHandlingOptions.First();
        SetSelections(ClientOffsetOptions, ParseList(plan.ClientOffsetsJson));
        SetSelections(ClientDayOptions, ParseList(plan.ClientMonthlyDaysJson));
        SetSelections(CommissionOffsetOptions, ParseList(plan.CommissionOffsetsJson));
        SetSelections(CommissionDayOptions, ParseList(plan.CommissionMonthlyDaysJson));
        UpdateSelectionSummaries();
    }

    private async Task SaveAsync()
    {
        StatusMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusMessage = _localization["CommissionPlan_NameRequired"];
            return;
        }

        if (SelectedClientMode is null || SelectedCommissionMode is null || SelectedDayHandling is null)
        {
            StatusMessage = _localization["CommissionPlan_ModeRequired"];
            return;
        }

        var clientOffsets = SelectedOptions(ClientOffsetOptions);
        var clientDays = SelectedOptions(ClientDayOptions);
        var commissionOffsets = SelectedOptions(CommissionOffsetOptions);
        var commissionDays = SelectedOptions(CommissionDayOptions);

        if (!TryBuildSchedule(SelectedClientMode.Mode, clientOffsets, clientDays, out var clientCount, out var clientError))
        {
            StatusMessage = clientError;
            return;
        }

        if (!TryBuildSchedule(SelectedCommissionMode.Mode, commissionOffsets, commissionDays, out var commissionCount, out var commissionError))
        {
            StatusMessage = commissionError;
            return;
        }

        var plan = new CommissionPlan
        {
            Id = _planId,
            Name = Name.Trim(),
            IsDefault = IsDefault,
            ClientScheduleMode = SelectedClientMode.Mode,
            ClientPaymentCount = clientCount,
            ClientOffsetsJson = JsonSerializer.Serialize(clientOffsets),
            ClientMonthlyDaysJson = JsonSerializer.Serialize(clientDays),
            CommissionScheduleMode = SelectedCommissionMode.Mode,
            CommissionPaymentCount = commissionCount,
            CommissionOffsetsJson = JsonSerializer.Serialize(commissionOffsets),
            CommissionMonthlyDaysJson = JsonSerializer.Serialize(commissionDays),
            CommissionDayHandling = SelectedDayHandling.Handling,
            IsDeleted = false
        };

        await _database.SaveCommissionPlanAsync(plan);
        await Shell.Current.GoToAsync("..");
    }

    private bool TryBuildSchedule(ScheduleMode mode, List<int> offsets, List<int> days, out int count, out string error)
    {
        count = 0;
        error = string.Empty;

        if (mode == ScheduleMode.Offsets)
        {
            if (offsets.Count == 0)
            {
                error = _localization["CommissionPlan_OffsetsRequired"];
                return false;
            }

            count = offsets.Count;
            return true;
        }

        if (days.Count == 0)
        {
            error = _localization["CommissionPlan_DaysRequired"];
            return false;
        }

        count = days.Count;

        return true;
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

    private static ObservableCollection<SelectableOption> BuildOffsetOptions()
    {
        var options = new ObservableCollection<SelectableOption>();
        for (var i = 1; i <= 60; i++)
        {
            options.Add(new SelectableOption(i, i.ToString(CultureInfo.InvariantCulture)));
        }

        return options;
    }

    private ObservableCollection<SelectableOption> BuildDayOptions()
    {
        var options = new ObservableCollection<SelectableOption>();
        options.Add(new SelectableOption(1, _localization["CommissionPlan_FirstDay"]));
        for (var i = 2; i <= 31; i++)
        {
            options.Add(new SelectableOption(i, i.ToString(CultureInfo.InvariantCulture)));
        }
        options.Add(new SelectableOption(0, _localization["CommissionPlan_LastDay"]));
        return options;
    }

    private static List<int> SelectedOptions(IEnumerable<SelectableOption> options)
    {
        return options
            .Where(option => option.IsSelected)
            .Select(option => option.Value)
            .Distinct()
            .OrderBy(value => value == 0 ? 32 : value)
            .ToList();
    }

    private static void SetSelections(IEnumerable<SelectableOption> options, IEnumerable<int> selectedValues)
    {
        var set = new HashSet<int>(selectedValues);
        foreach (var option in options)
        {
            option.IsSelected = set.Contains(option.Value);
        }
    }

    private void WireSelectionOptions(IEnumerable<SelectableOption> options, Action update)
    {
        foreach (var option in options)
        {
            option.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SelectableOption.IsSelected))
                {
                    update();
                }
            };
        }
    }

    private void UpdateSelectionSummaries()
    {
        UpdateClientOffsetsSummary();
        UpdateClientDaysSummary();
        UpdateCommissionOffsetsSummary();
        UpdateCommissionDaysSummary();
    }

    private void UpdateClientOffsetsSummary()
    {
        ClientOffsetsSummary = BuildOffsetsSummary(SelectedOptions(ClientOffsetOptions));
    }

    private void UpdateClientDaysSummary()
    {
        ClientDaysSummary = BuildDaysSummary(SelectedOptions(ClientDayOptions));
    }

    private void UpdateCommissionOffsetsSummary()
    {
        CommissionOffsetsSummary = BuildOffsetsSummary(SelectedOptions(CommissionOffsetOptions));
    }

    private void UpdateCommissionDaysSummary()
    {
        CommissionDaysSummary = BuildDaysSummary(SelectedOptions(CommissionDayOptions));
    }

    private string BuildOffsetsSummary(IReadOnlyCollection<int> offsets)
    {
        if (offsets.Count == 0)
        {
            return _localization["CommissionPlan_NoOffsets"];
        }

        var offsetsLabel = string.Join(", ", offsets);
        return string.Format(_localization["CommissionPlan_OffsetsFormat"], offsetsLabel);
    }

    private string BuildDaysSummary(IReadOnlyCollection<int> days)
    {
        if (days.Count == 0)
        {
            return _localization["CommissionPlan_NoDays"];
        }

        var dayLabels = days.Select(day => day switch
        {
            0 => _localization["CommissionPlan_LastDay"],
            1 => _localization["CommissionPlan_FirstDay"],
            _ => day.ToString(CultureInfo.InvariantCulture)
        });
        var daysLabel = string.Join(", ", dayLabels);
        return string.Format(_localization["CommissionPlan_DaysFormat"], daysLabel);
    }

    private void OpenSelectionDialog(SelectionDialogType type, ObservableCollection<SelectableOption> options, string title)
    {
        _selectionDialogType = type;
        _selectionBackup = SelectedOptions(options);
        SelectionDialogOptions = options;
        SelectionDialogTitle = title;
        IsSelectionDialogOpen = true;
    }

    private void CloseSelectionDialog()
    {
        IsSelectionDialogOpen = false;
    }

    private void CancelSelectionDialog()
    {
        if (_selectionDialogType == SelectionDialogType.None)
        {
            IsSelectionDialogOpen = false;
            return;
        }

        SetSelections(SelectionDialogOptions, _selectionBackup);
        IsSelectionDialogOpen = false;
    }
}

public record ScheduleModeOption(ScheduleMode Mode, string LabelKey);

public record DayHandlingOption(CommissionDayHandling Handling, string LabelKey);

public class SelectableOption : ViewModelBase
{
    private bool _isSelected;

    public SelectableOption(int value, string label)
    {
        Value = value;
        Label = label;
    }

    public int Value { get; }

    public string Label { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

}

public enum SelectionDialogType
{
    None,
    ClientOffsets,
    ClientDays,
    CommissionOffsets,
    CommissionDays
}
