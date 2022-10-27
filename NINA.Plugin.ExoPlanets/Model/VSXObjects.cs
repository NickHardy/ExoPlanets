using NINA.Astrometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NINA.Plugin.ExoPlanets.Model {
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class Root {
        public VSXObjects VSXObjects { get; set; }
    }

    public class VSXObject {
        public string Name { get; set; }
        public string AUID { get; set; }
        public string RA2000 { get; set; }
        public string Declination2000 { get; set; }
        public string ProperMotionRA { get; set; }
        public string ProperMotionDec { get; set; }
        public string VariabilityType { get; set; }
        public string Period { get; set; }
        public string Epoch { get; set; }
        public string RiseDuration { get; set; }
        public string MaxMag { get; set; }
        public string MinMag { get; set; }
        public string Category { get; set; }
        public string OID { get; set; }
        public string Constellation { get; set; }
        public string EclipseDuration { get; set; }
        public string SpectralType { get; set; }
        public string Discoverer { get; set; }

        public Coordinates Coordinates() {
            double Ra = double.Parse(RA2000);
            double Dec = double.Parse(Declination2000);
            return new Coordinates(Angle.ByDegree(Ra), Angle.ByDegree(Dec), Astrometry.Epoch.J2000);
        }
    }

    public class VSXObjects {
        public List<VSXObject> VSXObject { get; set; }
    }


}
