using Engine.Picking;
using Engine.Picking.Genetic;
using Engine.WarehouseGrid;
using Cell = Engine.WarehouseGrid.Cell; // Microsoft.Maui.Controls też ma klasę Cell

namespace Visualizer;

public partial class MainPage : ContentPage
{
    private const string RandomLayoutLabel = "Losowy";
    private const int MinWarehouseSize = WarehouseGenerator.MinSize;
    private const int MaxWarehouseSize = 400;
    private const int DefaultWarehouseWidth = 160;
    private const int DefaultWarehouseHeight = 100;

    private readonly WarehouseDrawable _drawable = new();

    private WarehouseGrid? _warehouse;
    private IReadOnlyList<OrderLine>? _order;
    private (int X, int Y) _target;
    private (int X, int Y) _planTarget;
    private PickingPlan? _plan;
    private CancellationTokenSource? _cancellation;
    private bool _isRunning;

    public MainPage()
    {
        InitializeComponent();

        WarehouseView.Drawable = _drawable;

        LayoutPicker.ItemsSource = new[] { RandomLayoutLabel }
            .Concat(Enum.GetNames<WarehouseLayoutType>())
            .ToList();
        LayoutPicker.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_warehouse is null)
            await GenerateWarehouseAsync();
    }

    // ---------- Magazyn ----------

    private async void OnGenerateClicked(object? sender, EventArgs e) => await GenerateWarehouseAsync();

    private async Task GenerateWarehouseAsync()
    {
        GenerateButton.IsEnabled = false;
        try
        {
            var mock = new WarehouseMock(new WarehouseMockOptions
            {
                Width = ReadWarehouseSize(WidthEntry, DefaultWarehouseWidth),
                Height = ReadWarehouseSize(HeightEntry, DefaultWarehouseHeight),
                Seed = ParseSeed(),
                LayoutType = ParseLayoutType()
            });

            var warehouse = await Task.Run(mock.Build);

            _warehouse = warehouse;
            _drawable.Warehouse = warehouse;
            _order = null;

            ShowSummary(mock, warehouse);
            OrderLabel.Text = "Brak zamówienia";
            ClearShelfSelection();
            SetTarget(0, 0); // obwód magazynu jest zawsze ścieżką
        }
        finally
        {
            GenerateButton.IsEnabled = !_isRunning;
        }
    }

    private int? ParseSeed() => int.TryParse(SeedEntry.Text, out var seed) ? seed : null;

    private WarehouseLayoutType? ParseLayoutType() =>
        LayoutPicker.SelectedIndex <= 0 ? null : (WarehouseLayoutType)(LayoutPicker.SelectedIndex - 1);

    private void ShowSummary(WarehouseMock mock, WarehouseGrid warehouse)
    {
        SummaryLabel.Text =
            $"Układ: {mock.LayoutType}\n" +
            $"Seed: {mock.Seed}\n" +
            $"Rozmiar: {warehouse.Width}x{warehouse.Height}\n" +
            $"Produkty: {mock.ProductNames.Count}";
    }

    // ---------- Klikanie w plan ----------

    private void OnWarehouseTouched(object? sender, TouchEventArgs e)
    {
        if (_isRunning || !_drawable.TryGetCell(e.Touches[0], out var cell)) return;

        if (cell.IsWalkable)
            SetTarget(cell.X, cell.Y);
        else
            SelectShelf(cell);
    }

    private void SetTarget(int x, int y)
    {
        _target = (x, y);
        _drawable.Target = _target;
        TargetLabel.Text = $"Target: ({x}, {y})";

        ClearPlan(); // plan dotyczył poprzedniego targetu
    }

    private void SelectShelf(Cell shelf)
    {
        _drawable.SelectedCell = shelf;
        _drawable.Highlight(null);
        HighlightLabel.Text = string.Empty;

        SelectionLabel.Text = $"Półka ({shelf.X}, {shelf.Y}) - {shelf.Shelf!.Items.Count}/{shelf.Shelf.Capacity}";
        ItemsView.ItemsSource = shelf.Shelf.Items;

        WarehouseView.Invalidate();
    }

    private void OnItemSelected(object? sender, SelectionChangedEventArgs e)
    {
        var item = e.CurrentSelection.FirstOrDefault() as Item;

        _drawable.Highlight(item?.Name);
        HighlightLabel.Text = item is null
            ? string.Empty
            : $"Produkt {item.Name} leży na {_drawable.HighlightedShelfCount} półkach";

        WarehouseView.Invalidate();
    }

    private void ClearShelfSelection()
    {
        _drawable.SelectedCell = null;
        _drawable.Highlight(null);

        SelectionLabel.Text = "Kliknij półkę na planie";
        HighlightLabel.Text = string.Empty;
        ItemsView.ItemsSource = null;
    }

    // ---------- Zamówienie ----------

    private void OnRandomOrderClicked(object? sender, EventArgs e)
    {
        if (_warehouse is null) return;

        _order = OrderGenerator.Generate(_warehouse, ReadInt(LinesEntry, 100), ReadInt(MaxQuantityEntry, 30));
        OrderLabel.Text = $"Zamówienie: {_order.Count} pozycji, {_order.Sum(line => line.Quantity)} szt.";

        ClearPlan();
    }

    // ---------- Optymalizacja ----------

    private async void OnRunClicked(object? sender, EventArgs e) => await RunOptimizationAsync();

    private GeneticOptions Preset => AggressiveSwitch.IsToggled ? GeneticOptions.Aggressive : GeneticOptions.Fast;

    private void OnAggressiveToggled(object? sender, ToggledEventArgs e) =>
        GenerationsEntry.Text = Preset.Generations.ToString(); // preset podpowiada liczbę pokoleń, którą można zmienić

    private void OnStopClicked(object? sender, EventArgs e) => _cancellation?.Cancel();

    private async Task RunOptimizationAsync()
    {
        if (_warehouse is null) return;
        if (_order is null)
        {
            StatusLabel.Text = "Najpierw wylosuj zamówienie.";
            return;
        }

        var warehouse = _warehouse;
        var request = new PickingRequest(_order, ReadInt(CapacityEntry, 60), _target);
        int generations = ReadInt(GenerationsEntry, Preset.Generations);
        int delayMs = ReadInt(DelayEntry, 0, min: 0);

        ClearPlan();
        _planTarget = _target;
        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;
        SetRunning(true);
        StatusLabel.Text = "Liczenie odległości...";

        try
        {
            var progress = new SolverProgress(p => Dispatcher.Dispatch(() => OnProgress(p, generations)), delayMs);
            var options = Preset with { Generations = generations };

            var plan = await Task.Run(() => PickingPlanner.Plan(warehouse, request, options, progress, token));
            ShowFinalPlan(plan, "Gotowe");
        }
        catch (OperationCanceledException)
        {
            if (_plan is not null) ShowFinalPlan(_plan, "Przerwano");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            StatusLabel.Text = ex.Message; // np. za mały stan magazynowy
        }
        finally
        {
            SetRunning(false);
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    /// <summary>Wywoływane na wątku UI po każdym pokoleniu - odświeża plan tylko, gdy wynik się poprawił.</summary>
    private void OnProgress(GeneticProgress progress, int totalGenerations)
    {
        if (!_isRunning) return;

        if (progress.BestPlan is not null)
        {
            _plan = progress.BestPlan;
            _drawable.Plan = progress.BestPlan;
            WarehouseView.Invalidate();
        }

        RunProgressBar.Progress = Math.Min(1.0, (double)progress.Generation / totalGenerations);
        StatusLabel.Text =
            $"Pokolenie {progress.Generation}/{totalGenerations}\n" +
            $"Najlepszy dystans: {progress.BestDistance:N0}\n" +
            $"Baseline (najbliższy sąsiad): {progress.BaselineDistance:N0}\n" +
            $"Poprawa: {Gain(progress.BaselineDistance, progress.BestDistance):F1}%";
    }

    private void ShowFinalPlan(PickingPlan plan, string title)
    {
        _plan = plan;
        _drawable.Plan = plan;
        TripsView.ItemsSource = plan.Trips.Select((trip, index) => new TripRow(index, trip)).ToList();

        StatusLabel.Text =
            $"{title}: {plan.Trips.Count} kursów, dystans {plan.TotalDistance:N0}\n" +
            $"Baseline: {plan.BaselineDistance:N0} (poprawa {Gain(plan.BaselineDistance, plan.TotalDistance):F1}%)\n" +
            $"Pokoleń: {plan.Generations}";

        WarehouseView.Invalidate();
    }

    private void OnTripSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is TripRow row && _warehouse is not null)
        {
            var route = TripRouteBuilder.Build(_warehouse, _planTarget, row.Trip);
            _drawable.ShowTrip(row.Index, route);
        }
        else
        {
            _drawable.ShowTrip(null, null);
        }

        WarehouseView.Invalidate();
    }

    private void ClearPlan()
    {
        _plan = null;
        _drawable.Plan = null;
        TripsView.ItemsSource = null;
        StatusLabel.Text = string.Empty;
        RunProgressBar.Progress = 0;

        WarehouseView.Invalidate();
    }

    private void SetRunning(bool running)
    {
        _isRunning = running;

        GenerateButton.IsEnabled = !running;
        RandomOrderButton.IsEnabled = !running;
        RunButton.IsVisible = !running;
        StopButton.IsVisible = running;
    }

    // ---------- Pomocnicze ----------

    /// <summary>Wczytuje rozmiar, przycina do dozwolonego zakresu i pokazuje poprawioną wartość w polu.</summary>
    private static int ReadWarehouseSize(Entry entry, int fallback)
    {
        int value = int.TryParse(entry.Text, out int parsed) ? parsed : fallback;
        value = Math.Clamp(value, MinWarehouseSize, MaxWarehouseSize);
        entry.Text = value.ToString();
        return value;
    }

    private static int ReadInt(Entry entry, int fallback, int min = 1) =>
        int.TryParse(entry.Text, out int value) && value >= min ? value : fallback;

    private static double Gain(int baseline, int best) =>
        baseline == 0 ? 0 : (baseline - best) * 100.0 / baseline;
}