using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using NINA.Core.Enum;
using NINA.Core.Utility;
using NINA.Plugin.ExoPlanets.Model;
using NINA.Plugin.ExoPlanets.Sequencer.Utility;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.SequenceItem;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace NINA.Plugin.ExoPlanets.Sequencer.Conditions {

    [ExportMetadata("Name", "Loop for iterations during transit")]
    [ExportMetadata("Description", "Loops a certain amount of iterations during the transit")]
    [ExportMetadata("Icon", "LoopSVG")]
    [ExportMetadata("Category", "ExoPlanet")]
    [Export(typeof(ISequenceCondition))]
    [JsonObject(MemberSerialization.OptIn)]
    public partial class DuringTransitCondition : SequenceCondition {

        [ImportingConstructor]
        public DuringTransitCondition() {
            Iterations = 1;
            ConditionWatchdog = new ConditionWatchdog(InterruptWhenTimeIsUp, TimeSpan.FromSeconds(1));
        }

        private async Task InterruptWhenTimeIsUp() {
            Tick();
            if (!Check(null, null)) {
                if (this.Parent != null) {
                    if (NINA.Sequencer.Utility.ItemUtility.IsInRootContainer(Parent) && this.Parent.Status == SequenceEntityStatus.RUNNING && this.Status != SequenceEntityStatus.DISABLED) {
                        Logger.Info("Time not during transit - Interrupting current Instruction Set");
                        Status = SequenceEntityStatus.FINISHED;
                        await this.Parent.Interrupt();
                    }
                }
            }
        }

        private void Tick() {
            RaisePropertyChanged(nameof(DuringTransit));
        }

        [ObservableProperty]
        private int completedIterations;

        [ObservableProperty]
        private int iterations;

        [ObservableProperty]
        private bool duringTransit;

        [ObservableProperty]
        private DateTime observationStart;

        [ObservableProperty]
        private DateTime observationEnd;


        public override void AfterParentChanged() {
            Validate();
            RunWatchdogIfInsideSequenceRoot();
        }

        private bool CheckTime(ISequenceItem nextItem) {
            DuringTransit = ObservationStart < DateTime.Now && DateTime.Now < ObservationEnd;
            if (nextItem != null) {
                DuringTransit = DateTime.Now + nextItem.GetEstimatedDuration() < ObservationEnd;
            }
            return DuringTransit;
        }

        public override bool Check(ISequenceItem previousItem, ISequenceItem nextItem) {
            if (!CheckTime(nextItem)) {
                return false;
            }
            var check = CompletedIterations < Iterations;
            if (!check && IsActive()) {
                Logger.Info($"{nameof(LoopCondition)} finished. Iterations: {CompletedIterations} / {Iterations}");
            }
            return check;
        }

        public override void ResetProgress() {
            CompletedIterations = 0;
            Status = Core.Enum.SequenceEntityStatus.CREATED;
        }

        public override void SequenceBlockFinished() {
            CompletedIterations++;
        }

        public override object Clone() {
            return new DuringTransitCondition() {
                Icon = Icon,
                Name = Name,
                Category = Category,
                Description = Description,
                Iterations = Iterations
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
                ObservationStart = exoPlanetDSO.ObservationStart;
                ObservationEnd = exoPlanetDSO.ObservationEnd;
            }

            Issues = i;
            return i.Count == 0;
        }

        public override string ToString() {
            return $"Condition: {nameof(DuringTransitCondition)}, Time valid: {CheckTime(null)} Iterations: {CompletedIterations} / {Iterations}";
        }
    }
}