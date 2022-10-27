using CsvHelper.Configuration;
using Newtonsoft.Json;
using NINA.Image.ImageAnalysis;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NINA.Plugin.ExoPlanets.Model {
    public class DetectedExoStar {
        public DetectedExoStar(DetectedStar ds, ExoPlanetInputTarget target) {
            this.type = "Target";
            this.HFR = ds.HFR;
            this.Position = ds.Position;
            this.AverageBrightness = ds.AverageBrightness;
            this.MaxBrightness = ds.MaxBrightness;
            this.Background = ds.Background;
            this.name = target.TargetName;
            this.ra = target.InputCoordinates.Coordinates.RADegrees;
            this.dec = target.InputCoordinates.Coordinates.Dec;
        }
        public DetectedExoStar(DetectedStar ds, VSXObject vstar) {
            this.type = "Var";
            this.HFR = ds.HFR;
            this.Position = ds.Position;
            this.AverageBrightness = ds.AverageBrightness;
            this.MaxBrightness = ds.MaxBrightness;
            this.Background = ds.Background;
            this.name = vstar.Name;
            this.ra = vstar.Coordinates().RADegrees;
            this.dec = vstar.Coordinates().Dec;
        }
        public DetectedExoStar(DetectedStar ds, SimbadCompStar cstar) {
            this.type = "Comp1";
            this.HFR = ds.HFR;
            this.Position = ds.Position;
            this.AverageBrightness = ds.AverageBrightness;
            this.MaxBrightness = ds.MaxBrightness;
            this.Background = ds.Background;
            this.name = cstar.main_id;
            this.ra = cstar.ra;
            this.dec = cstar.dec;
        }
        public DetectedExoStar(DetectedStar ds, ComparisonStar cstar) {
            this.type = "Comp2";
            this.HFR = ds.HFR;
            this.Position = ds.Position;
            this.AverageBrightness = ds.AverageBrightness;
            this.MaxBrightness = ds.MaxBrightness;
            this.Background = ds.Background;
            this.name = cstar.auid;
            this.ra = cstar.Coordinates().RADegrees;
            this.dec = cstar.Coordinates().Dec;
        }

        public string name { get; set; }
        public string type { get; set; }
        public double HFR { get; set; }
        public Accord.Point Position { get; set; }
        public double AverageBrightness { get; set; }
        public double MaxBrightness { get; set; }
        public double Background { get; set; }
        public double ra { get; set; }
        public double dec { get; set; }
    }
    public sealed class DetectedStarMap : ClassMap<DetectedExoStar> {

        public DetectedStarMap() {
            Map(m => m.type).Name("Type").Index(0).Optional().Default("");
            Map(m => m.name).Name("Name").Index(1).Optional().Default("");
            Map(m => m.HFR).Name("HFR").Index(2).Optional().Default(0);
            Map(m => m.Position.X).Name("xPos").Index(3).Optional().Default(0);
            Map(m => m.Position.Y).Name("yPos").Index(4).Optional().Default(0);
            Map(m => m.AverageBrightness).Name("AvgBright").Index(5).Optional().Default(0);
            Map(m => m.MaxBrightness).Name("MaxBright").Index(6).Optional().Default(0);
            Map(m => m.Background).Name("Background").Index(7).Optional().Default(0);
            Map(m => m.ra).Name("Ra").Index(8).Optional().Default(0);
            Map(m => m.dec).Name("Dec").Index(9).Optional().Default(0);
        }
    }
}
