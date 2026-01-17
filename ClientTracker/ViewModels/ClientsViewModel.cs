using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.Maui.ApplicationModel;
using ClientTracker.Models;
using ClientTracker.Services;

namespace ClientTracker.ViewModels;

public class ClientsViewModel : ViewModelBase
{
    private readonly DatabaseService _database;
    private readonly LocalizationResourceManager _localization;
    private string _clientSearch = string.Empty;
    private Client? _selectedClient;
    private Sale? _selectedSale;
    private DateTime _saleDate = DateTime.Today;
    private string _saleAmount = string.Empty;
    private string _saleCommissionPercent = string.Empty;
    private string _statusMessage = string.Empty;
    private int _currentPage;
    private int _totalPages;
    private int _totalClients;
    private int _activeClients;
    private int _totalSalesCount;
    private decimal _totalSalesAmount;
    private decimal _upcomingCommissionAmount;
    private ClientFilterOption? _selectedCountryFilter;
    private ClientSortOption? _selectedSortOption;
    private bool _hasSalesOnly;
    private bool _hasUpcomingOnly;
    private bool _isFilterSheetOpen;
    private bool _suppressReload;
    private bool _pendingReload;
    private bool _pendingResetPage;
    private CancellationTokenSource? _searchCts;
    private const int PageSize = 25;

    public ClientsViewModel(DatabaseService database, LocalizationResourceManager localization)
    {
        _database = database;
        _localization = localization;
        Clients = new ObservableCollection<ClientListItem>();
        Sales = new ObservableCollection<Sale>();
        CountryFilters = new ObservableCollection<ClientFilterOption>();
        SortOptions = new ObservableCollection<ClientSortOption>();
        SortOptions.Add(new ClientSortOption("Name", _localization["Clients_Sort_Name"]));
        SortOptions.Add(new ClientSortOption("Sales", _localization["Clients_Sort_Sales"]));
        SortOptions.Add(new ClientSortOption("Upcoming", _localization["Clients_Sort_Upcoming"]));

        LoadCommand = new Command(async () => await LoadAsync());
        SearchCommand = new Command(async () =>
        {
            CurrentPage = 0;
            await LoadAsync();
        });
        ApplyFiltersCommand = new Command(async () =>
        {
            CurrentPage = 0;
            await LoadAsync();
        });
        AddClientCommand = new Command(async () => await OpenAddClientAsync());
        SaveClientCommand = new Command(async () => await SaveClientAsync(), () => SelectedClient is not null);
        DeleteClientCommand = new Command(async () => await DeleteClientAsync(), () => SelectedClient is not null);

        AddSaleCommand = new Command(async () => await AddSaleAsync(), () => SelectedClient is not null);
        SaveSaleCommand = new Command(async () => await SaveSaleAsync(), () => SelectedSale is not null);
        DeleteSaleCommand = new Command(async () => await DeleteSaleAsync(), () => SelectedSale is not null);
        ClearSaleCommand = new Command(() => ClearSaleInputs());
        ViewClientCommand = new Command<ClientListItem>(async client => await OpenClientAsync(client));
        EditClientCommand = new Command<ClientListItem>(async client => await OpenEditClientAsync(client));
        DeleteClientItemCommand = new Command<ClientListItem>(async client => await DeleteClientAsync(client));
        PreviousPageCommand = new Command(async () => await ChangePageAsync(-1), () => CurrentPage > 0);
        NextPageCommand = new Command(async () => await ChangePageAsync(1), () => CurrentPage < TotalPages - 1);
        LoadMoreCommand = new Command(async () => await LoadMoreAsync(), () => HasMoreResults);
        ToggleHasSalesOnlyCommand = new Command(() => HasSalesOnly = !HasSalesOnly);
        ToggleHasUpcomingOnlyCommand = new Command(() => HasUpcomingOnly = !HasUpcomingOnly);
        OpenFilterSheetCommand = new Command(() => IsFilterSheetOpen = true);
        CloseFilterSheetCommand = new Command(() => IsFilterSheetOpen = false);
        ClearFiltersCommand = new Command(ClearFilters);
    }

    public ObservableCollection<ClientListItem> Clients { get; }
    public ObservableCollection<Sale> Sales { get; }
    public ObservableCollection<ClientFilterOption> CountryFilters { get; }
    public ObservableCollection<ClientSortOption> SortOptions { get; }

    public string ClientSearch
    {
        get => _clientSearch;
        set
        {
            if (SetProperty(ref _clientSearch, value))
            {
                NotifyFilterStateChanged();
                ScheduleSearchReload();
            }
        }
    }

    public Client? SelectedClient
    {
        get => _selectedClient;
        set
        {
            if (SetProperty(ref _selectedClient, value))
            {
                ClearSaleInputs();
                _ = LoadSalesAsync();
                SaveClientCommand.ChangeCanExecute();
                DeleteClientCommand.ChangeCanExecute();
                AddSaleCommand.ChangeCanExecute();
            }
        }
    }

    public Sale? SelectedSale
    {
        get => _selectedSale;
        set
        {
            if (SetProperty(ref _selectedSale, value))
            {
                if (value is null)
                {
                    ClearSaleInputs(false);
                }
                else
                {
                    SaleDate = value.SaleDate;
                    SaleAmount = value.Amount.ToString("0.00", CultureInfo.CurrentCulture);
                    SaleCommissionPercent = value.CommissionPercent.ToString("0.##", CultureInfo.CurrentCulture);
                }

                SaveSaleCommand.ChangeCanExecute();
                DeleteSaleCommand.ChangeCanExecute();
            }
        }
    }

    public DateTime SaleDate
    {
        get => _saleDate;
        set => SetProperty(ref _saleDate, value);
    }

    public string SaleAmount
    {
        get => _saleAmount;
        set => SetProperty(ref _saleAmount, value);
    }

    public string SaleCommissionPercent
    {
        get => _saleCommissionPercent;
        set => SetProperty(ref _saleCommissionPercent, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public int TotalClients
    {
        get => _totalClients;
        set
        {
            if (SetProperty(ref _totalClients, value))
            {
                OnPropertyChanged(nameof(ClientCountLabel));
            }
        }
    }

    public int ActiveClients
    {
        get => _activeClients;
        set => SetProperty(ref _activeClients, value);
    }

    public int TotalSalesCount
    {
        get => _totalSalesCount;
        set => SetProperty(ref _totalSalesCount, value);
    }

    public decimal TotalSalesAmount
    {
        get => _totalSalesAmount;
        set => SetProperty(ref _totalSalesAmount, value);
    }

    public decimal UpcomingCommissionAmount
    {
        get => _upcomingCommissionAmount;
        set => SetProperty(ref _upcomingCommissionAmount, value);
    }

    public ClientFilterOption? SelectedCountryFilter
    {
        get => _selectedCountryFilter;
        set
        {
            if (SetProperty(ref _selectedCountryFilter, value))
            {
                NotifyFilterStateChanged();
                RequestReload(true);
            }
        }
    }

    public ClientSortOption? SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (SetProperty(ref _selectedSortOption, value))
            {
                RequestReload(true);
            }
        }
    }

    public bool HasSalesOnly
    {
        get => _hasSalesOnly;
        set
        {
            if (SetProperty(ref _hasSalesOnly, value))
            {
                NotifyFilterStateChanged();
                RequestReload(true);
            }
        }
    }

    public bool HasUpcomingOnly
    {
        get => _hasUpcomingOnly;
        set
        {
            if (SetProperty(ref _hasUpcomingOnly, value))
            {
                NotifyFilterStateChanged();
                RequestReload(true);
            }
        }
    }

    public bool IsFiltered =>
        !string.IsNullOrWhiteSpace(ClientSearch) ||
        HasSalesOnly ||
        HasUpcomingOnly ||
        (SelectedCountryFilter is { Value: not "ALL" });

    public string ClientCountLabel =>
        IsFiltered ? $"• {TotalClients} filtered" : $"• {TotalClients}";

    public bool IsFilterSheetOpen
    {
        get => _isFilterSheetOpen;
        set => SetProperty(ref _isFilterSheetOpen, value);
    }

    public int CurrentPage
    {
        get => _currentPage;
        set
        {
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(PageLabel));
                PreviousPageCommand.ChangeCanExecute();
                NextPageCommand.ChangeCanExecute();
            }
        }
    }

    public int TotalPages
    {
        get => _totalPages;
        set
        {
            if (SetProperty(ref _totalPages, value))
            {
                OnPropertyChanged(nameof(PageLabel));
                OnPropertyChanged(nameof(HasMoreResults));
                PreviousPageCommand.ChangeCanExecute();
                NextPageCommand.ChangeCanExecute();
            }
        }
    }

    public string PageLabel => TotalPages == 0 ? "Page 0 of 0" : $"Page {CurrentPage + 1} of {TotalPages}";
    public bool HasMoreResults => CurrentPage < TotalPages - 1;

    public Command LoadCommand { get; }
    public Command SearchCommand { get; }
    public Command ApplyFiltersCommand { get; }
    public Command AddClientCommand { get; }
    public Command SaveClientCommand { get; }
    public Command DeleteClientCommand { get; }
    public Command AddSaleCommand { get; }
    public Command SaveSaleCommand { get; }
    public Command DeleteSaleCommand { get; }
    public Command ClearSaleCommand { get; }
    public Command<ClientListItem> ViewClientCommand { get; }
    public Command<ClientListItem> EditClientCommand { get; }
    public Command<ClientListItem> DeleteClientItemCommand { get; }
    public Command PreviousPageCommand { get; }
    public Command NextPageCommand { get; }
    public Command LoadMoreCommand { get; }
    public Command ToggleHasSalesOnlyCommand { get; }
    public Command ToggleHasUpcomingOnlyCommand { get; }
    public Command OpenFilterSheetCommand { get; }
    public Command CloseFilterSheetCommand { get; }
    public Command ClearFiltersCommand { get; }

    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            await LoadPageAsync(0, false);
        });

        if (_pendingReload)
        {
            var resetPage = _pendingResetPage;
            _pendingReload = false;
            _pendingResetPage = false;
            await LoadAsync();
        }
    }

    private async Task LoadMoreAsync()
    {
        if (IsBusy || !HasMoreResults)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await LoadPageAsync(CurrentPage + 1, true);
        });
    }

    private async Task LoadPageAsync(int pageIndex, bool append)
    {
        StatusMessage = string.Empty;
        var clients = await _database.GetClientsAsync(ClientSearch);
        await EnsureCountryFiltersAsync(clients);
        var baseIds = clients.Select(c => c.Id).ToList();
        var sales = await _database.GetSalesForClientIdsAsync(baseIds);
        var upcomingEnd = DateTime.Today.AddDays(30);
        var upcomingPayments = await _database.GetPaymentsForClientIdsBetweenAsync(baseIds, DateTime.Today, upcomingEnd);
        var filteredClients = ApplyActivityFilters(clients, sales, upcomingPayments);
        var sortedClients = ApplySort(filteredClients, sales, upcomingPayments);

        TotalClients = sortedClients.Count;
        TotalPages = TotalClients == 0 ? 0 : (int)Math.Ceiling(TotalClients / (double)PageSize);
        if (pageIndex >= TotalPages && TotalPages > 0)
        {
            pageIndex = TotalPages - 1;
        }

        var pageClients = sortedClients
            .Skip(pageIndex * PageSize)
            .Take(PageSize)
            .ToList();

        TotalSalesCount = sales.Count;
        TotalSalesAmount = sales.Sum(s => s.Amount);
        UpcomingCommissionAmount = upcomingPayments.Sum(p => p.Commission);
        ActiveClients = sortedClients.Count(c => sales.Any(s => s.ClientId == c.Id));

        if (!append)
        {
            Clients.Clear();
        }

        foreach (var client in pageClients)
        {
            if (append && Clients.Any(c => c.Id == client.Id))
            {
                continue;
            }

            var clientSales = sales.Where(s => s.ClientId == client.Id).ToList();
            var totalSalesAmount = clientSales.Sum(s => s.Amount);
            var upcomingClientPayments = upcomingPayments.Where(p => clientSales.Any(s => s.Id == p.SaleId)).ToList();

            Clients.Add(new ClientListItem
            {
                Id = client.Id,
                Name = client.Name,
                LocationLine = BuildLocationLine(client),
                Initials = BuildInitials(client.Name),
                TotalSalesCount = clientSales.Count,
                TotalSalesAmount = totalSalesAmount,
                UpcomingPaymentCount = upcomingClientPayments.Count,
                UpcomingCommissionAmount = upcomingClientPayments.Sum(p => p.Commission)
            });
        }

        CurrentPage = pageIndex;
        if (SelectedClient is not null)
        {
            SelectedClient = pageClients.FirstOrDefault(c => c.Id == SelectedClient.Id);
        }
    }

    private async Task LoadSalesAsync()
    {
        Sales.Clear();
        if (SelectedClient is null)
        {
            return;
        }

        var sales = await _database.GetSalesForClientAsync(SelectedClient.Id);
        foreach (var sale in sales)
        {
            Sales.Add(sale);
        }
    }

    private static Task OpenAddClientAsync()
    {
        return Shell.Current.GoToAsync("client-edit");
    }

    private async Task SaveClientAsync()
    {
        StatusMessage = string.Empty;
        if (SelectedClient is null)
        {
            return;
        }

        await _database.UpdateClientAsync(SelectedClient);
        await LoadAsync();
    }

    private async Task DeleteClientAsync()
    {
        StatusMessage = string.Empty;
        if (SelectedClient is null)
        {
            return;
        }

        var confirm = await Shell.Current.DisplayAlertAsync("Confirm Delete", "Delete this client and all related sales?", "Delete", "Cancel");
        if (!confirm)
        {
            return;
        }

        await _database.DeleteClientAsync(SelectedClient);
        SelectedClient = null;
        await LoadAsync();
    }

    private async Task AddSaleAsync()
    {
        StatusMessage = string.Empty;
        if (SelectedClient is null)
        {
            StatusMessage = "Select a client before adding a sale.";
            return;
        }

        if (!TryGetDecimal(SaleAmount, out var amount) || amount <= 0)
        {
            StatusMessage = "Enter a valid sale amount.";
            return;
        }

        if (!TryGetDecimal(SaleCommissionPercent, out var commissionPercent) || commissionPercent < 0)
        {
            StatusMessage = "Enter a valid commission percent.";
            return;
        }

        var sale = new Sale
        {
            ClientId = SelectedClient.Id,
            SaleDate = SaleDate.Date,
            Amount = amount,
            CommissionPercent = commissionPercent
        };

        await _database.AddSaleAsync(sale);
        await LoadSalesAsync();
        ClearSaleInputs();
    }

    private async Task SaveSaleAsync()
    {
        StatusMessage = string.Empty;
        if (SelectedSale is null)
        {
            return;
        }

        if (!TryGetDecimal(SaleAmount, out var amount) || amount <= 0)
        {
            StatusMessage = "Enter a valid sale amount.";
            return;
        }

        if (!TryGetDecimal(SaleCommissionPercent, out var commissionPercent) || commissionPercent < 0)
        {
            StatusMessage = "Enter a valid commission percent.";
            return;
        }

        SelectedSale.SaleDate = SaleDate.Date;
        SelectedSale.Amount = amount;
        SelectedSale.CommissionPercent = commissionPercent;

        await _database.UpdateSaleAsync(SelectedSale);
        await LoadSalesAsync();
    }

    private async Task DeleteSaleAsync()
    {
        StatusMessage = string.Empty;
        if (SelectedSale is null)
        {
            return;
        }

        await _database.DeleteSaleAsync(SelectedSale);
        SelectedSale = null;
        await LoadSalesAsync();
    }

    private void ClearSaleInputs(bool clearSelection = true)
    {
        SaleDate = DateTime.Today;
        SaleAmount = string.Empty;
        SaleCommissionPercent = string.Empty;
        if (clearSelection)
        {
            SelectedSale = null;
        }
    }

    private static bool TryGetDecimal(string input, out decimal value)
    {
        return decimal.TryParse(input, NumberStyles.Number, CultureInfo.CurrentCulture, out value);
    }

    private static Task OpenClientAsync(ClientListItem? client)
    {
        if (client is null)
        {
            return Task.CompletedTask;
        }

        return Shell.Current.GoToAsync("client-view", new Dictionary<string, object>
        {
            ["clientId"] = client.Id
        });
    }

    private static Task OpenEditClientAsync(ClientListItem? client)
    {
        if (client is null)
        {
            return Task.CompletedTask;
        }

        return Shell.Current.GoToAsync("client-edit", new Dictionary<string, object>
        {
            ["clientId"] = client.Id
        });
    }

    private async Task DeleteClientAsync(ClientListItem? client)
    {
        if (client is null)
        {
            return;
        }

        var confirm = await Shell.Current.DisplayAlertAsync("Confirm Delete", "Delete this client and all related sales?", "Delete", "Cancel");
        if (!confirm)
        {
            return;
        }

        await _database.DeleteClientAsync(new Client { Id = client.Id });
        await LoadAsync();
    }

    private async Task ChangePageAsync(int delta)
    {
        if (TotalPages == 0)
        {
            return;
        }

        CurrentPage = Math.Clamp(CurrentPage + delta, 0, Math.Max(0, TotalPages - 1));
        await LoadAsync();
    }

    private static string BuildLocationLine(Client client)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(client.City))
        {
            parts.Add(client.City.Trim());
        }

        if (!string.IsNullOrWhiteSpace(client.StateProvince))
        {
            parts.Add(client.StateProvince.Trim());
        }

        if (parts.Count == 0 && !string.IsNullOrWhiteSpace(client.Country))
        {
            parts.Add(client.Country.Trim());
        }

        return parts.Count == 0 ? "Location not set" : string.Join(", ", parts);
    }

    private static string BuildInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        var parts = name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        if (parts.Length == 0)
        {
            return "?";
        }

        return char.ToUpperInvariant(parts[0][0]).ToString();
    }

    private Task EnsureCountryFiltersAsync(IReadOnlyCollection<Client> clients)
    {
        _suppressReload = true;
        try
        {
            var selectedValue = SelectedCountryFilter?.Value ?? "ALL";
            CountryFilters.Clear();
            CountryFilters.Add(new ClientFilterOption("ALL", _localization["Clients_Filter_AllCountries"]));

            var countries = clients
                .Select(c => c.Country?.Trim())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase);
            foreach (var country in countries)
            {
                CountryFilters.Add(new ClientFilterOption(country!, country!));
            }

            SelectedCountryFilter = CountryFilters.FirstOrDefault(f => f.Value == selectedValue) ?? CountryFilters.FirstOrDefault();
            SelectedSortOption ??= SortOptions.FirstOrDefault();
        }
        finally
        {
            _suppressReload = false;
        }

        return Task.CompletedTask;
    }

    private void ClearFilters()
    {
        HasSalesOnly = false;
        HasUpcomingOnly = false;
        SelectedCountryFilter = CountryFilters.FirstOrDefault();
        SelectedSortOption = SortOptions.FirstOrDefault();
    }

    private void ScheduleSearchReload()
    {
        if (_suppressReload)
        {
            return;
        }

        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, cts.Token);
                if (!cts.Token.IsCancellationRequested)
                {
                    MainThread.BeginInvokeOnMainThread(() => RequestReload(true));
                }
            }
            catch (TaskCanceledException)
            {
            }
        });
    }

    private void NotifyFilterStateChanged()
    {
        OnPropertyChanged(nameof(IsFiltered));
        OnPropertyChanged(nameof(ClientCountLabel));
    }

    private void RequestReload(bool resetPage)
    {
        if (_suppressReload)
        {
            return;
        }

        if (resetPage)
        {
            CurrentPage = 0;
        }

        if (IsBusy)
        {
            _pendingReload = true;
            _pendingResetPage = _pendingResetPage || resetPage;
            return;
        }

        if (resetPage)
        {
            CurrentPage = 0;
        }

        _ = LoadAsync();
    }

    private List<Client> ApplyActivityFilters(IEnumerable<Client> clients, IReadOnlyCollection<Sale> sales, IReadOnlyCollection<Payment> upcomingPayments)
    {
        var filtered = clients;
        if (SelectedCountryFilter is { Value: not "ALL" } country)
        {
            filtered = filtered.Where(c => string.Equals(c.Country?.Trim(), country.Value, StringComparison.OrdinalIgnoreCase));
        }

        var list = filtered.ToList();
        if (!HasSalesOnly && !HasUpcomingOnly)
        {
            return list;
        }

        var salesByClient = sales.GroupBy(s => s.ClientId).ToDictionary(g => g.Key, g => g.Count());
        var saleById = sales.ToDictionary(s => s.Id, s => s.ClientId);
        var upcomingByClient = upcomingPayments
            .GroupBy(p => saleById.TryGetValue(p.SaleId, out var clientId) ? clientId : 0)
            .Where(g => g.Key > 0)
            .ToDictionary(g => g.Key, g => g.Count());

        list = list.Where(c =>
        {
            var hasSales = salesByClient.TryGetValue(c.Id, out var count) && count > 0;
            var hasUpcoming = upcomingByClient.TryGetValue(c.Id, out var upcomingCount) && upcomingCount > 0;
            return (!HasSalesOnly || hasSales) && (!HasUpcomingOnly || hasUpcoming);
        }).ToList();

        return list;
    }

    private List<Client> ApplySort(IEnumerable<Client> clients, IReadOnlyCollection<Sale> sales, IReadOnlyCollection<Payment> upcomingPayments)
    {
        var salesTotals = sales.GroupBy(s => s.ClientId).ToDictionary(g => g.Key, g => g.Sum(s => s.Amount));
        var saleById = sales.ToDictionary(s => s.Id, s => s.ClientId);
        var upcomingTotals = upcomingPayments
            .GroupBy(p => saleById.TryGetValue(p.SaleId, out var clientId) ? clientId : 0)
            .Where(g => g.Key > 0)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Commission));

        return SelectedSortOption?.Key switch
        {
            "Sales" => clients.OrderByDescending(c => salesTotals.GetValueOrDefault(c.Id, 0m)).ThenBy(c => c.Name).ToList(),
            "Upcoming" => clients.OrderByDescending(c => upcomingTotals.GetValueOrDefault(c.Id, 0m)).ThenBy(c => c.Name).ToList(),
            _ => clients.OrderBy(c => c.Name).ToList()
        };
    }
}

public record ClientFilterOption(string Value, string DisplayName);
public record ClientSortOption(string Key, string DisplayName);
