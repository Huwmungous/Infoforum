using System;
using System.IO;

class Ls
{
    static void Main(string[] args)
    {
        // Default directory to display files from
        string directoryPath = ".";
        string nameFilter = "*";

        // Parse command-line arguments
        if (args.Length > 0)
        {
            string inputPath = args[0];
            string fullPath = Path.GetFullPath(inputPath);

            if (Directory.Exists(fullPath))
            {
                directoryPath = fullPath;
                nameFilter = "*";
            }
            else
            {
                directoryPath = Path.GetDirectoryName(fullPath) ?? ".";
                nameFilter = Path.GetFileName(fullPath) ?? "*";
            }
        }

        // Validate the directory path
        if (!Directory.Exists(directoryPath))
            throw new Exception($"The specified directory '{directoryPath}' does not exist.");

        try
        {
            // Get all files in the specified directory that match the filter
            string[] files = Directory.GetFiles(directoryPath, nameFilter);

            // Display the files
            foreach (string file in files)
            {
                Console.WriteLine(file);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}