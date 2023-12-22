using NINA.Plugin.ExoPlanets.Model;

namespace NINA.Plugin.ExoPlanets.Interfaces {

    public interface IVariableBrightnessTargetContainer {
        ExoPlanetDeepSkyObject ExoPlanetDSO { get; set; }
        ExoPlanetInputTarget ExoPlanetInputTarget { get; set; }
    }
}