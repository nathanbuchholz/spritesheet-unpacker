using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace SpritesheetUnpacker.Services
{
    public static class SpriteAutoSlicer
    {
        public static SliceResult SliceIrregular(
            string path,
            byte alphaThreshold = 8,
            int minW = 2,
            int minH = 2,
            int pad = 1
        )
        {
            using var img = Image.Load<Rgba32>(path);
            int w = img.Width,
                h = img.Height;

            var visited = new bool[w, h];
            var result = new SliceResult
            {
                SourcePath = path,
                ImageWidth = w,
                ImageHeight = h,
            };
            var q = new Queue<(int x, int y)>();

            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    if (visited[x, y])
                        continue;

                    if (img[x, y].A < alphaThreshold)
                    {
                        visited[x, y] = true;
                        continue;
                    }

                    int minX = x,
                        minY = y,
                        maxX = x,
                        maxY = y;
                    visited[x, y] = true;
                    q.Enqueue((x, y));

                    while (q.Count > 0)
                    {
                        var (cx, cy) = q.Dequeue();

                        if (cx < minX)
                            minX = cx;
                        if (cy < minY)
                            minY = cy;
                        if (cx > maxX)
                            maxX = cx;
                        if (cy > maxY)
                            maxY = cy;

                        EnqueueIfOpaque(cx - 1, cy);
                        EnqueueIfOpaque(cx + 1, cy);
                        EnqueueIfOpaque(cx, cy - 1);
                        EnqueueIfOpaque(cx, cy + 1);
                    }

                    var rw = (maxX - minX + 1);
                    var rh = (maxY - minY + 1);
                    if (rw >= minW && rh >= minH)
                    {
                        var sx = Math.Max(0, minX - pad);
                        var sy = Math.Max(0, minY - pad);
                        var ex = Math.Min(w - 1, maxX + pad);
                        var ey = Math.Min(h - 1, maxY + pad);

                        result.Slices.Add(
                            new SliceRect
                            {
                                X = sx,
                                Y = sy,
                                Width = ex - sx + 1,
                                Height = ey - sy + 1,
                                Name = $"slice_{result.Slices.Count:000}",
                            }
                        );
                    }

                    continue;

                    void EnqueueIfOpaque(int nx, int ny)
                    {
                        if ((uint)nx >= (uint)w || (uint)ny >= (uint)h)
                            return;
                        if (visited[nx, ny])
                            return;
                        if (img[nx, ny].A < alphaThreshold)
                        {
                            visited[nx, ny] = true;
                            return;
                        }
                        visited[nx, ny] = true;
                        q.Enqueue((nx, ny));
                    }
                }
            }

            return result;
        }
    }
}
