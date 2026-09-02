using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NINA.Astrometry;
using NINA.Astrometry.Interfaces;
using NINA.Core.Model;
using NINA.Equipment.Interfaces;
using NINA.Plugin.ExoPlanets.Sequencer.Container;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using NUnit.Framework;
using OxyPlot.Axes;
using System;
using System.Linq;

namespace NINA.Plugin.ExoPlanets.Test {

    /// <summary>
    /// A saved sequence stores the altitude chart's deep sky object. Reloading it on a later night
    /// must not leave the chart plotting the night it was saved on, nor the altitudes of an
    /// observer at latitude 0 (the observer position lives in protected fields that are never
    /// serialized).
    /// </summary>
    [TestFixture]
    public class ObjectContainerReloadTest {

        // R Cas: a real variable star, far enough north that its maximum altitude differs sharply
        // between the observing site (84°) and the equator (39°). That gap is what tells us whether
        // the reloaded chart was recalculated for the profile's location.
        private static readonly Coordinates TargetCoordinates =
            new Coordinates(Angle.ByHours(23.9733333), Angle.ByDegree(51.3886111), Epoch.J2000);

        private const double Latitude = 45.5;
        private const double Longitude = 9.2;
        private const double ExpectedMaxAltitude = 90d - (51.3886111 - Latitude);

        private Mock<IProfileService> profileServiceMock;
        private Mock<INighttimeCalculator> nighttimeCalculatorMock;
        private Mock<IFramingAssistantVM> framingAssistantMock;
        private Mock<IApplicationMediator> applicationMediatorMock;
        private Mock<IPlanetariumFactory> planetariumFactoryMock;

        [SetUp]
        public void Setup() {
            var astrometrySettingsMock = new Mock<IAstrometrySettings>();
            astrometrySettingsMock.SetupGet(x => x.Latitude).Returns(Latitude);
            astrometrySettingsMock.SetupGet(x => x.Longitude).Returns(Longitude);
            astrometrySettingsMock.SetupGet(x => x.Horizon).Returns((CustomHorizon)null);

            var profileMock = new Mock<IProfile>();
            profileMock.SetupGet(x => x.AstrometrySettings).Returns(astrometrySettingsMock.Object);

            profileServiceMock = new Mock<IProfileService>();
            profileServiceMock.SetupGet(x => x.ActiveProfile).Returns(profileMock.Object);

            nighttimeCalculatorMock = new Mock<INighttimeCalculator>();
            framingAssistantMock = new Mock<IFramingAssistantVM>();
            applicationMediatorMock = new Mock<IApplicationMediator>();
            planetariumFactoryMock = new Mock<IPlanetariumFactory>();
        }

        private ExoPlanetObjectContainer CreateExoPlanetContainer() {
            return new ExoPlanetObjectContainer(
                profileServiceMock.Object,
                nighttimeCalculatorMock.Object,
                framingAssistantMock.Object,
                applicationMediatorMock.Object,
                planetariumFactoryMock.Object);
        }

        private VariableStarObjectContainer CreateVariableStarContainer() {
            return new VariableStarObjectContainer(
                profileServiceMock.Object,
                nighttimeCalculatorMock.Object,
                framingAssistantMock.Object,
                applicationMediatorMock.Object,
                planetariumFactoryMock.Object);
        }

        /// <summary>The night the sequence was saved on: half a year before it is loaded again.</summary>
        private static DateTime SavedNight() => NighttimeCalculator.GetReferenceDate(DateTime.Now.AddDays(-180));

        /// <summary>Builds the chart object the same way the plugin does when a target is loaded.</summary>
        private static Model.ExoPlanetDeepSkyObject BuildDsoForNight(DateTime night) {
            var dso = new Model.ExoPlanetDeepSkyObject("R Cas", TargetCoordinates, string.Empty, null);
            dso.SetDateAndPosition(night, Latitude, Longitude);
            return dso;
        }

        /// <summary>
        /// Mirrors what NINA does when a sequence file is opened: a fresh container comes out of the
        /// sequencer factory and the saved json is populated into it.
        /// </summary>
        private static T SaveAndReload<T>(T container, Func<T> createEmpty) {
            var json = JsonConvert.SerializeObject(container, Formatting.Indented, new JsonSerializerSettings {
                TypeNameHandling = TypeNameHandling.All,
                PreserveReferencesHandling = PreserveReferencesHandling.All
            });

            var reloaded = createEmpty();
            var serializer = JsonSerializer.Create(new JsonSerializerSettings());
            serializer.Populate(JObject.Parse(json).CreateReader(), reloaded);
            return reloaded;
        }

        [Test]
        public void ExoPlanetContainer_ReloadedOnALaterNight_RecalculatesTheChartForTonightAndTheProfileLocation() {
            var container = CreateExoPlanetContainer();
            container.Target.TargetName = "R Cas";
            container.Target.InputCoordinates.Coordinates = TargetCoordinates;
            container.ExoPlanetDSO = BuildDsoForNight(SavedNight());
            Assume.That(container.ExoPlanetDSO.Altitudes, Is.Not.Empty);

            var reloaded = SaveAndReload(container, CreateExoPlanetContainer);

            AssertChartIsUsableTonight(reloaded.ExoPlanetDSO);
        }

        [Test]
        public void VariableStarContainer_ReloadedOnALaterNight_RecalculatesTheChartForTonightAndTheProfileLocation() {
            var container = CreateVariableStarContainer();
            container.Target.TargetName = "R Cas";
            container.Target.InputCoordinates.Coordinates = TargetCoordinates;
            container.ExoPlanetDSO = BuildDsoForNight(SavedNight());
            Assume.That(container.ExoPlanetDSO.Altitudes, Is.Not.Empty);

            var reloaded = SaveAndReload(container, CreateVariableStarContainer);

            AssertChartIsUsableTonight(reloaded.ExoPlanetDSO);
        }

        private static void AssertChartIsUsableTonight(Model.ExoPlanetDeepSkyObject dso) {
            Assert.Multiple(() => {
                Assert.That(dso.Coordinates, Is.Not.Null, "the target coordinates must survive the reload");
                Assert.That(dso.Coordinates.RADegrees, Is.EqualTo(TargetCoordinates.RADegrees).Within(1e-6));
                Assert.That(dso.Coordinates.Dec, Is.EqualTo(TargetCoordinates.Dec).Within(1e-6));

                Assert.That(dso.ReferenceDate, Is.EqualTo(NighttimeCalculator.GetReferenceDate(DateTime.Now)),
                    "the chart must be drawn for the night the sequence is loaded on, not the night it was saved on");

                Assert.That(dso.Altitudes, Is.Not.Empty);

                // The saved altitudes are restored from the json as well, so it is not enough for the
                // curve to be non empty: every sample has to belong to tonight, otherwise the chart
                // plots points the axis will never show.
                var tonight = NighttimeCalculator.GetReferenceDate(DateTime.Now);
                var samples = dso.Altitudes.Select(point => DateTimeAxis.ToDateTime(point.X)).ToList();
                Assert.That(samples, Is.All.InRange(tonight, tonight.AddHours(24.1)),
                    "every altitude sample must belong to the night the sequence is loaded on");

                Assert.That(dso.MaxAltitude.Y, Is.EqualTo(ExpectedMaxAltitude).Within(1.0),
                    "the altitudes must be recalculated for the profile's latitude, not for latitude 0");
            });
        }
    }
}
