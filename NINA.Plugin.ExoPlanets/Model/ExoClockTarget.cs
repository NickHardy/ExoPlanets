#region "copyright"

/*
    Copyright © 2016 - 2021 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Astrometry;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;

namespace NINA.Plugin.ExoPlanets.Model {

    [JsonObject(MemberSerialization.OptIn)]
    public class ExoClockTarget {
        [JsonProperty]
        public string name { get; set; }
        [JsonProperty]
        public string priority { get; set; }
        [JsonProperty]
        public int total_observations { get; set; }
        [JsonProperty]
        public int total_observations_recent { get; set; }
        [JsonProperty]
        public int exoclock_observations { get; set; }
        [JsonProperty]
        public int exoclock_observations_recent { get; set; }
        [JsonProperty]
        public int etd_observations { get; set; }
        [JsonProperty]
        public int etd_observations_recent { get; set; }
        [JsonProperty]
        public int space_observations { get; set; }
        [JsonProperty]
        public int space_observations_recent { get; set; }
        [JsonProperty]
        public int literature_midtimes { get; set; }
        [JsonProperty]
        public int literature_midtimes_recent { get; set; }
        [JsonProperty]
        public double current_oc_min { get; set; }
        [JsonProperty]
        public string discovery_ref { get; set; }
        [JsonProperty]
        public double depth_r_mmag { get; set; }
        [JsonProperty]
        public double duration_hours { get; set; }
        [JsonProperty]
        public double ephem_mid_time { get; set; }
        [JsonProperty]
        public double ephem_mid_time_e1 { get; set; }
        [JsonProperty]
        public double ephem_mid_time_e2 { get; set; }
        [JsonProperty]
        public string ephem_mid_time_format { get; set; }
        [JsonProperty]
        public string ephem_mid_time_units { get; set; }
        [JsonProperty]
        public double ephem_period { get; set; }
        [JsonProperty]
        public double ephem_period_e1 { get; set; }
        [JsonProperty]
        public double ephem_period_e2 { get; set; }
        [JsonProperty]
        public string ephem_period_units { get; set; }
        [JsonProperty]
        public string ephem_parameters_ref { get; set; }
        [JsonProperty]
        public double eccentricity { get; set; }
        [JsonProperty]
        public double eccentricity_e1 { get; set; }
        [JsonProperty]
        public double eccentricity_e2 { get; set; }
        [JsonProperty]
        public double inclination { get; set; }
        [JsonProperty]
        public bool inclination_adjusted { get; set; }
        [JsonProperty]
        public double inclination_e1 { get; set; }
        [JsonProperty]
        public double inclination_e2 { get; set; }
        [JsonProperty]
        public string inclination_units { get; set; }
        [JsonProperty]
        public double periastron { get; set; }
        [JsonProperty]
        public double periastron_e1 { get; set; }
        [JsonProperty]
        public double periastron_e2 { get; set; }
        [JsonProperty]
        public string periastron_units { get; set; }
        [JsonProperty]
        public double rp_over_rs { get; set; }
        [JsonProperty]
        public bool rp_over_rs_adjusted { get; set; }
        [JsonProperty]
        public double rp_over_rs_e1 { get; set; }
        [JsonProperty]
        public double rp_over_rs_e2 { get; set; }
        [JsonProperty]
        public string rp_over_rs_units { get; set; }
        [JsonProperty]
        public double sma_over_rs { get; set; }
        [JsonProperty]
        public bool sma_over_rs_adjusted { get; set; }
        [JsonProperty]
        public double sma_over_rs_e1 { get; set; }
        [JsonProperty]
        public double sma_over_rs_e2 { get; set; }
        [JsonProperty]
        public string sma_over_rs_units { get; set; }
        [JsonProperty]
        public string transit_parameters_ref { get; set; }
        [JsonProperty]
        public double kepler_rp_over_rs { get; set; }
        [JsonProperty]
        public double kepler_rp_over_rs_e1 { get; set; }
        [JsonProperty]
        public double kepler_rp_over_rs_e2 { get; set; }
        [JsonProperty]
        public double tess_rp_over_rs { get; set; }
        [JsonProperty]
        public double tess_rp_over_rs_e1 { get; set; }
        [JsonProperty]
        public double tess_rp_over_rs_e2 { get; set; }
        [JsonProperty]
        public string star { get; set; }
        [JsonProperty]
        public string ra_j2000 { get; set; }
        [JsonProperty]
        public string dec_j2000 { get; set; }
        [JsonProperty]
        public double v_mag { get; set; }
        [JsonProperty]
        public double r_mag { get; set; }
        [JsonProperty]
        public double gaia_g_mag { get; set; }
        [JsonProperty]
        public double logg { get; set; }
        [JsonProperty]
        public double logg_e1 { get; set; }
        [JsonProperty]
        public double logg_e2 { get; set; }
        [JsonProperty]
        public string logg_units { get; set; }
        [JsonProperty]
        public double meta { get; set; }
        [JsonProperty]
        public double meta_e1 { get; set; }
        [JsonProperty]
        public double meta_e2 { get; set; }
        [JsonProperty]
        public string meta_units { get; set; }
        [JsonProperty]
        public double teff { get; set; }
        [JsonProperty]
        public double teff_e1 { get; set; }
        [JsonProperty]
        public double teff_e2 { get; set; }
        [JsonProperty]
        public string teff_units { get; set; }
        [JsonProperty]
        public string ldc_parameters_ref { get; set; }
        [JsonProperty]
        public double min_telescope_inches { get; set; }
        [JsonProperty]
        public double min_telescope_inches_hf { get; set; }
        [JsonProperty]
        public double expected_transit_snr_tess { get; set; }

        public Coordinates Coordinates() {
            return new Coordinates(Angle.ByDegree(AstroUtil.HMSToDegrees(ra_j2000)), Angle.ByDegree(AstroUtil.DMSToDegrees(dec_j2000)), Epoch.J2000);
        }
        // decimal d = Decimal.Parse("1.2345E-02", System.Globalization.NumberStyles.Float);

        // currently asuming that ephem_period is always in days
        public double Iterations() {
            double currentJDate = AstroUtil.GetJulianDate(DateTime.Now);
            return Math.Round((currentJDate - ephem_mid_time) / ephem_period, 0, MidpointRounding.AwayFromZero);
        }

        // currently asuming that ephem_period is always in days
        public double TransitMidpoint() {
            return ephem_mid_time + (Iterations() * ephem_period) + (Math.Abs(current_oc_min) > 10 ? (current_oc_min * 60d / 86400d) : 0d);
        }

        public double TransitStart() {
            return TransitMidpoint() - (duration_hours / 2 * 3600 / 86400);
        }

        public double TransitEnd() {
            return TransitMidpoint() + (duration_hours / 2 * 3600 / 86400);
        }
    }
}