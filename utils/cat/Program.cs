using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cat
{
    /// <summary>
    /// A simplified re‑implementation of GNU cat.
    /// Supports options: 
    ///   -A (show-all = equivalent to -vET), 
    ///   -b (number nonblank lines), 
    ///   -e (equivalent to -vE), 
    ///   -E (show ends), 
    ///   -n (number all lines), 
    ///   -s (squeeze blank lines), 
    ///   -t (equivalent to -vT), 
    ///   -T (show tabs), 
    ///   -u (ignored), 
    ///   -v (show nonprinting)
    /// </summary>
    public static class CatProgram
    {
        /// <summary>
        /// The main run method. Parses command‑line options, then processes each file (or standard input).
        /// </summary>
        public static int Run(string[] args)
        {
            // Option flags.
            bool showNonPrinting = false;
            bool showEnds = false;
            bool showTabs = false;
            bool number = false;
            bool numberNonBlank = false;
            bool squeezeBlank = false;
            // When none of the above options are set, we'll operate in raw mode.
            bool rawMode = true;

            // Collect file names.
            List<string> fileArgs = new List<string>();

            // Process command‑line arguments.
            foreach (string arg in args)
            {
                // Options (starting with '-' but not a lone "-" which means STDIN).
                if (arg.StartsWith("-") && arg.Length > 1)
                {
                    // Recognize long options for help/version.
                    if (arg == "--help")
                    {
                        PrintUsage();
                        return 0;
                    }
                    else if (arg == "--version")
                    {
                        Console.WriteLine("cat version 1.0");
                        return 0;
                    }
                    else
                    {
                        // Process each character after '-'.
                        for (int i = 1; i < arg.Length; i++)
                        {
                            switch (arg[i])
                            {
                                case 'A': // equivalent to -vET
                                    showNonPrinting = true;
                                    showEnds = true;
                                    showTabs = true;
                                    break;
                                case 'b': // number nonblank lines (implies numbering; overrides -n)
                                    number = true;
                                    numberNonBlank = true;
                                    break;
                                case 'e': // equivalent to -vE
                                    showEnds = true;
                                    showNonPrinting = true;
                                    break;
                                case 'E': // show ends ($ at line end)
                                    showEnds = true;
                                    break;
                                case 'n': // number all lines
                                    number = true;
                                    break;
                                case 's': // squeeze blank lines
                                    squeezeBlank = true;
                                    break;
                                case 't': // equivalent to -vT
                                    showTabs = true;
                                    showNonPrinting = true;
                                    break;
                                case 'T': // show tabs as "^I"
                                    showTabs = true;
                                    break;
                                case 'u': // ignored (unbuffered)
                                    break;
                                case 'v': // show nonprinting characters
                                    showNonPrinting = true;
                                    break;
                                default:
                                    Console.Error.WriteLine("Unknown option: -{0}", arg[i]);
                                    PrintUsage();
                                    return 1;
                            }
                        }
                    }
                }
                else
                {
                    // Not an option: treat as a file name.
                    fileArgs.Add(arg);
                }
            }

            // If any formatting option is set, we're not in raw mode.
            if (showNonPrinting || showEnds || showTabs || number || numberNonBlank || squeezeBlank)
            {
                rawMode = false;
            }

            // If no files were specified, read from standard input.
            if (fileArgs.Count == 0)
            {
                fileArgs.Add("-");
            }

            int exitCode = 0;
            foreach (var filename in fileArgs)
            {
                if (filename == "-")
                {
                    // Process standard input.
                    try
                    {
                        if (rawMode)
                        {
                            // In raw mode, perform a binary copy.
                            using (Stream input = Console.OpenStandardInput())
                            using (Stream output = Console.OpenStandardOutput())
                            {
                                input.CopyTo(output);
                            }
                        }
                        else
                        {
                            // In text mode, process line by line.
                            ProcessTextStream(Console.In, Console.Out, showNonPrinting, showEnds, showTabs, number, numberNonBlank, squeezeBlank);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("cat: standard input: {0}", ex.Message);
                        exitCode = 1;
                    }
                }
                else
                {
                    // Process a regular file.
                    if (!File.Exists(filename))
                    {
                        Console.Error.WriteLine("cat: {0}: No such file", filename);
                        exitCode = 1;
                        continue;
                    }
                    try
                    {
                        if (rawMode)
                        {
                            using (Stream input = File.OpenRead(filename))
                            using (Stream output = Console.OpenStandardOutput())
                            {
                                input.CopyTo(output);
                            }
                        }
                        else
                        {
                            using (StreamReader reader = new StreamReader(filename))
                            {
                                ProcessTextStream(reader, Console.Out, showNonPrinting, showEnds, showTabs, number, numberNonBlank, squeezeBlank);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("cat: {0}: {1}", filename, ex.Message);
                        exitCode = 1;
                    }
                }
            }
            return exitCode;
        }

        /// <summary>
        /// Processes the input text stream line by line, applying the desired options.
        /// </summary>
        static void ProcessTextStream(TextReader reader, TextWriter writer,
                                      bool showNonPrinting, bool showEnds, bool showTabs,
                                      bool number, bool numberNonBlank, bool squeezeBlank)
        {
            string? line;
            int lineNumber = 1;
            bool previousLineBlank = false;
            while ((line = reader.ReadLine()) != null)
            {
                // Determine whether this is an empty line.
                bool isBlank = string.IsNullOrEmpty(line);
                if (squeezeBlank && isBlank && previousLineBlank)
                {
                    // Skip repeated empty lines.
                    continue;
                }
                previousLineBlank = isBlank;

                // Process the line (convert control characters if requested).
                string processedLine = ProcessLine(line, showNonPrinting, showTabs, showEnds);

                if (number)
                {
                    // When numbering nonblank lines only, skip numbering blank lines.
                    if (numberNonBlank && isBlank)
                    {
                        writer.WriteLine(processedLine);
                    }
                    else
                    {
                        writer.WriteLine($"{lineNumber,6}\t{processedLine}");
                        lineNumber++;
                    }
                }
                else
                {
                    writer.WriteLine(processedLine);
                }
            }
        }

        /// <summary>
        /// Processes one line of text. Converts tab characters and non‑printing characters if requested,
        /// and appends a '$' at the end if showEnds is specified.
        /// </summary>
        static string ProcessLine(string line, bool showNonPrinting, bool showTabs, bool showEnds)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in line)
            {
                if (ch == '\t')
                {
                    if (showTabs)
                        sb.Append("^I");
                    else
                        sb.Append(ch);
                }
                else if (ch < 32)
                {
                    // Control characters (except newline, which is removed by ReadLine)
                    if (showNonPrinting)
                    {
                        sb.Append('^');
                        sb.Append((char)(ch + 64)); // e.g., 0x01 becomes ^A
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                }
                else if (ch == 127)
                {
                    if (showNonPrinting)
                        sb.Append("^?");
                    else
                        sb.Append(ch);
                }
                else
                {
                    sb.Append(ch);
                }
            }
            if (showEnds)
                sb.Append('$');
            return sb.ToString();
        }

        /// <summary>
        /// Prints a short usage message to standard error.
        /// </summary>
        static void PrintUsage()
        {
            Console.Error.WriteLine("Usage: cat [OPTION]... [FILE]...");
            Console.Error.WriteLine("Concatenate FILE(s) to standard output.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("  -A, --show-all           equivalent to -vET");
            Console.Error.WriteLine("  -b, --number-nonblank    number nonempty output lines, overrides -n");
            Console.Error.WriteLine("  -e                       equivalent to -vE");
            Console.Error.WriteLine("  -E, --show-ends          display $ at end of each line");
            Console.Error.WriteLine("  -n, --number             number all output lines");
            Console.Error.WriteLine("  -s, --squeeze-blank      suppress repeated empty output lines");
            Console.Error.WriteLine("  -t                       equivalent to -vT");
            Console.Error.WriteLine("  -T, --show-tabs          display TAB characters as ^I");
            Console.Error.WriteLine("  -u                       (ignored)");
            Console.Error.WriteLine("  -v, --show-nonprinting   use ^ and M- notation, except for LFD and TAB");
        }
    }

    /// <summary>
    /// The single entry point of the application.
    /// </summary>
    public class Program
    {
        public static int Main(string[] args)
        {
            return CatProgram.Run(args);
        }
    }
}
