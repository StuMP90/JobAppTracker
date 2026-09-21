using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JobAppTracker.Tests
{
    public static class IconGenerator
    {
        public static int Generate(string targetIcoPath)
        {
            try
            {
                var fullIcoPath = Path.GetFullPath(targetIcoPath);
                Console.WriteLine($"Generating application icon at: {fullIcoPath}");

                int[] sizes = new int[] { 256, 48, 32, 16 };
                var pngImages = new List<(int Size, byte[] Data)>();

                foreach (var size in sizes)
                {
                    var visual = new DrawingVisual();
                    using (var dc = visual.RenderOpen())
                    {
                        RenderIcon(dc, size);
                    }

                    var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
                    rtb.Render(visual);

                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(rtb));

                    using var ms = new MemoryStream();
                    encoder.Save(ms);
                    pngImages.Add((size, ms.ToArray()));
                }

                // Also save 256x256 PNG as app.png for docs/web
                var pngPath = Path.ChangeExtension(fullIcoPath, ".png");
                File.WriteAllBytes(pngPath, pngImages[0].Data);
                Console.WriteLine($"Saved preview PNG at: {pngPath}");

                // Assemble ICO file
                using var fs = new FileStream(fullIcoPath, FileMode.Create, FileAccess.Write);
                using var bw = new BinaryWriter(fs);

                // ICONDIR Header
                bw.Write((short)0); // Reserved
                bw.Write((short)1); // Type: 1 = Icon
                bw.Write((short)pngImages.Count); // Count of images

                int offset = 6 + (16 * pngImages.Count);

                // ICONDIRENTRY for each image
                foreach (var img in pngImages)
                {
                    bw.Write((byte)(img.Size >= 256 ? 0 : img.Size)); // Width (0 = 256)
                    bw.Write((byte)(img.Size >= 256 ? 0 : img.Size)); // Height (0 = 256)
                    bw.Write((byte)0); // Color count
                    bw.Write((byte)0); // Reserved
                    bw.Write((short)1); // Color planes
                    bw.Write((short)32); // Bits per pixel
                    bw.Write(img.Data.Length); // Image size in bytes
                    bw.Write(offset); // Offset to image data
                    offset += img.Data.Length;
                }

                // Write PNG image payloads
                foreach (var img in pngImages)
                {
                    bw.Write(img.Data);
                }

                Console.WriteLine($"Successfully generated {fullIcoPath} ({new FileInfo(fullIcoPath).Length} bytes)");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error generating icon: {ex.Message}\n{ex.StackTrace}");
                return 1;
            }
        }

        private static void RenderIcon(DrawingContext dc, double size)
        {
            // 1. Background squircle badge with sleek gradient
            var bgBrush = new LinearGradientBrush(
                Color.FromRgb(30, 58, 138),  // #1E3A8A Deep Royal Blue
                Color.FromRgb(15, 23, 42),   // #0F172A Slate 900
                new Point(0, 0),
                new Point(1, 1));

            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(100, 96, 165, 250)), size * 0.02);
            double cornerRadius = size * 0.22;
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(size * 0.02, size * 0.02, size * 0.96, size * 0.96), cornerRadius, cornerRadius);

            // 2. Briefcase Handle
            var handlePen = new Pen(new SolidColorBrush(Color.FromRgb(203, 213, 225)), size * 0.05)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            var handleGeometry = new PathGeometry();
            var handleFigure = new PathFigure { StartPoint = new Point(size * 0.38, size * 0.33) };
            handleFigure.Segments.Add(new BezierSegment(
                new Point(size * 0.38, size * 0.21),
                new Point(size * 0.62, size * 0.21),
                new Point(size * 0.62, size * 0.33),
                true));
            handleGeometry.Figures.Add(handleFigure);
            dc.DrawGeometry(null, handlePen, handleGeometry);

            // 3. Briefcase Main Body
            var caseBrush = new LinearGradientBrush(
                Color.FromRgb(241, 245, 249), // #F1F5F9 Slate 100
                Color.FromRgb(203, 213, 225), // #CBD5E1 Slate 300
                new Point(0, 0),
                new Point(0, 1));
            var casePen = new Pen(new SolidColorBrush(Color.FromRgb(148, 163, 184)), size * 0.02);
            double caseRadius = size * 0.06;
            dc.DrawRoundedRectangle(caseBrush, casePen, new Rect(size * 0.18, size * 0.32, size * 0.64, size * 0.46), caseRadius, caseRadius);

            // 4. Briefcase Top Flap (Angle downward to center)
            var flapGeometry = new PathGeometry();
            var flapFigure = new PathFigure
            {
                StartPoint = new Point(size * 0.18, size * 0.35),
                IsClosed = true,
                IsFilled = true
            };
            flapFigure.Segments.Add(new LineSegment(new Point(size * 0.82, size * 0.35), true));
            flapFigure.Segments.Add(new LineSegment(new Point(size * 0.82, size * 0.44), true));
            flapFigure.Segments.Add(new LineSegment(new Point(size * 0.50, size * 0.53), true));
            flapFigure.Segments.Add(new LineSegment(new Point(size * 0.18, size * 0.44), true));
            flapGeometry.Figures.Add(flapFigure);

            var flapBrush = new LinearGradientBrush(
                Color.FromRgb(226, 232, 240),
                Color.FromRgb(148, 163, 184),
                new Point(0, 0),
                new Point(0, 1));
            dc.DrawGeometry(flapBrush, casePen, flapGeometry);

            // 5. Golden Center Clasp
            var claspBrush = new LinearGradientBrush(
                Color.FromRgb(251, 191, 36),  // Amber 400
                Color.FromRgb(217, 119, 6),   // Amber 600
                new Point(0, 0),
                new Point(0, 1));
            dc.DrawRoundedRectangle(claspBrush, null, new Rect(size * 0.46, size * 0.49, size * 0.08, size * 0.08), size * 0.02, size * 0.02);

            // 6. Target / Success Check Badge (Bottom-Right corner)
            double badgeCenterX = size * 0.72;
            double badgeCenterY = size * 0.72;
            double badgeRadius = size * 0.20;

            // White halo / border around badge
            var badgeHaloPen = new Pen(new SolidColorBrush(Color.FromRgb(15, 23, 42)), size * 0.04);
            var badgeBrush = new LinearGradientBrush(
                Color.FromRgb(16, 185, 129),  // Emerald 500
                Color.FromRgb(5, 150, 105),   // Emerald 600
                new Point(0, 0),
                new Point(1, 1));
            dc.DrawEllipse(badgeBrush, badgeHaloPen, new Point(badgeCenterX, badgeCenterY), badgeRadius, badgeRadius);

            // White Checkmark
            var checkPen = new Pen(new SolidColorBrush(Colors.White), size * 0.055)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };

            var checkGeometry = new PathGeometry();
            var checkFigure = new PathFigure { StartPoint = new Point(badgeCenterX - size * 0.09, badgeCenterY - size * 0.01) };
            checkFigure.Segments.Add(new LineSegment(new Point(badgeCenterX - size * 0.02, badgeCenterY + size * 0.06), true));
            checkFigure.Segments.Add(new LineSegment(new Point(badgeCenterX + size * 0.09, badgeCenterY - size * 0.07), true));
            checkGeometry.Figures.Add(checkFigure);
            dc.DrawGeometry(null, checkPen, checkGeometry);
        }
    }
}
