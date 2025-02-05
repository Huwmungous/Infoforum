using System;
using System.IO;

class Grep
{
    public static string searchPattern = "";
    public static string? inputFilePath = "";
    public static string? outputFilePath = "";
    public static bool writeToConsole = true;
    public static bool recursive = false;
    public static bool useStdin = false;

    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: grep <search_pattern> [-r] [<file_path>] [<output_file_path>]");
            return;
        }
        else
            Grep.searchPattern = args[0];

        parseCommandLine(args);

        if (Console.IsInputRedirected)
        {
            // Read file paths from standard input
            string? line;
            while ((line = Console.ReadLine()) != null)
            {
                ProcessFile(line);
            }
        }
        else
        {
            if (Grep.useStdin)
            {
                Console.WriteLine("Enter the file path:");
                Grep.inputFilePath = Console.ReadLine();
            }

            if (string.IsNullOrEmpty(Grep.inputFilePath))
            {
                Console.WriteLine("Usage: grep <search_pattern> [-r] [<file_path>] [<output_file_path>]");
                return;
            }

            try
            {
                if (Grep.recursive)
                {
                    foreach (var file in Directory.GetFiles(Grep.inputFilePath, "*", SearchOption.AllDirectories))
                    {
                        ProcessFile(file);
                    }
                }
                else
                {
                    ProcessFile(Grep.inputFilePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }

    static void parseCommandLine(string[] args)
    {
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "-r")
            {
                Grep.recursive = true;
            }
            else if (args[i] == "-")
            {
                Grep.useStdin = true;
            }
            else if (i == args.Length - 1)
            {
                Grep.outputFilePath = args[i];
                Grep.writeToConsole = false;
            }
            else
            {
                Grep.inputFilePath = args[i];
            }
        }
    }

    static void WriteLineToDestination(string line, string? outputFilePath, bool writeToConsole)
    {
        if (writeToConsole || string.IsNullOrEmpty(outputFilePath))
        {
            Console.WriteLine(line);
        }

        if (!string.IsNullOrEmpty(outputFilePath))
        {
            File.AppendAllText(outputFilePath, line + Environment.NewLine);
        }
    }

    static void ProcessFile(string filePath)
    {
        foreach (var line in File.ReadLines(filePath))
        {
            if (line.Contains(Grep.searchPattern))
            {
                string outputLine = $"{filePath}: {line}";
                System.Console.WriteLine( filePath, Grep.writeToConsole );
                WriteLineToDestination(outputLine, Grep.outputFilePath, Grep.writeToConsole);
            }
        }
    }
}