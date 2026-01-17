using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Microsoft.Maui.ApplicationModel;
using ClientTracker.Models;
using ClientTracker.Services;

namespace ClientTracker.ViewModels;

public class SalesViewModel : ViewModelBase
{
    private readonly DatabaseService _database;
    private string _searchText = string.Empty;
    private string _statusMessage = string.Empty;
    private SalesSortOption? _selectedSortOption;
    private bool _hasInvoiceOnly;
    private bool _isFilterSheetOpen;
    private CancellationTokenSource? _searchCts;
    private bool _pendingReload;

    public SalesViewModel(DatabaseService database)
    {
        _database = database;
        Sales = new ObservableCollection<SaleOverview>();
        SortOptions = new ObservableCollection<SalesSortOption>
        {
            new("Date", "Date"),
            new("Amount", "Amount"),
            new("Client", "Client")
        };
        _selectedSortOption = SortOptions.FirstOrDefault();
        LoadCommand = new Command(async () => await LoadAsync());
        ViewSaleCommand = new Command<SaleOverview>(async sale => await OpenSaleAsync(sale));
        EditSaleCommand = new Command<SaleOverview>(async sale => await OpenSaleAsync(sale));
        DeleteSaleCommand = new Command<SaleOverview>(async sale => await DeleteSaleAsync(sale));
        AddSaleCommand = new Command(async () => await OpenAddSaleAsync());
        OpenFilterSheetCommand = new Command(() => IsFilterSheetOpen = true);
        CloseFilterSheetCommand = new Command(() => IsFilterSheetOpen = false);
        ClearFiltersCommand = new Command(ClearFilters);
    }

    public ObservableCollection<SaleOverview> Sales { get; }
    public ObservableCollection<SalesSortOption> SortOptions { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ScheduleSearchReload();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public SalesSortOption? SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (SetProperty(ref _selectedSortOption, value))
            {
                RequestReload();
            }
        }
    }

    public bool HasInvoiceOnly
    {
        get => _hasInvoiceOnly;
        set
        {
            if (SetProperty(ref _hasInvoiceOnly, value))
            {
                RequestReload();
            }
        }
    }

    public bool IsFilterSheetOpen
    {
        get => _isFilterSheetOpen;
        set => SetProperty(ref _isFilterSheetOpen, value);
    }

    public Command LoadCommand { get; }
    public Command<SaleOverview> ViewSaleCommand { get; }
    public Command<SaleOverview> EditSaleCommand { get; }
    public Command<SaleOverview> DeleteSaleCommand { get; }
    public Command AddSaleCommand { get; }
    public Command OpenFilterSheetCommand { get; }
    public Command CloseFilterSheetCommand { get; }
    public Command ClearFiltersCommand { get; }

    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            StatusMessage = string.Empty;
            var sales = await _database.GetSalesOverviewAsync();
            var filtered = ApplySearch(sales, SearchText);
            filtered = ApplyFilters(filtered);
            var sorted = ApplySort(filtered);

            Sales.Clear();
            foreach (var sale in sorted)
            {
                Sales.Add(sale);
            }
        });

        if (_pendingReload)
        {
            _pendingReload = false;
            await LoadAsync();
        }
    }

    private static IEnumerable<SaleOverview> ApplySearch(IEnumerable<SaleOverview> sales, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return sales;
        }

        var term = search.Trim();
        return sales.Where(s =>
            s.ClientName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            s.ContactName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            s.InvoiceNumber.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private IEnumerable<SaleOverview> ApplyFilters(IEnumerable<SaleOverview> sales)
    {
        if (HasInvoiceOnly)
        {
            sales = sales.Where(s => !string.IsNullOrWhiteSpace(s.InvoiceNumber));
        }

        return sales;
    }

    private IEnumerable<SaleOverview> ApplySort(IEnumerable<SaleOverview> sales)
    {
        return SelectedSortOption?.Key switch
        {
            "Amount" => sales.OrderByDescending(s => s.Amount),
            "Client" => sales.OrderBy(s => s.ClientName),
            _ => sales.OrderByDescending(s => s.SaleDate)
        };
    }

    private void ClearFilters()
    {
        HasInvoiceOnly = false;
        SelectedSortOption = SortOptions.FirstOrDefault();
    }

    private void ScheduleSearchReload()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        var cts = new CancellationTokenSource();
        _searchCts = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, cts.Token);
                if (!cts.Token.IsCancellationRequested)
                {
                    MainThread.BeginInvokeOnMainThread(RequestReload);
                }
            }
            catch (TaskCanceledException)
            {
            }
        });
    }

    private void RequestReload()
    {
        if (IsBusy)
        {
            _pendingReload = true;
            return;
        }

        _ = LoadAsync();
    }

    private async Task OpenSaleAsync(SaleOverview? sale)
    {
        if (sale is null)
        {
            return;
        }

        await Shell.Current.GoToAsync("sale-details", new Dictionary<string, object>
        {
            ["saleId"] = sale.SaleId
        });
    }

    private async Task DeleteSaleAsync(SaleOverview? sale)
    {
        if (sale is null)
        {
            return;
        }

        var confirm = await Shell.Current.DisplayAlertAsync("Confirm Delete", "Delete this sale and its payments?", "Delete", "Cancel");
        if (!confirm)
        {
            return;
        }

        await _database.DeleteSaleAsync(new Sale { Id = sale.SaleId });
        await LoadAsync();
    }

    private async Task OpenAddSaleAsync()
    {
        await Shell.Current.GoToAsync("sale-add");
    }

    public record SalesSortOption(string Key, string DisplayName);
}
