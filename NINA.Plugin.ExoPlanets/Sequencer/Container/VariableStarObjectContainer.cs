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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace NINA.Plugin.ExoPlanets.Sequencer.Container {

    [ExportMetadata("Name", "Variable Star object container")]
    [ExportMetadata("Description", "A DSO container to choose variable star target.")]
    [ExportMetadata("Icon", "VariableSVG")]
    [ExportMetadata("Category", "Lbl_SequenceCategory_Container")]
    [Export(typeof(ISequenceItem))]
    [Export(typeof(ISequenceContainer))]
    [JsonObject(MemberSerialization.OptIn)]
    public partial class VariableStarObjectContainer : SequenceContainer, IDeepSkyObjectContainer, IVariableBrightnessTargetContainer {
        private readonly IProfileService profileService;
        private readonly IFramingAssistantVM framingAssistantVM;
        private readonly IPlanetariumFactory planetariumFactory;
        private readonly IApplicationMediator applicationMediator;
        private readonly INighttimeCalculator nighttimeCalculator;
        private ExoPlanetInputTarget target;
        private readonly ExoPlanets exoPlanetsPlugin;

        [ImportingConstructor]
        public VariableStarObjectContainer(
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

            Task.Run(() => NighttimeData = nighttimeCalculator.Calculate(DateTime.Now.AddHours(4)));
            CoordsToFramingCommand = new AsyncRelayCommand(() => Task.Run(CoordsToFraming));
            exoPlanetsPlugin = new ExoPlanets();

            ExoPlanetInputTarget = new ExoPlanetInputTarget(Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Latitude), Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Longitude), profileService.ActiveProfile.AstrometrySettings.Horizon);
            DropTargetCommand = new RelayCommand<object>(DropTarget);
            LoadTargetsCommand = new AsyncRelayCommand(LoadTargets);
            VariableStarTargets = new List<VariableStar>();
            VariableStarTargetList = new List<VariableStar>();
            ExoPlanetDSO = new ExoPlanetDeepSkyObject(string.Empty, new Coordinates(Angle.Zero, Angle.Zero, Epoch.J2000), string.Empty, profileService.ActiveProfile.AstrometrySettings.Horizon);
            ExoPlanetDSO.SetDateAndPosition(NighttimeCalculator.GetReferenceDate(DateTime.Now.AddHours(4)), profileService.ActiveProfile.AstrometrySettings.Latitude, profileService.ActiveProfile.AstrometrySettings.Longitude);

            profileService.LocationChanged += (object sender, EventArgs e) => {
                Target?.SetPosition(Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Latitude), Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Longitude));
                VariableStarTargets.Clear();
                SelectedVariableStar = null;
            };

            profileService.HorizonChanged += (object sender, EventArgs e) => {
                Target?.DeepSkyObject?.SetCustomHorizon(profileService.ActiveProfile.AstrometrySettings.Horizon);
                ExoPlanetDSO?.SetCustomHorizon(profileService.ActiveProfile.AstrometrySettings.Horizon);
            };
        }

        private VariableStar selectedVariableStar;

        [JsonProperty]
        public VariableStar SelectedVariableStar {
            get => selectedVariableStar;
            set {
                selectedVariableStar = value;
                RaisePropertyChanged();
            }
        }

        private ExoPlanetDeepSkyObject exoPlanetDSO;

        [JsonProperty]
        public ExoPlanetDeepSkyObject ExoPlanetDSO {
            get => exoPlanetDSO;
            set {
                exoPlanetDSO = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public ExoPlanetInputTarget ExoPlanetInputTarget {
            get => target;
            set {
                if (ExoPlanetInputTarget != null) {
                    WeakEventManager<InputTarget, EventArgs>.RemoveHandler(ExoPlanetInputTarget, nameof(ExoPlanetInputTarget.CoordinatesChanged), Target_OnCoordinatesChanged);
                    // ExoPlanetInputTarget.CoordinatesChanged -= Target_OnCoordinatesChanged;
                }
                target = (ExoPlanetInputTarget)value;
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

        private List<VariableStar> variableStarTargets;

        public List<VariableStar> VariableStarTargets {
            get => variableStarTargets;
            set {
                variableStarTargets = value;
                RaisePropertyChanged();
            }
        }

        private List<VariableStar> variableStarTargetList;

        public List<VariableStar> VariableStarTargetList {
            get => variableStarTargetList;
            set {
                variableStarTargetList = value;
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
                RaisePropertyChanged();
            }
        }

        [RelayCommand]
        private void LoadSingleTarget(object obj) {
            if (SelectedVariableStar != null && SelectedVariableStar?.Name != null) {
                Target.TargetName = SelectedVariableStar.Name;
                Target.InputCoordinates.Coordinates = SelectedVariableStar.Coordinates();
                Target.DeepSkyObject.Coordinates = SelectedVariableStar.Coordinates();

                ExoPlanetDSO.Coordinates = SelectedVariableStar.Coordinates();
                ExoPlanetDSO.Magnitude = SelectedVariableStar.V;
                if (SelectedVariableStar.HasEvents) {
                    ExoPlanetDSO.SetMaximum(SelectedVariableStar.jd_start, SelectedVariableStar.jd_mid, SelectedVariableStar.jd_end, SelectedVariableStar.OCRange, SelectedVariableStar.amplitude);
                } else {
                    ExoPlanetDSO.SetAllNight(SelectedVariableStar.startTime, SelectedVariableStar.endTime);
                }

                AfterParentChanged();
            }
        }

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
            VariableStarTargetList = new List<VariableStar>(VariableStarTargets.Where(ep => ep.Name.ToLower().Contains(FilterTargets.ToLower())));
            SelectedVariableStar = VariableStarTargetList.FirstOrDefault();
        }

        private bool loadingTargets = false;

        public bool LoadingTargets {
            get => loadingTargets;
            set {
                loadingTargets = value;
                RaisePropertyChanged();
            }
        }

        private Task<bool> LoadTargets() {
            LoadingTargets = true;
            VariableStarTargets.Clear();
            VariableStarTargetList.Clear();

            return Task.Run(async () => {
                try {
                    await RetrieveTargetsFromFile(exoPlanetsPlugin.VarStarCatalog);
                } catch (Exception ex) {
                    Logger.Error(ex);
                }

                RetrievedTargets = VariableStarTargets.Count;
                PreFilterTargets();
                SearchExoPlanetTargets(null);
                LoadingTargets = false;
                AfterParentChanged();
                return true;
            });
        }

        private void PreFilterTargets() {
            // start with 0 filtered targets
            FilteredTargets = 0;

            // Check magnitude
            if (exoPlanetsPlugin.CheckMagnitude) {
                VariableStarTargets = new List<VariableStar>(VariableStarTargets.Where(ep => ep.V < exoPlanetsPlugin.MaxMagnitude));
                FilteredTargets = RetrievedTargets - VariableStarTargets.Count;
            }

            // check twilight
            if (exoPlanetsPlugin.WithinTwilight) {
                var rise = NighttimeData.TwilightRiseAndSet.Rise;
                var set = NighttimeData.TwilightRiseAndSet.Set;
                if (exoPlanetsPlugin.PartialTransits) {
                    VariableStarTargets = new List<VariableStar>(
                        VariableStarTargets.Where(ep => (ep.startTime > set && ep.startTime < rise)
                        || (ep.midTime > set && ep.midTime < rise) || (ep.endTime > set && ep.endTime < rise)));
                } else {
                    VariableStarTargets = new List<VariableStar>(VariableStarTargets.Where(ep => ep.startTime > set && ep.endTime < rise));
                }
                FilteredTargets = RetrievedTargets - VariableStarTargets.Count;
            }

            // check nautical
            if (exoPlanetsPlugin.WithinNautical) {
                var rise = NighttimeData.NauticalTwilightRiseAndSet.Rise;
                var set = NighttimeData.NauticalTwilightRiseAndSet.Set;
                if (exoPlanetsPlugin.PartialTransits) {
                    VariableStarTargets = new List<VariableStar>(
                        VariableStarTargets.Where(ep => (ep.startTime > set && ep.startTime < rise)
                        || (ep.midTime > set && ep.midTime < rise) || (ep.endTime > set && ep.endTime < rise)));
                } else {
                    VariableStarTargets = new List<VariableStar>(VariableStarTargets.Where(ep => ep.startTime > set && ep.endTime < rise));
                }
                FilteredTargets = RetrievedTargets - VariableStarTargets.Count;
            }

            // check horizon
            if (exoPlanetsPlugin.AboveHorizon) {
                Core.Model.CustomHorizon horizon = profileService.ActiveProfile.AstrometrySettings.Horizon;
                if (exoPlanetsPlugin.PartialTransits) {
                    VariableStarTargets = new List<VariableStar>(VariableStarTargets.Where(ep =>
                        CheckAboveHorizon(horizon, ep.Coordinates(), ep.startTime) ||
                        CheckAboveHorizon(horizon, ep.Coordinates(), ep.midTime) ||
                        CheckAboveHorizon(horizon, ep.Coordinates(), ep.endTime)
                    ));
                } else {
                    VariableStarTargets = new List<VariableStar>(VariableStarTargets.Where(ep =>
                        CheckAboveHorizon(horizon, ep.Coordinates(), ep.startTime) &&
                        CheckAboveHorizon(horizon, ep.Coordinates(), ep.midTime) &&
                        CheckAboveHorizon(horizon, ep.Coordinates(), ep.endTime)
                    ));
                }
                FilteredTargets = RetrievedTargets - VariableStarTargets.Count;
            }

            // Check meridian
            if (exoPlanetsPlugin.WithoutMeridianFlip) {
                VariableStarTargets = new List<VariableStar>(VariableStarTargets.Where(ep => {
                    var meridianTime = GetMeridianTime(ep.Coordinates(), ep.startTime.AddHours(-1d));
                    return !(ep.startTime < meridianTime && ep.endTime > meridianTime);
                }));
                FilteredTargets = RetrievedTargets - VariableStarTargets.Count;
            }
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
                altList.Add(new Dp(altitude, start));
                start = start.AddHours(0.1);
            }
            return altList.OrderByDescending((x) => x.Alt).FirstOrDefault().Datetime;
        }

        private DateTime GetNextSettingTime(CustomHorizon horizon, Coordinates coords, DateTime startTime) {
            var start = startTime;
            var siderealTime = AstroUtil.GetLocalSiderealTime(start, profileService.ActiveProfile.AstrometrySettings.Longitude);
            var hourAngle = AstroUtil.GetHourAngle(siderealTime, coords.RA);

            for (double angle = hourAngle; angle < hourAngle + 24; angle += 0.1) {
                var degAngle = AstroUtil.HoursToDegrees(angle);
                var altitude = AstroUtil.GetAltitude(degAngle, profileService.ActiveProfile.AstrometrySettings.Latitude, coords.Dec);
                var azimuth = AstroUtil.GetAzimuth(degAngle, altitude, profileService.ActiveProfile.AstrometrySettings.Latitude, coords.Dec);

                if ((horizon != null) && altitude < horizon.GetAltitude(azimuth)) {
                    break;
                } else if (altitude < 0) {
                    break;
                }

                start = start.AddHours(0.1);
            }
            return start;
        }

        private DateTime GetNextRiseTime(CustomHorizon horizon, Coordinates coords, DateTime startTime) {
            var start = startTime;
            var siderealTime = AstroUtil.GetLocalSiderealTime(start, profileService.ActiveProfile.AstrometrySettings.Longitude);
            var hourAngle = AstroUtil.GetHourAngle(siderealTime, coords.RA);

            for (double angle = hourAngle; angle < hourAngle + 24; angle += 0.1) {
                var degAngle = AstroUtil.HoursToDegrees(angle);
                var altitude = AstroUtil.GetAltitude(degAngle, profileService.ActiveProfile.AstrometrySettings.Latitude, coords.Dec);
                var azimuth = AstroUtil.GetAzimuth(degAngle, altitude, profileService.ActiveProfile.AstrometrySettings.Latitude, coords.Dec);

                if ((horizon != null) && altitude > horizon.GetAltitude(azimuth)) {
                    break;
                } else if (altitude > 0) {
                    break;
                }

                start = start.AddHours(0.1);
            }
            return start;
        }

        private Task RetrieveTargetsFromFile(string fileName) {
            if (!File.Exists(fileName)) {
                var errorStr = $"Variable star list {fileName} does not exist.";
                Logger.Error(errorStr);
                Notification.ShowError(errorStr);
                return Task.CompletedTask;
            }

            var sunSetLocal = NighttimeData.TwilightRiseAndSet.Set ?? DateTime.Now;
            var sunRiseLocal = NighttimeData.TwilightRiseAndSet.Rise ?? DateTime.Now;

            var sunSetUT = sunSetLocal.ToUniversalTime();
            var tNowUT = DateTime.Now.ToUniversalTime();
            var date = (sunSetUT.CompareTo(tNowUT) < 0) ? tNowUT : sunSetUT;
            var baseJd = date.ToOADate() + 2415018.5;
            var span = exoPlanetsPlugin.VarStarObservationSpan;

            var withEvents = new List<VariableStar>();
            var withoutEvents = new List<VariableStar>();

            Core.Model.CustomHorizon horizon = profileService.ActiveProfile.AstrometrySettings.Horizon;

            var config = new CsvConfiguration(CultureInfo.InvariantCulture) {
                MissingFieldFound = null,
            };

            using (var reader = new StreamReader(fileName))
            using (var csv = new CsvReader(reader, config)) {
                csv.Context.RegisterClassMap<VarStarMap>();
                while (csv.Read()) {
                    if (csv.GetRecord<VariableStar>() is VariableStar newStar) {
                        if (newStar.HasEvents) {
                            newStar.NextEvent(baseJd, span);
                            withEvents.Add(newStar);
                        } else {
                            var starRise = GetNextRiseTime(horizon, newStar.Coordinates(), sunSetLocal);
                            starRise = new DateTime(Math.Min(starRise.Ticks, sunRiseLocal.Ticks));
                            var starSet = GetNextSettingTime(horizon, newStar.Coordinates(), starRise.AddMinutes(10d));
                            starSet = new DateTime(Math.Min(starSet.Ticks, sunRiseLocal.Ticks));
                            newStar.AllNight(starRise, starSet);
                            withoutEvents.Add(newStar);
                        }
                    }
                }
            }

            withEvents.Sort((a, b) => a.CompareTo(b));
            withoutEvents.Sort((a, b) => a.CompareTo(b));

            VariableStarTargets.Clear();
            VariableStarTargets.AddRange(withEvents);
            VariableStarTargets.AddRange(withoutEvents);

            return Task.CompletedTask;
        }

        public NighttimeData NighttimeData { get; private set; }

        public ICommand DropTargetCommand { get; set; }
        public ICommand LoadTargetsCommand { get; set; }
        public ICommand CoordsToFramingCommand { get; set; }

        private void Target_OnCoordinatesChanged(object sender, EventArgs e) {
            ExoPlanetDSO.Coordinates = Target.InputCoordinates.Coordinates;
            AfterParentChanged();
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

        public override object Clone() {
            var clone = new VariableStarObjectContainer(profileService, nighttimeCalculator, framingAssistantVM, applicationMediator, planetariumFactory) {
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