using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using AcTools.DataAnalyzer;
using CommandLine;
using CommandLine.Text;
using Microsoft.SqlServer.Server;

namespace DataAnalyzer {
    class Program {
        public enum ProgramMode {
            CollectDatabase, TestCars
        }

        private class Options {
            [ValueList(typeof(List<string>))]
            public IList<string> Cars { get; set; }

            [Option('d', "directory", Required = true, DefaultValue = null, HelpText = "Path to content/cars directory.")]
            public string Directory { get; set; }

            [Option('r', "rules", Required = true, HelpText = "Rules file.")]
            public string RulesFile { get; set; }

            [Option('b', "database", Required = false, HelpText = "Database file (only for TestCars mode).")]
            public string DatabaseFile { get; set; }

            [Option('m', "mode", Required = false, DefaultValue = ProgramMode.TestCars, HelpText = "Program mode (CollectDatabase or TestCars by default).")]
            public ProgramMode Mode { get; set; }

            [Option('t', "threshold", Required = false, DefaultValue = 0.97, HelpText = "Threshold.")]
            public double Threshold { get; set; }

            [Option('i', "information", Required = false, DefaultValue = false, HelpText = "Detailed information on simular cars.")]
            public bool Information { get; set; }

            [Option('v', "verbose", Required = false, DefaultValue = false, HelpText = "Verbose mode.")]
            public bool Verbose { get; set; }

            [ParserState]
            public IParserState LastParserState { get; set; }

            [HelpOption]
            public string GetUsage() {
                return HelpText.AutoBuild(this, (c) => HelpText.DefaultParsingErrorsHandler(this, c));
            }
        }

        static int Main(string[] args) {
            var options = new Options();
            if (!Parser.Default.ParseArguments(args, options)) {
                return 1;
            }

            var cars = options.Cars.Count > 0 ? string.Join(",", options.Cars).Split(',').Select(x => x.Trim()) :
                    Directory.GetDirectories(options.Directory).Select(Path.GetFileName);
            var rulesSets = string.Join(",", options.RulesFile).Split(',').Select(x => RulesSet.FromFile(x.Trim())).ToList();
            var databasesFiles = string.Join(",", options.DatabaseFile).Split(',').Select(x => x.Trim()).ToArray();

            var hashStorage = options.Mode == ProgramMode.TestCars ? HashStorage.FromFile(databasesFiles) : new HashStorage();

            foreach (var carId in cars) {
                if (options.Verbose) {
                    Console.Error.WriteLine(carId);
                }

                var carDir = Path.Combine(options.Directory, carId);
                if (!Directory.Exists(carDir)) {
                    Console.Error.WriteLine("! directory '{0}' not found", carDir);
                    continue;
                }

                if (options.Mode == ProgramMode.CollectDatabase) {
                    var entry = new StringBuilder();
                    entry.Append(carId);
                    entry.Append(":");

                    foreach (var rulesSet in rulesSets) {
                        if (rulesSet != rulesSets[0]) {
                            entry.Append(",");
                        }

                        var hashValue = rulesSet.GetHash(carDir);
                        foreach (var simular in hashStorage.FindSimular(carId, rulesSet.Id, hashValue, options.Threshold, options.Information ? rulesSet : null)) {
                            Console.Error.WriteLine("! {0}: {1} and {2}, {3:F1}%", rulesSet.Id, carId, simular.CarId, simular.Value * 100);
                            if (options.Information) Console.Error.WriteLine("  " + string.Join(", ", simular.WorkedRules.Select(x => x.ToString())));
                        }

                        entry.Append(rulesSet.Id);
                        entry.Append("=");
                        entry.Append(hashValue);
                        hashStorage.Add(carId, rulesSet.Id, hashValue);
                    }

                    Console.WriteLine(entry.ToString());
                } else if (options.Mode == ProgramMode.TestCars) {
                    if (hashStorage.HasCar(carId)) continue;
                    foreach (var rulesSet in rulesSets) {
                        var hashValue = rulesSet.GetHash(carDir);

                        foreach (var simular in hashStorage.FindSimular(carId, rulesSet.Id, hashValue, options.Threshold, options.Information ? rulesSet : null)) {
                            Console.WriteLine("{0}: {1} and {2}, {3:F1}%", rulesSet.Id, carId, simular.CarId, simular.Value * 100);
                            if (options.Information) Console.Error.WriteLine("  " + string.Join(", ", simular.WorkedRules.Select(x => x.ToString())));
                        }
                    }
                }
            }

            return 0;
        }
    }
}
