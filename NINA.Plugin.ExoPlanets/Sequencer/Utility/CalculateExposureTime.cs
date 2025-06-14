#region "copyright"

/*
    Copyright © 2016 - 2021 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Accord.Statistics.Models.Regression.Linear;
using CsvHelper;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NINA.Astrometry;
using NINA.Core.Enum;
using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Equipment.MyCamera;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Image.ImageAnalysis;
using NINA.PlateSolving;
using NINA.Plugin.ExoPlanets.Model;
using NINA.Plugin.ExoPlanets.Utility;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static NINA.Astrometry.Coordinates;
using static NINA.Equipment.Model.CaptureSequence;

namespace NINA.Plugin.ExoPlanets.Sequencer.Utility {

    [ExportMetadata("Name", "Calculate exposure time")]
    [ExportMetadata("Description", "Lbl_SequenceItem_Imaging_TakeExposure_Description")]
    [ExportMetadata("Icon", "CameraSVG")]
    [ExportMetadata("Category", "ExoPlanet")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class CalculateExposureTime : SequenceItem, IValidatable {
        private readonly ICameraMediator cameraMediator;
        private readonly IImagingMediator imagingMediator;
        private readonly IImageSaveMediator imageSaveMediator;
        private readonly IImageHistoryVM imageHistoryVM;
        private readonly IProfileService profileService;
        private readonly ITelescopeMediator telescopeMediator;
        private readonly ExoPlanets exoPlanets;

        [ImportingConstructor]
        public CalculateExposureTime(IProfileService profileService, ICameraMediator cameraMediator, IImagingMediator imagingMediator, IImageSaveMediator imageSaveMediator, IImageHistoryVM imageHistoryVM, ITelescopeMediator telescopeMediator) {
            Gain = -1;
            Offset = -1;
            TargetADU = 0.66d;
            this.cameraMediator = cameraMediator;
            this.imagingMediator = imagingMediator;
            this.imageSaveMediator = imageSaveMediator;
            this.imageHistoryVM = imageHistoryVM;
            this.profileService = profileService;
            this.telescopeMediator = telescopeMediator;
            exoPlanets = new ExoPlanets();
        }

        private CalculateExposureTime(CalculateExposureTime cloneMe) : this(cloneMe.profileService, cloneMe.cameraMediator, cloneMe.imagingMediator, cloneMe.imageSaveMediator, cloneMe.imageHistoryVM, cloneMe.telescopeMediator) {
            CopyMetaData(cloneMe);
        }

        public override object Clone() {
            var clone = new CalculateExposureTime(this) {
                ExposureTime = ExposureTime,
                ExposureCount = 0,
                Binning = Binning,
                Gain = Gain,
                Offset = Offset,
                ExposureTimeFirst = ExposureTimeFirst,
                ExposureTimeSecond = ExposureTimeSecond,
                ExposureTimeMax = ExposureTimeMax,
                SaveImages = SaveImages,
                UpdateExposureTime = UpdateExposureTime
            };

            if (clone.Binning == null) {
                clone.Binning = new BinningMode(1, 1);
            }

            return clone;
        }

        private IList<string> issues = new List<string>();

        public IList<string> Issues { get => issues; set { issues = value; RaisePropertyChanged(); } }

        private double exposureTime = 30;

        public List<int> ExposureTimesList { get; set; }

        [JsonProperty]
        public double ExposureTime { get => exposureTime; set { exposureTime = value; RaisePropertyChanged(); } }

        private double exposureTimeFirst = 30;

        [JsonProperty]
        public double ExposureTimeFirst {
            get => exposureTimeFirst;
            set {
                if (value <= ExposureTimeSecond)
                    exposureTimeFirst = value;
                RaisePropertyChanged();
            }
        }

        private double exposureTimeSecond = 60;

        [JsonProperty]
        public double ExposureTimeSecond {
            get => exposureTimeSecond;
            set {
                if (value > ExposureTimeFirst && value < ExposureTimeMax)
                    exposureTimeSecond = value;
                RaisePropertyChanged();
            }
        }

        private double exposureTimeMax = 300;

        [JsonProperty]
        public double ExposureTimeMax {
            get => exposureTimeMax;
            set {
                if (value > ExposureTimeSecond)
                    exposureTimeMax = value;
                RaisePropertyChanged();
            }
        }

        private int gain;

        [JsonProperty]
        public int Gain {
            get => gain;
            set {
                gain = value;
                RaisePropertyChanged();
            }
        }

        private int offset;

        [JsonProperty]
        public int Offset {
            get => offset;
            set {
                offset = value;
                RaisePropertyChanged();
            }
        }

        private BinningMode binning;

        [JsonProperty]
        public BinningMode Binning {
            get => binning;
            set {
                binning = value;
                RaisePropertyChanged();
            }
        }

        private int exposureCount = 1;

        [JsonProperty]
        public int ExposureCount {
            get => exposureCount;
            set {
                exposureCount = value;
                RaisePropertyChanged();
            }
        }

        private CameraInfo cameraInfo;

        public CameraInfo CameraInfo {
            get => cameraInfo;
            private set {
                cameraInfo = value;
                RaisePropertyChanged();
            }
        }

        public double CameraMaxAdu { get; private set; }

        private ComparisonStarChart _compStarChart;

        public ComparisonStarChart CompStarChart {
            get => _compStarChart;
            set {
                _compStarChart = value;
                RaisePropertyChanged();
            }
        }

        private List<DetectedStar> _starList = [];

        public List<DetectedStar> StarList {
            get => _starList;
            set {
                _starList = value;
                RaisePropertyChanged();
            }
        }

        private List<VSXObject> _variableStarList = [];

        public List<VSXObject> VariableStarList {
            get => _variableStarList;
            set {
                _variableStarList = value;
                RaisePropertyChanged();
            }
        }

        private List<SimbadCompStar> _simbadCompStarList = [];

        public List<SimbadCompStar> SimbadCompStarList {
            get => _simbadCompStarList;
            set {
                _simbadCompStarList = value;
                RaisePropertyChanged();
            }
        }

        private DetectedStar _targetStar;

        public DetectedStar TargetStar {
            get => _targetStar;
            set {
                _targetStar = value;
                RaisePropertyChanged();
            }
        }

        public string _targetStarPosition;

        public string TargetStarPosition {
            get => _targetStarPosition;
            set {
                _targetStarPosition = value;
                RaisePropertyChanged();
            }
        }

        public double _targetADU;

        [JsonProperty]
        public double TargetADU {
            get => _targetADU;
            set {
                _targetADU = value;
                RaisePropertyChanged();
            }
        }

        private bool _saveImages;

        [JsonProperty]
        public bool SaveImages {
            get => _saveImages;
            set {
                _saveImages = value;
                RaisePropertyChanged();
            }
        }

        private bool _updateExposureTime;

        [JsonProperty]
        public bool UpdateExposureTime {
            get => _updateExposureTime;
            set {
                _updateExposureTime = value;
                RaisePropertyChanged();
            }
        }

        private int _compStarCount;

        public int CompStarCount {
            get => _compStarCount;
            set {
                _compStarCount = value;
                RaisePropertyChanged();
            }
        }

        private readonly List<double> inputs = [];
        private readonly List<double> outputs = [];

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            if (Validate()) {
                var keepTrying = true;
                var saveAnnotationJpg = string.Empty;
                ExposureCount = 1;
                inputs.Clear();
                outputs.Clear();
                TargetStar = null;
                // DetectedStar targetStarTemp = null;
                while (keepTrying && ExposureCount < 10) {
                    if (ExposureCount == 1) {
                        if (exoPlanets.UseExposureTimes) {
                            ExposureTimesList = exoPlanets.ExposureTimes.Split(',').Select(int.Parse).ToList();
                            ExposureTime = ExposureTimesList.OrderBy(item => Math.Abs(ExposureTimeFirst - item)).First();
                        } else {
                            ExposureTime = ExposureTimeFirst;
                        }
                    }
                    if (ExposureCount == 2) {
                        if (exoPlanets.UseExposureTimes) {
                            ExposureTime = ExposureTimesList.Where(x => x != ExposureTime).OrderBy(item => Math.Abs(ExposureTimeSecond - item)).First();
                        } else {
                            ExposureTime = ExposureTimeSecond;
                        }
                    }

                    var exoPlanetDSO = ItemUtility.RetrieveExoPlanetDSO(this.Parent);
                    var inputTarget = ItemUtility.RetrieveInputTarget(this.Parent);

                    var capture = new CaptureSequence() { Binning = Binning, Gain = Gain, ExposureTime = ExposureTime, Offset = Offset, ImageType = ImageTypes.LIGHT };
                    var exposureData = await imagingMediator.CaptureImage(capture, token, progress);

                    var imageData = await exposureData.ToImageData(progress, token);

                    var prepareTask = imagingMediator.PrepareImage(imageData, new PrepareImageParameters(true, false), token);

                    if (SaveImages) {
                        imageData.MetaData.Target.Name = inputTarget.TargetName;
                        imageData.MetaData.Target.Coordinates = inputTarget.InputCoordinates.Coordinates;
                        imageData.MetaData.Target.PositionAngle = inputTarget.PositionAngle;

                        await imageSaveMediator.Enqueue(imageData, prepareTask, progress, token);
                    }
                    var image = prepareTask.Result;

                    // Detect all the stars
                    var sensitivity = StarSensitivityEnum.Highest;
                    if (exoPlanetDSO.Magnitude < 12 || ExposureCount > 1)
                        sensitivity = StarSensitivityEnum.High;
                    if (exoPlanetDSO.Magnitude < 9 || ExposureCount > 2)
                        sensitivity = StarSensitivityEnum.Normal;

                    var starDetection = new StarDetection();
                    var starDetectionParams = new StarDetectionParams() {
                        Sensitivity = sensitivity,
                        NoiseReduction = NoiseReductionEnum.None
                    };
                    var starDetectionResult = await starDetection.Detect(image, image.Image.Format, starDetectionParams, progress, token);

                    // Platesolve the image
                    var solver = PlateSolverFactory.GetPlateSolver(profileService.ActiveProfile.PlateSolveSettings);

                    var imageSolver = new ImageSolver(solver, null);

                    var parameter = new PlateSolveParameter() {
                        Binning = Binning?.X ?? 1,
                        Coordinates = inputTarget.InputCoordinates.Coordinates,
                        DownSampleFactor = profileService.ActiveProfile.PlateSolveSettings.DownSampleFactor,
                        FocalLength = profileService.ActiveProfile.TelescopeSettings.FocalLength,
                        MaxObjects = profileService.ActiveProfile.PlateSolveSettings.MaxObjects,
                        PixelSize = profileService.ActiveProfile.CameraSettings.PixelSize,
                        Regions = profileService.ActiveProfile.PlateSolveSettings.Regions,
                        SearchRadius = profileService.ActiveProfile.PlateSolveSettings.SearchRadius,
                        DisableNotifications = false
                    };

                    var plateSolveResult = await imageSolver.Solve(image.RawImageData, parameter, progress, token);
                    if (!plateSolveResult.Success) {
                        Issues.Add("Platesolve failed.");
                        throw new SequenceEntityFailedException(string.Join(", ", Issues));
                    }

                    var arcsecPerPix = AstroUtil.ArcsecPerPixel(profileService.ActiveProfile.CameraSettings.PixelSize * Binning?.X ?? 1, profileService.ActiveProfile.TelescopeSettings.FocalLength);
                    var width = image.Image.PixelWidth;
                    var height = image.Image.PixelHeight;
                    var center = new Point(width / 2, height / 2);

                    //Translate your coordinates to x/y in relation to center coordinates
                    Point targetPoint = inputTarget.InputCoordinates.Coordinates.XYProjection(plateSolveResult.Coordinates, center, arcsecPerPix, arcsecPerPix, -plateSolveResult.PositionAngle, ProjectionType.Stereographic);
                    TargetStar = starDetectionResult.StarList
                        .GroupBy(p => Math.Pow(targetPoint.X - p.Position.X, 2) + Math.Pow(targetPoint.Y - p.Position.Y, 2))
                        .OrderBy(p => p.Key)
                        .FirstOrDefault()?.FirstOrDefault();

                    if (TargetStar == null) {
                        Notification.ShowError("Target star not found.");
                        Issues.Add("Target star not found.");
                        throw new SequenceEntityFailedException(string.Join(", ", Issues));
                    }
                    TargetStar.Position = TargetStar.Position.Round();
                    TargetStarPosition = TargetStar.Position.X.ToString() + "," + TargetStar.Position.Y.ToString();
                    Logger.Info("TargetStar: " + JsonConvert.SerializeObject(TargetStar));

                    // Create list for csv export
                    var exoStars = new List<DetectedExoStar> {
                        new(TargetStar, inputTarget)
                    };

                    var VStarList = new List<DetectedStar>();
                    FindVariableStars(progress, token, inputTarget.TargetName, plateSolveResult.Coordinates);
                    foreach (var vStar in VariableStarList) {
                        Point vStarPoint = inputTarget.InputCoordinates.Coordinates.XYProjection(vStar.Coordinates(), center, arcsecPerPix, arcsecPerPix, plateSolveResult.PositionAngle, ProjectionType.Stereographic);
                        if (!CheckPointWithinImage(vStarPoint, image)) continue;
                        DetectedStar dStar = starDetectionResult.StarList
                            .GroupBy(p => Math.Pow(vStarPoint.X - p.Position.X, 2) + Math.Pow(vStarPoint.Y - p.Position.Y, 2))
                            .OrderBy(p => p.Key)
                            .FirstOrDefault()?.FirstOrDefault();
                        dStar.Position = dStar.Position.Round();
                        if (dStar.Position != TargetStar.Position) {
                            VStarList.Add(dStar);
                            exoStars.Add(new DetectedExoStar(dStar, vStar));
                        }
                        Logger.Debug("VariableStar: " + JsonConvert.SerializeObject(vStar));
                        Logger.Debug("VstarPoint: " + JsonConvert.SerializeObject(vStarPoint));
                    }

                    // Simbad same colour comparison stars
                    var SimbadStarList = new List<DetectedStar>();
                    FindSimbadComparisonStars(progress, token, inputTarget);

                    if (SimbadCompStarList?.Count > 0) {
                        foreach (var compStar in SimbadCompStarList) {
                            Point compPoint = inputTarget.InputCoordinates.Coordinates.XYProjection(compStar.Coordinates(), center, arcsecPerPix, arcsecPerPix, plateSolveResult.PositionAngle, ProjectionType.Stereographic);
                            if (!CheckPointWithinImage(compPoint, image)) continue;
                            DetectedStar cStar = starDetectionResult.StarList
                                .GroupBy(p => Math.Pow(compPoint.X - p.Position.X, 2) + Math.Pow(compPoint.Y - p.Position.Y, 2))
                                .OrderBy(p => p.Key)
                                .FirstOrDefault()?.FirstOrDefault();
                            cStar.Position = cStar.Position.Round();
                            if (cStar.Position != TargetStar.Position) {
                                SimbadStarList.Add(cStar);
                                exoStars.Add(new DetectedExoStar(cStar, compStar));
                            }
                            Logger.Debug("Simbad CompStar: " + JsonConvert.SerializeObject(compStar));
                            Logger.Debug("Simbad compPoint: " + JsonConvert.SerializeObject(compPoint));
                        }
                    }
                    // Check the Compstars are not variable
                    SimbadStarList = SimbadStarList.Where(s => !VStarList.Any(v => v.Position == s.Position)).ToList<DetectedStar>();
                    SimbadStarList.ForEach(i => Logger.Info("Simbad Comparison star: " + JsonConvert.SerializeObject(i)));

                    StarList = [];
                    FindComparisonStars(progress, token, inputTarget.TargetName, plateSolveResult.Coordinates);
                    if (CompStarChart?.photometry?.Count > 0) {
                        foreach (var compStar in CompStarChart.photometry) {
                            Point compPoint = inputTarget.InputCoordinates.Coordinates.XYProjection(compStar.Coordinates(), center, arcsecPerPix, arcsecPerPix, plateSolveResult.PositionAngle, ProjectionType.Stereographic);
                            if (!CheckPointWithinImage(compPoint, image)) continue;
                            DetectedStar cStar = starDetectionResult.StarList
                                .GroupBy(p => Math.Pow(compPoint.X - p.Position.X, 2) + Math.Pow(compPoint.Y - p.Position.Y, 2))
                                .OrderBy(p => p.Key)
                                .FirstOrDefault()?.FirstOrDefault();
                            cStar.Position = cStar.Position.Round();
                            if (cStar.Position != TargetStar.Position) {
                                StarList.Add(cStar);
                                exoStars.Add(new DetectedExoStar(cStar, compStar));
                            }
                            Logger.Debug("CompStar: " + JsonConvert.SerializeObject(compStar));
                            Logger.Debug("compPoint: " + JsonConvert.SerializeObject(compPoint));
                        }
                    }

                    // Check the Compstars are not variable
                    StarList = StarList.Where(s => !VStarList.Any(v => v.Position == s.Position)).ToList<DetectedStar>()
                        .Where(s => !SimbadStarList.Any(v => v.Position == s.Position)).ToList<DetectedStar>();
                    CompStarCount = SimbadStarList.Count + StarList.Count;
                    StarList.ForEach(i => Logger.Info("Comparison star: " + JsonConvert.SerializeObject(i)));

                    // Check for similar avarage stars
                    var avgStarList = starDetectionResult.StarList.Where(x => Math.Abs(x.AverageBrightness - TargetStar.AverageBrightness) < TargetStar.AverageBrightness * 0.05d).ToList<DetectedStar>()
                        .Where(s => !VStarList.Any(v => v.Position == s.Position)).ToList<DetectedStar>()
                        .Where(s => !StarList.Any(v => v.Position == s.Position)).ToList<DetectedStar>()
                        .Where(s => !SimbadStarList.Any(v => v.Position == s.Position)).ToList<DetectedStar>()
                        .Except(new List<DetectedStar>() { TargetStar }).ToList<DetectedStar>();

                    var targetMax = TargetStar.MaxBrightness;

                    // Annotate image
                    var starAnnotator = new StarAnnotator();
                    saveAnnotationJpg = profileService.ActiveProfile.ImageFileSettings.FilePath + "\\" + inputTarget.TargetName + "_fov.jpg";
                    var annotatedImage = await StarAnnotator.GetAnnotatedImage(TargetStar, StarList, VStarList, avgStarList, SimbadStarList, image.Image, saveAnnotationJpg, ExposureTime, token);
                    imagingMediator.SetImage(annotatedImage);

                    // Save comparison and variable star csv
                    if (exoPlanets.SaveStarList) {
                        string csvfile = Path.Combine(profileService.ActiveProfile.ImageFileSettings.FilePath, inputTarget.TargetName + "_starList" + ".csv");
                        using var writer = new StreamWriter(csvfile);
                        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                        csv.Context.RegisterClassMap<DetectedStarMap>();
                        csv.WriteRecords(exoStars);
                    }

                    if (ExposureCount <= 2 && TargetStar.MaxBrightness >= CameraMaxAdu * 0.9d) {
                        Notification.ShowWarning("Target star blown out. Adjusting exposure settings.");
                        ExposureTimeFirst /= 2;
                        ExposureTimeSecond /= 2;
                        Logger.Info("Target star blown out. Adjusting exposure settings to: 1. " + ExposureTimeFirst + "s and 2. " + ExposureTimeSecond + "s");
                        if (ExposureTimeSecond - ExposureTimeFirst < 1) {
                            Notification.ShowWarning("Target star blown out.");
                            Issues.Add("Target star blown out. Please adjust your exposure settings.");
                            throw new SequenceEntityFailedException(string.Join(", ", Issues));
                        }
                        ExposureCount = 1;
                        continue;
                    }

                    outputs.Add(ExposureTime);
                    foreach(var input in inputs) {
                        // Just make sure the inputs are different to eachother
                        if (input == TargetStar.MaxBrightness) { TargetStar.MaxBrightness++; }
                    }
                    inputs.Add(TargetStar.MaxBrightness);

                    if (TargetStar.MaxBrightness > (CameraMaxAdu * (TargetADU - 0.1d)) && TargetStar.MaxBrightness < (CameraMaxAdu * (TargetADU + 0.1d))) {
                        keepTrying = false;
                    } else if (ExposureCount >= 2) {
                        var previousExposureTime = ExposureTime;
                        var ols = new OrdinaryLeastSquares();
                        SimpleLinearRegression regression = ols.Learn(inputs.ToArray(), outputs.ToArray());

                        //Get the trend and set the next exposuretime input
                        double CalculatedExposureTime = Math.Min(Math.Round(regression.Transform(CameraMaxAdu * TargetADU)), ExposureTimeMax);
                        ExposureTime = exoPlanets.UseExposureTimes ? ExposureTimesList.OrderBy(item => Math.Abs(CalculatedExposureTime - item)).First() : CalculatedExposureTime;
                        Logger.Debug("Trying to reach targetADU: " + Math.Round(CameraMaxAdu * TargetADU));
                        Logger.Info("New exposuretime calculated to: " + ExposureTime);
                        if (previousExposureTime == ExposureTime) {
                            // too little difference, exposureTime should be fine.
                            Logger.Info("New exposuretime calculated to be same as the previous exposuretime. No reason to take a new image.");
                            keepTrying = false;
                        }
                        if (ExposureTime < 0) {
                            Issues.Add("Exposure time calculated to less than 0 seconds. Something failed.");
                            ExposureTime = 1;
                            throw new SequenceItemSkippedException(string.Join(", ", Issues));
                        }
                    }

                    ExposureCount++;
                }
                Notification.ShowInformation("Exposure time calculated to be " + ExposureTime + "s.");
                Notification.ShowInformation("Annotated image saved: " + saveAnnotationJpg);
                if (UpdateExposureTime) {
                    ItemUtility.UpdateTakeExposureItems(this.Parent, ExposureTime);
                }
            } else {
                throw new SequenceItemSkippedException(string.Join(", ", Issues));
            }
        }

        private static bool CheckPointWithinImage(Point point, Image.Interfaces.IRenderedImage image) {
            if (point.X < 0 || point.X > image.Image.PixelWidth)
                return false;
            if (point.Y < 0 || point.Y > image.Image.PixelHeight)
                return false;
            return true;
        }

        private string comparisonTarget = string.Empty;

        private async void FindComparisonStars(IProgress<ApplicationStatus> progress, CancellationToken token, string targetName, Coordinates coords) {
            // Check properties
            if (!exoPlanets.RetrieveComparisonStars)
                return;

            if (comparisonTarget.Equals(targetName) && CompStarChart != null && CompStarChart.photometry.Count > 0)
                return;

            progress.Report(new ApplicationStatus() { Status = "Retrieving comparison stars" });
            try {
                using var localCTS = CancellationTokenSource.CreateLinkedTokenSource(token);
                localCTS.CancelAfter(TimeSpan.FromSeconds(30));
                comparisonTarget = targetName;
                // https://app.aavso.org/vsp/api/chart/?ra=20%3A13%3A31.62&dec=65%3A09%3A43.5&fov=35&maglimit=18.5&format=json
                // var container = JsonConvert.DeserializeObject<ComparisonStarChart>(sequenceJSON);
                var raString = coords.RAString.Replace(":", "%3A");
                var decString = AstroUtil.DegreesToFitsDMS(coords.Dec).Replace(" ", "%3A").Replace("+", string.Empty);
                var url = $"https://app.aavso.org/vsp/api/chart/?ra={raString}&dec={decString}&fov=35&maglimit=18.5&format=json";

                try {
                    var response = await HttpRequest.HttpRequestAsync(url, HttpMethod.Get, token);

                    var serializer = new JsonSerializer();

                    using var sr = new StreamReader(await response.Content?.ReadAsStreamAsync(token), Encoding.UTF8);
                    using var jsonTextReader = new JsonTextReader(sr);
                    CompStarChart = serializer.Deserialize<ComparisonStarChart>(jsonTextReader);
                } catch {
                    Logger.Info("Couldn't process comparison stars.");
                    comparisonTarget = string.Empty;
                }
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                Logger.Error(ex);
                Notification.ShowError(ex.Message);
            } finally {
                progress.Report(new ApplicationStatus() { Status = string.Empty });
            }
        }

        private string simbadTarget = string.Empty;

        private async void FindSimbadComparisonStars(IProgress<ApplicationStatus> progress, CancellationToken token, ExoPlanetInputTarget target) {
            // Check properties
            if (!exoPlanets.RetrieveComparisonStars)
                return;

            if (simbadTarget.Equals(target.TargetName) && SimbadCompStarList != null && SimbadCompStarList.Count > 0)
                return;

            progress.Report(new ApplicationStatus() { Status = "Retrieving comparison stars from simbad" });

            try {
                using var localCTS = CancellationTokenSource.CreateLinkedTokenSource(token);
                localCTS.CancelAfter(TimeSpan.FromSeconds(30));
                simbadTarget = target.TargetName;
                var url = $"https://simbad.cds.unistra.fr/simbad/sim-tap/sync";

                var dictionary = new Dictionary<string, string> {
                    { "request", "doQuery" },
                    { "lang", "adql" },
                    { "format", "json" },
                    { "maxrec", "100" },
                    { "runid", new Guid().ToString() },
                    { "upload", "" },
                    { "phase", "run" },
                    { "query", "SELECT distinct basic.oid as oid from basic WHERE otype = '*..' and CONTAINS(POINT('ICRS', basic.ra, basic.dec), CIRCLE('ICRS', " + target.InputCoordinates.Coordinates.RADegrees + ", " + target.InputCoordinates.Coordinates.Dec + ", 0.01)) = 1" }
                };

                VoTable voTable = await PostForm(url, dictionary, localCTS.Token);
                if (voTable == null || voTable.Data == null || voTable.Data.Count == 0 || voTable.Data[0].Count == 0) return;
                double target_oid = Convert.ToDouble(voTable.Data[0][0]);
                dictionary.Remove("query");
                dictionary.Add("query", "SELECT distinct top 100 basic.main_id as main_id, allfluxes.B as B, allfluxes.V as V, allfluxes.R as R, basic.ra as ra, basic.dec as dec " +
                    "from allfluxes JOIN ident USING(oidref) JOIN basic ON ident.oidref = basic.oid " +
                    "join(SELECT distinct basic.oid as oid, B, V, R, (B - V) * 0.9 as bvlow, (B - V) * 1.1 as bvhigh, (V - R) * 0.9 as vrlow, (V - R) * 1.1 as vrhigh, V * 0.9 as vlow, V * 1.1 as vhigh, ra, dec from allfluxes JOIN ident USING(oidref) JOIN basic ON ident.oidref = basic.oid WHERE oid = " + target_oid + ") as target ON CONTAINS(POINT('ICRS', basic.ra, basic.dec), CIRCLE('ICRS', target.ra, target.dec, 1.0)) = 1 " +
                    "WHERE basic.ra IS NOT NULL and basic.dec IS NOT NULL and allfluxes.v is not null and basic.otype = '*..' " +
                    "and((allfluxes.b is not null and allfluxes.b - allfluxes.v >= target.bvlow and allfluxes.b - allfluxes.v <= target.bvhigh) or(allfluxes.r is not null and allfluxes.v - allfluxes.r >= target.vrlow and allfluxes.v - allfluxes.r <= target.vrhigh) or(allfluxes.v >= target.vlow and allfluxes.v <= target.vhigh)); ");
                voTable = await PostForm(url, dictionary, localCTS.Token);
                if (voTable == null || voTable.Data == null || voTable.Data.Count == 0 || voTable.Data[0].Count == 0) return;
                foreach (List<object> obj in voTable.Data) {
                    SimbadCompStarList.Add(new SimbadCompStar(obj));
                }
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                Logger.Error(ex);
                Notification.ShowError(ex.Message);
            } finally {
                progress.Report(new ApplicationStatus() { Status = string.Empty });
            }
        }

        private async Task<VoTable> PostForm(string url, Dictionary<string, string> dictionary, CancellationToken token) {

            using var httpClient = new HttpClient();

            // Define your form data
            var formData = new MultipartFormDataContent();

            foreach (string key in dictionary.Keys) {
                formData.Add(new StringContent(dictionary[key]), key);
            }

            try {
                HttpResponseMessage response = await httpClient.PostAsync(url, formData, token);

                var serializer = new JsonSerializer();

                using var sr = new StreamReader(await response.Content?.ReadAsStreamAsync(), Encoding.UTF8);
                using var jsonTextReader = new JsonTextReader(sr);
                return serializer.Deserialize<VoTable>(jsonTextReader);
            } catch {
                Logger.Info("Couldn't process simbad comparison stars.");
                simbadTarget = string.Empty;
                return null;
            }
        }

        private static void AppendUrlEncoded(StringBuilder sb, string name, string value) {
            if (sb.Length != 0)
                sb.Append('&');
            sb.Append(WebUtility.UrlEncode(name));
            sb.Append('=');
            sb.Append(WebUtility.UrlEncode(value));
        }

        private string variableTarget = string.Empty;

        private async void FindVariableStars(IProgress<ApplicationStatus> progress, CancellationToken token, string targetName, Coordinates coords) {
            // Check properties
            if (!exoPlanets.RetrieveVariableStars)
                return;

            // Only retrieve the list once for the given target
            if (variableTarget.Equals(targetName) && VariableStarList != null && VariableStarList.Count > 0)
                return;

            progress.Report(new ApplicationStatus() { Status = "Retrieving variable stars" });
            try {
                using var localCTS = CancellationTokenSource.CreateLinkedTokenSource(token);
                localCTS.CancelAfter(TimeSpan.FromSeconds(30));

                VariableStarList = [];
                variableTarget = targetName;
                // var url = $"https://www.aavso.org/vsx/index.php?view=query.votable&coords={coords.RADegrees}+{coords.Dec}&size=35.0&unit=2&order=9";
                var url = $"https://www.aavso.org/vsx/index.php?view=api.list&ra={coords.RADegrees}&dec={coords.Dec}&radius=3&tomag=18&format=json";

                try {
                    var response = await HttpRequest.HttpRequestAsync(url, HttpMethod.Get, token);

                    var serializer = new JsonSerializer();

                    using var sr = new StreamReader(await response.Content?.ReadAsStreamAsync(), Encoding.UTF8);
                    using var jsonTextReader = new JsonTextReader(sr);
                    var root = serializer.Deserialize<Root>(jsonTextReader);
                    VariableStarList = root.VSXObjects.VSXObject;
                } catch {
                    Logger.Info("Couldn't process comparison stars.");
                    comparisonTarget = string.Empty;
                }
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                Logger.Error(ex);
                Notification.ShowError(ex.Message);
            } finally {
                progress.Report(new ApplicationStatus() { Status = string.Empty });
            }
        }

        public override void AfterParentChanged() {
            Validate();
        }

        public bool Validate() {
            var i = new List<string>();
            CameraInfo = this.cameraMediator.GetInfo();
            if (!CameraInfo.Connected) {
                i.Add(Loc.Instance["LblCameraNotConnected"]);
            } else {
                if (CameraInfo.CanSetGain && Gain > -1 && (Gain < CameraInfo.GainMin || Gain > CameraInfo.GainMax)) {
                    i.Add(string.Format(Loc.Instance["Lbl_SequenceItem_Imaging_TakeExposure_Validation_Gain"], CameraInfo.GainMin, CameraInfo.GainMax, Gain));
                }
                if (CameraInfo.CanSetOffset && Offset > -1 && (Offset < CameraInfo.OffsetMin || Offset > CameraInfo.OffsetMax)) {
                    i.Add(string.Format(Loc.Instance["Lbl_SequenceItem_Imaging_TakeExposure_Validation_Offset"], CameraInfo.OffsetMin, CameraInfo.OffsetMax, Offset));
                }
                CameraMaxAdu = HistogramMath.CameraBitDepthToAdu(CameraInfo.BitDepth);
            }

            var fileSettings = profileService.ActiveProfile.ImageFileSettings;

            if (string.IsNullOrWhiteSpace(fileSettings.FilePath)) {
                i.Add(Loc.Instance["Lbl_SequenceItem_Imaging_TakeExposure_Validation_FilePathEmpty"]);
            } else if (!Directory.Exists(fileSettings.FilePath)) {
                i.Add(Loc.Instance["Lbl_SequenceItem_Imaging_TakeExposure_Validation_FilePathInvalid"]);
            }

            ExoPlanetDeepSkyObject exoPlanetDSO = ItemUtility.RetrieveExoPlanetDSO(this.Parent);
            if (exoPlanetDSO == null) {
                i.Add("This instruction must be used within the ExoPlanet or VariableStar object container.");
            }

            Issues = i;
            return i.Count == 0;
        }

        public override TimeSpan GetEstimatedDuration() {
            return TimeSpan.FromSeconds(this.ExposureTime);
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(CalculateExposureTime)}, ExposureTime {ExposureTime}, Gain {Gain}, Offset {Offset}, Binning {Binning?.Name}";
        }
    }
}