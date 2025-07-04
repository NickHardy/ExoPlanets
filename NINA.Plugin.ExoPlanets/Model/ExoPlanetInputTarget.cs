﻿#region "copyright"

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

namespace NINA.Plugin.ExoPlanets.Model {

    [JsonObject(MemberSerialization.OptIn)]
    public class ExoPlanetInputTarget(Angle latitude, Angle longitude, CustomHorizon horizon) : InputTarget(latitude, longitude, horizon) {
        private ExoPlanetDeepSkyObject exoPlanetdeepSkyObject;

        public ExoPlanetDeepSkyObject ExoPlanetDeepSkyObject {
            get => exoPlanetdeepSkyObject;
            set {
                exoPlanetdeepSkyObject = value;
                RaisePropertyChanged();
            }
        }
    }
}