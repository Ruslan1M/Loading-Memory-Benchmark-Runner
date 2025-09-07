using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Benchmark.Benchmark
{
    public static class GraphExporter
    {
        public static void SaveMemoryGraph(
            List<MemoryPoint> samples,
            string pngPath,
            int width = 1200,
            int height = 600,
            string title = null)
        {
            if (samples == null || samples.Count < 2) return;

            double tMin = samples[0].timeMs, tMax = samples[^1].timeMs;
            if (tMax <= tMin) tMax = tMin + 1.0;

            double yMax = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                yMax = Math.Max(yMax,
                    Math.Max(Math.Max(s.allocatedMB, s.reservedMB), Math.Max(s.monoMB, s.systemUsedMB)));
            }

            if (yMax <= 0) yMax = 1;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };

            var bg = new Color32(255, 255, 255, 255);
            var px = new Color32[width * height];
            for (int i = 0; i < px.Length; i++) px[i] = bg;
            tex.SetPixels32(px);

            int left = 60, right = 20, top = 20, bottom = 40;
            int plotW = width - left - right;
            int plotH = height - top - bottom;

            DrawRect(tex, left, top, plotW, plotH, new Color32(245, 245, 245, 255)); // light back
            DrawAxes(tex, left, top, plotW, plotH, new Color32(0, 0, 0, 255));

            double yStep = NiceStep(yMax / 5.0);
            for (double y = 0; y <= yMax; y += yStep)
            {
                int py = YToPix(y, yMax, top, plotH);
                DrawHLine(tex, left, left + plotW, py, new Color32(220, 220, 220, 255));
            }

            double totalMs = tMax - tMin;
            double xStepMs = NiceTimeStep(totalMs / 6.0);
            for (double t = tMin; t <= tMax; t += xStepMs)
            {
                int pxX = XToPix(t, tMin, tMax, left, plotW);
                DrawVLine(tex, pxX, top, top + plotH, new Color32(220, 220, 220, 255));
            }

            var colAlloc = new Color32(33, 150, 243, 255);
            var colRes = new Color32(76, 175, 80, 255);
            var colMono = new Color32(255, 87, 34, 255);
            var colSys = new Color32(156, 39, 176, 255);

            Plot(samples, s => s.allocatedMB, colAlloc);
            Plot(samples, s => s.reservedMB, colRes);
            Plot(samples, s => s.monoMB, colMono);
            Plot(samples, s => s.systemUsedMB, colSys, skipZeros: true);

            if (!string.IsNullOrEmpty(title))
            {
                FillRect(tex, left, 4, plotW, 12, new Color32(240, 240, 240, 255));
            }

            tex.Apply();

            var bytes = tex.EncodeToPNG();
            Directory.CreateDirectory(Path.GetDirectoryName(pngPath) ?? ".");
            File.WriteAllBytes(pngPath, bytes);
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif

            void Plot(List<MemoryPoint> list, Func<MemoryPoint, double> sel, Color32 color, bool skipZeros = false)
            {
                Vector2Int? prev = null;
                for (int i = 0; i < list.Count; i++)
                {
                    var v = sel(list[i]);
                    if (skipZeros && v <= 0)
                    {
                        prev = null;
                        continue;
                    }

                    int x = XToPix(list[i].timeMs, tMin, tMax, left, plotW);
                    int y = YToPix(v, yMax, top, plotH);
                    var cur = new Vector2Int(x, y);
                    if (prev.HasValue) DrawLine(tex, prev.Value, cur, color);
                    prev = cur;
                }
            }
        }

        private static int XToPix(double t, double tMin, double tMax, int left, int plotW)
        {
            double u = (t - tMin) / (tMax - tMin);
            u = Mathf.Clamp01((float)u);
            return left + (int)Math.Round(u * (plotW - 1));
        }

        private static int YToPix(double val, double yMax, int top, int plotH)
        {
            double u = (val <= 0) ? 0 : (val / yMax);
            u = Mathf.Clamp01((float)u);
            return top + (plotH - 1) - (int)Math.Round(u * (plotH - 1));
        }

        private static double NiceStep(double raw)
        {
            double exp = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double f = raw / exp;
            double nice;
            if (f < 1.5) nice = 1;
            else if (f < 3) nice = 2;
            else if (f < 7) nice = 5;
            else nice = 10;
            return nice * exp;
        }

        private static double NiceTimeStep(double rawMs)
        {
            return NiceStep(rawMs);
        }

        private static void DrawAxes(Texture2D tex, int left, int top, int w, int h, Color32 col)
        {
            DrawHLine(tex, left, left + w, top + h, col);
            DrawVLine(tex, left, top, top + h, col);
        }

        private static void DrawRect(Texture2D tex, int x, int y, int w, int h, Color32 border)
        {
            DrawHLine(tex, x, x + w, y, border);
            DrawHLine(tex, x, x + w, y + h, border);
            DrawVLine(tex, x, y, y + h, border);
            DrawVLine(tex, x + w, y, y + h, border);
        }

        private static void FillRect(Texture2D tex, int x, int y, int w, int h, Color32 c)
        {
            for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++)
                SafeSetPixel(tex, xx, yy, c);
        }

        private static void DrawHLine(Texture2D tex, int x0, int x1, int y, Color32 c)
        {
            if (y < 0 || y >= tex.height) return;
            if (x0 > x1) (x0, x1) = (x1, x0);
            x0 = Mathf.Clamp(x0, 0, tex.width - 1);
            x1 = Mathf.Clamp(x1, 0, tex.width - 1);
            for (int x = x0; x <= x1; x++) SafeSetPixel(tex, x, y, c);
        }

        private static void DrawVLine(Texture2D tex, int x, int y0, int y1, Color32 c)
        {
            if (x < 0 || x >= tex.width) return;
            if (y0 > y1) (y0, y1) = (y1, y0);
            y0 = Mathf.Clamp(y0, 0, tex.height - 1);
            y1 = Mathf.Clamp(y1, 0, tex.height - 1);
            for (int y = y0; y <= y1; y++) SafeSetPixel(tex, x, y, c);
        }

        private static void DrawLine(Texture2D tex, Vector2Int a, Vector2Int b, Color32 c)
        {
            int x0 = a.x, y0 = a.y, x1 = b.x, y1 = b.y;
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy, e2;
            while (true)
            {
                SafeSetPixel(tex, x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }

                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private static void SafeSetPixel(Texture2D tex, int x, int y, Color32 c)
        {
            if ((uint)x >= (uint)tex.width || (uint)y >= (uint)tex.height) return;
            tex.SetPixel(x, y, c);
        }
    }
}