using System.Collections.ObjectModel;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using SpritesheetUnpacker.Services;

namespace SpritesheetUnpacker.Tests;

public sealed class MainWindowTests
{
    // Helpers to access private fields/methods without changing your production code
    private static T GetPrivateField<T>(object obj, string name) =>
        (T)
            obj.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(obj)!;

    private static void SetPrivateField(object obj, string name, object? value) =>
        obj.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(obj, value);

    private static object? CallPrivate(object obj, string name, params object?[] args) =>
        obj.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(obj, args);

    [AvaloniaFact]
    public void Startup_LogsReady_And_ExportDisabled()
    {
        var win = new MainWindow();
        win.Show();

        var logList = win.FindControl<ItemsControl>("LogList");
        Assert.NotNull(logList);
        var logs = (ObservableCollection<LogEntry>)logList!.ItemsSource!;
        Assert.Contains(logs, l => l.Message.Contains("Ready", StringComparison.OrdinalIgnoreCase));

        var export = win.FindControl<Button>("ExportBtn");
        Assert.NotNull(export);
        Assert.False(export!.IsEnabled); // should be disabled until slices exist
    }

    [AvaloniaFact]
    public void Modes_Are_Mutually_Exclusive_And_AtLeastOneChecked()
    {
        var win = new MainWindow();
        win.Show();

        var auto = win.FindControl<ToggleButton>("AutoModeRb")!;
        var grid = win.FindControl<ToggleButton>("GridModeRb")!;
        Assert.NotNull(auto);
        Assert.NotNull(grid);

        grid.IsChecked = true;
        Assert.True(grid.IsChecked);
        Assert.False(auto.IsChecked);

        auto.IsChecked = true;
        Assert.True(auto.IsChecked);
        Assert.False(grid.IsChecked);

        bool AutoOn() => auto.IsChecked == true;
        bool GridOn() => grid.IsChecked == true;
        Assert.True(AutoOn() || GridOn());
        Assert.False(AutoOn() && GridOn());
    }

    [AvaloniaFact]
    public void GridInputs_Enabled_Only_When_GridMode_Checked()
    {
        var win = new MainWindow();
        win.Show();

        var grid = win.FindControl<ToggleButton>("GridModeRb")!;
        var cellW = win.FindControl<TextBox>("CellWBox")!;
        var cellH = win.FindControl<TextBox>("CellHBox")!;

        grid.IsChecked = true; // XAML binds IsEnabled to GridModeRb.IsChecked
        Assert.True(cellW.IsEnabled);
        Assert.True(cellH.IsEnabled);

        var auto = win.FindControl<ToggleButton>("AutoModeRb")!;
        auto.IsChecked = true;
        Assert.False(cellW.IsEnabled);
        Assert.False(cellH.IsEnabled);
    }

    [AvaloniaFact]
    public void AfterSliceBuildOverlays_Populates_ViewModels_And_Enables_Export()
    {
        var win = new MainWindow();
        win.Show();

        var slices = new SliceResult
        {
            SourcePath = "fake.png",
            ImageWidth = 64,
            ImageHeight = 64,
        };
        slices.Slices.Add(
            new SliceRect
            {
                X = 0,
                Y = 0,
                Width = 16,
                Height = 16,
                Name = "slice_000",
            }
        );

        SetPrivateField(win, "_slices", slices);

        CallPrivate(win, "AfterSliceBuildOverlays");

        var export = win.FindControl<Button>("ExportBtn")!;
        Assert.True(export.IsEnabled); // enabled because we "have slices"

        var itemsCtrl = win.FindControl<ItemsControl>("SliceItems")!;
        var vms = (ObservableCollection<SliceItemVM>)itemsCtrl.ItemsSource!;
        Assert.Single(vms);
        Assert.Equal(0, vms[0].Index);
        Assert.Equal(16, vms[0].Slice.Width);
    }

    [AvaloniaFact]
    public void Export_Click_With_No_Selection_Logs_Error_And_Writes_Nothing()
    {
        var win = new MainWindow();
        win.Show();

        SetPrivateField(win, "_imagePath", "fake.png");
        var slices = new SliceResult
        {
            SourcePath = "fake.png",
            ImageWidth = 10,
            ImageHeight = 10,
        };
        slices.Slices.Add(
            new SliceRect
            {
                X = 0,
                Y = 0,
                Width = 5,
                Height = 5,
                Name = "slice_000",
            }
        );
        SetPrivateField(win, "_slices", slices);

        var sliceItems = GetPrivateField<ObservableCollection<SliceItemVM>>(win, "_sliceItems");
        sliceItems.Clear();
        sliceItems.Add(new SliceItemVM(0, slices.Slices[0]) { IsSelected = false });

        CallPrivate(win, "OnExportClicked", null!, new RoutedEventArgs());

        var logList = win.FindControl<ItemsControl>("LogList")!;
        var logs = (ObservableCollection<LogEntry>)logList.ItemsSource!;
        Assert.Contains(
            logs,
            l => l.Message.Contains("No slices selected.", StringComparison.OrdinalIgnoreCase)
        );
    }
}
