using Accord.Imaging.Filters;
using NINA.Core.Utility;
using NINA.Image.ImageAnalysis;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace NINA.Plugin.ExoPlanets.Sequencer.Utility {

    public class StarAnnotator {
        private static readonly Pen TARGET_PEN = new Pen(Brushes.DarkRed, 2);
        private static readonly Pen COMP_PEN = new Pen(Brushes.LightYellow, 1);
        private static readonly Pen VAR_PEN = new Pen(Brushes.LightBlue, 1);
        private static readonly Pen AVG_PEN = new Pen(Brushes.LightGreen, 1);
        private static readonly SolidBrush TARGET_TEXTBRUSH = new SolidBrush(Color.Red);
        private static readonly SolidBrush COMP_TEXTBRUSH = new SolidBrush(Color.Yellow);
        private static readonly SolidBrush VAR_TEXTBRUSH = new SolidBrush(Color.Blue);
        private static readonly SolidBrush AVG_TEXTBRUSH = new SolidBrush(Color.Green);
        private static readonly FontFamily FONTFAMILY = new FontFamily("Arial");
        private static readonly Font FONT = new Font(FONTFAMILY, 24, FontStyle.Regular, GraphicsUnit.Pixel);

        public static string Name => "NINA";

        public string ContentId => this.GetType().FullName;

        public static Task<BitmapSource> GetAnnotatedImage(DetectedStar targetStar, List<DetectedStar> starList, List<DetectedStar> VStarList, List<DetectedStar> avgStarList, List<DetectedStar> simbadStarList, BitmapSource imageToAnnotate, string annotationJpg, double exposuretime, CancellationToken token = default) {
            return Task.Run(() => {
                using (MyStopWatch.Measure()) {
                    if (imageToAnnotate.Format == System.Windows.Media.PixelFormats.Rgb48) {
                        using (var source = ImageUtility.BitmapFromSource(imageToAnnotate, System.Drawing.Imaging.PixelFormat.Format48bppRgb)) {
                            using (var img = new Grayscale(0.2125, 0.7154, 0.0721).Apply(source)) {
                                imageToAnnotate = ImageUtility.ConvertBitmap(img, System.Windows.Media.PixelFormats.Gray16);
                                imageToAnnotate.Freeze();
                            }
                        }
                    }

                    using (var bmp = ImageUtility.Convert16BppTo8Bpp(imageToAnnotate)) {
                        using (var newBitmap = new Bitmap(bmp.Width, bmp.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb)) {
                            Graphics graphics = Graphics.FromImage(newBitmap);
                            graphics.DrawImage(bmp, 0, 0);

                            int offset = 10;

                            int simbadStarListCount = simbadStarList.Count;
                            if (simbadStarListCount > 0) {
                                foreach (var star in simbadStarList) {
                                    token.ThrowIfCancellationRequested();
                                    graphics.DrawEllipse(COMP_PEN, new RectangleF(star.BoundingBox.X, star.BoundingBox.Y, star.BoundingBox.Width, star.BoundingBox.Height));
                                    if (simbadStarListCount < 20) {
                                        graphics.DrawString("C1 (" + star.Position.X.ToString("##") + "," + star.Position.Y.ToString("##") + ")", FONT, COMP_TEXTBRUSH, new PointF(Convert.ToSingle(star.Position.X - offset - (1.5 * offset)), Convert.ToSingle(star.Position.Y + (2.5 * offset))));
                                        graphics.DrawString("max pixel: " + star.MaxBrightness.ToString("##"), FONT, COMP_TEXTBRUSH, new PointF(Convert.ToSingle(star.Position.X - offset - (1.5 * offset)), Convert.ToSingle(star.Position.Y + (5 * offset))));
                                    } else {
                                        graphics.DrawString("C1", FONT, COMP_TEXTBRUSH, new PointF(Convert.ToSingle(star.Position.X - offset - (1.0 * offset)), Convert.ToSingle(star.Position.Y + (1.5 * offset))));
                                    }
                                }
                            }

                            int starListCount = starList.Count;
                            if (starListCount > 0) {
                                foreach (var star in starList) {
                                    token.ThrowIfCancellationRequested();
                                    graphics.DrawEllipse(COMP_PEN, new RectangleF(star.BoundingBox.X, star.BoundingBox.Y, star.BoundingBox.Width, star.BoundingBox.Height));
                                    if (starListCount < 20) {
                                        graphics.DrawString("C2 (" + star.Position.X.ToString("##") + "," + star.Position.Y.ToString("##") + ")", FONT, COMP_TEXTBRUSH, new PointF(Convert.ToSingle(star.Position.X - offset - (1.5 * offset)), Convert.ToSingle(star.Position.Y + (2.5 * offset))));
                                        graphics.DrawString("max pixel: " + star.MaxBrightness.ToString("##"), FONT, COMP_TEXTBRUSH, new PointF(Convert.ToSingle(star.Position.X - offset - (1.5 * offset)), Convert.ToSingle(star.Position.Y + (5 * offset))));
                                    } else {
                                        graphics.DrawString("C2", FONT, COMP_TEXTBRUSH, new PointF(Convert.ToSingle(star.Position.X - offset - (1.0 * offset)), Convert.ToSingle(star.Position.Y + (1.5 * offset))));
                                    }
                                }
                            }

                            int vStarListCount = VStarList.Count;
                            if (vStarListCount > 0) {
                                foreach (var star in VStarList) {
                                    token.ThrowIfCancellationRequested();
                                    graphics.DrawEllipse(VAR_PEN, new RectangleF(star.BoundingBox.X, star.BoundingBox.Y, star.BoundingBox.Width, star.BoundingBox.Height));
                                    if (vStarListCount < 20) {
                                        graphics.DrawString("V (" + star.Position.X.ToString("##") + "," + star.Position.Y.ToString("##") + ")", FONT, VAR_TEXTBRUSH, new PointF(Convert.ToSingle(star.Position.X - offset - (1.5 * offset)), Convert.ToSingle(star.Position.Y + (2.5 * offset))));
                                        graphics.DrawString("max pixel: " + star.MaxBrightness.ToString("##"), FONT, VAR_TEXTBRUSH, new PointF(Convert.ToSingle(star.Position.X - offset - (1.5 * offset)), Convert.ToSingle(star.Position.Y + (5 * offset))));
                                    } else {
                                        graphics.DrawString("V", FONT, VAR_TEXTBRUSH, new PointF(Convert.ToSingle(star.Position.X - offset - (1.0 * offset)), Convert.ToSingle(star.Position.Y + (1.5 * offset))));
                                    }
                                }
                            }

                            int avgStarListCount = avgStarList.Count;
                            if (avgStarListCount > 0) {
                                foreach (var star in avgStarList) {
                                    token.ThrowIfCancellationRequested();
                                    graphics.DrawEllipse(AVG_PEN, new RectangleF(star.BoundingBox.X, star.BoundingBox.Y, star.BoundingBox.Width, star.BoundingBox.Height));
                                    if (avgStarListCount < 20) {
                                        graphics.DrawString("A (" + star.Position.X.ToString("##") + "," + star.Position.Y.ToString("##") + ")", FONT, AVG_TEXTBRUSH, new PointF(Convert.ToSingle(star.Position.X - offset - (1.5 * offset)), Convert.ToSingle(star.Position.Y + (2.5 * offset))));
                                        graphics.DrawString("avg pixel: " + star.AverageBrightness.ToString("##"), FONT, AVG_TEXTBRUSH, new PointF(Convert.ToSingle(star.Position.X - offset - (1.5 * offset)), Convert.ToSingle(star.Position.Y + (5 * offset))));
                                    } else {
                                        graphics.DrawString("A", FONT, AVG_TEXTBRUSH, new PointF(Convert.ToSingle(star.Position.X - offset - (1.0 * offset)), Convert.ToSingle(star.Position.Y + (1.5 * offset))));
                                    }
                                }
                            }

                            if (targetStar != null) {
                                graphics.DrawEllipse(TARGET_PEN, new RectangleF(targetStar.BoundingBox.X, targetStar.BoundingBox.Y, targetStar.BoundingBox.Width, targetStar.BoundingBox.Height));
                                graphics.DrawString("T (" + targetStar.Position.X.ToString("##") + "," + targetStar.Position.Y.ToString("##") + ")", FONT, TARGET_TEXTBRUSH, new PointF(Convert.ToSingle(targetStar.Position.X - offset - (1.5 * offset)), Convert.ToSingle(targetStar.Position.Y + (2.5 * offset))));
                                graphics.DrawString("max pixel: " + targetStar.MaxBrightness.ToString("##"), FONT, TARGET_TEXTBRUSH, new PointF(Convert.ToSingle(targetStar.Position.X - offset - (1.5 * offset)), Convert.ToSingle(targetStar.Position.Y + (5 * offset))));
                                graphics.DrawString("Exposure: " + exposuretime.ToString("##") + "s", FONT, TARGET_TEXTBRUSH, new PointF(Convert.ToSingle(targetStar.Position.X - offset - (1.5 * offset)), Convert.ToSingle(targetStar.Position.Y + (7.5 * offset))));
                            }

                            if (annotationJpg != null)
                                newBitmap.Save(annotationJpg, System.Drawing.Imaging.ImageFormat.Jpeg);

                            var img = ImageUtility.ConvertBitmap(newBitmap, System.Windows.Media.PixelFormats.Bgr24);

                            img.Freeze();
                            return img;
                        }
                    }
                }
            });
        }
    }
}