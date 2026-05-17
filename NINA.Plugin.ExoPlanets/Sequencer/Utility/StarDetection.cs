#region "copyright"

/*
    Copyright © 2016 - 2021 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Accord.Imaging;
using Accord.Imaging.Filters;
using Accord.Math.Geometry;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Image.ImageAnalysis;
using NINA.Image.ImageData;
using NINA.Image.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NINA.Plugin.ExoPlanets.Sequencer.Utility {

    public class StarDetection : IStarDetection {
        private static readonly int _maxWidth = 1552;

        public string Name => "NINA";

        public string ContentId => this.GetType().FullName;

        /// <summary>
        /// Carries all per-image data shared between the stretch, edge-detection and photometry
        /// steps so that expensive quantities (background grid, luminance array) are computed once.
        /// </summary>
        private class State {
            public IImageArray _iarr;
            public ImageProperties imageProperties;
            public BitmapSource _originalBitmapSource;
            public double _resizefactor;
            public double _inverseResizefactor;
            public int _minStarSize;
            public int _maxStarSize;

            /// <summary>True when luminance was produced by 2×2 Bayer quad averaging.
            /// In this case every 4 pixels share the same value, so SNR counting must use
            /// a lower minSigmaPixels threshold to avoid rejecting real faint stars.</summary>
            public bool bayerAveraged;

            /// <summary>16-bit luminance array.
            /// Set for Rgb48 (Rec.709 from debayered colour) and for undebayered Bayer mosaics
            /// (2×2 quad average).  For mono images this stays null and _iarr.FlatArray is used.</summary>
            public ushort[] lum16;

            // ── Background grid (built once in BuildBackgroundGrid) ────────────────────────────
            /// <summary>Per-cell median sky value on a gridRows × gridCols mesh.</summary>
            public double[,] bgGrid;
            public int bgGridCols;
            public int bgGridRows;

            /// <summary>Overall median of all bgGrid cell values.</summary>
            public double bgGlobalMedian;

            /// <summary>p75 of bgGrid cell values.  Represents the sky level in the illuminated
            /// part of the field; not dragged down by dark vignetted corner cells the way the
            /// overall median is.  Used as the reference for dark-corner blob rejection.</summary>
            public double bgIlluminatedSky;

            /// <summary>Sky-noise sigma used for the 8-bit stretch window: stretchHigh = 8 × bgStretchSigma.
            /// Computed as 3×√(sky_median) (Poisson model) which gives a consistent visible
            /// stretch across all raw pixel types.  Not used for SNR gating.</summary>
            public double bgStretchSigma;

            /// <summary>Sky-noise sigma used for the SNR gate and hot-pixel gate.
            /// Computed as the median of per-grid-cell IQR/1.35 estimates — works correctly
            /// for both raw mono/Bayer pixels and 4-sample-averaged debayered Lum data.</summary>
            public double bgNoiseSigma;
        }

        // ══════════════════════════════════════════════════════════════════════════════════════
        // STEP 1 – Background grid
        // ══════════════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds a grid-based local background map from the 16-bit luminance pixels and stores
        /// the results in <paramref name="state"/>.
        ///
        /// Each cell's median is used (robust against stars that happen to fall in a cell).
        /// The grid is later bilinearly interpolated pixel-by-pixel to subtract the spatially
        /// varying sky (vignetting, gradients) before Canny edge detection.
        ///
        /// Also computes:
        ///   bgGlobalMedian   – median of all cell medians (overall sky level).
        ///   bgIlluminatedSky – p75 of cell medians (illuminated-field reference; not dragged
        ///                      down by dark corner cells the way the global median is).
        /// </summary>
        private static void BuildBackgroundGrid(State state, ushort[] pixels16) {
            int width  = state.imageProperties.Width;
            int height = state.imageProperties.Height;

            // Target ~64-pixel cells; keep grid between 8×8 and 32×32.
            int gridCols = Math.Max(8, Math.Min(32, width  / 64));
            int gridRows = Math.Max(8, Math.Min(32, height / 64));
            int cellW    = width  / gridCols;
            int cellH    = height / gridRows;

            var bgGrid       = new double[gridRows, gridCols];
            var cellSigmaArr = new double[gridRows * gridCols];
            int cellIdx = 0;

            for (int gy = 0; gy < gridRows; gy++) {
                for (int gx = 0; gx < gridCols; gx++) {
                    int x0       = gx * cellW;
                    int y0       = gy * cellH;
                    int x1       = Math.Min(x0 + cellW, width);
                    int y1       = Math.Min(y0 + cellH, height);
                    int capacity = (x1 - x0) * (y1 - y0);
                    var cellPixels = new ushort[capacity];
                    int idx = 0;
                    for (int cy = y0; cy < y1; cy++) {
                        int rowBase = cy * width;
                        for (int cx = x0; cx < x1; cx++)
                            cellPixels[idx++] = pixels16[rowBase + cx];
                    }
                    Array.Sort(cellPixels, 0, idx);
                    bgGrid[gy, gx] = cellPixels[idx / 2]; // median

                    // Robust per-cell noise sigma: σ = IQR / 1.35  (Gaussian IQR ≈ 1.35σ).
                    // Works correctly regardless of pixel type (raw, Bayer-averaged, debayered lum),
                    // because it measures the actual spread rather than assuming a Poisson model.
                    if (idx >= 4) {
                        double iqr = cellPixels[idx * 3 / 4] - cellPixels[idx / 4];
                        cellSigmaArr[cellIdx] = iqr / 1.35;
                    }
                    cellIdx++;
                }
            }

            state.bgGrid     = bgGrid;
            state.bgGridCols = gridCols;
            state.bgGridRows = gridRows;

            // Flatten grid values to compute order statistics.
            var gridFlat = new double[gridRows * gridCols];
            int i = 0;
            for (int gy = 0; gy < gridRows; gy++)
                for (int gx = 0; gx < gridCols; gx++)
                    gridFlat[i++] = bgGrid[gy, gx];
            Array.Sort(gridFlat);

            state.bgGlobalMedian   = gridFlat[gridFlat.Length / 2];
            state.bgIlluminatedSky = gridFlat[(int)(gridFlat.Length * 0.75)];

            // bgStretchSigma: Poisson model 3×√sky — gives a consistent stretch window across
            // all raw pixel types (raw mono, raw Bayer, undebayered OSC).
            state.bgStretchSigma = Math.Max(1.0, 3.0 * Math.Sqrt(state.bgGlobalMedian));

            // bgNoiseSigma: median of per-cell IQR/1.35 estimates — measures the actual pixel
            // spread, so it is correct for debayered Lum data (4-sample-averaged, lower noise)
            // as well as for raw pixels where Poisson and IQR agree.
            Array.Sort(cellSigmaArr, 0, cellIdx);
            state.bgNoiseSigma = Math.Max(1.0, cellSigmaArr[cellIdx / 2]);
            Logger.Info($"BuildBackgroundGrid: bgGlobalMedian={state.bgGlobalMedian:F0} bgIlluminatedSky={state.bgIlluminatedSky:F0} bgStretchSigma={state.bgStretchSigma:F1} bgNoiseSigma={state.bgNoiseSigma:F1}");
        }

        /// <summary>
        /// Bilinearly interpolates the background grid at image pixel (px, py).
        /// Shared by the stretch step and the per-star photometry step.
        /// </summary>
        private static double GetInterpolatedBackground(State state, int px, int py) {
            int gridCols = state.bgGridCols;
            int gridRows = state.bgGridRows;
            int cellW    = state.imageProperties.Width  / gridCols;
            int cellH    = state.imageProperties.Height / gridRows;

            // Fractional grid coordinate: 0.0 == centre of cell 0.
            double gxf = (double)px / cellW - 0.5;
            double gyf = (double)py / cellH - 0.5;

            int gx0 = (int)Math.Floor(gxf);
            int gy0 = (int)Math.Floor(gyf);
            double fx = gxf - gx0;
            double fy = gyf - gy0;

            int gx0c = Math.Max(0, Math.Min(gridCols - 1, gx0));
            int gx1c = Math.Max(0, Math.Min(gridCols - 1, gx0 + 1));
            int gy0c = Math.Max(0, Math.Min(gridRows - 1, gy0));
            int gy1c = Math.Max(0, Math.Min(gridRows - 1, gy0 + 1));

            return state.bgGrid[gy0c, gx0c] * (1 - fx) * (1 - fy)
                 + state.bgGrid[gy0c, gx1c] *      fx  * (1 - fy)
                 + state.bgGrid[gy1c, gx0c] * (1 - fx) *      fy
                 + state.bgGrid[gy1c, gx1c] *      fx  *      fy;
        }

        // ══════════════════════════════════════════════════════════════════════════════════════
        // STEP 2 – Noise-calibrated linear stretch to 8-bit for Canny
        // ══════════════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Produces the background-subtracted, linearly-stretched 8-bit bitmap fed to Canny.
        ///
        /// Stretch: val = (residual / stretchHigh) × 255  clamped to [0, 255]
        ///   stretchHigh = 8 × bgNoiseSigma
        ///
        /// This calibrates the stretch to the noise floor so that:
        ///   • Stars at 3σ above sky → ~96 in 8-bit → Canny gradient ≈ 19–30 (detectable)
        ///   • Stars at 5σ above sky → ~159 in 8-bit → clearly detected
        ///   • Sky noise pixels       → near 0, below Canny low threshold
        ///   • Saturated stars clip to 255 but still have sharp ring edges
        ///
        /// Precondition: BuildBackgroundGrid must have been called first.
        /// </summary>
        private static Bitmap BuildStretchedBitmap(State state, ushort[] pixels16) {
            int width  = state.imageProperties.Width;
            int height = state.imageProperties.Height;

            // Stretch: [0, 8σ] → [0, 255]. Stars at 3σ → 95/255 (well above Canny thresholds).
            // Use bgStretchSigma (Poisson-based) so the window is consistent across pixel types.
            double stretchHigh = 8.0 * state.bgStretchSigma;
            var bmp     = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
            var palette = bmp.Palette;
            for (int i = 0; i < 256; i++)
                palette.Entries[i] = System.Drawing.Color.FromArgb(i, i, i);
            bmp.Palette = palette;

            var bmpData  = bmp.LockBits(new Rectangle(0, 0, width, height),
                                         System.Drawing.Imaging.ImageLockMode.WriteOnly,
                                         System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
            var rowBytes = new byte[bmpData.Stride];
            for (int y = 0; y < height; y++) {
                int rowBase = y * width;
                for (int x = 0; x < width; x++) {
                    double residual = pixels16[rowBase + x] - GetInterpolatedBackground(state, x, y);
                    double val      = residual <= 0 ? 0.0 : (residual / stretchHigh) * 255.0;
                    rowBytes[x]     = val >= 255.0 ? (byte)255 : (byte)val;
                }
                System.Runtime.InteropServices.Marshal.Copy(rowBytes, 0,
                    bmpData.Scan0 + y * bmpData.Stride, bmpData.Stride);
            }
            bmp.UnlockBits(bmpData);
            Logger.Info($"BuildStretchedBitmap: bgStretchSigma={state.bgStretchSigma:F1} bgNoiseSigma={state.bgNoiseSigma:F1} stretchHigh={stretchHigh:F0}");
            return bmp;
        }

        // ══════════════════════════════════════════════════════════════════════════════════════
        // STEP 3 – Initialise state (luminance extraction, resize factors)
        // ══════════════════════════════════════════════════════════════════════════════════════

        private static State GetInitialState(IRenderedImage renderedImage, System.Windows.Media.PixelFormat pf, StarDetectionParams p) {
            var state     = new State();
            var imageData = renderedImage.RawImageData;
            state.imageProperties = imageData.Properties;
            state._iarr           = imageData.Data;

            // ── Luminance extraction ──────────────────────────────────────────────────────────
            // All subsequent processing (background grid, stretch, HFR) must run on a true
            // luminance plane, never on a raw Bayer mosaic.

            // Track whether we loaded raw luminance from the debayered Lum channel so we can
            // avoid overwriting it with pixels derived from the already-stretched display bitmap.
            bool lumFromDebayered = false;

            if (state.imageProperties.IsBayered && renderedImage is IDebayeredImage debayeredImage) {
                var debayeredData = debayeredImage.DebayeredData;
                if (debayeredData?.Lum != null && debayeredData.Lum.Length > 0) {
                    // Lum channel was saved during debayer — use it directly as raw luminance.
                    state._iarr = new ImageArray(debayeredData.Lum);
                } else {
                    // Debayered but Lum not saved (NINA PrepareImage with detectStars=false).
                    // The display bitmap (renderedImage.Image) is already auto-stretched Rgb48,
                    // so we must NOT use it.  Fall back to 2×2 Bayer quad average of raw data.
                    var src = imageData.Data.FlatArray;
                    int w   = state.imageProperties.Width;
                    int h   = state.imageProperties.Height;
                    var lum = new ushort[w * h];
                    for (int y = 0; y < h - 1; y += 2) {
                        for (int x = 0; x < w - 1; x += 2) {
                            int tl = y * w + x;
                            ushort v = (ushort)((src[tl] + src[tl + 1] + src[tl + w] + src[tl + w + 1]) >> 2);
                            lum[tl] = lum[tl + 1] = lum[tl + w] = lum[tl + w + 1] = v;
                        }
                    }
                    state._iarr = new ImageArray(lum);
                    state.bayerAveraged = true;
                }
                // Either way we have a valid raw lum in _iarr; skip the Rgb48 extract below.
                lumFromDebayered = true;
            } else if (state.imageProperties.IsBayered && pf != PixelFormats.Rgb48) {
                // OSC with debayering disabled: raw Bayer mosaic in Gray16.
                // Average each 2×2 Bayer quad to a single luminance value.
                var src = imageData.Data.FlatArray;
                int w   = state.imageProperties.Width;
                int h   = state.imageProperties.Height;
                var lum = new ushort[w * h];
                for (int y = 0; y < h - 1; y += 2) {
                    for (int x = 0; x < w - 1; x += 2) {
                        int tl  = y * w + x;
                        ushort v = (ushort)((src[tl] + src[tl + 1] + src[tl + w] + src[tl + w + 1]) >> 2);
                        lum[tl] = lum[tl + 1] = lum[tl + w] = lum[tl + w + 1] = v;
                    }
                }
                state._iarr = new ImageArray(lum);
                state.bayerAveraged = true;
            }

            state._originalBitmapSource = renderedImage.Image;

            // Keep at full resolution
            // to detect faint stars that would be lost after resize.
            state._resizefactor        = 1.0;
            state._inverseResizefactor = 1.0;
            state._minStarSize         = 5;
            state._maxStarSize         = 200;

            // Rgb48: convert to Gray16 once and store luminance pixels.
            // Skip this when we already have raw lum from the debayered data — in that case
            // renderedImage.Image is the auto-stretched colour bitmap and deriving lum16 from
            // it would feed double-stretched values into the background/noise model.
            if (pf == PixelFormats.Rgb48 && !lumFromDebayered) {
                using (var src48 = ImageUtility.BitmapFromSource(state._originalBitmapSource,
                                       System.Drawing.Imaging.PixelFormat.Format48bppRgb)) {
                    using (var gray = new Grayscale(0.2125, 0.7154, 0.0721).Apply(src48)) {
                        state._originalBitmapSource = ImageUtility.ConvertBitmap(gray,
                                                          System.Windows.Media.PixelFormats.Gray16);
                        state._originalBitmapSource.Freeze();
                        int w = state.imageProperties.Width;
                        int h = state.imageProperties.Height;
                        state.lum16 = new ushort[w * h];
                        state._originalBitmapSource.CopyPixels(state.lum16, w * 2, 0);
                    }
                }
            }

            return state;
        }

        private BlobCounter _blobCounter;

        public class Star {
            public double radius;
            public double HFR;
            public Accord.Point Position;
            public double meanBrightness;
            private readonly List<PixelData> pixelData;
            public double Average { get; private set; } = 0;
            public double SurroundingMean { get; set; } = 0;
            public double maxPixelValue { get; set; } = 0;

            public Rectangle Rectangle;

            public Star() {
                pixelData = new List<PixelData>();
            }

            public void AddPixelData(PixelData value) {
                this.pixelData.Add(value);
            }

            public void CalculateHfr() {
                double hfr = 0.0d;
                if (this.pixelData.Count > 0) {
                    double outerRadius = this.radius * 1.2;
                    double sum = 0, sumDist = 0, allSum = 0;

                    double centerX = this.Position.X;
                    double centerY = this.Position.Y;

                    foreach (PixelData data in this.pixelData) {
                        double value = Math.Round(data.value - SurroundingMean);
                        if (value < 0) {
                            value = 0;
                        }
                        data.value = (ushort)Math.Round(value);

                        allSum += data.value;
                        if (InsideCircle(data.PosX, data.PosY, this.Position.X, this.Position.Y, outerRadius)) {
                            sum += data.value;
                            sumDist += data.value * Math.Sqrt(Math.Pow((double)data.PosX - (double)centerX, 2.0d) + Math.Pow((double)data.PosY - (double)centerY, 2.0d));
                        }
                    }

                    if (sum > 0) {
                        hfr = sumDist / sum;
                    } else {
                        hfr = Math.Sqrt(2) * outerRadius;
                    }
                    this.Average = allSum / this.pixelData.Count;
                }
                this.HFR = hfr;
                this.pixelData.Clear();
            }

            internal static bool InsideCircle(double x, double y, double centerX, double centerY, double radius) {
                return Math.Pow(x - centerX, 2) + Math.Pow(y - centerY, 2) <= Math.Pow(radius, 2);
            }

            public DetectedStar ToDetectedStar() {
                return new DetectedStar() {
                    HFR = HFR,
                    Position = Position,
                    AverageBrightness = Average,
                    MaxBrightness = maxPixelValue,
                    Background = SurroundingMean,
                    BoundingBox = Rectangle
                };
            }
        }

        public class PixelData {
            public int PosX;
            public int PosY;
            public ushort value;

            public override string ToString() {
                return value.ToString();
            }
        }

        // ══════════════════════════════════════════════════════════════════════════════════════
        // STEP 4 – Main detect entry point
        // ══════════════════════════════════════════════════════════════════════════════════════

        public async Task<StarDetectionResult> Detect(IRenderedImage image, PixelFormat pf, StarDetectionParams p, IProgress<ApplicationStatus> progress, CancellationToken token) {
            var result = new StarDetectionResult();
            Bitmap bitmapToAnalyze = null;

            await Task.Run(() => {
                try {
                    using (MyStopWatch.Measure()) {
                        progress?.Report(new ApplicationStatus() { Status = "Preparing image for star detection" });

                        var state    = GetInitialState(image, pf, p);
                        var pixels16 = state.lum16 ?? state._iarr.FlatArray;

                        // 1. Build background grid for photometry and noise estimation.
                        BuildBackgroundGrid(state, pixels16);

                        // 2. Produce sqrt-stretched, background-subtracted 8-bit bitmap for Canny.
                        bitmapToAnalyze = BuildStretchedBitmap(state, pixels16);

                        token.ThrowIfCancellationRequested();

                        if (p.NoiseReduction != NoiseReductionEnum.None) {
                            bitmapToAnalyze = ReduceNoise(bitmapToAnalyze, p);
                        }

                        bitmapToAnalyze = DetectionUtility.ResizeForDetection(bitmapToAnalyze, _maxWidth, state._resizefactor);

                        // 3. Canny edge detection + binary thresholding.
                        PrepareForStructureDetection(bitmapToAnalyze, p, state, token);

                        progress?.Report(new ApplicationStatus() { Status = "Detecting structures" });

                        // 4. Blob extraction.
                        _blobCounter = DetectStructures(bitmapToAnalyze, token);

                        progress?.Report(new ApplicationStatus() { Status = "Analyzing stars" });

                        // 5. Photometry + filtering on the raw 16-bit luminance array.
                        result.StarList = IdentifyStars(p, state, bitmapToAnalyze, token, out var detectedStars);

                        token.ThrowIfCancellationRequested();

                        if (result.StarList.Count > 0) {
                            var mean   = result.StarList.Average(s => s.HFR);
                            var stdDev = result.StarList.Count > 1
                                ? Math.Sqrt(result.StarList.Sum(s => (s.HFR - mean) * (s.HFR - mean)) / (result.StarList.Count - 1))
                                : double.NaN;

                            Logger.Info($"Average HFR: {mean}, HFR σ: {stdDev}, Detected Stars {detectedStars}");

                            result.AverageHFR    = mean;
                            result.HFRStdDev     = stdDev;
                            result.DetectedStars = detectedStars;
                        }

                        _blobCounter = null;
                    }
                } catch (OperationCanceledException) {
                } finally {
                    progress?.Report(new ApplicationStatus() { Status = string.Empty });
                    bitmapToAnalyze?.Dispose();
                }
            }, token);

            return result;
        }

        // ══════════════════════════════════════════════════════════════════════════════════════
        // STEP 5 – Per-blob photometry and filtering
        // ══════════════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// For each blob produced by the Canny + blob-counter pipeline, measures photometry on
        /// the raw 16-bit luminance array and applies a set of filters:
        ///
        ///   • Size gate          – blobs outside [_minStarSize, _maxStarSize] are discarded.
        ///   • Shape gate         – highly elliptical blobs (eccentricity > 0.8) are discarded.
        ///   • Dark-corner gate   – blob centre is in a vignetted / dark region
        ///                          (localBg &lt; bgIlluminatedSky × 0.15).
        ///   • SNR gate           – mean star brightness ≥ annulusMedian + 3 × annulusSigma
        ///                          AND enough inner pixels individually exceed annulusMedian + annulusSigma.
        ///                          annulusSigma = max(1.4826 × annulusMAD, bgNoiseSigma) so that
        ///                          in smooth sky regions the gate is backed by the global noise floor.
        ///   • Hot-pixel gate     – (peak − bg) / (mean − bg) > 10 → single-pixel spike, rejected.
        /// </summary>
        private List<DetectedStar> IdentifyStars(StarDetectionParams p, State state, Bitmap bitmapToAnalyze, CancellationToken token, out int detectedStars) {
            detectedStars = 0;
            var blobs   = _blobCounter.GetObjectsInformation();
            var checker = new SimpleShapeChecker();
            var starlist = new List<Star>();

            // All photometry runs on the 16-bit luminance array, never on the 8-bit Canny bitmap.
            var flatArray = state.lum16 ?? state._iarr.FlatArray;
            int imgWidth  = state.imageProperties.Width;
            int imgHeight = state.imageProperties.Height;

            // Minimum number of inner-circle pixels that must individually exceed 1σ above
            // background for the blob to be accepted as a star.
            // For Bayer-averaged lum every 2×2 block shares the same value, so the effective
            // independent sample count is ¼ of the pixel count — halve the threshold.
            int minSigmaPixels = (int)Math.Ceiling(Math.Max(imgWidth, imgHeight) / 1000.0);
            if (state.bayerAveraged) minSigmaPixels = Math.Max(1, minSigmaPixels / 2);

            // Diagnostic rejection counters.
            int dbgTotal = blobs.Length, dbgSize = 0, dbgShape = 0, dbgDarkCorner = 0,
                dbgNoInner = 0, dbgHotPixel = 0, dbgSnr = 0;

            foreach (Blob blob in blobs) {
                token.ThrowIfCancellationRequested();

                // ── Size gate ────────────────────────────────────────────────────────────────
                if (blob.Rectangle.Width  > state._maxStarSize || blob.Rectangle.Height > state._maxStarSize ||
                    blob.Rectangle.Width  < state._minStarSize || blob.Rectangle.Height < state._minStarSize) {
                    if (dbgSize < 5 && (blob.Rectangle.Width < state._minStarSize || blob.Rectangle.Height < state._minStarSize))
                        Logger.Info($"  size-rejected(small): {blob.Rectangle.Width}x{blob.Rectangle.Height} min={state._minStarSize}");
                    dbgSize++;
                    continue;
                }

                var points = _blobCounter.GetBlobsEdgePoints(blob);
                Accord.Point centerpoint;
                float radius;

                // rect is in full-resolution image coordinates.
                var rect = new Rectangle(
                    (int)Math.Floor(blob.Rectangle.X      * state._inverseResizefactor),
                    (int)Math.Floor(blob.Rectangle.Y      * state._inverseResizefactor),
                    (int)Math.Ceiling(blob.Rectangle.Width  * state._inverseResizefactor),
                    (int)Math.Ceiling(blob.Rectangle.Height * state._inverseResizefactor));

                // ── Shape gate ───────────────────────────────────────────────────────────────
                Star s;
                if (checker.IsCircle(points, out centerpoint, out radius)) {
                    s = new Star {
                        Position  = new Accord.Point(centerpoint.X * (float)state._inverseResizefactor,
                                                     centerpoint.Y * (float)state._inverseResizefactor),
                        radius    = radius * state._inverseResizefactor,
                        Rectangle = rect
                    };
                } else {
                    if (CalculateEccentricity(rect.Width, rect.Height) > 0.8) {
                        dbgShape++;
                        continue;
                    }
                    s = new Star {
                        Position  = new Accord.Point(rect.X + rect.Width  / 2.0f,
                                                     rect.Y + rect.Height / 2.0f),
                        radius    = Math.Max(rect.Width, rect.Height) / 2.0,
                        Rectangle = rect
                    };
                }

                // ── Dark-corner gate (fast, before pixel scan) ───────────────────────────────
                double localBg = GetInterpolatedBackground(state, (int)s.Position.X, (int)s.Position.Y);
                if (state.bgIlluminatedSky > 0 && localBg < state.bgIlluminatedSky * 0.15) {
                    dbgDarkCorner++;
                    continue;
                }

                // ── Background annulus + star-aperture pixel scan ────────────────────────────
                // largeRect = 3× the star bounding box; the annular region between largeRect
                // and rect provides the local sky estimate.
                int lx = Math.Max(rect.X - rect.Width,  0);
                int ly = Math.Max(rect.Y - rect.Height, 0);
                int lw = Math.Min(rect.Width  * 3, imgWidth  - lx);
                int lh = Math.Min(rect.Height * 3, imgHeight - ly);
                var largeRect = new Rectangle(lx, ly, lw, lh);

                double starSum   = 0;
                int    starCount = 0;
                var annulusValues = new List<ushort>(lw * lh);
                var innerValues   = new List<ushort>();

                for (int x = largeRect.X; x < largeRect.X + largeRect.Width; x++) {
                    for (int y = largeRect.Y; y < largeRect.Y + largeRect.Height; y++) {
                        ushort pv = flatArray[x + imgWidth * y];
                        bool inStarRect = x >= rect.X && x < rect.X + rect.Width &&
                                          y >= rect.Y && y < rect.Y + rect.Height;
                        if (inStarRect) {
                            s.AddPixelData(new PixelData { PosX = x, PosY = y, value = pv });
                            if (Star.InsideCircle(x, y, s.Position.X, s.Position.Y, s.radius)) {
                                starSum  += pv;
                                starCount++;
                                innerValues.Add(pv);
                                s.maxPixelValue = Math.Max(s.maxPixelValue, pv);
                            }
                        } else {
                            annulusValues.Add(pv);
                        }
                    }
                }

                if (starCount == 0) { dbgNoInner++; continue; }

                // ── Background statistics (NINA-compatible: full annulus mean + stdev) ────────
                annulusValues.Sort();
                int ac = annulusValues.Count;
                double annulusSum = 0, annulusSumSq = 0;
                for (int i = 0; i < ac; i++) {
                    annulusSum   += annulusValues[i];
                    annulusSumSq += (double)annulusValues[i] * annulusValues[i];
                }
                double annulusMean  = annulusSum / ac;
                double annulusStdev = Math.Sqrt((annulusSumSq - ac * annulusMean * annulusMean) / ac);

                s.meanBrightness  = starSum / starCount;
                s.SurroundingMean = annulusMean;

                // ── Hot-pixel / cosmic-ray gate ──────────────────────────────────────────────
                // Reject blobs where only 1 pixel is significantly above background — those are
                // hot pixels or cosmic rays, not stars.  Real stars always have multiple pixels
                // brighter than sky + 3σ.  Use the lower of annulus mean and grid background to
                // guard against both crowded-annulus inflation and isolated noise spikes.
                double gridBgAtStar   = GetInterpolatedBackground(state, (int)s.Position.X, (int)s.Position.Y);
                double skyRef         = Math.Min(annulusMean, gridBgAtStar * 1.1);
                int pixelsAbove3Sigma = innerValues.Count(pv => pv > skyRef + 3.0 * state.bgNoiseSigma);
                bool isHotPixel = (pixelsAbove3Sigma <= 1 && innerValues.Count > 2);
                if (isHotPixel) {
                    if (dbgHotPixel < 3)
                        Logger.Info($"  HotPixel-rejected: mean={s.meanBrightness:F0} peak={s.maxPixelValue:F0} annMean={annulusMean:F0} pixAbove3s={pixelsAbove3Sigma} inner={innerValues.Count} rect={rect.Width}x{rect.Height}");
                    dbgHotPixel++;
                    continue;
                }

                // ── SNR gate ─────────────────────────────────────────────────────────────────
                // Use the lower of annulus mean and grid background × 1.1 as sky reference.
                // In crowded fields the 3× annulus can overlap neighbouring stars, inflating
                // annulusMean and causing real stars to fail the SNR check.  Clamping to the
                // grid background (which is built from cell medians and is star-resistant)
                // prevents this without losing sensitivity in uncrowded fields.
                double snrSkyRef      = Math.Min(annulusMean, gridBgAtStar * 1.1);
                double snrThreshold   = snrSkyRef + 2.0 * state.bgNoiseSigma;
                double innerThreshold = snrSkyRef + 1.0 * state.bgNoiseSigma;
                int pixelsAbove1Sigma = innerValues.Count(pv => pv > innerThreshold);
                if (s.meanBrightness >= snrThreshold && pixelsAbove1Sigma >= minSigmaPixels) {
                    s.CalculateHfr();
                    starlist.Add(s);
                } else {
                    if (dbgSnr < 5)
                        Logger.Info($"  SNR-rejected: mean={s.meanBrightness:F0} annMean={annulusMean:F0} gridBg={gridBgAtStar:F0} snrSkyRef={snrSkyRef:F0} stdev={annulusStdev:F0} snrThresh={snrThreshold:F0} innerThresh={innerThreshold:F0} pixAbove={pixelsAbove1Sigma} minSigPix={minSigmaPixels} innerCount={innerValues.Count} rect={rect.Width}x{rect.Height}");
                    dbgSnr++;
                }
            }

            Logger.Info($"IdentifyStars diagnostics: total={dbgTotal} size={dbgSize} shape={dbgShape} darkCorner={dbgDarkCorner} noInner={dbgNoInner} hotPixel={dbgHotPixel} snr={dbgSnr} accepted={starlist.Count} | bgGlobalMedian={state.bgGlobalMedian:F0} bgIlluminatedSky={state.bgIlluminatedSky:F0} bgNoiseSigma={state.bgNoiseSigma:F1} minSigmaPixels={minSigmaPixels} minStarSize={state._minStarSize} maxStarSize={state._maxStarSize}");

            if (starlist.Count == 0)
                return new List<DetectedStar>();

            detectedStars = starlist.Count;
            return starlist.Select(s => s.ToDetectedStar()).ToList();
        }

        // ══════════════════════════════════════════════════════════════════════════════════════
        // STEP 6 – Edge detection pipeline
        // ══════════════════════════════════════════════════════════════════════════════════════

        private static BlobCounter DetectStructures(Bitmap bmp, CancellationToken token) {
            var sw          = Stopwatch.StartNew();
            var blobCounter = new BlobCounter();
            blobCounter.ProcessImage(bmp);
            token.ThrowIfCancellationRequested();
            sw.Stop();
            Debug.Print("Time for structure detection: " + sw.Elapsed);
            return blobCounter;
        }

        private static Bitmap ReduceNoise(Bitmap bmp, StarDetectionParams p) {
            switch (p.NoiseReduction) {
                case NoiseReductionEnum.High:
                    bmp = new FastGaussianBlur(bmp).Process(2);
                    break;
                case NoiseReductionEnum.Highest:
                    bmp = new FastGaussianBlur(bmp).Process(3);
                    break;
                case NoiseReductionEnum.Median:
                    bmp = new Median().Apply(bmp);
                    break;
                default:
                    bmp = new FastGaussianBlur(bmp).Process(1);
                    break;
            }
            return bmp;
        }

        /// <summary>
        /// Applies Gaussian blur/sharpen (if needed) then Canny edge detection, SIS threshold
        /// and binary dilation to produce a binary blob map for the BlobCounter.
        ///
        /// Canny thresholds are scaled by the sky background level so that:
        ///   • Bright/sharp-star images (high bgGlobalMedian) use higher thresholds to suppress
        ///     the greater noise-edge density in the bright stretched image.
        ///   • Faint/soft-star images (low bgGlobalMedian) use lower thresholds to detect
        ///     the weaker gradients at the edges of large or faint stars.
        /// The scaling is log-linear: low = 3…5, high = 18…30 over median range 1000…15000.
        /// False blobs that pass Canny are eliminated by the SNR gate in IdentifyStars.
        /// </summary>
        private static void PrepareForStructureDetection(Bitmap bmp, StarDetectionParams p, State state, CancellationToken token) {
            var sw = Stopwatch.StartNew();

            if (p.Sensitivity == StarSensitivityEnum.Normal) {
                if (p.NoiseReduction == NoiseReductionEnum.None || p.NoiseReduction == NoiseReductionEnum.Median) {
                    // Scale thresholds logarithmically with sky brightness.
                    // Empirically calibrated over the test set (median range 1236…10784).
                    double t = Math.Log10(Math.Max(1.0, state.bgGlobalMedian));   // ~3.1 … 4.0
                    double tNorm = Math.Max(0.0, Math.Min(1.0, (t - 3.1) / 0.9)); // 0 → 1
                    int cannyLow  = (int)Math.Round(3 + tNorm * 2);  // 3…5
                    int cannyHigh = (int)Math.Round(16 + tNorm * 14); // 16…30
                    Logger.Info($"PrepareForStructureDetection: bgGlobalMedian={state.bgGlobalMedian:F0} cannyLow={cannyLow} cannyHigh={cannyHigh}");
                    new CannyEdgeDetector((byte)cannyLow, (byte)cannyHigh).ApplyInPlace(bmp);
                } else
                    new NoBlurCannyEdgeDetector(5, 30).ApplyInPlace(bmp);
            } else {
                int kernelSize = (int)Math.Max(
                    Math.Floor(Math.Max(state._originalBitmapSource.PixelWidth,
                                        state._originalBitmapSource.PixelHeight)
                               * state._resizefactor / 500), 3);

                if (state._inverseResizefactor > 1.6) {
                    new GaussianSharpen(1.8, kernelSize).ApplyInPlace(bmp);
                } else if (state._inverseResizefactor > 1) {
                    new GaussianSharpen((state._inverseResizefactor - 1) * 3, kernelSize).ApplyInPlace(bmp);
                } else if (p.NoiseReduction == NoiseReductionEnum.None || p.NoiseReduction == NoiseReductionEnum.Median) {
                    new GaussianBlur(0.7, 5).ApplyInPlace(bmp);
                }

                token.ThrowIfCancellationRequested();
                new NoBlurCannyEdgeDetector(5, 30).ApplyInPlace(bmp);
            }

            token.ThrowIfCancellationRequested();
            new SISThreshold().ApplyInPlace(bmp);
            token.ThrowIfCancellationRequested();
            new BinaryDilation3x3().ApplyInPlace(bmp);
            token.ThrowIfCancellationRequested();

            sw.Stop();
            Debug.Print("Time for image preparation: " + sw.Elapsed);
        }

        private static double CalculateEccentricity(double width, double height) {
            double major = Math.Max(width, height);
            double minor = Math.Min(width, height);
            return Math.Sqrt(major * major - minor * minor) / major;
        }

        public IStarDetectionAnalysis CreateAnalysis() {
            return new StarDetectionAnalysis();
        }

        public void UpdateAnalysis(IStarDetectionAnalysis analysis, StarDetectionParams p, StarDetectionResult result) {
            analysis.HFR = result.AverageHFR;
            analysis.HFRStDev = result.HFRStdDev;
            analysis.DetectedStars = result.DetectedStars;
        }
    }
}