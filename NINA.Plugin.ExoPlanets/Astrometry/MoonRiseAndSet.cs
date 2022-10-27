#region "copyright"

/*
    Copyright © 2016 - 2021 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Astrometry;
using NINA.Astrometry.Body;
using NINA.Astrometry.RiseAndSet;
using OxyPlot;
using OxyPlot.Axes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NINA.Plugin.ExoPlanets.RiseAndSet {

    public class MoonRiseAndSet : RiseAndSetEvent {

        public MoonRiseAndSet(DateTime date, double latitude, double longitude) : base(date, latitude, longitude) {
        }

        protected override double AdjustAltitude(BasicBody body) {
            /* Readjust moon altitude based on earth radius and refraction */
            var horizon = 90.0;
            var location = new NOVAS.OnSurface() {
                Latitude = Latitude,
                Longitude = Longitude
            };
            var refraction = NOVAS.Refract(ref location, NOVAS.RefractionOption.StandardRefraction, horizon); ;
            var altitude = body.Altitude - AstroUtil.ToDegree(Earth.Radius) / body.Distance + AstroUtil.ToDegree(body.Radius) / body.Distance + refraction;
            return altitude;
        }

        protected override BasicBody GetBody(DateTime date) {
            return new Moon(date, Latitude, Longitude);
        }

        public async Task<List<DataPoint>> CalculateTransitAsync(DateTime start) {
            List<DataPoint> _transit = new List<DataPoint>();
            DateTime time = start;
            for (int i = 1; i <= 240; i++) {
                BasicBody moon = GetBody(time);
                await Task.WhenAll(moon.Calculate());
                _transit.Add(new DataPoint(DateTimeAxis.ToDouble(time), AdjustAltitude(moon)));
                time = time.AddHours(0.1d);
            }
            return _transit;
        }
    }
}