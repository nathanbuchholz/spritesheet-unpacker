using System.Collections.Generic;

namespace SpritesheetUnpacker.Services;

public sealed class SliceRect
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public string Name { get; init; } = "";
}

public sealed class SliceResult
{
    public string SourcePath { get; init; } = "";
    public int ImageWidth { get; init; }
    public int ImageHeight { get; init; }
    public List<SliceRect> Slices { get; } = new();
}
