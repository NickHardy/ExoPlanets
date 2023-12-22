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
using NINA.Astrometry;
using NINA.Core.Model;
using OxyPlot;
using OxyPlot.Axes;
using System;
using System.Collections.Generic;

namespace NINA.Plugin.ExoPlanets.Model {

    public class ExoPlanetDeepSkyObject(string id, Coordinates coords, string imageRepository, CustomHorizon customHorizon) : DeepSkyObject(id, coords, imageRepository, customHorizon) {
        private List<DataPoint> _lightCurve;
        private DateTime _observationStart;

        [JsonProperty]
        public DateTime ObservationStart {
            get { return _observationStart; }
            set {
                _observationStart = value;
                RaisePropertyChanged();
            }
        }

        private DateTime _observationEnd;

        [JsonProperty]
        public DateTime ObservationEnd {
            get { return _observationEnd; }
            set {
                _observationEnd = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public List<DataPoint> LightCurve {
            get {
                if (_lightCurve == null) {
                    _lightCurve = new List<DataPoint>();
                }
                return _lightCurve;
            }
            set {
                _lightCurve = value;
                RaisePropertyChanged();
            }
        }

        public void SetTransit(double StarttimeJD, double MidtimeJD, double EndtimeJD, double TransitDepth) {
            var lightCurve = new List<DataPoint>();
            var transitHeight = TransitDepth + 5D;
            var slopePoint = (EndtimeJD - StarttimeJD) / 8;
            ObservationStart = JulianToDateTime(StarttimeJD).AddHours(-1D);
            ObservationEnd = JulianToDateTime(EndtimeJD).AddHours(1D);
            lightCurve.Add(new DataPoint(DateTimeAxis.ToDouble(ObservationStart), transitHeight));
            lightCurve.Add(new DataPoint(DateTimeAxis.ToDouble(JulianToDateTime(StarttimeJD)), transitHeight));
            lightCurve.Add(new DataPoint(DateTimeAxis.ToDouble(JulianToDateTime(StarttimeJD + slopePoint)), transitHeight - TransitDepth));
            lightCurve.Add(new DataPoint(DateTimeAxis.ToDouble(JulianToDateTime(MidtimeJD)), transitHeight - TransitDepth));
            lightCurve.Add(new DataPoint(DateTimeAxis.ToDouble(JulianToDateTime(EndtimeJD - slopePoint)), transitHeight - TransitDepth));
            lightCurve.Add(new DataPoint(DateTimeAxis.ToDouble(JulianToDateTime(EndtimeJD)), transitHeight));
            lightCurve.Add(new DataPoint(DateTimeAxis.ToDouble(ObservationEnd), transitHeight));
            LightCurve = lightCurve;
            RaisePropertyChanged();
        }

        public void SetMaximum(double StarttimeJD, double MidtimeJD, double EndtimeJD, double OCDrift, double Amplitude) {
            ObservationStart = JulianToDateTime(StarttimeJD);
            ObservationEnd = JulianToDateTime(EndtimeJD);
            var drift = OCDrift / 1440.0;
            var height = Amplitude * 40;

            var lightCurve = new List<DataPoint>();
            lightCurve.Add(new DataPoint(DateTimeAxis.ToDouble(JulianToDateTime(StarttimeJD)), 5));
            lightCurve.Add(new DataPoint(DateTimeAxis.ToDouble(JulianToDateTime(MidtimeJD - drift)), height));
            lightCurve.Add(new DataPoint(DateTimeAxis.ToDouble(JulianToDateTime(MidtimeJD + drift)), height));
            lightCurve.Add(new DataPoint(DateTimeAxis.ToDouble(JulianToDateTime(EndtimeJD)), 5));
            LightCurve = lightCurve;
            RaisePropertyChanged();
        }

        public void SetAllNight(DateTime starRise, DateTime starSet) {
            ObservationStart = starRise;
            ObservationEnd = starSet;

            var lightCurve = new List<DataPoint>();
            lightCurve.Add(new DataPoint(DateTimeAxis.ToDouble(starRise), 30));
            lightCurve.Add(new DataPoint(DateTimeAxis.ToDouble(starSet), 30));
            LightCurve = lightCurve;
            RaisePropertyChanged();
        }

        private static DateTime JulianToDateTime(double julianDate) {
            return DateTime.FromOADate(julianDate - 2415018.5).ToLocalTime();
        }
    }
}