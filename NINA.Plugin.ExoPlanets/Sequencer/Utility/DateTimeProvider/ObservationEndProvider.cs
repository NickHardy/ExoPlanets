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
using NINA.Astrometry.Interfaces;
using NINA.Core.Utility;
using NINA.Sequencer;
using NINA.Sequencer.Utility.DateTimeProvider;
using System;

namespace NINA.Plugin.ExoPlanets.Sequencer.Utility.DateTimeProvider {

    [JsonObject(MemberSerialization.OptIn)]
    public class ObservationEndProvider : IDateTimeProvider {
        private readonly INighttimeCalculator nighttimeCalculator;

        public ObservationEndProvider(INighttimeCalculator nighttimeCalculator) {
            this.nighttimeCalculator = nighttimeCalculator;
        }

        public string Name { get; } = "End observation"; //Loc.Instance["LblMeridian"];
        public ICustomDateTime DateTime { get; set; } = new SystemDateTime();

        public DateTime GetDateTime(ISequenceEntity context) {
            var exoPlanetDSO = ItemUtility.RetrieveExoPlanetDSO(context?.Parent);
            if (exoPlanetDSO != null) {
                return new DateTime(Math.Max(exoPlanetDSO.ObservationEnd.Ticks, DateTime.Now.Ticks));
            }
            return DateTime.Now;
        }

        public TimeOnly GetRolloverTime(ISequenceEntity context) {
            var dawn = nighttimeCalculator.Calculate().SunRiseAndSet.Rise;
            if (!dawn.HasValue || (this.GetDateTime(context) > dawn.Value)) {
                return new TimeOnly(12, 0, 0);
            }
            return TimeOnly.FromDateTime(dawn.Value);
        }
    }
}