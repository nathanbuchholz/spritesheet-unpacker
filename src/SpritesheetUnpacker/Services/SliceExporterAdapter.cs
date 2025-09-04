namespace SpritesheetUnpacker.Services;

public sealed class SliceExporterAdapter : ISliceExporter
{
    public void ExportSlices(string srcPath, SliceResult slices, string outDir) =>
        SpriteExporter.ExportSlices(srcPath, slices, outDir);
}
