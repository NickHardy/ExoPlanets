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
using System.Collections.Generic;

namespace NINA.Plugin.ExoPlanets.Model {

    [JsonObject(MemberSerialization.OptIn)]
    public class ComparisonStarChart {
        [JsonProperty]
        public string chartid { get; set; }
        [JsonProperty]
        public string image_uri { get; set; }
        [JsonProperty]
        public string star { get; set; }
        [JsonProperty]
        public decimal fov { get; set; }
        [JsonProperty]
        public decimal maglimit { get; set; }
        [JsonProperty]
        public string title { get; set; }
        [JsonProperty]
        public string comment { get; set; }
        [JsonProperty]
        public decimal resolution { get; set; }
        [JsonProperty]
        public bool dss { get; set; }
        [JsonProperty]
        public string special { get; set; }

        [JsonProperty]
        public IList<ComparisonStar> photometry { get; set; }

        [JsonProperty]
        public string ra { get; set; }
        [JsonProperty]
        public string dec { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ComparisonStar {
        [JsonProperty]
        public string auid { get; set; }
        [JsonProperty]
        public string ra { get; set; }
        [JsonProperty]
        public string dec { get; set; }
        [JsonProperty]
        public decimal label { get; set; }

        [JsonProperty]
        public IList<BandDetail> bands { get; set; }

        [JsonProperty]
        public string comments { get; set; }

        public Coordinates Coordinates() {
            return new Coordinates(Angle.ByDegree(AstroUtil.HMSToDegrees(ra)), Angle.ByDegree(AstroUtil.DMSToDegrees(dec)), Epoch.J2000);
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class BandDetail {
        [JsonProperty]
        public string band { get; set; }
        [JsonProperty]
        public decimal mag { get; set; }
        [JsonProperty]
        public decimal error { get; set; }
    }
}