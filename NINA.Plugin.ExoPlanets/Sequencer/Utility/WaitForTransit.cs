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
using NINA.Astrometry.Interfaces;
using NINA.Core.Model;
using NINA.Plugin.ExoPlanets.Model;
using NINA.Plugin.ExoPlanets.Sequencer.Utility.DateTimeProvider;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Utility.DateTimeProvider;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TimeProvider = NINA.Sequencer.Utility.DateTimeProvider.TimeProvider;

namespace NINA.Plugin.ExoPlanets.Sequencer.Utility {

    [ExportMetadata("Name", "Wait for Transit Observation Time")]
    [ExportMetadata("Description", "Lbl_SequenceItem_Utility_WaitForTime_Description")]
    [ExportMetadata("Icon", "ClockSVG")]
    [ExportMetadata("Category", "ExoPlanet")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class WaitForTransit : SequenceItem {
        private IList<IDateTimeProvider> dateTimeProviders;
        private int hours;
        private int minutes;
        private int minutesOffset;
        private int seconds;
        private IDateTimeProvider selectedProvider;
        private readonly INighttimeCalculator nighttimeCalculator;

        [ImportingConstructor]
        public WaitForTransit(IList<IDateTimeProvider> dateTimeProviders, INighttimeCalculator nighttimeCalculator) {
            this.nighttimeCalculator = nighttimeCalculator;
            DateTimeProviders = dateTimeProviders;
            if (!DateTimeProviders.Where(d => d is ObservationStartProvider).Any())
                DateTimeProviders.Add(new ObservationStartProvider(nighttimeCalculator));
            if (!DateTimeProviders.Where(d => d is ObservationEndProvider).Any())
                DateTimeProviders.Add(new ObservationEndProvider(nighttimeCalculator));
            SelectedProvider = DateTimeProviders.FirstOrDefault(d => d is ObservationStartProvider);
        }

        public WaitForTransit(IList<IDateTimeProvider> dateTimeProviders, IDateTimeProvider selectedProvider) {
            DateTimeProviders = dateTimeProviders;
            SelectedProvider = selectedProvider;
        }

        public IList<IDateTimeProvider> DateTimeProviders {
            get => dateTimeProviders;
            set {
                dateTimeProviders = value;
                RaisePropertyChanged();
            }
        }

        public bool HasFixedTimeProvider {
            get {
                return selectedProvider != null && selectedProvider is not TimeProvider;
            }
        }

        [JsonProperty]
        public int Hours {
            get => hours;
            set {
                hours = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public int Minutes {
            get => minutes;
            set {
                minutes = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public int MinutesOffset {
            get => minutesOffset;
            set {
                minutesOffset = value;
                UpdateTime();
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public int Seconds {
            get => seconds;
            set {
                seconds = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public IDateTimeProvider SelectedProvider {
            get => selectedProvider;
            set {
                selectedProvider = value;
                if (selectedProvider != null) {
                    UpdateTime();
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(HasFixedTimeProvider));
                }
            }
        }

        private void UpdateTime() {
            if (HasFixedTimeProvider) {
                var t = SelectedProvider.GetDateTime(this) + TimeSpan.FromMinutes(MinutesOffset);
                Hours = t.Hour;
                Minutes = t.Minute;
                Seconds = t.Second;
            }
        }

        public override void AfterParentChanged() {
            UpdateTime();
            Validate();
        }

        public override object Clone() {
            return new WaitForTransit(DateTimeProviders, SelectedProvider) {
                Icon = Icon,
                Hours = Hours,
                Minutes = Minutes,
                Seconds = Seconds,
                MinutesOffset = MinutesOffset,
                Name = Name,
                Category = Category,
                Description = Description,
            };
        }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            return NINA.Core.Utility.CoreUtil.Wait(GetEstimatedDuration(), token, progress);
        }

        public override TimeSpan GetEstimatedDuration() {
            var now = DateTime.Now;
            var then = new DateTime(now.Year, now.Month, now.Day, Hours, Minutes, Seconds);

            if (now.Hour <= 12 && then.Hour > 12) {
                then = then.AddDays(-1);
            }

            //In case it is 22:00:00 but you want to wait until 01:00:00 o'clock a day of 1 needs to be added
            if (now.Hour > 12 && then.Hour < 12) {
                then = then.AddDays(1);
            }

            return then - DateTime.Now;
        }

        private IList<string> issues = new List<string>();

        public IList<string> Issues { get => issues; set { issues = value; RaisePropertyChanged(); } }

        public bool Validate() {
            var i = new List<string>();

            ExoPlanetDeepSkyObject exoPlanetDSO = ItemUtility.RetrieveExoPlanetDSO(this.Parent);
            if (exoPlanetDSO == null) {
                i.Add("This instruction must be used within the ExoPlanet or VariableStar object container.");
            } else {
                if (exoPlanetDSO.ObservationStart == null) {
                    i.Add("You must select a target from the list.");
                }
            }

            Issues = i;
            return i.Count == 0;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(WaitForTransit)}, Time: {Hours}:{Minutes}:{Seconds}h";
        }
    }
}