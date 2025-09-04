using System.Collections.ObjectModel;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using SpritesheetUnpacker.Services;

namespace SpritesheetUnpacker.Tests;

public sealed class ExportTests
{
    private static void SetPrivateField(object obj, string name, object? value) =>
        obj.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(obj, value);

    private static object? CallPrivate(object obj, string name, params object?[] args) =>
        obj.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(obj, args);

    private sealed class FakeExporter : ISliceExporter
    {
        public (string src, SliceResult result, string dir)? Last;

        public void ExportSlices(string srcPath, SliceResult slices, string outDir) =>
            Last = (srcPath, slices, outDir);
    }

    [AvaloniaFact]
    public void Export_Uses_Indirection_And_Skips_IO()
    {
        var fake = new FakeExporter();
        var win = new MainWindow(fake);
        win.Show();

        var slices = new SliceResult
        {
            SourcePath = "fake.png",
            ImageWidth = 16,
            ImageHeight = 16,
        };
        var r = new SliceRect
        {
            X = 0,
            Y = 0,
            Width = 8,
            Height = 8,
            Name = "slice_0",
        };
        slices.Slices.Add(r);

        SetPrivateField(win, "_imagePath", "fake.png");
        SetPrivateField(win, "_slices", slices);

        var selected = new List<SliceRect> { r };
        CallPrivate(win, "ExportToFolder", "/tmp/out", selected);

        Assert.NotNull(fake.Last);
        Assert.Equal("fake.png", fake.Last!.Value.src);
        Assert.Equal("/tmp/out", fake.Last!.Value.dir);
        Assert.Single(fake.Last!.Value.result.Slices);

        var logList = win.FindControl<ItemsControl>("LogList")!;
        var logs = (ObservableCollection<LogEntry>)logList.ItemsSource!;
        Assert.Contains(
            logs,
            l => l.Message.Contains("Exported 1 slice", StringComparison.OrdinalIgnoreCase)
        );
    }
}
