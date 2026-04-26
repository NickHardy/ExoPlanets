using NINA.Plugin.ExoPlanets.Utility;
using System;
using System.Collections.Generic;
using System.IO;

// Quick test to verify UnifiedVariableStarCsvParser works correctly
namespace NINA.Plugin.ExoPlanets.Tests {
    public class CsvParserTests {
        public static void Main(string[] args) {
            Console.WriteLine("Testing UnifiedVariableStarCsvParser...\n");

            // Test with test files in repository
            Console.WriteLine("=== TEST FILES (Repository) ===\n");
            
            TestParse("test_manual.csv", "Manual format");
            TestParse("test_aavso.csv", "AAVSO format");
            TestParse("test_flexible.csv", "Flexible format");
            TestParse("test_with_errors.csv", "Format with errors");

            // Test with real files from OneDrive
            Console.WriteLine("\n=== REAL FILES (User OneDrive) ===\n");
            
            string basePath = @"C:\Users\rbarb\OneDrive\N.I.N.A\Variables";
            TestParse(Path.Combine(basePath, "obsplan.csv"), "obsplan.csv");
            TestParse(Path.Combine(basePath, "special_cases.csv"), "special_cases.csv");
            TestParse(Path.Combine(basePath, "geos.csv"), "geos.csv");
            TestParse(Path.Combine(basePath, "aavso_short.csv"), "aavso_short.csv (first 100 lines)");

            Console.WriteLine("\n✓ All tests completed!");
        }

        private static void TestParse(string filename, string description) {
            Console.WriteLine($"Testing: {description}");
            Console.WriteLine($"File: {filename}");
            try {
                if (!File.Exists(filename)) {
                    Console.WriteLine($"  ✗ File not found\n");
                    return;
                }

                var stars = UnifiedVariableStarCsvParser.Parse(filename, msg => Console.WriteLine($"  → {msg}"));
                Console.WriteLine($"  ✓ Loaded {stars.Count} stars");
                
                if (stars.Count > 0) {
                    var star = stars[0];
                    Console.WriteLine($"    First: {star.Name} | RA: {star.RA} | Dec: {star.Dec} | V: {star.V}");
                }
            } catch (Exception ex) {
                Console.WriteLine($"  ✗ ERROR: {ex.Message}");
                if (ex.InnerException != null) {
                    Console.WriteLine($"       Inner: {ex.InnerException.Message}");
                }
            }
            Console.WriteLine();
        }
    }
}
