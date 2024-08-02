#region "copyright"

/*
    Copyright © 2016 - 2021 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Accord.Math;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Newtonsoft.Json;
using NINA.Astrometry;
using System;

namespace NINA.Plugin.ExoPlanets.Model {

    [JsonObject(MemberSerialization.OptIn)]
    public class PandoraStar {

        [JsonProperty]
        public string Star { get; set; }

        [JsonProperty]
        public string Planet { get; set; }

        [JsonProperty]
        public double Ra { get; set; }

        [JsonProperty]
        public double Dec { get; set; }

        [JsonProperty]
        public double Vmag { get; set; }

        [JsonProperty]
        public double Jmag { get; set; }

        [JsonProperty]
        public string Spectral_Type { get; set; }

        [JsonProperty]
        public double RotationPeriod { get; set; }

        [JsonProperty]
        public double PlateRadius { get; set; }

        public Coordinates Coordinates() {
            return new Coordinates(Angle.ByDegree(Ra), Angle.ByDegree(Dec), Epoch.J2000);
        }
    }

    public sealed class PandoraStarMap : ClassMap<PandoraStar> {

        public PandoraStarMap() {
            Map(m => m.Star).Name("Star");
            Map(m => m.Planet).Name("Planet");
            Map(m => m.Ra).Name("RA");
            Map(m => m.Dec).Name("Dec");
            Map(m => m.Vmag).Name("Vmag");
            Map(m => m.Jmag).Name("Jmag");
            Map(m => m.Spectral_Type).Name("Spectral Type");
            Map(m => m.RotationPeriod).Name("Star Rotation Period [d]").Default(0);
            Map(m => m.PlateRadius).Name("Planet Radius [R_Earth]").Default(0);
        }


    }

}