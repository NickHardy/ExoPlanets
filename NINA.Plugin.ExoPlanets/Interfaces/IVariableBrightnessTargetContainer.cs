using NINA.Plugin.ExoPlanets.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NINA.Plugin.ExoPlanets.Interfaces
{
    public interface IVariableBrightnessTargetContainer
    {
        ExoPlanetDeepSkyObject ExoPlanetDSO { get; set; }
        ExoPlanetInputTarget ExoPlanetInputTarget { get; set; }
    }
}
