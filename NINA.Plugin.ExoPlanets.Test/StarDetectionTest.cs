using Moq;
using NINA.Core.Enum;
using NINA.Core.Interfaces;
using NINA.Core.Model;
using NINA.Image.ImageAnalysis;
using NINA.Image.ImageData;
using NINA.Image.Interfaces;
using NINA.Plugin.ExoPlanets.Sequencer.Utility;
using NINA.Profile.Interfaces;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.ExoPlanets.Test {

    [TestFixture]
    public class StarDetectionTest {

        private IImageDataFactory _imageDataFactory;
        private IProfileService _profileService;
        private IStarDetection _starDetectionStub;

        [SetUp]
        public void Setup() {
            var profileMock = new Mock<IProfileService>();

            var starDetectionMock = new Mock<IStarDetection>();
            starDetectionMock.Setup(s => s.CreateAnalysis()).Returns(() => new StarDetectionAnalysis());
            var starDetectionSelectorMock = new Mock<IPluggableBehaviorSelector<IStarDetection>>();
            starDetectionSelectorMock.Setup(x => x.GetBehavior()).Returns(starDetectionMock.Object);

            var starAnnotatorMock = new Mock<IStarAnnotator>();
            var starAnnotatorSelectorMock = new Mock<IPluggableBehaviorSelector<IStarAnnotator>>();
            starAnnotatorSelectorMock.Setup(x => x.GetBehavior()).Returns(starAnnotatorMock.Object);

            _profileService    = profileMock.Object;
            _starDetectionStub = starDetectionMock.Object;
            _imageDataFactory  = new ImageDataFactory(
                profileMock.Object,
                starDetectionSelectorMock.Object,
                starAnnotatorSelectorMock.Object);
        }

        [TestCase("resources/K2-121b_Red_140.00s_-10.00C_0000.fits", true)] // Average HFR: 4.274909894462603, HFR σ: 0.3735044238705187, Detected Stars 22, Sensitivity Normal, ResizeFactor: 0.37
        [TestCase("resources/TOI-1811b_Red_120.00s_17.20C_0000.fits", true)] // Average HFR: 3.990853709846907, HFR σ: 0.3646935687610461, Detected Stars 10, Sensitivity Normal, ResizeFactor: 0.37
        [TestCase("resources/TrES-3b_Red_100.00s_-10.50C_0000.fits", true)] // Average HFR: 4.680716766351612, HFR σ: 0.4190953220277426, Detected Stars 6, Sensitivity Normal, ResizeFactor: 0.37
        [TestCase("resources/IR-Qatar-1b-FL1064.fits", false)] // Average HFR: 3.259721254962794, HFR σ: 0.35363105920172555, Detected Stars 152, Sensitivity Normal, ResizeFactor: 0.38
        [TestCase("resources/IR-0000_-14.10_60.00s-Tres-2b.fits", false)] // Average HFR: 3.4711570301076575, HFR σ: 0.3445048975730119, Detected Stars 215, Sensitivity Normal, ResizeFactor: 0.38
        public async Task DetectStars_ExoDetectorCoversNinaStarsAndFindsMore(string relativeFilePath, bool isBayered) {
            var filePath = Path.Combine(TestContext.CurrentContext.TestDirectory, relativeFilePath);
            Assert.That(File.Exists(filePath), Is.True, $"FITS file not found: {filePath}");

            // ── Load image ────────────────────────────────────────────────────────────────────
            var imageData = await _imageDataFactory.CreateFromFile(
                filePath,
                bitDepth: 16,
                isBayered: isBayered,
                rawConverter: RawConverterEnum.FREEIMAGE);
            Assert.That(imageData, Is.Not.Null, "Failed to load FITS file");

            var bitmapSource = imageData.RenderBitmapSource();
            bitmapSource.Freeze();

            var starAnnotatorMock = new Mock<IStarAnnotator>();
            var renderedImage = RenderedImage.Create(
                bitmapSource, imageData, _profileService,
                _starDetectionStub, starAnnotatorMock.Object);

            var detectionParams = new StarDetectionParams {
                Sensitivity    = StarSensitivityEnum.Normal,
                NoiseReduction = NoiseReductionEnum.None,
                IsAutoFocus    = false
            };
            var pf = bitmapSource.Format;

            // ── Auto-stretch: NINA always stretches before star detection ─────────────────────
            // In real NINA, ProcessImage calls renderedImage.Stretch(factor, blackClipping)
            // before DetectStars, using profile defaults factor=0.2 and blackClipping=-2.8.
            // Without this the raw 16-bit image converts to a very dark 8-bit bitmap where
            // almost no stars are visible to the NINA Canny pipeline.
            var stretchedRenderedImage = await renderedImage.Stretch(
                factor: 0.2, blackClipping: -2.8, unlinked: false);

            // ── Run NINA built-in detector on the stretched image ─────────────────────────────
            var ninaDetector = new NINA.Image.ImageAnalysis.StarDetection();
            var ninaResult   = await ninaDetector.Detect(stretchedRenderedImage, pf, detectionParams, progress: null, token: CancellationToken.None);
            var ninaStars    = ninaResult.StarList ?? new List<DetectedStar>();

            // ── Run ExoPlanets detector on the original (unstretched) rendered image ──────────
            // The ExoPlanets detector builds its own calibrated stretch internally, so it
            // should run on the raw rendered image rather than the pre-stretched one.
            var exoDetector  = new Sequencer.Utility.StarDetection();
            var exoResult    = await exoDetector.Detect(renderedImage, pf, detectionParams, progress: null, token: CancellationToken.None);
            var exoStars     = exoResult.StarList ?? new List<DetectedStar>();

            // ── Match tolerance: 2× the average NINA HFR, minimum 10 pixels ─────────────────
            // Two independent detectors can place the same star's centre several pixels apart,
            // especially for large/soft stars (high HFR). Using 2×HFF gives enough slack while
            // still requiring spatial coincidence.
            double matchRadius = Math.Max(10.0, ninaResult.AverageHFR * 2.0);

            // For each NINA star find the nearest ExoPlanets star within matchRadius.
            int ninaMatchedCount = 0;
            var unmatchedNinaStars = new List<DetectedStar>();
            foreach (var ns in ninaStars) {
                bool matched = exoStars.Any(es =>
                    Math.Sqrt(Math.Pow(es.Position.X - ns.Position.X, 2) +
                              Math.Pow(es.Position.Y - ns.Position.Y, 2)) <= matchRadius);
                if (matched)
                    ninaMatchedCount++;
                else
                    unmatchedNinaStars.Add(ns);
            }

            // ── Report ────────────────────────────────────────────────────────────────────────
            Console.WriteLine($"File:              {Path.GetFileName(filePath)}");
            Console.WriteLine($"NINA stars:        {ninaStars.Count}  (avgHFR={ninaResult.AverageHFR:F2})");
            Console.WriteLine($"ExoPlanets stars:  {exoStars.Count}  (avgHFR={exoResult.AverageHFR:F2})");
            Console.WriteLine($"Match radius:      {matchRadius:F1} px");
            Console.WriteLine($"NINA stars matched by ExoPlanets: {ninaMatchedCount}/{ninaStars.Count}");
            Console.WriteLine($"Extra stars found by ExoPlanets:  {Math.Max(0, exoStars.Count - ninaMatchedCount)}");
            if (ninaStars.Count <= 20) {
                Console.WriteLine("NINA star positions:");
                foreach (var s in ninaStars)
                    Console.WriteLine($"  pos=({s.Position.X:F0},{s.Position.Y:F0}) HFR={s.HFR:F2}");
            }
            if (unmatchedNinaStars.Count > 0) {
                Console.WriteLine($"Unmatched NINA stars (first 5):");
                foreach (var ns in unmatchedNinaStars.Take(5)) {
                    Console.WriteLine($"  pos=({ns.Position.X:F0},{ns.Position.Y:F0}) HFR={ns.HFR:F2} brightness={ns.AverageBrightness:F0}");
                    // Show nearest ExoPlanets stars to help diagnose
                    var nearest = exoStars
                        .OrderBy(es => Math.Sqrt(Math.Pow(es.Position.X - ns.Position.X, 2) + Math.Pow(es.Position.Y - ns.Position.Y, 2)))
                        .Take(3);
                    foreach (var es in nearest)
                        Console.WriteLine($"    nearest exo: pos=({es.Position.X:F0},{es.Position.Y:F0}) dist={Math.Sqrt(Math.Pow(es.Position.X - ns.Position.X, 2) + Math.Pow(es.Position.Y - ns.Position.Y, 2)):F1}px HFR={es.HFR:F2}");
                }
            }
            Console.WriteLine();

            // ── Assertions ────────────────────────────────────────────────────────────────────
            // 1. ExoPlanets must detect at least 90% of the stars the NINA detector finds.
            //    When NINA detects very few stars (≤10), allow 1 miss to avoid brittle failures
            //    from a single borderline star that sits at the edge of detection thresholds.
            double minCoverage = ninaStars.Count <= 10 ? Math.Max(0.0, 1.0 - 1.0 / ninaStars.Count) : 0.9;
            double coverageRatio = ninaStars.Count > 0 ? (double)ninaMatchedCount / ninaStars.Count : 1.0;
            Assert.That(coverageRatio, Is.GreaterThanOrEqualTo(minCoverage),
                $"ExoPlanets detector missed too many NINA stars: only {ninaMatchedCount}/{ninaStars.Count} matched " +
                $"within {matchRadius:F1}px. Coverage={coverageRatio:P0} (min required={minCoverage:P0})");

            // 2. ExoPlanets must detect more stars in total than the NINA detector.
            Assert.That(exoStars.Count, Is.GreaterThan(ninaStars.Count),
                $"ExoPlanets detector ({exoStars.Count}) should find more stars than NINA ({ninaStars.Count})");
        }

        /// <summary>
        /// Regression test for the double-stretch bug on colour (Bayer) cameras.
        /// Simulates the exact production path: Debayer(saveLumChannel) → Stretch → Detect.
        /// Ground truth is RealStars.csv — an independent photometry catalogue for TrES-3b.
        /// The detector must find ≥80% of the catalogue stars.
        /// </summary>
        [Test]
        public async Task DetectStars_TrES3b_ProductionPath_CoversGroundTruthStars() {
            var fitsPath = Path.Combine(TestContext.CurrentContext.TestDirectory,
                                        "resources/TrES-3b_Red_100.00s_-10.50C_0000.fits");
            var csvPath  = Path.Combine(TestContext.CurrentContext.TestDirectory,
                                        "resources/RealStars.csv");

            Assert.That(File.Exists(fitsPath), Is.True, $"FITS not found: {fitsPath}");
            Assert.That(File.Exists(csvPath),  Is.True, $"Ground-truth CSV not found: {csvPath}");

            // Parse ground-truth pixel positions from RealStars.csv (columns: SeqNum, Xcen, Ycen, Total, SNR, …)
            // Only keep stars with catalogue SNR > 50 — fainter entries are below the detection
            // threshold for a 100-second exposure and should not count against coverage.
            var gtPositions = new List<(double X, double Y)>();
            foreach (var line in File.ReadLines(csvPath).Skip(1)) {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var cols = line.Split(',');
                if (cols.Length < 5) continue;
                if (!double.TryParse(cols[1].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double x)) continue;
                if (!double.TryParse(cols[2].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double y)) continue;
                if (!double.TryParse(cols[4].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double snr)) continue;
                if (snr > 50.0)
                    gtPositions.Add((x, y));
            }
            Assert.That(gtPositions, Is.Not.Empty, "No ground-truth positions parsed from CSV");

            // Load image with isBayered=true (as a colour camera would deliver it in NINA).
            var imageData = await _imageDataFactory.CreateFromFile(
                fitsPath, bitDepth: 16, isBayered: true,
                rawConverter: RawConverterEnum.FREEIMAGE);
            Assert.That(imageData, Is.Not.Null);

            var bitmapSource = imageData.RenderBitmapSource();
            bitmapSource.Freeze();

            var starAnnotatorMock = new Mock<IStarAnnotator>();
            var renderedImage = RenderedImage.Create(
                bitmapSource, imageData, _profileService,
                _starDetectionStub, starAnnotatorMock.Object);

            // ── Production path: Debayer → Stretch (same order as NINA PrepareImage) ───────────
            // NINA calls PrepareImage(autoStretch=true, detectStars=false), which means:
            //   saveLumChannel = DebayeredHFR && detectStars = false → Lum is NOT saved.
            // This is the scenario where the double-stretch bug originally fired.
            var debayeredImage = renderedImage.Debayer(saveColorChannels: false, saveLumChannel: false);
            // Stretch the debayered image (Lum=null in DebayeredData, just like production).
            var stretchedImage = await debayeredImage.Stretch(factor: 0.2, blackClipping: -2.8, unlinked: false);

            var detectionParams = new StarDetectionParams {
                Sensitivity    = StarSensitivityEnum.Normal,
                NoiseReduction = NoiseReductionEnum.None
            };

            // Detector receives the stretched IDebayeredImage with Rgb48 format — identical to
            // what CalculateExposureTime.cs passes in production.
            var exoDetector = new Sequencer.Utility.StarDetection();
            var exoResult   = await exoDetector.Detect(
                stretchedImage, stretchedImage.Image.Format,
                detectionParams, progress: null, token: CancellationToken.None);
            var exoStars = exoResult.StarList ?? new List<DetectedStar>();

            double matchRadius = 15.0;
            int matched = 0;
            var unmatched = new List<(double X, double Y)>();
            foreach (var (gx, gy) in gtPositions) {
                bool found = exoStars.Any(es =>
                    Math.Sqrt(Math.Pow(es.Position.X - gx, 2) +
                              Math.Pow(es.Position.Y - gy, 2)) <= matchRadius);
                if (found) matched++;
                else unmatched.Add((gx, gy));
            }

            double coverage = (double)matched / gtPositions.Count;
            Console.WriteLine($"TrES-3b production path: exo={exoStars.Count} gt={gtPositions.Count} matched={matched} coverage={coverage:P1}");
            if (unmatched.Count > 0) {
                Console.WriteLine($"First 5 unmatched ground-truth stars:");
                foreach (var (gx, gy) in unmatched.Take(5))
                    Console.WriteLine($"  pos=({gx:F0},{gy:F0})");
            }

            Assert.That(coverage, Is.GreaterThanOrEqualTo(0.80),
                $"ExoPlanets detector matched only {matched}/{gtPositions.Count} ground-truth stars ({coverage:P0}). " +
                $"Detector found {exoStars.Count} stars total. " +
                "Likely cause: double-stretch bug (Rgb48 display bitmap used instead of raw Lum channel).");
        }
    }
}
