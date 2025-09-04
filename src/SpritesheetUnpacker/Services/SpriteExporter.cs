using System.IO;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SpritesheetUnpacker.Services;

public static class SpriteExporter
{
    public static void ExportSlices(string srcPath, SliceResult slices, string outDir)
    {
        Directory.CreateDirectory(outDir);

        using (var img = Image.Load<Rgba32>(srcPath))
        {
            foreach (var r in slices.Slices)
            {
                using var cropped = img.Clone(ctx =>
                    ctx.Crop(new Rectangle(r.X, r.Y, r.Width, r.Height))
                );
                var file = Path.Combine(outDir, $"{r.Name}.png");
                cropped.SaveAsPng(file);
            }
        }

        var json = JsonSerializer.Serialize(
            slices,
            new JsonSerializerOptions { WriteIndented = true }
        );
        File.WriteAllText(Path.Combine(outDir, "atlas.json"), json);
    }
}
