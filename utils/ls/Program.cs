using System;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        // Default directory to display files from
        string directoryPath = ".";
        string filter = "*";

        // Parse command-line arguments
        if (args.Length > 0)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-d" && i + 1 < args.Length)
                {
                    directoryPath = args[i + 1];
                    i++;
                }
                else if (args[i] == "-f" && i + 1 < args.Length)
                {
                    filter = args[i + 1];
                    i++;
                }
            }
        }

        // Validate the directory path
        if (!Directory.Exists(directoryPath))
        {
            Console.WriteLine($"The specified directory '{directoryPath}' does not exist.");
            return;
        }

        try
        {
            // Get all files in the specified directory that match the filter
            string[] files = Directory.GetFiles(directoryPath, filter);

            // Display the list of files
            foreach (string file in files)
            {
                Console.WriteLine(file);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
}
