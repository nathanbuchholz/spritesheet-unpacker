using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SpritesheetUnpacker.Services;

public sealed class SliceItemVM : INotifyPropertyChanged
{
    private const float Tolerance = 0.5f;
    public int Index { get; }
    public SliceRect Slice { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    private double _left,
        _top,
        _width,
        _height;
    public double Left
    {
        get => _left;
        set
        {
            if (Math.Abs(_left - value) < Tolerance)
                return;
            _left = value;
            OnPropertyChanged();
        }
    }
    public double Top
    {
        get => _top;
        set
        {
            if (Math.Abs(_top - value) < Tolerance)
                return;
            _top = value;
            OnPropertyChanged();
        }
    }
    public double Width
    {
        get => _width;
        set
        {
            if (Math.Abs(_width - value) < Tolerance)
                return;
            _width = value;
            OnPropertyChanged();
        }
    }
    public double Height
    {
        get => _height;
        set
        {
            if (Math.Abs(_height - value) < Tolerance)
                return;
            _height = value;
            OnPropertyChanged();
        }
    }

    public SliceItemVM(int index, SliceRect slice)
    {
        Index = index;
        Slice = slice;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
