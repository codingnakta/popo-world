using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PopoWorld.Core.Models;
using PopoWorld.Core.Validation;

namespace PopoWorld.App.Store;

/// <summary>상점 창. 카테고리 탭, 목록, 보유·장착 표시, 미리보기, 구매, 장착.</summary>
public partial class StoreWindow : Window
{
    /// <summary>창이 쓰는 바깥 세계와의 계약.</summary>
    public interface IStoreHost
    {
        int Balance { get; }
        IReadOnlySet<string> Inventory { get; }
        IReadOnlyList<CatalogItem> Catalog { get; }
        User Me { get; }
        void Preview(CatalogItem? item);
        Task<RpcResult> PurchaseAsync(CatalogItem item);
        Task<RpcResult> EquipAsync(string slot, string? itemId);
        Task RefreshAsync();
    }

    private readonly IStoreHost _host;
    private bool _busy;

    public StoreWindow(IStoreHost host)
    {
        InitializeComponent();
        _host = host;
        Closed += (_, _) => _host.Preview(null);
        Build();
    }

    private void Build()
    {
        var selectedTab = Tabs.SelectedIndex;
        Tabs.Items.Clear();
        BalanceText.Text = $"{_host.Balance} 포포";

        foreach (var category in ItemCategory.All)
        {
            var list = new ListBox { BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            list.SelectionChanged += (_, _) =>
            {
                if (list.SelectedItem is ListBoxItem { Tag: CatalogItem item }) _host.Preview(item);
            };
            foreach (var item in _host.Catalog.Where(c => c.Category == category).OrderBy(c => c.Sort))
                list.Items.Add(new ListBoxItem { Content = BuildRow(item), Tag = item, Padding = new Thickness(4) });

            if (category == ItemCategory.Chair)
            {
                var none = new CatalogItem { Id = string.Empty, Category = ItemCategory.Chair, Name = "의자 없음", Price = 0 };
                list.Items.Insert(0, new ListBoxItem { Content = BuildRow(none), Tag = none, Padding = new Thickness(4) });
            }

            Tabs.Items.Add(new TabItem { Header = ItemCategory.Label(category), Content = list });
        }

        Tabs.SelectedIndex = Math.Max(0, selectedTab);
    }

    private UIElement BuildRow(CatalogItem item)
    {
        var equipped = string.IsNullOrEmpty(item.Id)
            ? string.IsNullOrEmpty(_host.Me.GetSlot(item.Category))
            : _host.Me.GetSlot(item.Category) == item.Id;
        var owned = string.IsNullOrEmpty(item.Id) || PurchaseValidator.IsOwned(item, _host.Inventory);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var name = new TextBlock { Text = item.Name, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(name, 0);
        grid.Children.Add(name);

        var price = new TextBlock
        {
            Text = owned ? (equipped ? "장착중" : "보유") : $"{item.Price} 포포",
            Foreground = owned ? Brushes.SeaGreen : Brushes.DimGray,
            Margin = new Thickness(8, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(price, 1);
        grid.Children.Add(price);

        Button button;
        if (!owned)
        {
            var error = PurchaseValidator.Validate(item, _host.Inventory, _host.Balance);
            var shortfall = error is not null && error.StartsWith("insufficient:") ? error["insufficient:".Length..] : null;
            button = new Button
            {
                Content = shortfall is null ? "구매" : $"{shortfall}포포 부족",
                IsEnabled = error is null,
                Padding = new Thickness(10, 2, 10, 2),
                MinWidth = 70,
            };
            button.Click += async (_, _) => await PurchaseAsync(item);
        }
        else if (!equipped)
        {
            button = new Button { Content = "장착", Padding = new Thickness(10, 2, 10, 2), MinWidth = 70 };
            button.Click += async (_, _) => await EquipAsync(item);
        }
        else
        {
            button = new Button { Content = "장착중", IsEnabled = false, Padding = new Thickness(10, 2, 10, 2), MinWidth = 70 };
        }
        Grid.SetColumn(button, 2);
        grid.Children.Add(button);
        return grid;
    }

    private async Task PurchaseAsync(CatalogItem item)
    {
        if (_busy) return;
        var confirm = MessageBox.Show(this, $"{item.Name}을(를) {item.Price}포포에 살까요?\n남는 포포: {_host.Balance - item.Price}", "구매 확인",
            MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        _busy = true;
        StatusText.Text = "구매 중…";
        try
        {
            var r = await _host.PurchaseAsync(item);
            if (r.Ok)
            {
                StatusText.Text = $"{item.Name} 구매 완료! 잔액 {r.Balance}포포";
                var equip = MessageBox.Show(this, "바로 장착할까요?", "장착", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (equip == MessageBoxResult.Yes) await _host.EquipAsync(item.Category, item.Id);
            }
            else
            {
                StatusText.Text = r.Describe();
            }
            await _host.RefreshAsync();
            Build();
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task EquipAsync(CatalogItem item)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var r = await _host.EquipAsync(item.Category, string.IsNullOrEmpty(item.Id) ? null : item.Id);
            StatusText.Text = r.Ok ? $"{item.Name} 장착!" : r.Describe();
            await _host.RefreshAsync();
            Build();
        }
        finally
        {
            _busy = false;
        }
    }

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source == Tabs) _host.Preview(null);
    }

    public void Refresh() => Build();
}
