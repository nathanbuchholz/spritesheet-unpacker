using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SixLabors.ImageSharp.PixelFormats;
using SpritesheetUnpacker.Services;

namespace SpritesheetUnpacker;

public partial class MainWindow : Window
{
    private readonly Button? _openBtn;
    private readonly Button? _clearBtn;
    private readonly Button? _sliceBtn;
    private readonly ToggleButton? _autoModeRb;
    private readonly ToggleButton? _gridModeRb;
    private readonly StackPanel? _gridInputs;
    private readonly TextBox? _cellWBox;
    private readonly TextBox? _cellHBox;
    private readonly TextBox? _marginBox;
    private readonly Button? _exportBtn;
    private readonly Image? _previewImage;
    private readonly Border? _dropZone;
    private readonly Border? _previewContainer;
    private readonly SelectableTextBlock? _currentFileTxt;
    private readonly ScrollViewer? _logScroll;

    private readonly ItemsControl? _sliceItemsCtrl;
    private readonly ObservableCollection<SliceItemVM> _sliceItems = new();
    private Rect _imageDestRect;
    private int _imgW,
        _imgH;

    private readonly ObservableCollection<LogEntry> _logs = new();

    private string? _imagePath;
    private SliceResult? _slices;

    private static readonly HashSet<string> AllowedExtensions = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".gif",
        ".tif",
        ".tiff",
        ".webp",
    };

    private bool _modeGuard;
    private readonly ISliceExporter _exporter;

    public MainWindow()
        : this(new SliceExporterAdapter()) { }

    public MainWindow(ISliceExporter exporter)
    {
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));

        InitializeComponent();

        Closed += OnWindowClosed;

        _openBtn = this.FindControl<Button>("OpenBtn");
        _clearBtn = this.FindControl<Button>("ClearBtn");
        _sliceBtn = this.FindControl<Button>("SliceBtn");
        _exportBtn = this.FindControl<Button>("ExportBtn");

        _autoModeRb  = this.FindControl<ToggleButton>("AutoModeRb");
        _gridModeRb  = this.FindControl<ToggleButton>("GridModeRb");

        _gridInputs = this.FindControl<StackPanel>("GridInputs");
        _cellWBox = this.FindControl<TextBox>("CellWBox");
        _cellHBox = this.FindControl<TextBox>("CellHBox");
        _marginBox = this.FindControl<TextBox>("MarginBox");

        _previewImage = this.FindControl<Image>("PreviewImage");
        _dropZone = this.FindControl<Border>("DropZone");
        _previewContainer = this.FindControl<Border>("PreviewContainer");
        _sliceItemsCtrl = this.FindControl<ItemsControl>("SliceItems");
        _currentFileTxt = this.FindControl<SelectableTextBlock>("CurrentFileTxt");
        var logList = this.FindControl<ItemsControl>("LogList");
        _logScroll = this.FindControl<ScrollViewer>("LogScroll");

        if (logList != null)
            logList.ItemsSource = _logs;

        if (_openBtn != null)
            _openBtn.Click += OnOpenClicked;
        if (_sliceBtn != null)
            _sliceBtn.Click += OnSliceClicked;
        if (_exportBtn != null)
            _exportBtn.Click += OnExportClicked;
        if (_clearBtn != null)
            _clearBtn.Click += OnClearClicked;

        _autoModeRb?.AddHandler(
            ToggleButton.IsCheckedChangedEvent,
            OnModeIsCheckedChanged,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble
        );

        _gridModeRb?.AddHandler(
            ToggleButton.IsCheckedChangedEvent,
            OnModeIsCheckedChanged,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble
        );

        if (_dropZone != null)
        {
            _dropZone.PointerReleased += OnDropZoneClicked;
            _dropZone.AddHandler(DragDrop.DragOverEvent, OnDropZoneDragOver);
            _dropZone.AddHandler(DragDrop.DragLeaveEvent, OnDropZoneDragLeave);
            _dropZone.AddHandler(DragDrop.DropEvent, OnDropZoneDrop);
        }

        if (_sliceItemsCtrl != null)
        {
            _sliceItemsCtrl.ItemsSource = _sliceItems;
            _sliceItemsCtrl.SizeChanged += (_, __) =>
            {
                UpdateImageDestRect();
                UpdateSliceItemsRects();
            };
        }

        SetGridInputsEnabled(_gridModeRb?.IsChecked == true);
        UpdateCurrentFileLabel();
        LogNeutral("Ready");
    }

    private async void OnOpenClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var imagesFilter = new FilePickerFileType("Images")
            {
                Patterns =
                [
                    "*.png",
                    "*.jpg",
                    "*.jpeg",
                    "*.bmp",
                    "*.gif",
                    "*.tif",
                    "*.tiff",
                    "*.webp",
                ],
            };

            var files = await StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    AllowMultiple = false,
                    FileTypeFilter = [imagesFilter, FilePickerFileTypes.All],
                }
            );

            if (files is { Count: > 0 })
            {
                var local = files[0].TryGetLocalPath();
                if (!string.IsNullOrWhiteSpace(local) && IsSupportedImage(local))
                {
                    LoadImageFromPath(local);
                    LogNeutral($"Loaded: {local}");
                }
                else
                {
                    LogError("Unsupported or unreadable image.");
                }
            }
        }
        catch (Exception exception)
        {
            LogError(exception.Message);
        }
    }

    private void OnDropZoneClicked(object? sender, PointerReleasedEventArgs e)
    {
        OnOpenClicked(sender, e);
    }

    private void OnDropZoneDragOver(object? sender, DragEventArgs e)
    {
        var items = e.Data.GetFiles()?.ToList();

        bool ok =
            items is { Count: > 0 }
            && items.All(it =>
            {
                var name = it.Name; // works even if not local
                var ext = Path.GetExtension(name);
                return AllowedExtensions.Contains(ext);
            });

        e.DragEffects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;

        if (_dropZone == null)
            return;
        if (ok)
        {
            _dropZone.BorderBrush = Brushes.DodgerBlue;
            _dropZone.Background = new SolidColorBrush(Color.Parse("#142033"));
        }
        else
        {
            _dropZone.BorderBrush = new SolidColorBrush(Color.Parse("#993333")); // “nope” hint
            _dropZone.Background = new SolidColorBrush(Color.Parse("#220000"));
        }
    }

    private void OnDropZoneDragLeave(object? sender, RoutedEventArgs e)
    {
        if (_dropZone == null)
            return;
        _dropZone.BorderBrush = new SolidColorBrush(Color.Parse("#666666"));
        _dropZone.Background = new SolidColorBrush(Color.Parse("#111111"));
    }

    private void OnDropZoneDrop(object? sender, DragEventArgs e)
    {
        if (_dropZone == null)
            return;

        _dropZone.BorderBrush = new SolidColorBrush(Color.Parse("#666666"));
        _dropZone.Background = new SolidColorBrush(Color.Parse("#111111"));

        var items = e.Data.GetFiles()?.ToList();
        if (items is not { Count: > 0 })
        {
            LogError("Dropped data had no files.");
            return;
        }

        if (!items.All(it => AllowedExtensions.Contains(Path.GetExtension(it.Name))))
        {
            LogError("Only image files are supported (png, jpg, jpeg, bmp, gif, tif, tiff, webp).");
            return;
        }

        foreach (var it in items)
        {
            var path = it.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(path) && IsSupportedImage(path))
            {
                LoadImageFromPath(path);
                LogNeutral($"Loaded: {path}");
                return;
            }
        }

        LogError("Could not read the dropped image file.");
    }

    private void OnModeIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_modeGuard)
            return;
        _modeGuard = true;
        try
        {
            var auto = _autoModeRb?.IsChecked == true;
            var grid = _gridModeRb?.IsChecked == true;

            if (Equals(sender, _autoModeRb) && auto && _gridModeRb != null)
                _gridModeRb.IsChecked = false;
            if (Equals(sender, _gridModeRb) && grid && _autoModeRb != null)
                _autoModeRb.IsChecked = false;

            if (_autoModeRb?.IsChecked != true && _gridModeRb?.IsChecked != true)
                _autoModeRb!.IsChecked = true;

            SetGridInputsEnabled(_gridModeRb!.IsChecked == true);
        }
        finally
        {
            _modeGuard = false;
        }
    }

    private void SetGridInputsEnabled(bool enabled)
    {
        if (_gridInputs != null)
            _gridInputs.IsEnabled = enabled;
    }

    private void OnSliceClicked(object? sender, RoutedEventArgs e)
    {
        if (_imagePath is null || _previewImage?.Source is not Bitmap)
        {
            LogError("Load an image first.");
            return;
        }

        ClearSlices();

        if (_autoModeRb?.IsChecked == true)
        {
            try
            {
                _slices = SpriteAutoSlicer.SliceIrregular(
                    _imagePath,
                    alphaThreshold: 8,
                    minW: 2,
                    minH: 2,
                    pad: 1
                );
                LogSuccess($"Auto: {_slices.Slices.Count} slices");
                AfterSliceBuildOverlays();
            }
            catch (Exception ex)
            {
                LogError($"Auto slice failed: {ex.Message}");
            }
        }
        else
        {
            if (!TryParseInt(_cellWBox, out var cellW) || cellW <= 0)
            {
                LogError("Cell W must be a positive integer.");
                return;
            }

            if (!TryParseInt(_cellHBox, out var cellH) || cellH <= 0)
            {
                LogError("Cell H must be a positive integer.");
                return;
            }

            if (!TryParseInt(_marginBox, out var margin) || margin < 0)
            {
                LogError("Margin must be a non-negative integer.");
                return;
            }

            var res = SliceByGrid(cellW, cellH, margin);
            if (res is null)
                return;

            _slices = res;
            LogSuccess($"Grid: {_slices.Slices.Count} slices ({cellW}x{cellH}, margin {margin})");
            AfterSliceBuildOverlays();
        }
    }

    private static bool TryParseInt(TextBox? tb, out int value)
    {
        if (tb is not null && int.TryParse(tb.Text?.Trim(), out value))
            return true;
        value = 0;
        return false;
    }

    private void AfterSliceBuildOverlays()
    {
        if (_slices == null)
            return;

        if (_exportBtn != null)
            _exportBtn.IsEnabled = _slices.Slices.Count > 0;

        _sliceItems.Clear();
        for (int i = 0; i < _slices.Slices.Count; i++)
            _sliceItems.Add(new SliceItemVM(i, _slices.Slices[i]));

        UpdateSliceItemsRects();
    }

    private SliceResult? SliceByGrid(int cellW, int cellH, int margin)
    {
        if (_imgW <= 0 || _imgH <= 0)
        {
            LogError("Image size unknown.");
            return null;
        }

        var usableW = _imgW - 2 * margin;
        var usableH = _imgH - 2 * margin;

        if (usableW <= 0 || usableH <= 0)
        {
            LogError("Margin too large for this image.");
            return null;
        }

        if (usableW % cellW != 0 || usableH % cellH != 0)
        {
            LogError(
                $"Grid doesn't fit: ({_imgW}x{_imgH}) with margin {margin} is not divisible by {cellW}x{cellH}."
            );
            return null;
        }

        var cols = usableW / cellW;
        var rows = usableH / cellH;

        var result = new SliceResult
        {
            SourcePath = _imagePath!,
            ImageWidth = _imgW,
            ImageHeight = _imgH,
        };

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var x = margin + c * cellW;
                var y = margin + r * cellH;

                result.Slices.Add(
                    new SliceRect
                    {
                        X = x,
                        Y = y,
                        Width = cellW,
                        Height = cellH,
                    }
                );
            }
        }

        return result;
    }

    private void LoadImageFromPath(string path)
    {
        ClearSlices();

        _imagePath = path;

        if (_previewImage != null)
        {
            if (_previewImage.Source is Bitmap oldBmp)
            {
                _previewImage.Source = null;
                oldBmp.Dispose();
            }

            using var fs = File.OpenRead(path);
            _previewImage.Source = new Bitmap(fs);
        }

        if (_sliceBtn != null)
            _sliceBtn.IsEnabled = true;
        if (_clearBtn != null)
            _clearBtn.IsEnabled = true;

        if (_dropZone != null)
            _dropZone.IsVisible = false;
        if (_previewContainer != null)
            _previewContainer.IsVisible = true;

        UpdateCurrentFileLabel();

        if (_previewImage?.Source is Bitmap bmp)
        {
            _imgW = bmp.PixelSize.Width;
            _imgH = bmp.PixelSize.Height;

            Dispatcher.UIThread.Post(
                () =>
                {
                    UpdateImageDestRect();
                    UpdateSliceItemsRects();
                },
                DispatcherPriority.Background
            );
        }
    }

    private void OnClearClicked(object? sender, RoutedEventArgs e) => ClearLoadedFile();

    private void OnAutoSliceClicked(object? sender, RoutedEventArgs e)
    {
        if (_imagePath is null)
            return;

        try
        {
            _slices = SpriteAutoSlicer.SliceIrregular(
                _imagePath,
                alphaThreshold: 8,
                minW: 2,
                minH: 2,
                pad: 1
            );
            LogSuccess($"Found: {_slices.Slices.Count} slices");
            if (_exportBtn != null)
                _exportBtn.IsEnabled = _slices.Slices.Count > 0;

            _sliceItems.Clear();
            for (int i = 0; i < _slices.Slices.Count; i++)
                _sliceItems.Add(new SliceItemVM(i, _slices.Slices[i]));

            UpdateSliceItemsRects();
        }
        catch (Exception ex)
        {
            LogError($"Slice failed: {ex.Message}");
        }
    }

    private async void OnExportClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_imagePath is null || _slices is null)
            {
                LogError("No image/slices to export.");
                return;
            }

            var selected = _sliceItems.Where(x => x.IsSelected).Select(x => x.Slice).ToList();
            if (selected.Count == 0)
            {
                LogError("No slices selected.");
                return;
            }

            var folder = await StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions { AllowMultiple = false }
            );

            if (folder is { Count: > 0 })
            {
                var local = folder[0].TryGetLocalPath();
                if (!string.IsNullOrEmpty(local))
                {
                    var subset = new SliceResult
                    {
                        SourcePath = _imagePath,
                        ImageWidth = _slices.ImageWidth,
                        ImageHeight = _slices.ImageHeight,
                    };
                    foreach (var r in selected)
                        subset.Slices.Add(r);

                    try
                    {
                        ExportToFolder(local, selected);
                        OpenInFileManager(local);
                    }
                    catch (Exception ex)
                    {
                        LogError($"Export failed: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception exception)
        {
            LogError(exception.Message);
        }
    }

    private static void OpenInFileManager(string folderPath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(
                new ProcessStartInfo("explorer.exe", folderPath) { UseShellExecute = true }
            );
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", folderPath);
        }
        else
        {
            Process.Start("xdg-open", folderPath);
        }
    }

    private static bool IsSupportedImage(string path)
    {
        try
        {
            using var _ = SixLabors.ImageSharp.Image.Load<Rgba32>(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void UpdateCurrentFileLabel()
    {
        if (_currentFileTxt == null)
            return;
        _currentFileTxt.Text = string.IsNullOrWhiteSpace(_imagePath) ? "Load file..." : _imagePath;
    }

    private void LogNeutral(string message) => AddLog(message, Brushes.White);

    private void LogSuccess(string message) => AddLog(message, Brushes.LimeGreen);

    private void LogError(string message) => AddLog(message, Brushes.OrangeRed);

    private void AddLog(string message, IBrush color)
    {
        _logs.Add(new LogEntry(DateTime.Now, message, color));
        if (_logScroll != null)
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    var extent = _logScroll.Extent;
                    _logScroll.Offset = new Vector(_logScroll.Offset.X, extent.Height);
                },
                DispatcherPriority.Background
            );
        }
    }

    private void UpdateImageDestRect()
    {
        if (_sliceItemsCtrl == null || _imgW <= 0 || _imgH <= 0)
            return;

        var viewW = _sliceItemsCtrl.Bounds.Width;
        var viewH = _sliceItemsCtrl.Bounds.Height;
        if (viewW <= 0 || viewH <= 0)
            return;

        var scale = Math.Min(viewW / _imgW, viewH / _imgH);
        var drawW = _imgW * scale;
        var drawH = _imgH * scale;
        var left = (viewW - drawW) * 0.5;
        var top = (viewH - drawH) * 0.5;

        _imageDestRect = new Rect(left, top, drawW, drawH);
    }

    private void UpdateSliceItemsRects()
    {
        if (_sliceItemsCtrl == null || _slices == null || _imgW <= 0 || _imgH <= 0)
            return;

        var scale = Math.Min(_imageDestRect.Width / _imgW, _imageDestRect.Height / _imgH);
        if (scale <= 0)
            return;

        foreach (var vm in _sliceItems)
        {
            var s = vm.Slice;
            vm.Left = _imageDestRect.X + s.X * scale;
            vm.Top = _imageDestRect.Y + s.Y * scale;
            vm.Width = s.Width * scale;
            vm.Height = s.Height * scale;
        }
    }

    private void ClearSlices()
    {
        _sliceItems.Clear();
        _slices = null;
        if (_exportBtn != null)
            _exportBtn.IsEnabled = false;
    }

    private void ClearLoadedFile()
    {
        ClearSlices();

        if (_previewImage?.Source is Bitmap old)
        {
            _previewImage.Source = null;
            old.Dispose();
        }

        _imagePath = null;
        _imgW = _imgH = 0;
        _imageDestRect = default;

        if (_sliceBtn != null)
            _sliceBtn.IsEnabled = false;
        if (_clearBtn != null)
            _clearBtn.IsEnabled = false;

        if (_dropZone != null)
            _dropZone.IsVisible = true;
        if (_previewContainer != null)
            _previewContainer.IsVisible = false;

        UpdateCurrentFileLabel();
        LogNeutral("Cleared.");
    }

    private void ExportToFolder(string folderPath, List<SliceRect> selected)
    {
        if (_imagePath is null || _slices is null)
            throw new InvalidOperationException("No image/slices.");

        var subset = new SliceResult
        {
            SourcePath = _imagePath,
            ImageWidth = _slices.ImageWidth,
            ImageHeight = _slices.ImageHeight,
        };
        foreach (var r in selected)
            subset.Slices.Add(r);

        _exporter.ExportSlices(_imagePath, subset, folderPath);
        LogSuccess($"Exported {selected.Count} slice(s) to: {folderPath}");
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_previewImage?.Source is Bitmap bmp)
        {
            _previewImage.Source = null;
            bmp.Dispose();
        }

        if (_dropZone != null)
        {
            _dropZone.PointerReleased -= OnDropZoneClicked;
            _dropZone.RemoveHandler(DragDrop.DragOverEvent, OnDropZoneDragOver);
            _dropZone.RemoveHandler(DragDrop.DragLeaveEvent, OnDropZoneDragLeave);
            _dropZone.RemoveHandler(DragDrop.DropEvent, OnDropZoneDrop);
        }

        if (_openBtn != null)
            _openBtn.Click -= OnOpenClicked;
        if (_sliceBtn != null)
            _sliceBtn.Click -= OnSliceClicked;
        if (_exportBtn != null)
            _exportBtn.Click -= OnExportClicked;
        if (_clearBtn != null)
            _clearBtn.Click -= OnClearClicked;
    }
}
