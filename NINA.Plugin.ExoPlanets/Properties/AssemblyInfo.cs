using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
//[assembly: AssemblyTitle("ExoPlanets")]
[assembly: AssemblyDescription("A plugin to help get exoplanet or variable star data.")]
//[assembly: AssemblyConfiguration("")]

//Your name
//[assembly: AssemblyCompany("Nick Hardy & Rafa Barbera")]
//The product name that this plugin is part of
//[assembly: AssemblyProduct("NINA Plugin ExoPlanets")]
[assembly: AssemblyCopyright("Copyright © 2023")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("6d0e07f2-8773-4229-dc2b-f451e53c677f")]

//The assembly versioning
//Should be incremented for each new release build of a plugin
//[assembly: AssemblyVersion("2.1.5.0")]
//[assembly: AssemblyFileVersion("2.1.5.0")]

//The minimum Version of N.I.N.A. that this plugin is compatible with
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.1.2.9001")]

//Your plugin homepage - omit if not applicaple
[assembly: AssemblyMetadata("Homepage", "https://nighttime-imaging.eu/")]
//The license your plugin code is using
[assembly: AssemblyMetadata("License", "MPL-2.0")]
//The url to the license
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
//The repository where your pluggin is hosted
[assembly: AssemblyMetadata("Repository", "https://github.com/NickHardy/ExoPlanets")]

//Common tags that quickly describe your plugin
[assembly: AssemblyMetadata("Tags", "ExoPlanet,VariableStar,Sequencer")]

//The featured logo that will be displayed in the plugin list next to the name
[assembly: AssemblyMetadata("FeaturedImageURL", "https://github.com/NickHardy/ExoPlanets/blob/main/NINA.Plugin.ExoPlanets/resources/FinalLightCurve_TrES-2b.png?raw=true")]
//An example screenshot of your plugin in action
[assembly: AssemblyMetadata("ScreenshotURL", "https://github.com/NickHardy/ExoPlanets/blob/main/NINA.Plugin.ExoPlanets/resources/TransitDSOcontainerScreenShot.png?raw=true")]
//An additional example screenshot of your plugin in action
[assembly: AssemblyMetadata("AltScreenshotURL", "https://github.com/NickHardy/ExoPlanets/blob/main/NINA.Plugin.ExoPlanets/resources/Tres-1b-fov.png?raw=true")]
[assembly: AssemblyMetadata("LongDescription", @"# N.I.N.A. - Nighttime Imaging 'N' Astronomy ExoPlanet and Variable star Plugin#

[https://nighttime-imaging.eu/](https://nighttime-imaging.eu/)

# Plugin Information #

*Instructions*
* ExoPlanet object container
  This is similar to the DSO container, but it has an added button to retrieve a list of exoplanet targets
  You can then select a target from the dropdownlist. They are sorted by observability and depth. The coordinates will be filled out.
  You can then create your sequence as you wish
* VariableStar object container
  This is similar to the DSO container also, but it has an added button to calculate the list of variable stars (from the user defined catalog), thay will be on a defined period phase tonight.
  You can then select a target from the dropdownlist. They are sorted by the time of the event, so early events go first on the list.
  When a target is selected, its coordinates will be filled out.
  You can then create your secuence as you wish.
* Wait for transit observation time
  Basically a wait for time instruction where you can choose the observation start time
* Loop until transit observation time
  Same as the loop until time, but you can choose the observation end time.
* Calculate exposure time
  This instruction can calculate the proper exposure time for the given target and target ADU percentage.
  Enter the exposure time for the first and second image. It will take the first image and platesolve it. Then it will try to find the star in the image and check the MaxPixelValue.
  Next it will take the second image and repeat the process.
  There is also the option to only select exposuretimes from preselected values. That way you will be able to use a dark library.
  It will then calculate the exposure time to get the star to the given target ADU for the camera and take another image.
  This process will repeat until the MaxPixelValue for the target star is within 10 percent of the given target ADU
  This instruction will also check the image for comparison stars and variable stars and show their locations on the image and save the fov image to your imaging directory.
  Make sure the coordinates for the target star are correct and that the correct pixel size and focal length are used in the Nina options.

*Variable Stars Catalog:*

The plugin supports flexible variable star CSV files with header-based column detection. Columns can be in any order, and the parser automatically recognizes common column name variations (aliases).

**Required Columns (must be present):**
* `Name` (aliases: Star Name, Object, Target): The star designation
* `RA` (aliases: RA (J2000.0), RA_J2000): Right Ascension in HMS format (e.g., 18 17 52.16)
* `Dec` (aliases: Dec (J2000.0), Dec_J2000, Declination): Declination in DMS format (e.g., +77 17 49.43)

**Optional Columns (defaults applied if missing):**
* `Magnitude` / `V` (aliases: Max Mag, Min Mag): Visual magnitude for sorting (default: 12.0)
* `Period` (aliases: Period (d)): Orbital period in days (default: 0; no events computed if zero)
* `Epoch` (aliases: Epoch (JD), JD): JD of primary minimum (default: 0; no events computed if zero)
* `Amplitude` (aliases: Amp, Range): Light curve amplitude in magnitudes (default: 1.0)
* `Type` (aliases: Var Type, Var. Type): Variable star classification (default: --)
* `OC Range` (aliases: O-C Range, OCRange): O-C drift compensation for period variations (default: 0)
* `Phase` (aliases: ObsPhase, StartPhase): Light curve phase for observation planning, 0-1 range (default: 0)
* `Comments` (aliases: Notes, Remarks, Source): Observation notes or source reference (default: empty)
* `Filter` (aliases: Mode, Filter/Mode): Observation filter or mode (default: empty)

**Notes:**
* If epoch is zero, no event times are computed and the star will be shown when observability criteria are met.
* If period is zero, no event times are computed regardless of epoch value.
* The parser logs a summary showing how many stars were successfully loaded and how many rows were skipped.
* Invalid rows are skipped with detailed error messages logged (row number, column name, error reason).
* You can now mix Manual catalog and AAVSO catalog formats in the same file.

*Template*
* [Example exoplanet sequence](https://github.com/NickHardy/ExoPlanets/raw/refs/heads/main/NINA.Plugin.ExoPlanets/resources/TransitPlanetImagingSequence.json)
* [Example variable star sequence](https://github.com/NickHardy/ExoPlanets/raw/refs/heads/main/NINA.Plugin.ExoPlanets/resources/VariableStarImagingSequence.json)

*More to read*
* [ExoClock](https://www.exoclock.space/)
* [Exoplanet-watch](https://exoplanets.nasa.gov/exoplanet-watch)
* [AAVSO](https://www.aavso.org/)
* [Siril Processing](https://siril.readthedocs.io/en/latest/photometry/lightcurves.html#nina-exoplanet-button)  
  A Nina Exoplanet button has been added to easily process the data collected. Make sure to select save the csv starlist in the options.

This plugin uses online data from:
* [https://astro.swarthmore.edu/transits/](https://astro.swarthmore.edu/transits/)
* [https://app.aavso.org/vsp/](https://app.aavso.org/vsp/)
* [http://simbad.u-strasbg.fr/simbad/](http://simbad.u-strasbg.fr/simbad/)

This plugin also support the Pandora mission:
* [https://pandoramission.github.io/pandorawebsite/](https://pandoramission.github.io/pandorawebsite/)  
  If a target is in the target list for the Pandora mission, it will show in the comment. It would be great if you could grab data for those targets and upload it to the AAVSO. Thx.

Tutorials:
* [Patriot Astro: Imaging](https://www.youtube.com/watch?v=dN_s_4HjSZU)
* [- Processing in AstroImageJ](https://www.youtube.com/watch?v=GW--rE5O-c8)
* [- Processing in Hops](https://www.youtube.com/watch?v=8q0TV0KaE2k)

A big thank you goes out to Dominique(@DominiqueD84) for testing this plugin. :)

I would also like to thank Rafa Barbera for adding Variable Star support.

Please report any issues in the [Nina discord server](https://discord.gg/rWRbVbw) and tag me: @NickHolland#5257 or rbarbera#1806

If you would like to buy me a whisky: [click here](https://www.paypal.com/paypalme/NickHardyHolland)
")]