#region "copyright"

/*
    Copyright © 2016 - 2021 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using CsvHelper.Configuration;
using Newtonsoft.Json;
using NINA.Astrometry;
using System;

namespace NINA.Plugin.ExoPlanets.Model {

    [JsonObject(MemberSerialization.OptIn)]
    public class VariableStar {

        [JsonProperty]
        public string Name { get; set; }

        [JsonProperty]
        public string Comments { get; set; }

        [JsonProperty]
        public double V { get; set; }

        [JsonProperty]
        public DateTime startTime { get; set; }

        [JsonProperty]
        public DateTime midTime { get; set; }

        [JsonProperty]
        public DateTime endTime { get; set; }

        [JsonProperty]
        public double jd_start { get; set; }

        [JsonProperty]
        public double jd_mid { get; set; }

        [JsonProperty]
        public double jd_end { get; set; }

        [JsonProperty]
        public string RA { get; set; }

        [JsonProperty]
        public string Dec { get; set; }

        [JsonProperty]
        public double epoch { get; set; }

        [JsonProperty]
        public double period { get; set; }

        [JsonProperty]
        public double OCRange { get; set; }

        [JsonProperty]
        public double amplitude { get; set; }

        [JsonProperty]
        public double observedPhase { get; set; }

        [JsonProperty]
        public double Altitude { get; set; }

        [JsonProperty]
        public double Azimuth { get; set; }

        public Coordinates Coordinates() {
            return new Coordinates(Angle.ByDegree(AstroUtil.HMSToDegrees(RA)), Angle.ByDegree(AstroUtil.DMSToDegrees(Dec)), Epoch.J2000);
        }

        public string formattedPeriod {
            get {
                if (period > 0) {
                    return period < 1.0 ? string.Format("{0}d ({1:F2}h)", period, period * 24.0) : string.Format("{0}d", period);
                } else {
                    return "--";
                }
            }
        }

        public void CalculateAltAz(double latitude, double longitude) {
            var siderealTime = AstroUtil.GetLocalSiderealTime(midTime, longitude);
            var hourAngle = AstroUtil.GetHourAngle(siderealTime, Coordinates().RA);

            var degAngle = AstroUtil.HoursToDegrees(hourAngle);
            Altitude = AstroUtil.GetAltitude(degAngle, latitude, Coordinates().Dec);
            Azimuth = AstroUtil.GetAzimuth(degAngle, Altitude, latitude, Coordinates().Dec);
        }

        public void NextEvent(double referenceJD, double span) {
            if (!HasEvents) return;

            var shiftedEpoch = epoch + (period * observedPhase);
            var cycle = Math.Floor((referenceJD - shiftedEpoch) / period);
            var nextEvent = shiftedEpoch + (period * (cycle + 1));
            var window = (span + OCRange) / 1440.0;
            jd_start = nextEvent - window;
            jd_mid = nextEvent;
            jd_end = nextEvent + window;

            startTime = JulianToDateTime(jd_start).ToLocalTime();
            midTime = JulianToDateTime(jd_mid).ToLocalTime();
            endTime = JulianToDateTime(jd_end).ToLocalTime();
        }

        public void AllNight(DateTime set, DateTime rise) {
            var nightDuration = rise.Subtract(set).Ticks;
            startTime = set.AddMinutes(5);
            midTime = set.AddTicks(nightDuration / 2);
            endTime = rise.AddMinutes(-5);
        }

        public bool HasEvents {
            get {
                return epoch > 0;
            }
        }

        public int CompareTo(VariableStar other) {
            if (this.HasEvents && other.HasEvents) {
                return jd_start.CompareTo(other.jd_start);
            } else {
                var RA = this.Coordinates().RA;
                var otherRA = other.Coordinates().RA;

                if ((RA > otherRA) && (RA - otherRA) > 12) {
                    return RA.CompareTo(otherRA + 24);
                } else {
                    return RA.CompareTo(otherRA);
                }
            }
        }

        private static DateTime JulianToDateTime(double julianDate) {
            return DateTime.FromOADate(julianDate - 2415018.5).ToLocalTime();
        }
    }

    public sealed class ManualVarStarMap : ClassMap<VariableStar> {

        public ManualVarStarMap() {
            Map(m => m.Name).Name("name");
            Map(m => m.Comments).Name("comments");
            Map(m => m.V).Name("v");
            Map(m => m.RA).Name("ra");
            Map(m => m.Dec).Name("dec");
            Map(m => m.epoch).Name("epoch").Default(0);
            Map(m => m.period).Name("period").Default(0);
            Map(m => m.amplitude).Name("amplitude").Default(1);
            Map(m => m.OCRange).Name("ocrange").Default(0);
            Map(m => m.observedPhase).Name("phase").Default(0);
        }
    }

    public sealed class AavsoVarStarMap : ClassMap<AavsoDTO> {

        public AavsoVarStarMap() {
            Map(m => m.Name).Name("Star Name");
            Map(m => m.Comments).Name("Notes");
            Map(m => m.MaxMag).Name("Max Mag");
            Map(m => m.MinMag).Name("Min Mag");
            Map(m => m.RA).Name("RA (J2000.0)");
            Map(m => m.Dec).Name("Dec (J2000.0)");
            Map(m => m.Period).Name("Period (d)");
        }
    }

    public sealed class AavsoDTO {
        public string Name;
        public string Comments;
        public string MinMag;
        public string MaxMag;
        public string RA;
        public string Dec;
        public string Period;

        public VariableStar AsVariableStar() {
            VariableStar newStar = new VariableStar {
                Name = Name,
                Comments = Comments,
                RA = RA,
                Dec = Dec
            };


            if (Double.TryParse(Period, out double period)) {
                newStar.period = period;
            } else {
                newStar.period = 0;
            }
            return newStar;
        }
    }
}