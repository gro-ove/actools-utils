using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Windows;
using AcTools.Windows.Input;
using AcTools.Windows.Input.Native;
using CommandLine;
using CommandLine.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AiOptimizer {
    [Obsolete]
    class Program {
        private class Options {
            [Option('d', "directory", Required = true, DefaultValue = null, HelpText = "Path to content/cars directory.")]
            public string CarsDir { get; set; }

            [Option('c', "car", Required = true, HelpText = "Car to optimize.")]
            public string CarId { get; set; }

            [Option('t', "track", Required = true, HelpText = "Track id")]
            public string TrackId { get; set; }

            [Option('l', "laps", Required = true, HelpText = "Laps number.")]
            public int RaceLaps { get; set; }

            [Option('i', "iterations", Required = true, HelpText = "Iterations per generation.")]
            public int IterationsPerGeneration { get; set; }

            [Option('m', "laptime", Required = true, HelpText = "Maximum time per lap (minutes).")]
            public double RaceTimeout { get; set; }

            [Option('s', "size", Required = false, DefaultValue = 20, HelpText = "Generation size.")]
            public int GenerationSize { get; set; }

            [Option('v', "verbose", Required = false, DefaultValue = false, HelpText = "Verbose mode.")]
            public bool Verbose { get; set; }

            [ParserState]
            public IParserState LastParserState { get; set; }

            [HelpOption]
            public string GetUsage() {
                return HelpText.AutoBuild(this, (c) => HelpText.DefaultParsingErrorsHandler(this, c));
            }
        }

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        private static bool CopyAsHardLink(string from, string to) {
            return CreateHardLink(to, from, IntPtr.Zero);
        }

        private static void RecoursiveCopy(string from, string to) {
            if (!Directory.Exists(to)) {
                Directory.CreateDirectory(to);
            }

            foreach (var sub in Directory.GetFiles(from)) {
                CopyAsHardLink(sub, Path.Combine(to, Path.GetFileName(sub)));
            }

            foreach (var sub in Directory.GetDirectories(from)) {
                RecoursiveCopy(sub, Path.Combine(to, Path.GetFileName(sub)));
            }
        }

        private static string[] GetNewGenerationIds(string carId, int generationSize) {
            var childrenIds = new List<string>(generationSize);
            for (var i = 0; i < generationSize; i++) {
                childrenIds.Add("__" + carId + "__" + i);
            }

            return childrenIds.ToArray();
        }

        private static void CreateNewGeneration(string carsDir, string carId, string[] childrenIds) {
            var originalDir = Path.Combine(carsDir, carId);
            Console.WriteLine("iteration: {0}, {1}", originalDir, childrenIds.Length);
            foreach (var childId in childrenIds) {
                var targetDir = Path.Combine(carsDir, childId);
                RecoursiveCopy(originalDir, targetDir);
                MutateAiFile(Path.Combine(originalDir, "data", "ai.ini"), Path.Combine(targetDir, "data", "ai.ini"));
                Console.WriteLine("  copied & mutated: {0}", childId);
            }
        }

        private static void TestNewGeneration(string carsDir, string carId, string[] childrenIds, string trackId,
                                              int lapsNumber, int timeout) {
            Console.WriteLine("starting game...");
            var acRoot = Path.GetDirectoryName(Path.GetDirectoryName(carsDir));
            Game.StartRace(acRoot, carId, "", trackId, null, new Game.RaceProperties {
                AiLevel = 100,
                BotCars = childrenIds.Select(x => new Game.AiCar {
                    CarId = x,
                    AiLevel = 100,
                    DriverName = x
                }).Concat(new[] { new Game.AiCar {
                    CarId = carId,
                    DriverName = "base"
                } }).ToArray(),
                Penalties = true,
                RaceLaps = lapsNumber,
                StartingPosition = childrenIds.Length
            });

            Thread.Sleep(5000);

            /* TODO:
             * 2. строить суммарный рейтинг, брать среднее или лучшее время круга
             * 4. выбирать лучшие машины и генерировать следующее поколение
             */

            // var process = Process.GetProcessesByName("ac.exe").First();
            // User32.BringProcessWindowToFront(process);

            var inputSimulator = new InputSimulator();
            inputSimulator.Mouse.MoveMouseTo(65536 * 50 / Screen.PrimaryScreen.Bounds.Width,
                65536 * 150 / Screen.PrimaryScreen.Bounds.Height);
            inputSimulator.Mouse.LeftButtonClick();

            Thread.Sleep(15000);
            inputSimulator.Keyboard.KeyPress(VirtualKeyCode.F3);

            var waitTimeout = timeout * 1000 + 15000;
            Console.WriteLine("timeout: {0}", waitTimeout);
            Thread.Sleep(waitTimeout);

            Console.WriteLine("closing game...");
            inputSimulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.LMENU, VirtualKeyCode.F4);
            Thread.Sleep(1000);
            Console.WriteLine("game closed");
        }

        public class BestLapEntry {
            public string CarId;
            public int Time;
        }

        private static void ExtractResults(string carId, string[] childrenIds) {
            var resultFile = FileUtils.GetResultJsonFilename();
            var jsonObject = JObject.Parse(File.ReadAllText(resultFile));

            JArray bestLaps = null;
            try {
                bestLaps = jsonObject["sessions"][0]["bestLaps"] as JArray;
            } catch (Exception) {
            }

            if (bestLaps.Count == 0) {
                Console.Error.WriteLine("best laps aren't registered");
            }

            if (bestLaps != null) {
                var entries = bestLaps.Select(x => {
                    var n = (int)x["car"] - 1;
                    return new BestLapEntry {
                        CarId = n < childrenIds.Length ? childrenIds[n] : carId,
                        Time = (int)x["time"]
                    };
                });
                foreach (var entry in entries.OrderBy(x => x.Time)) {
                    Console.WriteLine("  version: {0}, best time: {1} seconds", entry.CarId, entry.Time);
                }
            } else {
                Console.Error.WriteLine("cannot read best laps");
            }
        }

        private static void Iteration(string carsDir, string carId, int generationSize, string trackId, int lapsNumber, int timeout, int iterations) {
            var childrenIds = GetNewGenerationIds(carId, generationSize);
            CreateNewGeneration(carsDir, carId, childrenIds);

            for (var i = 0; i < iterations; i++) {
                TestNewGeneration(carsDir, carId, childrenIds, trackId, lapsNumber, timeout);
                ExtractResults(carId, childrenIds);
            }
        }

        static readonly Random R = new Random();
        static double RandomDouble() {
            return R.NextDouble();
        }

        private static double MutateDouble(double value, double minDelta, double maxDelta, double minValue, double maxValue) {
            var result = value;
            if (RandomDouble() > 0.5) {
                var randomValue = Math.Pow(RandomDouble(), 3.6);
                var deltaAbs = minDelta + (maxDelta - minDelta) * randomValue;
                var sign = RandomDouble() < 0.5 ? -1.0 : 1.0;

                result = value + deltaAbs * sign;
            }

            if (result < minValue) result = minValue;
            if (result > maxValue) result = maxValue;

            return result;
        }

        private static void MutateAiFile(string originalFile, string targetFile) {
            File.Delete(targetFile);

            File.WriteAllLines(targetFile, File.ReadAllLines(originalFile).Select(x => {
                var split = x.Trim().Split('=');
                double oldValue, newValue;
                if (split.Length != 2 ||
                    !double.TryParse(split[1], NumberStyles.Any, CultureInfo.InvariantCulture, out oldValue)) {
                    return x;
                }

                switch (split[0]) {
                    case "GASGAIN":
                    case "BRAKE_HINT":
                    case "TRAIL_HINT":
                    case "STEER_GAIN":
                        newValue = MutateDouble(oldValue, 0.01, 0.5, 0.3, 4.5);
                        break;

                    case "DOWN":
                        newValue = MutateDouble(oldValue, 10, 500, 1000, 5000);
                        break;

                    default:
                        return x;
                }

                return newValue.Equals(oldValue) ? x : split[0] + "=" + newValue.ToString("F5");
            }));
        }

        static int Main(string[] args) {
            var options = new Options();
            if (!Parser.Default.ParseArguments(args, options)) {
                return 1;
            }

            Iteration(options.CarsDir, options.CarId, options.GenerationSize, options.TrackId, options.RaceLaps,
                (int)(60 * options.RaceTimeout * options.RaceLaps), options.IterationsPerGeneration);

            Console.ReadKey();
            return 0;
        }
    }
}
