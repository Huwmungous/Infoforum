using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Grep
{
    /// <summary>
    /// A simplified re‑implementation of grep.
    /// Supports options:
    ///   -i, --ignore-case         Ignore case distinctions
    ///   -v, --invert-match        Select non-matching lines
    ///   -n, --line-number         Prefix output lines with their line number
    ///   -c, --count               Only print a count of matching lines per FILE
    ///   -l, --files-with-matches  Only print FILE names that contain matches
    ///   -r, --recursive           Recursively search subdirectories
    /// </summary>
    public static class GrepProgram
    {
        /// <summary>
        /// Main processing routine.
        /// </summary>
        public static int Run(string[] args)
        {
            // Option flags.
            bool ignoreCase = false;
            bool invertMatch = false;
            bool printLineNumbers = false;
            bool countMatches = false;
            bool listFileNames = false;
            bool recursive = false;

            // List of file names to process.
            List<string> files = new List<string>();

            // The search pattern.
            string? pattern = null;

            // Parse command‑line options.
            int argIndex = 0;
            while (argIndex < args.Length && args[argIndex].StartsWith("-") && args[argIndex] != "-")
            {
                string arg = args[argIndex];
                if (arg == "--help")
                {
                    PrintUsage();
                    return 0;
                }
                else if (arg == "--version")
                {
                    Console.WriteLine("grep version 1.0");
                    return 0;
                }
                else if (arg.StartsWith("--"))
                {
                    // Process long options.
                    switch (arg)
                    {
                        case "--ignore-case":
                            ignoreCase = true;
                            break;
                        case "--invert-match":
                            invertMatch = true;
                            break;
                        case "--line-number":
                            printLineNumbers = true;
                            break;
                        case "--count":
                            countMatches = true;
                            break;
                        case "--files-with-matches":
                            listFileNames = true;
                            break;
                        case "--recursive":
                            recursive = true;
                            break;
                        default:
                            Console.Error.WriteLine("Unknown option: {0}", arg);
                            PrintUsage();
                            return 1;
                    }
                }
                else
                {
                    // Process short options (e.g. -i, -v, etc.).
                    for (int i = 1; i < arg.Length; i++)
                    {
                        switch (arg[i])
                        {
                            case 'i':
                                ignoreCase = true;
                                break;
                            case 'v':
                                invertMatch = true;
                                break;
                            case 'n':
                                printLineNumbers = true;
                                break;
                            case 'c':
                                countMatches = true;
                                break;
                            case 'l':
                                listFileNames = true;
                                break;
                            case 'r':
                                recursive = true;
                                break;
                            default:
                                Console.Error.WriteLine("Unknown option: -{0}", arg[i]);
                                PrintUsage();
                                return 1;
                        }
                    }
                }
                argIndex++;
            }

            // The next argument must be the search pattern.
            if (argIndex >= args.Length)
            {
                Console.Error.WriteLine("grep: missing search pattern");
                PrintUsage();
                return 1;
            }
            pattern = args[argIndex++];
            
            // Compile the regular expression.
            RegexOptions regexOptions = RegexOptions.Compiled;
            if (ignoreCase)
            {
                regexOptions |= RegexOptions.IgnoreCase;
            }
            Regex regex;
            try
            {
                regex = new Regex(pattern, regexOptions);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine("grep: invalid regular expression: {0}", ex.Message);
                return 1;
            }

            // Remaining arguments are files. If none, use standard input.
            if (argIndex < args.Length)
            {
                while (argIndex < args.Length)
                {
                    files.Add(args[argIndex++]);
                }
            }
            else
            {
                files.Add("-");
            }

            int exitCode = 0;
            // Process each file (or standard input).
            foreach (string file in files)
            {
                if (file == "-")
                {
                    exitCode |= ProcessStream(Console.In, regex, printLineNumbers, invertMatch, countMatches, listFileNames, "standard input");
                }
                else
                {
                    if (File.Exists(file))
                    {
                        // If recursive search is enabled and the file is a directory,
                        // process all files under that directory.
                        if (recursive && Directory.Exists(file))
                        {
                            exitCode |= ProcessDirectory(file, regex, printLineNumbers, invertMatch, countMatches, listFileNames);
                        }
                        else if (Directory.Exists(file))
                        {
                            Console.Error.WriteLine("grep: {0}: Is a directory", file);
                            exitCode |= 2;
                        }
                        else
                        {
                            exitCode |= ProcessFile(file, regex, printLineNumbers, invertMatch, countMatches, listFileNames);
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine("grep: {0}: No such file or directory", file);
                        exitCode |= 2;
                    }
                }
            }
            return exitCode;
        }

        /// <summary>
        /// Processes a directory recursively.
        /// </summary>
        static int ProcessDirectory(string directory, Regex regex, bool printLineNumbers, bool invertMatch, bool countMatches, bool listFileNames)
        {
            int exitCode = 0;
            try
            {
                foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    exitCode |= ProcessFile(file, regex, printLineNumbers, invertMatch, countMatches, listFileNames);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("grep: error processing directory {0}: {1}", directory, ex.Message);
                exitCode |= 2;
            }
            return exitCode;
        }

        /// <summary>
        /// Processes a single file.
        /// </summary>
        static int ProcessFile(string filename, Regex regex, bool printLineNumbers, bool invertMatch, bool countMatches, bool listFileNames)
        {
            try
            {
                using (StreamReader reader = new StreamReader(filename))
                {
                    return ProcessStream(reader, regex, printLineNumbers, invertMatch, countMatches, listFileNames, filename);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("grep: {0}: {1}", filename, ex.Message);
                return 2;
            }
        }

        /// <summary>
        /// Processes a text stream (from a file or standard input).
        /// </summary>
        static int ProcessStream(TextReader reader, Regex regex, bool printLineNumbers, bool invertMatch, bool countMatches, bool listFileNames, string sourceName)
        {
            int exitCode = 1; // 1 indicates no match found.
            int matchCount = 0;
            int lineNumber = 1;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                bool isMatch = regex.IsMatch(line);
                if (invertMatch)
                {
                    isMatch = !isMatch;
                }
                if (isMatch)
                {
                    matchCount++;
                    exitCode = 0; // At least one match found.
                    if (listFileNames)
                    {
                        Console.WriteLine(sourceName);
                        return exitCode;
                    }
                    if (!countMatches)
                    {
                        // When processing more than one file, prefix output with the file name.
                        if (sourceName != "standard input" && File.Exists(sourceName))
                        {
                            Console.Write($"{sourceName}:");
                        }
                        if (printLineNumbers)
                        {
                            Console.Write($"{lineNumber}:");
                        }
                        Console.WriteLine(line);
                    }
                }
                lineNumber++;
            }
            if (countMatches)
            {
                if (sourceName != "standard input" && File.Exists(sourceName))
                {
                    Console.Write($"{sourceName}:");
                }
                Console.WriteLine(matchCount);
            }
            return exitCode;
        }

        /// <summary>
        /// Prints a usage message to standard error.
        /// </summary>
        static void PrintUsage()
        {
            Console.Error.WriteLine("Usage: grep [OPTIONS] PATTERN [FILE...]");
            Console.Error.WriteLine("Search for PATTERN in each FILE.");
            Console.Error.WriteLine("Example: grep -n 'hello' foo.txt");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  -i, --ignore-case         Ignore case distinctions");
            Console.Error.WriteLine("  -v, --invert-match        Select non-matching lines");
            Console.Error.WriteLine("  -n, --line-number         Prefix each line with its line number");
            Console.Error.WriteLine("  -c, --count               Print only a count of matching lines per FILE");
            Console.Error.WriteLine("  -l, --files-with-matches  Print only names of FILEs with matching lines");
            Console.Error.WriteLine("  -r, --recursive           Recursively search subdirectories");
            Console.Error.WriteLine("      --help                Display this help and exit");
            Console.Error.WriteLine("      --version             Display version information and exit");
        }
    }

    /// <summary>
    /// Single entry point.
    /// </summary>
    public class Program
    {
        public static int Main(string[] args)
        {
            return GrepProgram.Run(args);
        }
    }
}
