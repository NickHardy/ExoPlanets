using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("ExoPlanets")]
[assembly: AssemblyDescription("A plugin to help get exoplanet or variable star data.")]
[assembly: AssemblyConfiguration("")]

//Your name
[assembly: AssemblyCompany("Nick Hardy & Rafa Barbera")]
//The product name that this plugin is part of
[assembly: AssemblyProduct("NINA Plugin ExoPlanets")]
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
[assembly: AssemblyVersion("2.0.5.1")]
[assembly: AssemblyFileVersion("2.0.5.1")]

//The minimum Version of N.I.N.A. that this plugin is compatible with
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.0.0.9001")]

//Your plugin homepage - omit if not applicaple
[assembly: AssemblyMetadata("Homepage", "https://nighttime-imaging.eu/")]
//The license your plugin code is using
[assembly: AssemblyMetadata("License", "MPL-2.0")]
//The url to the license
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
//The repository where your pluggin is hosted
[assembly: AssemblyMetadata("Repository", "https://bitbucket.org/NickHardy/exoplanets/src/main/")]

//Common tags that quickly describe your plugin
[assembly: AssemblyMetadata("Tags", "ExoPlanet,VariableStar,Sequencer")]

//The featured logo that will be displayed in the plugin list next to the name
[assembly: AssemblyMetadata("FeaturedImageURL", "https://bitbucket.org/NickHardy/exoplanets/downloads/FinalLightCurve_TrES-2b.png")]
//An example screenshot of your plugin in action
[assembly: AssemblyMetadata("ScreenshotURL", "https://bitbucket.org/NickHardy/exoplanets/downloads/TransitDSOcontainerScreenShot.png")]
//An additional example screenshot of your plugin in action
[assembly: AssemblyMetadata("AltScreenshotURL", "https://bitbucket.org/NickHardy/exoplanets/downloads/Tres-1b-fov.png")]
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

We support two kind of files

* Manual Catalog.
    - This is a simple [CSV file](https://bitbucket.org/NickHardy/exoplanets/downloads/geos.csv) with the mandatory columns name,ra,dec,v,epoch and period.
	- amplitude (optional): if you want to show each variable with different variation height.
	- ocrange (optional): to compensate for variable O-C like on RRab with Blazhko effect.
	- phase (optional): use a number between 0 and 1 to observe different portions of the light curve.

  if you set the epoch to zero, no min or max will be computed and the star will be shown always it meets the observability criteria.

* AAVSO CSV catalog. [CSV example file](https://bitbucket.org/NickHardy/exoplanets/downloads/aavso.csv)
    - The expected file format is the one downloaded from AVVSO's [Observation Planner Tool](https://www.aavso.org/observation-planner-tool)
    - On this dataset, no epoch is given, so no min or max could be computed.
    - You have three criteria to sort the stars: Visibility, Culmination and Name.

*Template*
* [Example exoplanet sequence](https://bitbucket.org/NickHardy/exoplanets/downloads/TransitPlanetImagingSequence.json)
* [Example variable star sequence](https://bitbucket.org/NickHardy/exoplanets/downloads/VariableStarImagingSequence.json)

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