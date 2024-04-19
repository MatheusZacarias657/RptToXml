using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RptToXml
{
	internal class Program
	{
		internal static int Main(string[] args)
		{
            var inputArg = new Argument<string>(
                name: "input",
                description:
                    "-r            Recursively convert all rpt files in current directory and sub directories." + Environment.NewLine +
                    "RPT filename  Process a single RPT file.");

            var ignoreErrorsOption = new Option<bool>(
                name: "--ignore-errors",
                getDefaultValue: () => false,
                description: "When processing multiple files, continue to the next file if an error occurs.");

            var stdoutOption = new Option<bool>(
                name: "--stdout",
                getDefaultValue: () => false,
                description: "Write the XML to console output. Suppresses all other output text (status, warnings, etc). Not valid with outputfilename.");

            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

            int exitCode = 0;
            var command = new RootCommand("RPT2XML");
			command.AddArgument(inputArg);
			command.AddOption(ignoreErrorsOption);
			command.AddOption(stdoutOption);
            command.SetHandler((
                    input,
                    ignoreErrors,
                    stdout) => exitCode = Execute(input, ignoreErrors, stdout), inputArg,
                ignoreErrorsOption, stdoutOption);

            command.Invoke(args);

            return exitCode;

        }

        private static int Execute(string input, bool ignoreErrors, bool stdOut)
        {
            List<string> rptPaths = FindRptPaths(input);
            
            if (rptPaths.Count == 0)
            {
                string errorMessage = input.Equals("-r", StringComparison.OrdinalIgnoreCase)
                    ? "No *.RPT files found rescursively in current directory."
                    : $"No input files matched {input}.";
				Console.WriteLine(errorMessage);
                return 1;
            }

            int exitCode = 0;


            string folderPath = @"XMLConverted";

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            Parallel.ForEach(
                rptPaths,
                stdOut ? new ParallelOptions { MaxDegreeOfParallelism = 1 } : new ParallelOptions(),
                rptPath =>
                {
                    if (!File.Exists(rptPath))
                    {
                        Console.WriteLine($"{rptPath} does not exist.");
                        exitCode = 1;
                        return;
                    }

                    try
                    {
                        if (!stdOut)
                        {
                            Trace.WriteLine("Dumping " + rptPath);
                        }

                        

                        using (var writer = new RptDefinitionWriter(rptPath, stdOut))
                        {
                            if (stdOut)
                            {
                                writer.WriteToXml();
                            }
                            else
                            {
                                string xmlPath = $"{folderPath}/{Path.ChangeExtension(rptPath, "xml")}";
                                Trace.WriteLine("Saving in " + xmlPath);
                                writer.WriteToXml(xmlPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ignoreErrors)
                        {
                            Trace.WriteLine(ex.Message);
                        }
                        else
                        {
                            throw;
                        }
                    }
                });

            return exitCode;
        }

        private static List<string> FindRptPaths(string input)
        {
            if (input.Equals("-r", StringComparison.OrdinalIgnoreCase))
            {
                return Directory.GetFiles(".", "*.rpt", SearchOption.AllDirectories).ToList();
            }

            if (input.Contains("*"))
            {
                return Directory.GetFiles(Path.GetDirectoryName(input) ?? ".", Path.GetFileName(input)).ToList();
            }

            return new List<string> { input };
        }
    }
}
