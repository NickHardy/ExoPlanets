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
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Utility.DateTimeProvider;
using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using NINA.Sequencer.Conditions;
using NINA.Plugin.ExoPlanets.Sequencer.Utility.DateTimeProvider;
using NINA.Plugin.ExoPlanets.Model;
using NINA.Plugin.ExoPlanets.Sequencer.Utility;
using NINA.Astrometry.Interfaces;
using TimeProvider = NINA.Sequencer.Utility.DateTimeProvider.TimeProvider;

namespace NINA.Plugin.ExoPlanets.Sequencer.Conditions {

    [ExportMetadata("Name", "Loop Until Transit Observation Time")]
    [ExportMetadata("Description", "Lbl_SequenceCondition_TimeCondition_Description")]
    [ExportMetadata("Icon", "ClockSVG")]
    [ExportMetadata("Category", "ExoPlanet")]
    [Export(typeof(ISequenceCondition))]
    [JsonObject(MemberSerialization.OptIn)]
    public class TransitCondition : SequenceCondition {
        private IList<IDateTimeProvider> dateTimeProviders;
        private int hours;
        private int minutes;
        private int minutesOffset;
        private int seconds;
        private IDateTimeProvider selectedProvider;
        private INighttimeCalculator nighttimeCalculator;

        [ImportingConstructor]
        public TransitCondition(IList<IDateTimeProvider> dateTimeProviders, INighttimeCalculator nighttimeCalculator) {
            this.nighttimeCalculator = nighttimeCalculator;
            DateTime = new SystemDateTime();
            DateTimeProviders = dateTimeProviders;
            if (DateTimeProviders.Where(d => d is ObservationStartProvider).Count() == 0)
                DateTimeProviders.Add(new ObservationStartProvider(nighttimeCalculator));
            if (DateTimeProviders.Where(d => d is ObservationEndProvider).Count() == 0)
                DateTimeProviders.Add(new ObservationEndProvider(nighttimeCalculator));
            this.SelectedProvider = DateTimeProviders.FirstOrDefault(d => d is ObservationEndProvider);
            ConditionWatchdog = new ConditionWatchdog(() => { Tick(); return Task.CompletedTask; }, TimeSpan.FromSeconds(1));
        }

        public TransitCondition(IList<IDateTimeProvider> dateTimeProviders, IDateTimeProvider selectedProvider) {
            DateTime = new SystemDateTime();
            this.DateTimeProviders = dateTimeProviders;
            this.SelectedProvider = selectedProvider;
            ConditionWatchdog = new ConditionWatchdog(() => { Tick(); return Task.CompletedTask; }, TimeSpan.FromSeconds(1));
        }

        public ICustomDateTime DateTime { get; set; }

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
                RaisePropertyChanged(nameof(RemainingTime));
            }
        }

        [JsonProperty]
        public int Minutes {
            get => minutes;
            set {
                minutes = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(RemainingTime));
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

        public TimeSpan RemainingTime {
            get {
                TimeSpan remaining = (CalculateRemainingTime() - DateTime.Now);
                if (remaining.TotalSeconds < 0) return new TimeSpan(0);
                return remaining;
            }
        }

        [JsonProperty]
        public int Seconds {
            get => seconds;
            set {
                seconds = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(RemainingTime));
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

        private DateTime CalculateRemainingTime() {
            var now = DateTime.Now;
            var then = new DateTime(now.Year, now.Month, now.Day, Hours, Minutes, Seconds);

            //In case it is 22:00:00 but you want to wait until 01:00:00 o'clock a day of 1 needs to be added
            if (now.Hour > 12 && then.Hour < 12) {
                then = then.AddDays(1);
            }

            return then;
        }

        private void Tick() {
            RaisePropertyChanged(nameof(RemainingTime));
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
            RunWatchdogIfInsideSequenceRoot();
        }

        public override bool Check(ISequenceItem previousItem, ISequenceItem nextItem) {
            return DateTime.Now + (nextItem?.GetEstimatedDuration() ?? TimeSpan.Zero) <= CalculateRemainingTime();
        }

        public override object Clone() {
            return new TransitCondition(DateTimeProviders, SelectedProvider) {
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

        private IList<string> issues = new List<string>();

        public IList<string> Issues { get => issues; set { issues = value; RaisePropertyChanged(); } }

        public bool Validate() {
            var i = new List<string>();

            ExoPlanetDeepSkyObject exoPlanetDSO = ItemUtility.RetrieveExoPlanetDSO(this.Parent);
            if (exoPlanetDSO == null) {
                i.Add("This instruction must be used within the ExoPlanet or VariableStar object container.");
            } else {
                if (exoPlanetDSO.ObservationEnd == null) {
                    i.Add("You must select a target from the list.");
                }
            }

            Issues = i;
            return i.Count == 0;
        }

        public override string ToString() {
            return $"Condition: {nameof(TransitCondition)}, Time: {Hours}:{Minutes}:{Seconds}h";
        }
    }
}