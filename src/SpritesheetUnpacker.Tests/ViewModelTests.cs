using SpritesheetUnpacker.Services;

namespace SpritesheetUnpacker.Tests;

public sealed class ViewModelTests
{
    [Fact]
    public void SliceItemVM_Raises_PropertyChanged_For_All_Setters()
    {
        var vm = new SliceItemVM(
            3,
            new SliceRect
            {
                X = 1,
                Y = 2,
                Width = 3,
                Height = 4,
                Name = "n",
            }
        );
        var count = 0;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null)
                count++;
        };

        vm.IsSelected = true;
        vm.Left = 1.5;
        vm.Top = 2.5;
        vm.Width = 3.5;
        vm.Height = 4.5;

        Assert.True(count >= 5);
        Assert.Equal(3, vm.Index);
        Assert.Equal(3, vm.Slice.Width);
    }

    [Fact]
    public void LogEntry_Holds_Constructed_Values()
    {
        var now = new DateTime(2025, 1, 2, 3, 4, 5);
        var brush = Avalonia.Media.Brushes.White;
        var logEntry = new LogEntry(now, "hello", brush);
        Assert.Equal(now, logEntry.TimestampLocal);
        Assert.Equal("hello", logEntry.Message);
        Assert.Equal(brush, logEntry.Brush);
    }
}
