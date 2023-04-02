#region "copyright"

/*
    Copyright © 2016 - 2021 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Sequencer.Container;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.Container.ExecutionStrategy;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Core.Utility;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using NINA.Astrometry;
using NINA.Plugin.ExoPlanets.Model;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.Equipment.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.Astrometry.Interfaces;
using System.Net;
using System.IO;
using CsvHelper;
using System.Globalization;
using System.Data;
using System.Windows.Data;
using System.ComponentModel;
using System.Windows;
using NINA.Sequencer.Utility.DateTimeProvider;
using System.Collections.Generic;
using NINA.Plugin.ExoPlanets.Sequencer.Utility.DateTimeProvider;
using System.Text;
using Newtonsoft.Json.Serialization;
using NINA.Core.Model;
using NINA.Core.Enum;
using NINA.Plugin.ExoPlanets.RiseAndSet;
using NINA.Sequencer;
using NINA.Plugin.ExoPlanets.Interfaces;

namespace NINA.Plugin.ExoPlanets.Sequencer.Container {

    [ExportMetadata("Name", "ExoPlanet object container")]
    [ExportMetadata("Description", "A DSO container to choose a transiting exo planet target.")]
    [ExportMetadata("Icon", "TelescopeSVG")]
    [ExportMetadata("Category", "Lbl_SequenceCategory_Container")]
    [Export(typeof(ISequenceItem))]
    [Export(typeof(ISequenceContainer))]
    [JsonObject(MemberSerialization.OptIn)]
    public class ExoPlanetObjectContainer : SequenceContainer, IDeepSkyObjectContainer, IVariableBrightnessTargetContainer
    {
        private readonly IProfileService profileService;
        private readonly IFramingAssistantVM framingAssistantVM;
        private readonly IPlanetariumFactory planetariumFactory;
        private readonly IApplicationMediator applicationMediator;
        private INighttimeCalculator nighttimeCalculator;
        private ExoPlanetInputTarget target;
        private ExoPlanets exoPlanetsPlugin;

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

            Task.Run(() => NighttimeData = nighttimeCalculator.Calculate(DateTime.Now.AddHours(4)) );
            CoordsToFramingCommand = new AsyncCommand<bool>(() => Task.Run(CoordsToFraming));
            exoPlanetsPlugin = new ExoPlanets();

            ExoPlanetInputTarget = new ExoPlanetInputTarget(Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Latitude), Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Longitude), profileService.ActiveProfile.AstrometrySettings.Horizon);
            DropTargetCommand = new GalaSoft.MvvmLight.Command.RelayCommand<object>(DropTarget);
            LoadTargetsCommand = new AsyncCommand<bool>(LoadTargets);
            LoadSingleTargetCommand = new RelayCommand(LoadSingleTarget);
            SearchExoPlanetTargetsCommand = new RelayCommand(SearchExoPlanetTargets);
            ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>();
            ExoPlanetTargetsList = new AsyncObservableCollection<ExoPlanet>();
            ExoPlanetDSO = new ExoPlanetDeepSkyObject(string.Empty, new Coordinates(Angle.Zero, Angle.Zero, Epoch.J2000), string.Empty, profileService.ActiveProfile.AstrometrySettings.Horizon);
            ExoPlanetDSO.SetDateAndPosition(NighttimeCalculator.GetReferenceDate(DateTime.Now.AddHours(4)), profileService.ActiveProfile.AstrometrySettings.Latitude, profileService.ActiveProfile.AstrometrySettings.Longitude);

            profileService.LocationChanged += (object sender, EventArgs e) => {
                Target?.SetPosition(Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Latitude), Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Longitude));
                ExoPlanetTargets.Clear();
                SelectedExoPlanet = null;
            };

            profileService.HorizonChanged += (object sender, EventArgs e) => {
                Target?.DeepSkyObject?.SetCustomHorizon(profileService.ActiveProfile.AstrometrySettings.Horizon);
                ExoPlanetDSO?.SetCustomHorizon(profileService.ActiveProfile.AstrometrySettings.Horizon);
            };
        }

        private void LoadSingleTarget(object obj) {
            if (SelectedExoPlanet != null && SelectedExoPlanet?.Name != null) {
                Target.TargetName = SelectedExoPlanet.Name;
                Target.InputCoordinates.Coordinates = SelectedExoPlanet.Coordinates();
                Target.DeepSkyObject.Coordinates = SelectedExoPlanet.Coordinates();

                ExoPlanetDSO.Coordinates = SelectedExoPlanet.Coordinates();
                ExoPlanetDSO.Magnitude = SelectedExoPlanet.V;
                ExoPlanetDSO.SetTransit(SelectedExoPlanet.jd_start, SelectedExoPlanet.jd_mid, SelectedExoPlanet.jd_end, SelectedExoPlanet.depth);
                AfterParentChanged();
            }
        }

        private void DropTarget(object obj) {
            var p = obj as NINA.Sequencer.DragDrop.DropIntoParameters;
            if (p != null) {
                var con = p.Source as TargetSequenceContainer;
                if (con != null) {
                    var dropTarget = con.Container.Target;
                    if (dropTarget != null) {
                        this.Name = dropTarget.TargetName;
                        this.Target.TargetName = dropTarget.TargetName;
                        this.Target.InputCoordinates = dropTarget.InputCoordinates.Clone();
                        this.Target.Rotation = dropTarget.Rotation;
                    }
                }
            }
        }

        private ExoPlanet _SelectedExoPlanet;

        [JsonProperty]
        public ExoPlanet SelectedExoPlanet {
            get {
                return _SelectedExoPlanet;
            }
            set {
                _SelectedExoPlanet = value;
                RaisePropertyChanged();
            }
        }

        private ExoPlanetDeepSkyObject _ExoPlanetDSO;

        [JsonProperty]
        public ExoPlanetDeepSkyObject ExoPlanetDSO {
            get {
                return _ExoPlanetDSO;
            }
            set {
                _ExoPlanetDSO = value;
                RaisePropertyChanged();
            }
        }

        private AsyncObservableCollection<ExoPlanet> _exoPlanetTargets;

        public AsyncObservableCollection<ExoPlanet> ExoPlanetTargets {
            get {
                return _exoPlanetTargets;
            }
            set {
                _exoPlanetTargets = value;
                RaisePropertyChanged();
            }
        }

        private AsyncObservableCollection<ExoPlanet> _exoPlanetTargetsList;

        public AsyncObservableCollection<ExoPlanet> ExoPlanetTargetsList {
            get {
                return _exoPlanetTargetsList;
            }
            set {
                _exoPlanetTargetsList = value;
                RaisePropertyChanged();
            }
        }

        private int retrievedTargets { get; set; } = 0;
        private int filteredTargets = 0;
        public int FilteredTargets { get => filteredTargets; set { filteredTargets = value; RaisePropertyChanged(); } }

        private string filterTargets = "";
        public string FilterTargets { get => filterTargets; set { filterTargets = value; RaisePropertyChanged(); } }

        private void SearchExoPlanetTargets(object obj) {
            ExoPlanetTargetsList = new AsyncObservableCollection<ExoPlanet>(ExoPlanetTargets.Where(ep => ep.Name.ToLower().Contains(FilterTargets.ToLower())));
            SelectedExoPlanet = ExoPlanetTargetsList.FirstOrDefault();
        }

        private Boolean _LoadingTargets = false;

        public Boolean LoadingTargets {
            get { return _LoadingTargets; }
            set {
                _LoadingTargets = value;
                RaisePropertyChanged();
            }
        }

        private Task<bool> LoadTargets(object obj) {
            LoadingTargets = true;
            ExoPlanetTargets.Clear();
            ExoPlanetTargetsList.Clear();

            return Task.Run(() => {
                switch (exoPlanetsPlugin.TargetList) {
                    case 0:
                        RetrieveTargetsFromSwarthmore(0);
                        break;
                    case 1:
                        RetrieveTargetsFromSwarthmore(2);
                        break;
                    case 2:
                        RetrieveTargetsFromExoClock();
                        break;
                    case 3:
                        RetrieveTargetsFromSwarthmore(3);
                        break;
                }

                retrievedTargets = ExoPlanetTargets.Count();
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
                ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>(ExoPlanetTargets.Where(ep => ep.V < exoPlanetsPlugin.MaxMagnitude));
                FilteredTargets = retrievedTargets - ExoPlanetTargets.Count();
            }

            // check twilight
            if (exoPlanetsPlugin.WithinTwilight) {
                var rise = NighttimeData.TwilightRiseAndSet.Rise;
                var set = NighttimeData.TwilightRiseAndSet.Set;
                if (exoPlanetsPlugin.PartialTransits) {
                    ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>(
                        ExoPlanetTargets.Where(ep => (ep.startTime > set && ep.startTime < rise) 
                        || (ep.midTime > set && ep.midTime < rise) || (ep.endTime > set && ep.endTime < rise)) );
                } else {
                    ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>(ExoPlanetTargets.Where(ep => ep.midTime > set && ep.midTime < rise));
                }
                FilteredTargets = retrievedTargets - ExoPlanetTargets.Count();
            }

            // check nautical
            if (exoPlanetsPlugin.WithinNautical) {
                var rise = NighttimeData.NauticalTwilightRiseAndSet.Rise;
                var set = NighttimeData.NauticalTwilightRiseAndSet.Set;
                if (exoPlanetsPlugin.PartialTransits) {
                    ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>(
                        ExoPlanetTargets.Where(ep => (ep.startTime > set && ep.startTime < rise)
                        || (ep.midTime > set && ep.midTime < rise) || (ep.endTime > set && ep.endTime < rise)));
                } else {
                    ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>(ExoPlanetTargets.Where(ep => ep.midTime > set && ep.midTime < rise));
                }
                FilteredTargets = retrievedTargets - ExoPlanetTargets.Count();
            }

            // check horizon
            if (exoPlanetsPlugin.AboveHorizon) {
                Core.Model.CustomHorizon horizon = profileService.ActiveProfile.AstrometrySettings.Horizon;
                if (exoPlanetsPlugin.PartialTransits) {
                    ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>(ExoPlanetTargets.Where(ep =>
                        CheckAboveHorizon(horizon, ep.Coordinates(), ep.startTime) ||
                        CheckAboveHorizon(horizon, ep.Coordinates(), ep.midTime) ||
                        CheckAboveHorizon(horizon, ep.Coordinates(), ep.endTime)
                    ));
                } else {
                    ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>(ExoPlanetTargets.Where(ep =>
                        CheckAboveHorizon(horizon, ep.Coordinates(), ep.startTime) &&
                        CheckAboveHorizon(horizon, ep.Coordinates(), ep.midTime) &&
                        CheckAboveHorizon(horizon, ep.Coordinates(), ep.endTime)
                    ));
                }
                FilteredTargets = retrievedTargets - ExoPlanetTargets.Count();
            }

            // Check meridian
            if (exoPlanetsPlugin.WithoutMeridianFlip) {
                ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>(ExoPlanetTargets.Where(ep => {
                    var meridianTime = GetMeridianTime(ep.Coordinates(), ep.startTime.AddHours(-1d));
                    return !(ep.startTime < meridianTime && ep.endTime > meridianTime);
                }));
                FilteredTargets = retrievedTargets - ExoPlanetTargets.Count();
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

        private class Dp {
            public DateTime datetime { get; set; }
            public double alt { get; set; }

            public Dp(Double alt, DateTime datetime) {
                this.alt = alt;
                this.datetime = datetime;
            }
        }

        private DateTime GetMeridianTime(Coordinates coords, DateTime startTime) {
            var start = new DateTime(Math.Min(startTime.Ticks, DateTime.Now.Ticks));
            var siderealTime = AstroUtil.GetLocalSiderealTime(start, profileService.ActiveProfile.AstrometrySettings.Longitude);
            var hourAngle = AstroUtil.GetHourAngle(siderealTime, coords.RA);

            List<Dp> altList = new List<Dp>();
            for (double angle = hourAngle; angle < hourAngle + 24; angle += 0.1) {
                var degAngle = AstroUtil.HoursToDegrees(angle);
                var altitude = AstroUtil.GetAltitude(degAngle, profileService.ActiveProfile.AstrometrySettings.Latitude, coords.Dec);
                //var azimuth = AstroUtil.GetAzimuth(degAngle, altitude, profileService.ActiveProfile.AstrometrySettings.Latitude, coords.Dec);
                // Run the whole thing and get the top value
                altList.Add(new Dp(altitude, start));
                start = start.AddHours(0.1);
            }
            return altList.OrderByDescending((x) => x.alt).FirstOrDefault().datetime;
        }

        private void RetrieveTargetsFromExoClock() {
            List<ExoClockTarget> targets = getExoClockDatabase();
            foreach (ExoClockTarget target in targets) {
                ExoPlanet exoPlanet = ExoClock2ExoPlanet(target);
                ExoPlanetTargets.Add(exoPlanet);
            }
        }

        private ExoPlanet ExoClock2ExoPlanet(ExoClockTarget item) {
            ExoPlanet exoPlanet = new ExoPlanet();
            exoPlanet.Name = item.name;
            exoPlanet.coords = item.ra_j2000 + " " + item.dec_j2000;

            var latitude = profileService.ActiveProfile.AstrometrySettings.Latitude;
            var longitude = profileService.ActiveProfile.AstrometrySettings.Longitude;

            exoPlanet.jd_start = item.TransitStart();
            exoPlanet.jd_mid = item.TransitMidpoint();
            exoPlanet.jd_end = item.TransitEnd();
            exoPlanet.startTime = JulianToDateTime(exoPlanet.jd_start);
            exoPlanet.midTime = JulianToDateTime(exoPlanet.jd_mid);
            exoPlanet.endTime = JulianToDateTime(exoPlanet.jd_end);
            exoPlanet.V = item.v_mag;
            exoPlanet.depth = item.depth_r_mmag;
            exoPlanet.Comments = "Priority: " + item.priority + " Total obs/Recent: " + item.total_observations + "/" + item.total_observations_recent;
            exoPlanet.Comments += "\r\nMin aperture: " + item.min_telescope_inches;
            return exoPlanet;
        }

        private static DateTime JulianToDateTime(double julianDate) {
            return DateTime.FromOADate(julianDate - 2415018.5).ToLocalTime();
        }

        private void RetrieveTargetsFromSwarthmore(int targetlist) {
            WebRequest request = WebRequest.Create(CreateUrl(targetlist));
            request.Timeout = 30 * 60 * 1000;
            request.UseDefaultCredentials = true;
            request.Proxy.Credentials = request.Credentials;
            WebResponse response = (WebResponse)request.GetResponse();
            ExoPlanetTargets.Clear();
            using (var reader = new StreamReader(response.GetResponseStream()))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture)) {
                // Do any configuration to `CsvReader` before creating CsvDataReader.
                using (var dr = new CsvDataReader(csv)) {
                    csv.Context.RegisterClassMap<ExoPlanetMap>();
                    var records = csv.GetRecords<ExoPlanet>();
                    foreach (ExoPlanet record in records.ToList()) {
                        record.startTime = record.startTime.ToLocalTime();
                        record.midTime = record.midTime.ToLocalTime();
                        record.endTime = record.endTime.ToLocalTime();
                        record.jd_start += 2450000D; // Add the default deviation for swarthmore
                        record.jd_mid += 2450000D;
                        record.jd_end += 2450000D;
                        ExoPlanetTargets.Add(record);
                    }
                    ExoPlanetTargets = new AsyncObservableCollection<ExoPlanet>(ExoPlanetTargets.OrderByDescending(i => i.pbto));
                }
            }
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

        private List<ExoClockTarget> getExoClockDatabase() {
            var url = $"https://www.exoclock.space/database/planets_json";
            WebRequest request = WebRequest.Create(url);
            request.Timeout = 30 * 60 * 1000;
            request.UseDefaultCredentials = true;
            request.Proxy.Credentials = request.Credentials;
            WebResponse response = (WebResponse)request.GetResponse();

            var serializer = new JsonSerializer();

            using (var sr = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            using (var jsonTextReader = new JsonTextReader(sr)) {
                var exoPlanetDict = serializer.Deserialize<Dictionary<string, ExoClockTarget>>(jsonTextReader);
                List<ExoClockTarget> exoClockTargets = exoPlanetDict.Select(item => item.Value).ToList();
                return exoClockTargets;
            }

/*            // For debugging
            ITraceWriter traceWriter = new MemoryTraceWriter();

            var settings = new JsonSerializerSettings {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                Formatting = Formatting.None,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                FloatParseHandling = FloatParseHandling.Decimal,
                TraceWriter = traceWriter
            };

            using (Stream stream = response.GetResponseStream()) {
                StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                String responseString = reader.ReadToEnd();
                Dictionary<string, ExoClockTarget> exoClockTargets = JsonConvert.DeserializeObject<Dictionary<string,ExoClockTarget>>(responseString, settings);
                return exoClockTargets;
            }*/
        }

        public NighttimeData NighttimeData { get; private set; }

        public ICommand DropTargetCommand { get; set; }
        public ICommand LoadTargetsCommand { get; set; }
        public ICommand LoadSingleTargetCommand { get; set; }
        public ICommand SearchExoPlanetTargetsCommand { get; set; }

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
                ExoPlanetInputTarget.Rotation = value.Rotation;
                ExoPlanetDSO.Coordinates = value.InputCoordinates.Coordinates;
            }
        }

        private void Target_OnCoordinatesChanged(object sender, EventArgs e) {
            ExoPlanetDSO.Coordinates = Target.InputCoordinates.Coordinates;
            AfterParentChanged();
        }

        public ICommand CoordsToFramingCommand { get; set; }

        private async Task<bool> CoordsToFraming() {
            if (Target.DeepSkyObject?.Coordinates != null) {
                var dso = new DeepSkyObject(Target.DeepSkyObject.Name, Target.DeepSkyObject.Coordinates, profileService.ActiveProfile.ApplicationSettings.SkyAtlasImageRepository, profileService.ActiveProfile.AstrometrySettings.Horizon);
                dso.Rotation = Target.Rotation;
                applicationMediator.ChangeTab(ApplicationTab.FRAMINGASSISTANT);
                return await framingAssistantVM.SetCoordinates(dso);
            }
            return false;
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
            clone.Target.Rotation = this.Target.Rotation;

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
            return $"{baseString}, Target: {Target?.TargetName} {Target?.InputCoordinates?.Coordinates} {Target?.Rotation}";
        }
    }
}