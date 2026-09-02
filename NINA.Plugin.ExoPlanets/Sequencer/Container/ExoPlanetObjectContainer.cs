#region "copyright"

/*
    Copyright © 2016 - 2021 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using CommunityToolkit.Mvvm.Input;
using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NINA.Astrometry;
using NINA.Astrometry.Interfaces;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces;
using NINA.Plugin.ExoPlanets.Interfaces;
using NINA.Plugin.ExoPlanets.Model;
using NINA.Plugin.ExoPlanets.Utility;
using NINA.Profile.Interfaces;
using NINA.Sequencer;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.Container;
using NINA.Sequencer.Container.ExecutionStrategy;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace NINA.Plugin.ExoPlanets.Sequencer.Container {

    [ExportMetadata("Name", "ExoPlanet object container")]
    [ExportMetadata("Description", "A DSO container to choose a transiting exo planet target.")]
    [ExportMetadata("Icon", "TelescopeSVG")]
    [ExportMetadata("Category", "Lbl_SequenceCategory_Container")]
    [Export(typeof(ISequenceItem))]
    [Export(typeof(ISequenceContainer))]
    [JsonObject(MemberSerialization.OptIn)]
    public partial class ExoPlanetObjectContainer : SequenceContainer, IDeepSkyObjectContainer, IVariableBrightnessTargetContainer {
        private readonly IProfileService profileService;
        private readonly IFramingAssistantVM framingAssistantVM;
        private readonly IPlanetariumFactory planetariumFactory;
        private readonly IApplicationMediator applicationMediator;
        private readonly INighttimeCalculator nighttimeCalculator;
        private ExoPlanetInputTarget _target;
        private readonly ExoPlanets exoPlanetsPlugin;

        [ImportingConstructor]
        public ExoPlanetObjectContainer(
                IProfileService profileService,
                INighttimeCalculator nighttimeCalculator,
                IFramingAssistantVM framingAssistantVM,
                IApplicationMediator applicationMediator,
                IPlanetariumFactory planetariumFactory) : base(new SequentialStrategy()) {
            this.profileService = profileService;
            this.nighttimeCalculator = nighttimeCalculator;
            this.applicationMediator = applicationMediator;
            this.framingAssistantVM = framingAssistantVM;
            this.planetariumFactory = planetariumFactory;

            Task.Run(() => NighttimeData = nighttimeCalculator.Calculate());
            CoordsToFramingCommand = new AsyncRelayCommand(() => Task.Run(CoordsToFraming));
            exoPlanetsPlugin = new ExoPlanets();
            _pandoraStars = new List<PandoraStar>();

            ExoPlanetInputTarget = new ExoPlanetInputTarget(Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Latitude), Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Longitude), profileService.ActiveProfile.AstrometrySettings.Horizon);
            LoadTargetsCommand = new AsyncRelayCommand(LoadTargets);
            ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>();
            ExoPlanetTargetsList = new AsyncObservableCollection<ExoPlanet>();
            ExoPlanetDSO = new ExoPlanetDeepSkyObject(string.Empty, new Coordinates(Angle.Zero, Angle.Zero, Epoch.J2000), string.Empty, profileService.ActiveProfile.AstrometrySettings.Horizon);
            ExoPlanetDSO.SetDateAndPosition(NighttimeCalculator.GetReferenceDate(DateTime.Now), profileService.ActiveProfile.AstrometrySettings.Latitude, profileService.ActiveProfile.AstrometrySettings.Longitude);

            profileService.LocationChanged += (object sender, EventArgs e) => {
                Target?.SetPosition(Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Latitude), Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Longitude));
                ExoPlanetTargets.Clear();
                SelectedExoPlanet = null;
            };

            profileService.HorizonChanged += (object sender, EventArgs e) => {
                Target?.DeepSkyObject?.SetCustomHorizon(profileService.ActiveProfile.AstrometrySettings.Horizon);
                ExoPlanetDSO?.SetCustomHorizon(profileService.ActiveProfile.AstrometrySettings.Horizon);
            };

            nighttimeCalculator.OnReferenceDayChanged += (object sender, EventArgs e) => {
                // Midnight has passed and the reference day rolled over — refresh altitude data and
                // nighttime shading so the chart stays correct.
                Task.Run(() => {
                    NighttimeData = nighttimeCalculator.Calculate();
                    RebuildDsoForCurrentNight();
                    RaisePropertyChanged(nameof(NighttimeData));
                });
            };
        }

        /// <summary>
        /// A saved sequence carries the chart object with the reference date of the night it was
        /// created on, the altitudes that were calculated for that night, and no observer position
        /// at all - latitude and longitude are protected fields of the deep sky object that are
        /// never serialized. Reopening the sequence on a later night would therefore leave the chart
        /// plotting a curve the time axis never shows, which is what makes it look empty.
        /// </summary>
        [OnDeserialized]
        public void OnDeserialized(StreamingContext context) {
            RebuildDsoForCurrentNight();
        }

        /// <summary>
        /// Rebuilds <see cref="ExoPlanetDSO"/> for the night that is starting now, as seen from the
        /// location in the active profile, keeping the target identity and the plotted light curve.
        /// </summary>
        /// <remarks>
        /// A new instance is assigned rather than the current one refreshed on purpose: the observer
        /// position can only be filled in through SetDateAndPosition, and swapping the instance
        /// raises PropertyChanged, which makes the chart re-read every series instead of holding on
        /// to the previous altitude list.
        /// </remarks>
        private void RebuildDsoForCurrentNight() {
            var currentDso = exoPlanetDSO;
            if (currentDso == null) { return; }

            var refDate = NighttimeCalculator.GetReferenceDate(DateTime.Now);
            var lat = profileService.ActiveProfile.AstrometrySettings.Latitude;
            var lon = profileService.ActiveProfile.AstrometrySettings.Longitude;
            var horizon = profileService.ActiveProfile.AstrometrySettings.Horizon;

            var newDso = new ExoPlanetDeepSkyObject(currentDso.Name, currentDso.Coordinates, string.Empty, horizon);
            newDso.Magnitude = currentDso.Magnitude;
            newDso.SetDateAndPosition(refDate, lat, lon);
            if (currentDso.LightCurve?.Count > 0) {
                newDso.LightCurve = currentDso.LightCurve;
                newDso.ObservationStart = currentDso.ObservationStart;
                newDso.ObservationEnd = currentDso.ObservationEnd;
            }
            ExoPlanetDSO = newDso;
        }

        private ExoPlanet selectedExoPlanet;

        [JsonProperty]
        public ExoPlanet SelectedExoPlanet {
            get => selectedExoPlanet;
            set {
                selectedExoPlanet = value;
                RaisePropertyChanged();
            }
        }

        private ExoPlanetDeepSkyObject exoPlanetDSO;

        [JsonProperty]
        public ExoPlanetDeepSkyObject ExoPlanetDSO {
            get {
                if (exoPlanetDSO == null) {
                    ExoPlanetDSO = new ExoPlanetDeepSkyObject(string.Empty, new Coordinates(Angle.Zero, Angle.Zero, Epoch.J2000), string.Empty, profileService.ActiveProfile.AstrometrySettings.Horizon);
                    ExoPlanetDSO.SetDateAndPosition(NighttimeCalculator.GetReferenceDate(DateTime.Now), profileService.ActiveProfile.AstrometrySettings.Latitude, profileService.ActiveProfile.AstrometrySettings.Longitude);
                }
                return exoPlanetDSO;
            }
            set {
                exoPlanetDSO = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public ExoPlanetInputTarget ExoPlanetInputTarget {
            get => _target;
            set {
                if (ExoPlanetInputTarget != null) {
                    WeakEventManager<InputTarget, EventArgs>.RemoveHandler(ExoPlanetInputTarget, nameof(ExoPlanetInputTarget.CoordinatesChanged), Target_OnCoordinatesChanged);
                    // ExoPlanetInputTarget.CoordinatesChanged -= Target_OnCoordinatesChanged;
                }
                _target = (ExoPlanetInputTarget)value;
                if (ExoPlanetInputTarget != null) {
                    WeakEventManager<InputTarget, EventArgs>.AddHandler(ExoPlanetInputTarget, nameof(ExoPlanetInputTarget.CoordinatesChanged), Target_OnCoordinatesChanged);
                    // ExoPlanetInputTarget.CoordinatesChanged += Target_OnCoordinatesChanged;
                }
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public InputTarget Target {
            get => ExoPlanetInputTarget;
            set {
                ExoPlanetInputTarget.TargetName = value.TargetName;
                ExoPlanetInputTarget.InputCoordinates = value.InputCoordinates;
                ExoPlanetInputTarget.PositionAngle = value.PositionAngle;
                ExoPlanetDSO.Coordinates = value.InputCoordinates.Coordinates;
            }
        }

        private AsyncObservableCollection<ExoPlanet> exoPlanetTargets;

        public AsyncObservableCollection<ExoPlanet> ExoPlanetTargets {
            get => exoPlanetTargets;
            set {
                exoPlanetTargets = value;
                RaisePropertyChanged();
            }
        }

        private AsyncObservableCollection<ExoPlanet> exoPlanetTargetsList;

        public AsyncObservableCollection<ExoPlanet> ExoPlanetTargetsList {
            get => exoPlanetTargetsList;
            set {
                exoPlanetTargetsList = value;
                RaisePropertyChanged();
            }
        }

        private int RetrievedTargets { get; set; } = 0;

        private int filteredTargets = 0;

        public int FilteredTargets {
            get => filteredTargets;
            set {
                filteredTargets = value;
                RaisePropertyChanged();
            }
        }

        private string filterTargets = string.Empty;

        public string FilterTargets {
            get => filterTargets;
            set {
                filterTargets = value;
                RaisePropertyChanged(); }
        }

        private bool loadingTargets = false;

        public bool LoadingTargets {
            get => loadingTargets;
            set {
                loadingTargets = value;
                RaisePropertyChanged();
            }
        }


        public DateTime StartTimeUtc => SelectedExoPlanet.startTime.ToUniversalTime();
        public DateTime EndTimeUtc => SelectedExoPlanet.endTime.ToUniversalTime();
        public TimeSpan TransitDuration => SelectedExoPlanet.endTime.Subtract(SelectedExoPlanet.startTime);

        [RelayCommand]
        private void LoadSingleTarget(object obj) {
            if (SelectedExoPlanet != null && SelectedExoPlanet?.Name != null) {
                // Capture values for the background task to avoid closure over a mutable property.
                var planet = SelectedExoPlanet;
                var transitReferenceDate = NighttimeCalculator.GetReferenceDate(planet.midTime);
                var lat = profileService.ActiveProfile.AstrometrySettings.Latitude;
                var lon = profileService.ActiveProfile.AstrometrySettings.Longitude;
                var horizon = profileService.ActiveProfile.AstrometrySettings.Horizon;

                // Set target name on the UI thread (safe — no altitude computation triggered).
                Target.TargetName = planet.Name;

                // Build a brand-new DSO on a background thread so SetDateAndPosition (240 trig
                // iterations) doesn't block the UI. Assigning a new object to ExoPlanetDSO raises
                // PropertyChanged, which causes the chart DataContext to swap to the new instance
                // and OxyPlot re-reads every series from scratch — fixing the stale-altitude issue
                // that occurs when the same List<DataPoint> is mutated in-place.
                //
                // IMPORTANT: Target.InputCoordinates.Coordinates is also set here (not before
                // Task.Run) to prevent a race condition: setting it on the UI thread chains
                // synchronously into DeepSkyObject.UpdateHorizonAndTransit, which enumerates
                // Altitudes — while the DeepSkyObjectDailyRefresher timer may simultaneously
                // call Refresh() on the same object from a pool thread, mutating Altitudes and
                // causing an InvalidOperationException ("Collection was modified").
                Task.Run(() => {
                    NighttimeData = nighttimeCalculator.Calculate(transitReferenceDate);

                    var newDso = new ExoPlanetDeepSkyObject(planet.Name, planet.coords, string.Empty, horizon);
                    newDso.Magnitude = planet.V;
                    newDso.SetDateAndPosition(transitReferenceDate, lat, lon);
                    newDso.SetTransit(planet.jd_start, planet.jd_mid, planet.jd_end, planet.depth);

                    ExoPlanetDSO = newDso;

                    // Update the InputTarget coordinates on the UI thread so WPF bindings are
                    // satisfied from the correct dispatcher context.
                    System.Windows.Application.Current.Dispatcher.Invoke(() => {
                        Target.InputCoordinates.Coordinates = planet.coords;
                    });

                    RaisePropertyChanged(nameof(NighttimeData));
                });

                RaiseAllPropertiesChanged();
                AfterParentChanged();
            }
        }

        [RelayCommand]
        private void DropTarget(object obj) {
            if (obj is NINA.Sequencer.DragDrop.DropIntoParameters p) {
                if (p.Source is TargetSequenceContainer con) {
                    var dropTarget = con.Container.Target;
                    if (dropTarget != null) {
                        this.Name = dropTarget.TargetName;
                        this.Target.TargetName = dropTarget.TargetName;
                        this.Target.InputCoordinates = dropTarget.InputCoordinates.Clone();
                        this.Target.PositionAngle = dropTarget.PositionAngle;
                    }
                }
            }
        }

        [RelayCommand]
        private void SearchExoPlanetTargets(object obj) {
            ExoPlanetTargetsList = new AsyncObservableCollection<ExoPlanet>(ExoPlanetTargets.Where(ep => ep.Name.ToLower().Contains(FilterTargets.ToLower())).OrderBy(x => x.startTime));
            SelectedExoPlanet = ExoPlanetTargetsList.FirstOrDefault();
            this.LoadSingleTarget(null);
        }

        private async Task<bool> CoordsToFraming() {
            if (Target.DeepSkyObject?.Coordinates != null) {
                var dso = new DeepSkyObject(Target.DeepSkyObject.Name, Target.DeepSkyObject.Coordinates, profileService.ActiveProfile.ApplicationSettings.SkyAtlasImageRepository, profileService.ActiveProfile.AstrometrySettings.Horizon) {
                    RotationPositionAngle = Target.PositionAngle
                };
                applicationMediator.ChangeTab(ApplicationTab.FRAMINGASSISTANT);

                return await framingAssistantVM.SetCoordinates(dso);
            }
            return false;
        }

        private Task<bool> LoadTargets() {
            return Task.Run(async () => {
                LoadingTargets = true;
                ExoPlanetTargets.Clear();
                NighttimeData = nighttimeCalculator.Calculate(NighttimeCalculator.GetReferenceDate(DateTime.Now));

                switch (exoPlanetsPlugin.TargetList) {
                    case 0:
                        await RetrieveTargetsFromSwarthmore(0);
                        break;

                    case 1:
                        await RetrieveTargetsFromSwarthmore(2);
                        break;

                    case 2:
                        await RetrieveTargetsFromExoClock();
                        break;

                    case 3:
                        await RetrieveTargetsFromSwarthmore(3);
                        break;

                    default:
                        return false;
                }

                if (ExoPlanetTargets.Count == 0) {
                    LoadingTargets = false;
                    return false;
                }

                ExoPlanetTargetsList.Clear();
                RetrievedTargets = ExoPlanetTargets.Count;

                PreFilterTargets();
                CheckPanadoraStars();
                SearchExoPlanetTargets(null);

                LoadingTargets = false;
                AfterParentChanged();
                return true;
            });
        }

        private readonly Dictionary<DateTime, NighttimeData> _nighttimeDataCache = new Dictionary<DateTime, NighttimeData>();

        private NighttimeData GetNighttimeDataForTransit(ExoPlanet ep) {
            var refDate = NighttimeCalculator.GetReferenceDate(ep.midTime);
            if (!_nighttimeDataCache.TryGetValue(refDate, out var data)) {
                data = nighttimeCalculator.Calculate(refDate);
                _nighttimeDataCache[refDate] = data;
            }
            return data;
        }

        private void PreFilterTargets() {
            // start with 0 filtered targets
            FilteredTargets = 0;
            _nighttimeDataCache.Clear();

            // Check if transit has already finished
            ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>(ExoPlanetTargets.Where(ep => ep.endTime > DateTime.Now));

            // Check magnitude
            if (exoPlanetsPlugin.CheckMagnitude) {
                ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>(ExoPlanetTargets.Where(ep => ep.V < exoPlanetsPlugin.MaxMagnitude));
            }

            // Check depth
            ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>(ExoPlanetTargets.Where(ep => ep.depth == 0 || ep.depth >= exoPlanetsPlugin.MinDepth));

            // check twilight — use the nighttime data for each transit's own night
            if (exoPlanetsPlugin.WithinTwilight) {
                if (exoPlanetsPlugin.PartialTransits) {
                    ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>(
                        ExoPlanetTargets.Where(ep => {
                            var nd = GetNighttimeDataForTransit(ep);
                            var rise = nd.TwilightRiseAndSet.Rise;
                            var set = nd.TwilightRiseAndSet.Set;
                            return (ep.startTime > set && ep.startTime < rise)
                                || (ep.midTime > set && ep.midTime < rise)
                                || (ep.endTime > set && ep.endTime < rise);
                        }));
                } else {
                    ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>(
                        ExoPlanetTargets.Where(ep => {
                            var nd = GetNighttimeDataForTransit(ep);
                            return ep.midTime > nd.TwilightRiseAndSet.Set && ep.midTime < nd.TwilightRiseAndSet.Rise;
                        }));
                }
            }

            // check nautical — use the nighttime data for each transit's own night
            if (exoPlanetsPlugin.WithinNautical) {
                if (exoPlanetsPlugin.PartialTransits) {
                    ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>(
                        ExoPlanetTargets.Where(ep => {
                            var nd = GetNighttimeDataForTransit(ep);
                            var rise = nd.NauticalTwilightRiseAndSet.Rise;
                            var set = nd.NauticalTwilightRiseAndSet.Set;
                            return (ep.startTime > set && ep.startTime < rise)
                                || (ep.midTime > set && ep.midTime < rise)
                                || (ep.endTime > set && ep.endTime < rise);
                        }));
                } else {
                    ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>(
                        ExoPlanetTargets.Where(ep => {
                            var nd = GetNighttimeDataForTransit(ep);
                            return ep.midTime > nd.NauticalTwilightRiseAndSet.Set && ep.midTime < nd.NauticalTwilightRiseAndSet.Rise;
                        }));
                }
            }

            // check horizon
            if (exoPlanetsPlugin.AboveHorizon) {
                Core.Model.CustomHorizon horizon = profileService.ActiveProfile.AstrometrySettings.Horizon;
                if (exoPlanetsPlugin.PartialTransits) {
                    ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>(ExoPlanetTargets.Where(ep =>
                        CheckAboveHorizon(horizon, ep.coords, ep.startTime) ||
                        CheckAboveHorizon(horizon, ep.coords, ep.midTime) ||
                        CheckAboveHorizon(horizon, ep.coords, ep.endTime)
                    ));
                } else {
                    ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>(ExoPlanetTargets.Where(ep =>
                        CheckAboveHorizon(horizon, ep.coords, ep.startTime) &&
                        CheckAboveHorizon(horizon, ep.coords, ep.midTime) &&
                        CheckAboveHorizon(horizon, ep.coords, ep.endTime)
                    ));
                }
            }

            // Check meridian
            if (exoPlanetsPlugin.WithoutMeridianFlip) {
                ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>(ExoPlanetTargets.Where(ep => {
                    var meridianTime = GetMeridianTime(ep.coords, ep.startTime.AddHours(-1d));
                    return !(ep.startTime < meridianTime && ep.endTime > meridianTime);
                }));
            }

            ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>(ExoPlanetTargets.OrderBy(x => x.startTime));
            FilteredTargets = RetrievedTargets - ExoPlanetTargets.Count;
            Logger.Debug($"Filters accepted {FilteredTargets} out of {RetrievedTargets} total targets. {RetrievedTargets - FilteredTargets} were removed");
        }

        private bool CheckAboveHorizon(CustomHorizon horizon, Coordinates coords, DateTime time) {
            var siderealTime = AstroUtil.GetLocalSiderealTime(time, profileService.ActiveProfile.AstrometrySettings.Longitude);
            var hourAngle = AstroUtil.GetHourAngle(siderealTime, coords.RA);

            var degAngle = AstroUtil.HoursToDegrees(hourAngle);
            var altitude = AstroUtil.GetAltitude(degAngle, profileService.ActiveProfile.AstrometrySettings.Latitude, coords.Dec);
            var azimuth = AstroUtil.GetAzimuth(degAngle, altitude, profileService.ActiveProfile.AstrometrySettings.Latitude, coords.Dec);
            return horizon != null ? horizon.GetAltitude(azimuth) < altitude : 0 < altitude;
        }

        private class Dp(double alt, DateTime datetime) {
            public DateTime Datetime { get; set; } = datetime;
            public double Alt { get; set; } = alt;
        }

        private DateTime GetMeridianTime(Coordinates coords, DateTime startTime) {
            var start = new DateTime(Math.Min(startTime.Ticks, DateTime.Now.Ticks));
            var siderealTime = AstroUtil.GetLocalSiderealTime(start, profileService.ActiveProfile.AstrometrySettings.Longitude);
            var hourAngle = AstroUtil.GetHourAngle(siderealTime, coords.RA);

            var altList = new List<Dp>();
            for (double angle = hourAngle; angle < hourAngle + 24; angle += 0.1) {
                var degAngle = AstroUtil.HoursToDegrees(angle);
                var altitude = AstroUtil.GetAltitude(degAngle, profileService.ActiveProfile.AstrometrySettings.Latitude, coords.Dec);
                //var azimuth = AstroUtil.GetAzimuth(degAngle, altitude, profileService.ActiveProfile.AstrometrySettings.Latitude, coords.Dec);
                // Run the whole thing and get the top value
                altList.Add(new Dp(altitude, start));
                start = start.AddHours(0.1);
            }
            return altList.OrderByDescending((x) => x.Alt).FirstOrDefault().Datetime;
        }

        private List<PandoraStar> _pandoraStars;
        private void CheckPanadoraStars() {
            if (_pandoraStars.Count == 0) {
                List<PandoraStar> pandoraStars = new List<PandoraStar>();
                var assemblyFolder = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).LocalPath;

                var config = new CsvConfiguration(CultureInfo.InvariantCulture);
                config.MissingFieldFound = null;
                using (var reader = new StreamReader(Path.Combine(assemblyFolder, "pandora_website_2024.csv")))
                using (var csv = new CsvReader(reader, config)) {
                    csv.Context.RegisterClassMap<PandoraStarMap>();
                    var records = csv.GetRecords<PandoraStar>();
                    foreach (var record in records) {
                        _pandoraStars.Add(record);
                    }
                }
            }
            foreach(var target in ExoPlanetTargets) {
                var pStar = _pandoraStars.Where(x => x.Star.Replace(" ", "") + x.Planet == target.Name || x.Star + " " + x.Planet == target.Name).FirstOrDefault();
                if (pStar != null) {
                    target.Comments += target.Comments.Length > 0 ? $"{Environment.NewLine}" : "";
                    target.Comments += "Pandora target";
                }
            }
        }

        private async Task RetrieveTargetsFromExoClock() {
            List<ExoClockTarget> targets = await GetExoClockDatabase();
            Logger.Debug($"Retrieved {targets.Count} targets from ExoClock");

            var exoplanets = new AsyncObservableCollection<ExoPlanet>();
            var cutoff = DateTime.Now.AddDays(2);

            foreach (ExoClockTarget target in targets) {
                var exoPlanet = ExoClock2ExoPlanet(target);
                if (exoPlanet.midTime <= cutoff) {
                    exoplanets.Add(exoPlanet);
                }
            }

            ExoPlanetTargets = exoplanets;
        }

        private static ExoPlanet ExoClock2ExoPlanet(ExoClockTarget item) {
            var exoPlanet = new ExoPlanet {
                Name = item.name,
                coords = new Coordinates(Angle.ByDegree(AstroUtil.HMSToDegrees(item.ra_j2000)),
                                         Angle.ByDegree(AstroUtil.DMSToDegrees(item.dec_j2000)),
                                         Epoch.J2000),

                jd_start = item.TransitStart(),
                jd_mid = item.TransitMidpoint(),
                jd_end = item.TransitEnd(),
            };

            exoPlanet.startTime = JulianToDateTime(exoPlanet.jd_start);
            exoPlanet.midTime = JulianToDateTime(exoPlanet.jd_mid);
            exoPlanet.endTime = JulianToDateTime(exoPlanet.jd_end);
            exoPlanet.V = item.v_mag;
            exoPlanet.depth = item.depth_r_mmag;

            exoPlanet.Comments = $"Priority:  {item.priority.ToUpperInvariant()}{Environment.NewLine}";
            exoPlanet.Comments += $"Min. aperture:  {item.min_telescope_inches * 25.4:F1}mm / {item.min_telescope_inches:F1}\"{Environment.NewLine}";
            exoPlanet.Comments += $"Total obs/Recent:  {item.total_observations} / {item.total_observations_recent}";

            return exoPlanet;
        }

        private static DateTime JulianToDateTime(double julianDate) {
            return DateTime.FromOADate(julianDate - 2415018.5).ToLocalTime();
        }

        private async Task RetrieveTargetsFromSwarthmore(int targetlist) {
            var exoplanets = new AsyncObservableCollection<ExoPlanet>();

            try {
                var response = await HttpRequest.HttpRequestAsync(CreateUrl(targetlist), HttpMethod.Get, CancellationToken.None);

                using var reader = new StreamReader(await response.Content?.ReadAsStreamAsync());
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                using var dr = new CsvDataReader(csv);
                csv.Context.RegisterClassMap<ExoPlanetMap>();
                var records = csv.GetRecords<ExoPlanet>();

                foreach (ExoPlanet record in records.ToList()) {
                    record.startTime = record.startTime.ToLocalTime();
                    record.midTime = record.midTime.ToLocalTime();
                    record.endTime = record.endTime.ToLocalTime();
                    record.jd_start += 2450000D; // Add the default deviation for swarthmore
                    record.jd_mid += 2450000D;
                    record.jd_end += 2450000D;
                    exoplanets.Add(record);
                }
            } catch (Exception ex) {
                var errorStr = "Error retrieving targets from Swarthmore";
                Logger.Error(ex, errorStr);
                Notification.ShowError(errorStr);
                return;
            }

            ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>(exoplanets.OrderByDescending(i => i.pbto));
        }

        private string CreateUrl(int targetlist) {
            // var url = "https://astro.swarthmore.edu/transits/print_transits.cgi?single_object=0&ra=&dec=&epoch=&period=&duration=&depth=&target=&observatory_string=50.16276%3B6.85%3BEurope%2FBerlin%3BObservatorium+Hoher+List+%28Universit%C3%A4t+Bonn%29+-+Germany&use_utc=1&observatory_latitude=50.16276&observatory_longitude=6.85&timezone=UTC&start_date=today&days_to_print=1&days_in_past=0&minimum_start_elevation=30&and_vs_or=or&minimum_end_elevation=30&minimum_ha=-12&maximum_ha=12&baseline_hrs=1&show_unc=1&minimum_depth=0&maximum_V_mag=&target_string=&print_html=2&twilight=-12&max_airmass=2.4";
            string url = "https://astro.swarthmore.edu/transits/print_transits.cgi";
            //string targetlist = exoPlanetsPlugin.TargetList == 1 ? "2" : "0";
            url += "?single_object=" + targetlist;
            url += "&ra=";
            url += "&dec=";
            url += "&epoch=";
            url += "&period=";
            url += "&duration=";
            url += "&depth=";
            url += "&target=";
            url += "&observatory_string=Specified_Lat_Long";
            url += "&use_utc=1";
            url += "&observatory_latitude=" + profileService.ActiveProfile.AstrometrySettings.Latitude;
            url += "&observatory_longitude=" + profileService.ActiveProfile.AstrometrySettings.Longitude;
            url += "&timezone=UTC";
            url += "&start_date=today";
            url += "&days_to_print=1";
            url += "&days_in_past=0";
            url += "&minimum_start_elevation=30";
            url += "&and_vs_or=or";
            url += "&minimum_end_elevation=30";
            url += "&minimum_ha=-12";
            url += "&maximum_ha=12";
            url += "&baseline_hrs=1";
            url += "&show_unc=1";
            url += "&minimum_depth=0";
            url += "&maximum_V_mag=";
            url += "&target_string=";
            url += "&print_html=2";
            url += "&twilight=-12";
            url += "&max_airmass=2.4";

            return url;
        }

        private static async Task<List<ExoClockTarget>> GetExoClockDatabase() {
            List<ExoClockTarget> exoClockTargets;
            var url = $"https://www.exoclock.space/database/planets_json";

            try {
                var response = await HttpRequest.HttpRequestAsync(url, HttpMethod.Get, CancellationToken.None);

                var serializer = new JsonSerializer();
                using var sr = new StreamReader(await response.Content?.ReadAsStreamAsync(), Encoding.UTF8);
                using var jsonTextReader = new JsonTextReader(sr);

                var exoPlanetDict = serializer.Deserialize<Dictionary<string, ExoClockTarget>>(jsonTextReader);
                exoClockTargets = exoPlanetDict.Select(item => item.Value).ToList();
            } catch (Exception ex) {
                var errorStr = "Error retrieving targets from ExoClock";
                Logger.Error(ex, errorStr);
                Notification.ShowError(errorStr);
                return [];
            }

            return exoClockTargets;
        }

        public NighttimeData NighttimeData { get; private set; }

        public ICommand LoadTargetsCommand { get; set; }
        public ICommand CoordsToFramingCommand { get; set; }


        private void Target_OnCoordinatesChanged(object sender, EventArgs e) {
            ExoPlanetDSO.Coordinates = Target.InputCoordinates.Coordinates;
            AfterParentChanged();
        }

        public override object Clone() {
            var clone = new ExoPlanetObjectContainer(profileService, nighttimeCalculator, framingAssistantVM, applicationMediator, planetariumFactory) {
                Icon = Icon,
                Name = Name,
                Category = Category,
                Description = Description,
                Items = new ObservableCollection<ISequenceItem>(Items.Select(i => i.Clone() as ISequenceItem)),
                Triggers = new ObservableCollection<ISequenceTrigger>(Triggers.Select(t => t.Clone() as ISequenceTrigger)),
                Conditions = new ObservableCollection<ISequenceCondition>(Conditions.Select(t => t.Clone() as ISequenceCondition)),
                ExoPlanetInputTarget = new ExoPlanetInputTarget(Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Latitude), Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Longitude), profileService.ActiveProfile.AstrometrySettings.Horizon)
            };

            clone.Target.TargetName = this.Target.TargetName;
            clone.Target.InputCoordinates.Coordinates = this.Target.InputCoordinates.Coordinates.Transform(Epoch.J2000);
            clone.Target.PositionAngle = this.Target.PositionAngle;

            foreach (var item in clone.Items) {
                item.AttachNewParent(clone);
            }

            foreach (var condition in clone.Conditions) {
                condition.AttachNewParent(clone);
            }

            foreach (var trigger in clone.Triggers) {
                trigger.AttachNewParent(clone);
            }

            return clone;
        }

        public override string ToString() {
            var baseString = base.ToString();
            return $"{baseString}, Target: {Target?.TargetName} {Target?.InputCoordinates?.Coordinates} {Target?.PositionAngle}";
        }
    }
}