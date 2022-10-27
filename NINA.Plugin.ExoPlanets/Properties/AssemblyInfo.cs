using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("ExoPlanets")]
[assembly: AssemblyDescription("A plugin to help get exoplanet data.")]
[assembly: AssemblyConfiguration("")]

//Your name
[assembly: AssemblyCompany("Nick Hardy")]
//The product name that this plugin is part of
[assembly: AssemblyProduct("NINA Plugin ExoPlanets")]
[assembly: AssemblyCopyright("Copyright ©  2021")]
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
[assembly: AssemblyVersion("1.1.7.1")]
[assembly: AssemblyFileVersion("1.1.7.1")]

//The minimum Version of N.I.N.A. that this plugin is compatible with
[assembly: AssemblyMetadata("MinimumApplicationVersion", "2.0.0.9001")]

//Your plugin homepage - omit if not applicaple
[assembly: AssemblyMetadata("Homepage", "https://nighttime-imaging.eu/")]
//The license your plugin code is using
[assembly: AssemblyMetadata("License", "MPL-2.0")]
//The url to the license
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
//The repository where your pluggin is hosted
[assembly: AssemblyMetadata("Repository", "https://bitbucket.org/NickHardy/nina/src/plugins/NINA.Plugin.ExoPlanets/")]

//Common tags that quickly describe your plugin
[assembly: AssemblyMetadata("Tags", "ExoPlanet,Sequencer")]

//The featured logo that will be displayed in the plugin list next to the name
[assembly: AssemblyMetadata("FeaturedImageURL", "https://bitbucket.org/NickHardy/nina/downloads/FinalLightCurve_TrES-2b.png")]
//An example screenshot of your plugin in action
[assembly: AssemblyMetadata("ScreenshotURL", "https://bitbucket.org/NickHardy/nina/downloads/TransitDSOcontainerScreenShot.png")]
//An additional example screenshot of your plugin in action
[assembly: AssemblyMetadata("AltScreenshotURL", "https://bitbucket.org/NickHardy/nina/downloads/Tres-1b-fov.png")]
[assembly: AssemblyMetadata("LongDescription", @"# N.I.N.A. - Nighttime Imaging 'N' Astronomy ExoPlanet Plugin#

[https://nighttime-imaging.eu/](https://nighttime-imaging.eu/)

# Plugin Information #

*Instructions*
* ExoPlanet object container
  This is similar to the DSO container, but it has an added button to retrieve a list of exoplanet targets
  You can then select a target from the dropdownlist. They are sorted by observability and depth. The coordinates will be filled out.
  You can then create your sequence as you wish
* Wait for transit observation time
  Basically a wait for time instruction where you can choose the observation start time
* Loop until transit observation time
  Same as the loop until time, but you can choose the observation end time.
* Calculate exposure time
  This instruction can calculate the proper exposure time for the given target and target ADU percentage.
  Enter the exposure time for the first and second image. It will take the first image and platesolve it. Then it will try to find the star in the image and check the MaxPixelValue.
  Next it will take the second image and repeat the process.
  It will then calculate the exposure time to get the star to the given target ADU for the camera and take another image.
  This process will repeat until the MaxPixelValue for the target star is within 10 percent of the given target ADU
  This instruction will also check the image for comparison stars and variable stars and show their locations on the image and save the fov image to your imaging directory.
  Make sure the coordinates for the target star are correct.

*Template*
* [Example template](https://bitbucket.org/NickHardy/nina/downloads/TransitPlanetImagingSequence.json)
  This is an example template you could use or modify as you wish

*More to read*
* [ExoClock](https://www.exoclock.space/)
* [Exoplanet-watch](https://exoplanets.nasa.gov/exoplanet-watch)

This plugin uses online data from:
* [https://astro.swarthmore.edu/transits/](https://astro.swarthmore.edu/transits/)
* [https://app.aavso.org/vsp/](https://app.aavso.org/vsp/)
* [http://simbad.u-strasbg.fr/simbad/](http://simbad.u-strasbg.fr/simbad/)

Tutorials:
* [Patriot Astro: Imaging](https://www.youtube.com/watch?v=dN_s_4HjSZU)
* [- Processing in AstroImageJ](https://www.youtube.com/watch?v=GW--rE5O-c8)
* [- Processing in Hops](https://www.youtube.com/watch?v=8q0TV0KaE2k)

A big thank you goes out to Dominique(@DominiqueD84) for testing this plugin. :)

Please report any issues in the [Nina discord server](https://discord.gg/rWRbVbw) and tag me: @NickHolland#5257 

If you would like to buy me a whisky: [click here](https://www.paypal.com/paypalme/NickHardyHolland)
")]