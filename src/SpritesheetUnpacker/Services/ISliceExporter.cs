namespace SpritesheetUnpacker.Services;

public interface ISliceExporter
{
    void ExportSlices(string srcPath, SliceResult slices, string outDir);
}
