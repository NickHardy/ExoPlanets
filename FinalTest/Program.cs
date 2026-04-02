using NINA.Plugin.ExoPlanets.Utility;
using System;
using System.Collections.Generic;
using System.IO;

class FinalTest {
    static void Main() {
        var testFiles = new[] {
            ("C:\\Users\\rbarb\\OneDrive\\N.I.N.A\\Variables\\obsplan.csv", "obsplan"),
            ("C:\\Users\\rbarb\\OneDrive\\N.I.N.A\\Variables\\special_cases.csv", "special_cases"),
            ("C:\\Users\\rbarb\\OneDrive\\N.I.N.A\\Variables\\geos.csv", "geos"),
            ("C:\\Users\\rbarb\\OneDrive\\N.I.N.A\\Variables\\aavso_short.csv", "aavso_short"),
        };

        Console.WriteLine("CSV PARSER FINAL TEST\n");
        int okCount = 0;

        foreach (var (path, name) in testFiles) {
            if (!File.Exists(path)) {
                Console.WriteLine($"SKIP {name}: FILE NOT FOUND");
                continue;
            }

            try {
                var stars = UnifiedVariableStarCsvParser.Parse(path, m => {});
                Console.WriteLine($"OK   {name,-20} {stars.Count,5} stars");
                okCount++;
            } catch (Exception ex) {
                Console.WriteLine($"FAIL {name,-20} {ex.Message}");
            }
        }

        Console.WriteLine($"\nResult: {okCount}/4 files parsed successfully");
        Environment.Exit(okCount == 4 ? 0 : 1);
    }
}
