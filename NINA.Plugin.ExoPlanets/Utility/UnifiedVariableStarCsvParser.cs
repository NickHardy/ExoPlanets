#region "copyright"

/*
    Copyright © 2016 - 2021 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using CsvHelper;
using NINA.Core.Utility;
using NINA.Plugin.ExoPlanets.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NINA.Plugin.ExoPlanets.Utility {

    /// <summary>
    /// Unified CSV parser for variable stars that supports multiple CSV formats.
    /// 
    /// Identifies columns by header name (case-insensitive) rather than position,
    /// allowing flexible CSV structures. Supports both Manual and AAVSO formats
    /// (and any custom format with compatible headers).
    /// 
    /// Required columns: Name, RA, Dec
    /// Optional columns: Magnitude, Period, Epoch, Amplitude, Type, Comments, Filter, etc.
    /// </summary>
    public class UnifiedVariableStarCsvParser {

        private const double DEFAULT_MAGNITUDE = 12.0;
        private const double DEFAULT_PERIOD = 0;
        private const double DEFAULT_EPOCH = 0;
        private const double DEFAULT_AMPLITUDE = 1.0;
        private const string DEFAULT_TYPE = "--";

        /// <summary>
        /// Parse a variable star CSV file with flexible column detection.
        /// </summary>
        /// <param name="filePath">Path to CSV file</param>
        /// <param name="logAction">Action to call for logging messages (row numbers, errors, etc.)</param>
        /// <returns>List of successfully parsed VariableStar objects</returns>
        /// <exception cref="FileNotFoundException">If file doesn't exist</exception>
        /// <exception cref="InvalidOperationException">If required columns are missing</exception>
        public static Task<List<VariableStar>> ParseAsync(string filePath, Action<string> logAction = null) {
            return Task.Run(() => Parse(filePath, logAction));
        }

        /// <summary>
        /// Parse a variable star CSV file with flexible column detection.
        /// </summary>
        /// <param name="filePath">Path to CSV file</param>
        /// <param name="logAction">Action to call for logging messages (row numbers, errors, etc.)</param>
        /// <returns>List of successfully parsed VariableStar objects</returns>
        /// <exception cref="FileNotFoundException">If file doesn't exist</exception>
        /// <exception cref="InvalidOperationException">If required columns are missing</exception>
        public static List<VariableStar> Parse(string filePath, Action<string> logAction = null) {
            if (!File.Exists(filePath)) {
                throw new FileNotFoundException($"Variable star catalog file not found: {filePath}");
            }

            var results = new List<VariableStar>();
            var skippedRows = new List<string>();

            try {
                using (var reader = new StreamReader(filePath, Encoding.UTF8))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture)) {
                    // Try to read header
                    if (!csv.Read()) {
                        throw new InvalidOperationException("CSV file is empty");
                    }
                    
                    // Get the first row as headers manually if ReadHeader fails
                    string[] headerRecord = null;
                    try {
                        csv.ReadHeader();
                        headerRecord = csv.HeaderRecord;
                    } catch {
                        // If ReadHeader fails, extract headers from Parser
                        headerRecord = new string[csv.Parser.Count];
                        for (int i = 0; i < csv.Parser.Count; i++) {
                            headerRecord[i] = csv.Parser[i] ?? "";
                        }
                    }
                    
                    if (headerRecord == null || headerRecord.Length == 0) {
                        throw new InvalidOperationException("CSV file has no headers - first row appears to be empty");
                    }

                    // Detect and map columns
                    var headerMap = DetectColumns(headerRecord);
                    ValidateRequiredColumns(headerMap);

                    // Log header detection
                    logAction?.Invoke($"Detected CSV columns - Name: {headerMap["name"]}, RA: {headerMap["ra"]}, Dec: {headerMap["dec"]}");

                    // Process data rows
                    int rowNumber = 2; // Header is row 1
                    while (csv.Read()) {
                        try {
                            var star = ParseRow(csv, headerMap, rowNumber);
                            if (star != null) {
                                results.Add(star);
                            }
                        } catch (Exception ex) {
                            var starName = GetRowValue(csv, headerMap, "name") ?? "unknown";
                            var errorMsg = $"Row {rowNumber} ({starName}): {ex.Message}";
                            skippedRows.Add(errorMsg);
                            logAction?.Invoke($"⚠ Skipped {errorMsg}");
                        }
                        rowNumber++;
                    }
                }
            } catch (InvalidOperationException) {
                throw;
            } catch (FileNotFoundException) {
                throw;
            } catch (Exception ex) {
                throw new InvalidOperationException($"Error reading CSV file: {ex.Message}", ex);
            }

            // Log summary
            var summary = $"✓ Loaded {results.Count} variable stars";
            if (skippedRows.Count > 0) {
                summary += $", {skippedRows.Count} rows skipped";
            }
            logAction?.Invoke(summary);

            if (skippedRows.Count > 0) {
                logAction?.Invoke("Skipped rows:");
                foreach (var error in skippedRows.Take(10)) {
                    logAction?.Invoke($"  - {error}");
                }
                if (skippedRows.Count > 10) {
                    logAction?.Invoke($"  ... and {skippedRows.Count - 10} more");
                }
            }

            return results;
        }

        /// <summary>
        /// Detect CSV columns by scanning header row for known column names and aliases.
        /// Returns a mapping of standard column names to actual header values.
        /// </summary>
        private static Dictionary<string, string> DetectColumns(string[] headers) {
            if (headers == null || headers.Length == 0) {
                throw new InvalidOperationException("CSV has no headers - the first row appears to be empty or was not properly read by the CSV parser.");
            }

            // Create case-insensitive header lookup
            var headerLookup = headers
                .Select((h, i) => new { header = h?.Trim(), index = i })
                .Where(x => !string.IsNullOrEmpty(x.header))
                .ToDictionary(x => x.header.ToLower(), x => x.header);

            if (headerLookup.Count == 0) {
                throw new InvalidOperationException($"CSV header row has {headers.Length} columns, but they are all empty after trimming. Headers: [{string.Join(", ", headers.Select(h => $"\"{h}\""))}]");
            }

            var map = new Dictionary<string, string>();

            // Map required columns with aliases
            map["name"] = FindColumn(headerLookup, "name", "star name", "object", "target");
            map["ra"] = FindColumn(headerLookup, "ra", "ra (j2000.0)", "ra_j2000", "ra_2000", "ra(2000)");
            map["dec"] = FindColumn(headerLookup, "dec", "dec (j2000.0)", "dec_j2000", "dec_2000", "declination", "dec(2000)");

            // Map optional columns with aliases (may be null)
            map["magnitude"] = FindColumn(headerLookup, "magnitude", "v", "mag", "max mag", "max_mag");
            map["period"] = FindColumn(headerLookup, "period", "period (d)", "p", "orbital_period");
            map["epoch"] = FindColumn(headerLookup, "epoch", "epoch (jd)", "jd", "epoch_jd", "primary_minimum");
            map["amplitude"] = FindColumn(headerLookup, "amplitude", "amp", "range", "mag_range");
            map["type"] = FindColumn(headerLookup, "type", "var type", "var. type", "vartype", "classification");
            map["comments"] = FindColumn(headerLookup, "comments", "notes", "remarks", "source", "remarks");
            map["ocrange"] = FindColumn(headerLookup, "ocrange", "o-c range", "oc_range", "o-c", "drift");
            map["phase"] = FindColumn(headerLookup, "phase", "obsphase", "obs_phase", "start_phase", "observed_phase");
            map["filter"] = FindColumn(headerLookup, "filter", "mode", "filter/mode", "observation_filter");
            map["minmag"] = FindColumn(headerLookup, "min mag", "min_mag", "minmag", "minimum_magnitude");
            map["maxmag"] = FindColumn(headerLookup, "max mag", "max_mag", "maxmag", "maximum_magnitude");

            return map;
        }

        /// <summary>
        /// Find a column by trying multiple alias names. Returns null if not found.
        /// </summary>
        private static string FindColumn(Dictionary<string, string> headerLookup, params string[] aliases) {
            foreach (var alias in aliases) {
                if (headerLookup.TryGetValue(alias.ToLower(), out var header)) {
                    return header;
                }
            }
            return null;
        }

        /// <summary>
        /// Validate that all required columns were found.
        /// </summary>
        private static void ValidateRequiredColumns(Dictionary<string, string> map) {
            var missing = new List<string>();
            if (string.IsNullOrEmpty(map["name"])) missing.Add("Name (aliases: 'Star Name', 'Object')");
            if (string.IsNullOrEmpty(map["ra"])) missing.Add("RA (aliases: 'RA (J2000.0)', 'RA_J2000')");
            if (string.IsNullOrEmpty(map["dec"])) missing.Add("Dec (aliases: 'Dec (J2000.0)', 'Dec_J2000', 'Declination')");

            if (missing.Count > 0) {
                throw new InvalidOperationException(
                    $"CSV is missing required columns: {string.Join(", ", missing)}. " +
                    "At minimum, the CSV must have Name, RA, and Dec columns.");
            }
        }

        /// <summary>
        /// Parse a single CSV row into a VariableStar object.
        /// </summary>
        private static VariableStar ParseRow(CsvReader csv, Dictionary<string, string> columnMap, int rowNumber) {
            var name = GetRowValue(csv, columnMap, "name");
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Name is empty");
            }

            // Parse coordinates (required, will throw if invalid)
            var raStr = GetRowValue(csv, columnMap, "ra");
            var decStr = GetRowValue(csv, columnMap, "dec");
            var coords = CoordinateParser.ParseCoordinates(raStr, decStr, rowNumber);

            var star = new VariableStar {
                Name = name.Trim(),
                RA = raStr?.Trim() ?? "",
                Dec = decStr?.Trim() ?? "",
            };

            // Parse magnitude (optional, default to 12.0)
            var magStr = GetRowValue(csv, columnMap, "magnitude") ?? GetRowValue(csv, columnMap, "maxmag");
            if (double.TryParse(magStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var mag)) {
                star.V = mag;
            } else {
                star.V = DEFAULT_MAGNITUDE;
            }

            // Parse period (optional, default to 0)
            var periodStr = GetRowValue(csv, columnMap, "period");
            if (double.TryParse(periodStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var period)) {
                star.period = period;
            } else {
                star.period = DEFAULT_PERIOD;
            }

            // Parse epoch (optional, default to 0)
            var epochStr = GetRowValue(csv, columnMap, "epoch");
            if (double.TryParse(epochStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var epoch)) {
                star.epoch = epoch;
            } else {
                star.epoch = DEFAULT_EPOCH;
            }

            // Parse amplitude (optional, default to 1.0)
            var ampStr = GetRowValue(csv, columnMap, "amplitude") ?? GetRowValue(csv, columnMap, "minmag");
            if (!string.IsNullOrEmpty(ampStr) && double.TryParse(ampStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var amp)) {
                // If we got minmag, calculate amplitude from min/max
                if (ampStr == GetRowValue(csv, columnMap, "minmag")) {
                    var minMag = amp;
                    var maxMagStr = GetRowValue(csv, columnMap, "maxmag");
                    if (double.TryParse(maxMagStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var maxMag)) {
                        star.amplitude = Math.Abs(maxMag - minMag) / 2.0;
                    } else {
                        star.amplitude = DEFAULT_AMPLITUDE;
                    }
                } else {
                    star.amplitude = amp;
                }
            } else {
                star.amplitude = DEFAULT_AMPLITUDE;
            }

            // Parse type (optional, default to "--")
            star.VarType = GetRowValue(csv, columnMap, "type") ?? DEFAULT_TYPE;

            // Parse comments (optional, default to "")
            star.Comments = GetRowValue(csv, columnMap, "comments") ?? 
                          GetRowValue(csv, columnMap, "filter") ?? "";

            // Parse O-C range (optional, default to 0)
            var ocStr = GetRowValue(csv, columnMap, "ocrange");
            if (double.TryParse(ocStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var ocRange)) {
                star.OCRange = ocRange;
            } else {
                star.OCRange = 0;
            }

            // Parse observed phase (optional, default to 0)
            var phaseStr = GetRowValue(csv, columnMap, "phase");
            if (double.TryParse(phaseStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var phase)) {
                star.observedPhase = Math.Max(0, Math.Min(1, phase)); // Clamp to [0, 1]
            } else {
                star.observedPhase = 0;
            }

            return star;
        }

        /// <summary>
        /// Get a value from the current CSV row by column name (using the column map).
        /// Returns null if the column doesn't exist in the CSV.
        /// </summary>
        private static string GetRowValue(CsvReader csv, Dictionary<string, string> columnMap, string columnKey) {
            if (columnMap.TryGetValue(columnKey, out var columnName) && !string.IsNullOrEmpty(columnName)) {
                try {
                    return csv.GetField(columnName)?.Trim();
                } catch {
                    return null;
                }
            }
            return null;
        }
    }
}
