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
using System;

namespace NINA.Plugin.ExoPlanets.Utility {

    /// <summary>
    /// Utility class for safely parsing RA/Dec coordinates from CSV strings.
    /// Validates format before converting to degrees, providing clear error messages.
    /// </summary>
    public static class CoordinateParser {

        /// <summary>
        /// Parse a right ascension string in HMS (Hours Minutes Seconds) format.
        /// Valid formats: "HH MM SS.SS", "HH:MM:SS.SS", "HH MM SS", etc.
        /// </summary>
        /// <param name="raString">RA string in HMS format</param>
        /// <param name="rowNumber">CSV row number for error reporting</param>
        /// <returns>RA angle in degrees</returns>
        /// <exception cref="ArgumentException">If format is invalid or parsing fails</exception>
        public static Angle ParseRA(string raString, int rowNumber = -1) {
            if (string.IsNullOrWhiteSpace(raString)) {
                throw new ArgumentException($"RA value is empty or null{GetRowContext(rowNumber)}");
            }

            try {
                var raInDegrees = AstroUtil.HMSToDegrees(raString);
                if (raInDegrees < 0 || raInDegrees >= 360) {
                    throw new ArgumentException($"RA value {raString} results in {raInDegrees}° which is outside valid range [0°, 360°){GetRowContext(rowNumber)}");
                }
                return Angle.ByDegree(raInDegrees);
            } catch (ArgumentException) {
                throw;
            } catch (Exception ex) {
                throw new ArgumentException($"Failed to parse RA '{raString}': {ex.Message}{GetRowContext(rowNumber)}", ex);
            }
        }

        /// <summary>
        /// Parse a declination string in DMS (Degrees Minutes Seconds) format.
        /// Valid formats: "+DD MM SS.SS", "-DD:MM:SS.SS", "±DD MM SS", etc.
        /// </summary>
        /// <param name="decString">Dec string in DMS format</param>
        /// <param name="rowNumber">CSV row number for error reporting</param>
        /// <returns>Dec angle in degrees</returns>
        /// <exception cref="ArgumentException">If format is invalid or parsing fails</exception>
        public static Angle ParseDec(string decString, int rowNumber = -1) {
            if (string.IsNullOrWhiteSpace(decString)) {
                throw new ArgumentException($"Dec value is empty or null{GetRowContext(rowNumber)}");
            }

            try {
                var decInDegrees = AstroUtil.DMSToDegrees(decString);
                if (decInDegrees < -90 || decInDegrees > 90) {
                    throw new ArgumentException($"Dec value {decString} results in {decInDegrees}° which is outside valid range [-90°, 90°){GetRowContext(rowNumber)}");
                }
                return Angle.ByDegree(decInDegrees);
            } catch (ArgumentException) {
                throw;
            } catch (Exception ex) {
                throw new ArgumentException($"Failed to parse Dec '{decString}': {ex.Message}{GetRowContext(rowNumber)}", ex);
            }
        }

        /// <summary>
        /// Parse both RA and Dec and create a Coordinates object.
        /// </summary>
        /// <param name="raString">RA string in HMS format</param>
        /// <param name="decString">Dec string in DMS format</param>
        /// <param name="rowNumber">CSV row number for error reporting</param>
        /// <returns>Coordinates object with J2000 epoch</returns>
        /// <exception cref="ArgumentException">If either RA or Dec is invalid</exception>
        public static Coordinates ParseCoordinates(string raString, string decString, int rowNumber = -1) {
            var ra = ParseRA(raString, rowNumber);
            var dec = ParseDec(decString, rowNumber);
            return new Coordinates(ra, dec, Epoch.J2000);
        }

        private static string GetRowContext(int rowNumber) {
            return rowNumber >= 0 ? $" (CSV row {rowNumber})" : "";
        }
    }
}
