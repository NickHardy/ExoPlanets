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
using NINA.Core.Model;
using NINA.Astrometry;
using CsvHelper.Configuration;
using System;

namespace NINA.Plugin.ExoPlanets.Model {

    [JsonObject(MemberSerialization.OptIn)]
    public class ExoPlanet{
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
        public string coords { get; set; }
        [JsonProperty]
        public double depth { get; set; }
        [JsonProperty]
        public int pto { get; set; }
        [JsonProperty]
        public int pbo { get; set; }
        [JsonProperty]
        public double pbto { get { return pto + pbo + depth; } }
        
        public Coordinates Coordinates() {
            string RaString = this.coords.Split(' ')[0];
            string DecString = this.coords.Split(' ')[1];
            return new Coordinates(Angle.ByDegree(AstroUtil.HMSToDegrees(RaString)), Angle.ByDegree(AstroUtil.DMSToDegrees(DecString)), Epoch.J2000);
        }


        [JsonProperty]
        public double Altitude { get; set; }
        [JsonProperty]
        public double Azimuth { get; set; }

        public void CalculateAltAz(double latitude, double longitude) {
            var siderealTime = AstroUtil.GetLocalSiderealTime(midTime, longitude);
            var hourAngle = AstroUtil.GetHourAngle(siderealTime, Coordinates().RA);

            var degAngle = AstroUtil.HoursToDegrees(hourAngle);
            Altitude = AstroUtil.GetAltitude(degAngle, latitude, Coordinates().Dec);
            Azimuth = AstroUtil.GetAzimuth(degAngle, Altitude, latitude, Coordinates().Dec);
        }

    }

    public sealed class ExoPlanetMap : ClassMap<ExoPlanet> {

        public ExoPlanetMap() {
            Map(m => m.Name).Name("Name");
            Map(m => m.Comments).Name("comments");
            Map(m => m.V).Name("V");
            Map(m => m.startTime).Name("start time");
            Map(m => m.midTime).Name("mid time");
            Map(m => m.endTime).Name("end time");
            Map(m => m.jd_start).Name("jd_start");
            Map(m => m.jd_mid).Name("jd_mid");
            Map(m => m.jd_end).Name("jd_end");
            Map(m => m.coords).Name("coords(J2000)");
            Map(m => m.depth).Name("depth(ppt)");
            Map(m => m.pto).Name("percent_transit_observable");
            Map(m => m.pbo).Name("percent_baseline_observable");
        }
    }
}