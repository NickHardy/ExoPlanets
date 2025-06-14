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
using System.Collections.Generic;

namespace NINA.Plugin.ExoPlanets.Model {

    [JsonObject(MemberSerialization.OptIn)]
    public class SimbadCompStar {

        public SimbadCompStar(List<object> obj) {
            this.main_id = (string)obj[0];
            this.b = Convert.ToDouble(obj[1]);
            this.v = Convert.ToDouble(obj[2]);
            this.r = Convert.ToDouble(obj[3]);
            this.ra = Convert.ToDouble(obj[4]);
            this.dec = Convert.ToDouble(obj[5]);
        }

        [JsonProperty]
        public string main_id { get; set; }

        [JsonProperty]
        public double b { get; set; }

        [JsonProperty]
        public double v { get; set; }

        [JsonProperty]
        public double r { get; set; }

        [JsonProperty]
        public double ra { get; set; }

        [JsonProperty]
        public double dec { get; set; }

        public Coordinates Coordinates() {
            return new Coordinates(Angle.ByDegree(ra), Angle.ByDegree(dec), Epoch.J2000);
        }
    }

    public sealed class SimbadCompStarMap : ClassMap<SimbadCompStar> {

        public SimbadCompStarMap() {
            Map(m => m.main_id).Name("main_id").Optional().Default("");
            Map(m => m.b).Name("b").Optional().Default(0);
            Map(m => m.v).Name("v").Optional().Default(0);
            Map(m => m.r).Name("r").Optional().Default(0);
            Map(m => m.ra).Name("ra").Optional().Default(0);
            Map(m => m.dec).Name("dec").Optional().Default(0);
        }
    }
}