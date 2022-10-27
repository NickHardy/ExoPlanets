# N.I.N.A. - Nighttime Imaging 'N' Astronomy ExoPlanet Plugin#

This repository contains the source code distribution of the N.I.N.A. imaging software ExoPlanet Plugin.

https://nighttime-imaging.eu/

# Plugin Information: #

To add this plugin to Nina just go to the plugin section, select the available plugins page and download this plugin. After a restart you can use it's features

This can only be used in the Advanced Sequencer in Nina 2.0 or later.

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

Template:
- https://bitbucket.org/NickHardy/nina/downloads/Transiting_planet_Object_Sequence.template.json
  This is an example template you could use or modify as you wish

More to read:
- https://www.exoclock.space/
- https://exoplanets.nasa.gov/exoplanet-watch

This plugin uses online data from:
- https://astro.swarthmore.edu/transits/
- https://app.aavso.org/vsp/
- http://simbad.u-strasbg.fr/simbad/

Tutorials:
* Patriot Astro: Imaging https://www.youtube.com/watch?v=dN_s_4HjSZU
* Processing in AstroImageJ: https://www.youtube.com/watch?v=GW--rE5O-c8
* Processing in Hops: https://www.youtube.com/watch?v=8q0TV0KaE2k

A big thank you goes out to Dominique(@DominiqueD84) for testing this plugin. :)

Please report any issues in the Nina discord server.
https://discord.gg/rWRbVbw and tag me: @NickHolland#5257 

If you would like to buy me a whisky: https://www.paypal.com/paypalme/NickHardyHolland
