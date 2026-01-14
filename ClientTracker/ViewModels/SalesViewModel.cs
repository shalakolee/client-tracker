using System.Collections.ObjectModel;
using System.Linq;
using ClientTracker.Models;
using ClientTracker.Services;

namespace ClientTracker.ViewModels;

public class SalesViewModel : ViewModelBase
{
    private readonly DatabaseService _database;
    private string _searchText = string.Empty;
    private string _statusMessage = string.Empty;

    public SalesViewModel(DatabaseService database)
    {
        _database = database;
        Sales = new ObservableCollection<SaleOverview>();
        LoadCommand = new Command(async () => await LoadAsync());
        ViewSaleCommand = new Command<SaleOverview>(async sale => await OpenSaleAsync(sale));
        EditSaleCommand = new Command<SaleOverview>(async sale => await OpenSaleAsync(sale));
        DeleteSaleCommand = new Command<SaleOverview>(async sale => await DeleteSaleAsync(sale));
        AddSaleCommand = new Command(async () => await OpenAddSaleAsync());
    }

    public ObservableCollection<SaleOverview> Sales { get; }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public Command LoadCommand { get; }
    public Command<SaleOverview> ViewSaleCommand { get; }
    public Command<SaleOverview> EditSaleCommand { get; }
    public Command<SaleOverview> DeleteSaleCommand { get; }
    public Command AddSaleCommand { get; }

    public async Task LoadAsync()
    {
        StatusMessage = string.Empty;
        var sales = await _database.GetSalesOverviewAsync();
        var filtered = ApplySearch(sales, SearchText);

        Sales.Clear();
        foreach (var sale in filtered)
        {
            Sales.Add(sale);
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
}
