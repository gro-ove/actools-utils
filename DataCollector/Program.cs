using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.DataFile;
// ReSharper disable LocalizableElement

namespace DataCollector {
    class Program {
        enum Format {
            TEXT, CSV, CSV_FORMULA, CSV_COMMA, HTML, JSON
        }

        private class Options {
            [ValueList(typeof(List<string>))]
            public IList<string> Cars { get; set; }

            [Option('d', "directory", Required = true, DefaultValue = null, HelpText = "Path to content/cars directory.")]
            public string Directory { get; set; }

            [OptionList('f', "fields", Required = true, HelpText = "List of fields to collect (FILE1/HEADER1/PROPERTY1, PROPERTY2, FILE2/HEADER2/PROPERTY3, ...).")]
            public IList<string> Fields { get; set; }

            [Option('o', "format", Required = false, DefaultValue = Format.TEXT, HelpText = "Output format (TEXT/CSV/HTML/JSON).")]
            public Format Format { get; set; }

            [Option('v', "verbose", Required = false, DefaultValue = false, HelpText = "Verbose mode.")]
            public bool Verbose { get; set; }

            [ParserState]
            public IParserState LastParserState { get; set; }

            [HelpOption]
            public string GetUsage() {
                return HelpText.AutoBuild(this, (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
            }
        }

        class Field {
            public string Filename, Header, Property;
            public Dictionary<string, string> Values = new Dictionary<string,string>();

            public override string ToString(){
                return Filename + "/" + Header + "/" + Property;
            }
        }

        static int Main(string[] args) {
            var options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(args, options) && options.Cars.Count > 0) {
                var list = String.Join(",", options.Cars).Split(',').Select(x => x.Trim());

                var fields = new Dictionary<string, List<Field>>();
                var fieldsList = new List<Field>();

                string file = null, header = null, property = null;
                foreach (var raw in String.Join(",", options.Fields).Split(',').Select(x => x.Trim())) {
                    var s = raw.Split(new char[]{ '/' }, 3);
                    property = s[s.Length - 1];

                    if (s.Length > 1) {
                        header = s[s.Length - 2];
                    }

                    if (s.Length > 2) {
                        file = s[s.Length - 3];
                    }

                    if (file == null || header == null) {
                        Console.Error.WriteLine("invalid field '{0}/{1}/{2}'", file, header, property);
                    }

                    if (!fields.ContainsKey(file)) {
                        fields[file] = new List<Field>();
                    }

                    var f = new Field { Filename = file, Header = header, Property = property };
                    fieldsList.Add(f);
                    fields[file].Add(f);
                }

                switch (options.Format) {
                    case Format.CSV:
                    case Format.CSV_COMMA:
                    case Format.CSV_FORMULA:
                        Console.Write("Car Name");
                        foreach (var field in fieldsList) {
                            Console.Write(",\"{0}\"", field);
                        }
                        Console.Write("\n");
                        break;
                    case Format.JSON:
                        Console.Write("[");
                        break;
                    case Format.HTML:
                        Console.Write("<table>\n  <thead>\n    <tr><th>Car Name</th>");
                        foreach (var field in fieldsList) {
                            Console.Write("<th>{0}</th>", field);
                        }
                        Console.Write("</tr>\n  </thead>\n  <tbody>");
                        break;
                }

                var number = 0;
                foreach (var car in list) {
                    var dir = Path.Combine(options.Directory, car);
                    if (!Directory.Exists(dir)) {
                        Console.Error.WriteLine("directory '{0}' not found", dir);
                        continue;
                    }

                    switch (options.Format) {
                        case Format.TEXT:
                            Console.WriteLine("car: {0}", car);
                            break;
                        case Format.CSV:
                        case Format.CSV_COMMA:
                        case Format.CSV_FORMULA:
                            if (options.Verbose) Console.Error.WriteLine("car: {0}", car);
                            Console.Write("\"{0}\"", car);
                            break;
                        case Format.JSON:
                            if (options.Verbose) Console.Error.WriteLine("car: {0}", car);
                            if (number > 0) {
                                Console.Write(",");
                            }
                            Console.Write("\n  { \"name\": \"" + car + "\"");
                            break;
                        case Format.HTML:
                            if (options.Verbose) Console.Error.WriteLine("car: {0}", car);
                            Console.Write("\n    <tr><td>{0}</td>", car);
                            break;
                    }

                    foreach (var entry in fields) {
                        var iniFile = new IniFile(dir, entry.Key);

                        foreach (var field in entry.Value) {
                            var value = iniFile[field.Header].Get(field.Property);
                            field.Values[car] = value;

                            switch (options.Format) {
                                case Format.TEXT:
                                    Console.WriteLine("\t{0}: {1}", field, value);
                                    break;
                                case Format.CSV:
                                    Console.Write(",\"{0}\"", value);
                                    break;
                                case Format.CSV_COMMA:
                                    Console.Write(",\"{0}\"", value.Replace('.', ','));
                                    break;
                                case Format.CSV_FORMULA:
                                    Console.Write(",\"=\"\"{0}\"\"\"", value);
                                    break;
                                case Format.JSON:
                                    Console.Write(", \"" + field + "\": \"" + value + "\"");
                                    break;
                                case Format.HTML:
                                    Console.Write("<td>{0}</td>", value);
                                    break;
                            }
                        }
                    }

                    switch (options.Format) {
                        case Format.CSV:
                        case Format.CSV_COMMA:
                        case Format.CSV_FORMULA:
                            Console.Write("\n");
                            break;
                        case Format.JSON:
                            Console.Write(" }");
                            break;
                        case Format.HTML:
                            Console.Write("</tr>");
                            break;
                    }

                    number++;
                }

                switch (options.Format) {
                    case Format.JSON:
                        Console.Write("\n]");
                        break;
                    case Format.HTML:
                        Console.Write("\n  </tbody>\n</table>");
                        break;
                }

                if (options.Format == Format.TEXT) {
                    Console.ReadKey();
                }

                return 0;
            } else {
                return 1;
            }
        }
    }
}
