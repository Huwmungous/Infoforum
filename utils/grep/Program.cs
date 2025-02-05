using System;
using System.IO;

class Grep
{
    static bool useStdin = false;
    static string searchPattern = "";
    static string inputFilePath = ".";
    static string outputFilePath = "";
    static bool writeToConsole = true;
    static bool recursive = false;

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

        if (Grep.useStdin == false && string.IsNullOrEmpty(Grep.inputFilePath))
        {
            Console.WriteLine("Usage: grep <search_pattern> [-r] [<file_path>] [<output_file_path>]");
            return;
        }

        if (Grep.useStdin)
        {
            // Read from stdin
            while ((Grep.inputFilePath? = Console.ReadLine()) != null)
            {
                if (Grep.recursive || Grep.inputFilePath.Contains(searchPattern))
                {
                    WriteLineToDestination(Grep.inputFilePath, Grep.outputFilePath, Grep.writeToConsole);
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

    static void parseSwitch(string sw)
    {
        if(sw == "-r")
          recursive = true;
    }

    static void parseCommandLine(string[] args)
    {
        for (int i = 1; i < args.Length; i++)
        {
            Console.WriteLine(args[i]);
            
            if (args[i].StartsWith("-")) 
              parseSwitch(args[i]);

            else if (!useStdin)
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
