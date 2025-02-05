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

        // Check if no arguments are provided, which means stdin should be used
        if (args.Length == 0)
        {
            useStdin = true;
        }
        else if (args.Length >= 3)
        {
            searchPattern = args[0];
            inputFilePath = args[1];
            outputFilePath = args[2];
        }
        else if (args.Length == 2)
        {
            searchPattern = args[0];
            inputFilePath = args[1];
        }
        else
        {
            Console.WriteLine("Usage: grep <search_pattern> [<file_path>] [<output_file_path>]");
            return;
        }

        if (useStdin)
        {
            // Read from stdin
            while ((inputFilePath = Console.ReadLine()) != null)
            {
                if (inputFilePath.Contains(searchPattern))
                {
                    WriteLineToDestination(inputFilePath, outputFilePath, writeToConsole);
                }
            }
        }
        else
        {
            // Read from file path provided as argument
            try
            {
                foreach (string line in File.ReadLines(inputFilePath))
                {
                    if (line.Contains(searchPattern))
                    {
                        WriteLineToDestination(line, outputFilePath, writeToConsole);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
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