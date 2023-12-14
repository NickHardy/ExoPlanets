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
using NINA.Profile.Interfaces;
using NINA.Core.Utility;
using NINA.Astrometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NINA.Core.Locale;
using NINA.Sequencer.Utility.DateTimeProvider;
using NINA.Sequencer;
using NINA.Astrometry.Interfaces;

namespace NINA.Plugin.ExoPlanets.Sequencer.Utility.DateTimeProvider {

    [JsonObject(MemberSerialization.OptIn)]
    public class ObservationStartProvider(INighttimeCalculator nighttimeCalculator) : IDateTimeProvider {
        private readonly INighttimeCalculator nighttimeCalculator = nighttimeCalculator;

        public string Name { get; } = "Start observation"; //Loc.Instance["LblMeridian"];
        public ICustomDateTime DateTime { get; set; } = new SystemDateTime();

        public DateTime GetDateTime(ISequenceEntity context) {
            var exoPlanetDSO = ItemUtility.RetrieveExoPlanetDSO(context?.Parent);
            if (exoPlanetDSO != null) {
                return new DateTime(Math.Max(exoPlanetDSO.ObservationStart.Ticks, DateTime.Now.Ticks));
            }
            return DateTime.Now;
        }

        public TimeOnly GetRolloverTime(ISequenceEntity context) {
            var dawn = nighttimeCalculator.Calculate().SunRiseAndSet.Rise;
            if (!dawn.HasValue) {
                return new TimeOnly(12, 0, 0);
            }
            return TimeOnly.FromDateTime(dawn.Value);
        }
    }
}