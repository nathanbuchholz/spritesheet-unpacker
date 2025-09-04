using System;
using Avalonia.Media;

namespace SpritesheetUnpacker.Services;

public sealed class LogEntry(DateTime timestampLocal, string message, IBrush brush)
{
    public DateTime TimestampLocal { get; } = timestampLocal;
    public string Message { get; } = message;
    public IBrush Brush { get; } = brush;
}
