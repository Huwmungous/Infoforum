using System;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        bool useStdin = false;
        string searchPattern = "";
        string inputFilePath = "";
        string outputFilePath = "";
        bool writeToConsole = true;
        bool recursive = false;

        // Parse command line arguments to determine options and inputs
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-r")
            {
                recursive = true;
            }
            else if (useStdin == false)
            {
                if (i + 1 < args.Length && args[i + 1] != "-r" && !args[i + 1].StartsWith("-"))
                {
                    inputFilePath = args[i];
                    outputFilePath = args[++i];
                }
                else
                {
                    searchPattern = args[i];
                }
            }
        }

        if (args.Length == 0 || (useStdin == false && string.IsNullOrEmpty(inputFilePath)))
        {
            Console.WriteLine("Usage: grep <search_pattern> [-r] [<file_path>] [<output_file_path>]");
            return;
        }

        if (useStdin)
        {
            // Read from stdin
            while ((inputFilePath = Console.ReadLine()) != null)
            {
                if (recursive || inputFilePath.Contains(searchPattern))
                {
                    WriteLineToDestination(inputFilePath, outputFilePath, writeToConsole);
                }
            }
        }
        else
        {
            // Read from file path provided as argument and search recursively if needed
            try
            {
                SearchDirectoryForPattern(inputFilePath, searchPattern, outputFilePath, recursive, writeToConsole);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }

    static void SearchDirectoryForPattern(string directoryPath, string searchPattern, string outputFilePath, bool recursive, bool writeToConsole)
    {
        foreach (string file in Directory.GetFiles(directoryPath))
        {
            if (File.Exists(file)) // Ensure the path is a file and not a directory entry
            {
                foreach (string line in File.ReadLines(file))
                {
                    if (line.Contains(searchPattern))
                    {
                        WriteLineToDestination(line, outputFilePath, writeToConsole);
                    }
                }
            }
        }

        if (recursive)
        {
            foreach (string directory in Directory.GetDirectories(directoryPath))
            {
                SearchDirectoryForPattern(directory, searchPattern, outputFilePath, recursive, writeToConsole);
            }
        }
    }

    static void WriteLineToDestination(string line, string outputFilePath, bool writeToConsole)
    {
        if (writeToConsole)
        {
            Console.WriteLine(line);
        }

        if (!string.IsNullOrEmpty(outputFilePath))
        {
            File.AppendAllText(outputFilePath, line + Environment.NewLine);
        }
    }
}
