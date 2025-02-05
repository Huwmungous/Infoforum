using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace LS
{
    // A simple entry representing a file or directory,
    // roughly analogous to the FTSENT structure.
    public class FileSystemEntry
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public FileAttributes Attributes { get; set; }
        public long Size { get; set; }
        public DateTime LastWriteTime { get; set; }
        // (Other fields such as inode, nlink, owner, etc. are omitted or stubbed.)
        public string Mode { get; set; } = "----------"; // stub permissions string
        public int Nlink { get; set; } = 1;
        public string Owner { get; set; } = "owner"; // stub
    }

    // Structure to hold display parameters (like the DISPLAY struct in C)
    public class Display
    {
        public List<FileSystemEntry> List { get; set; }
        public int Entries { get; set; }
        public int MaxLen { get; set; }
        public long Btotal { get; set; }
        public long Stotal { get; set; }
        // Additional formatting fields can be added as needed.
    }

    public static class LSProgram
    {
        // --- Global variables (mirroring the C globals) ---
        // Terminal and sort settings.
        static int termwidth = 80;
        const int BY_NAME = 0;
        const int BY_SIZE = 1;
        const int BY_TIME = 2;
        static int sortkey = BY_NAME;
        static int rval = 0;  // exit value (0 = success)

        // Flags (most default to false; some may be tri‐state in the original)
        static bool f_accesstime = false;
        static bool f_column = false;
        static bool f_columnacross = false;
        static bool f_flags = false;
        static bool f_grouponly = false;
        static bool f_humanize = false;
        static bool f_inode = false;
        static bool f_listdir = false;
        static bool f_listdot = false;
        static bool f_longform = false;
        static bool f_nonprint = false;
        static bool f_nosort = false;
        static bool f_numericonly = false;
        static bool f_octal = false;
        static bool f_octal_escape = false;
        static bool f_recursive = false;
        static bool f_reversesort = false;
        static bool f_sectime = false;
        static bool f_singlecol = false;
        static bool f_size = false;
        static bool f_statustime = false;
        static bool f_stream = false;
        static bool f_type = false;
        static bool f_typedir = false;
        static bool f_whiteout = false;

        // Block size and kflag (for -k and -h)
        static long blocksize = 512;
        static int kflag = 0;

        // Delegates for printing and sorting.
        delegate void PrintFcn(Display d);
        static PrintFcn printfcn;

        delegate int SortFcn(FileSystemEntry a, FileSystemEntry b);
        static SortFcn sortfcn;

        /// <summary>
        /// Instead of serving as an entry point, this method does the work that was previously in Main.
        /// </summary>
        public static int Run(string[] args)
        {
            // Set the locale if desired. In .NET, the current culture is used.
            CultureInfo.CurrentCulture = CultureInfo.CurrentCulture;

            // Terminal defaults.
            if (!Console.IsOutputRedirected)
            {
                try
                {
                    termwidth = Console.WindowWidth;
                }
                catch { /* if not available, keep default */ }
                f_column = f_nonprint = true;
            }
            else
            {
                f_singlecol = true;
            }

            // Parse options.
            List<string> paths = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.StartsWith("-") && arg.Length > 1)
                {
                    // Process each flag character.
                    for (int j = 1; j < arg.Length; j++)
                    {
                        char ch = arg[j];
                        switch (ch)
                        {
                            case '1':
                                f_singlecol = true;
                                f_column = f_columnacross = f_longform = f_stream = false;
                                break;
                            case 'C':
                                f_column = true;
                                f_columnacross = f_longform = f_singlecol = f_stream = false;
                                break;
                            case 'g':
                                f_grouponly = true;
                                f_longform = true;
                                f_column = f_columnacross = f_singlecol = f_stream = false;
                                break;
                            case 'l':
                                f_longform = true;
                                f_column = f_columnacross = f_singlecol = f_stream = false;
                                f_grouponly = false;
                                break;
                            case 'm':
                                f_stream = true;
                                f_column = f_columnacross = f_longform = f_singlecol = false;
                                break;
                            case 'x':
                                f_columnacross = true;
                                f_column = f_longform = f_singlecol = f_stream = false;
                                break;
                            case 'c':
                                f_statustime = true;
                                f_accesstime = false;
                                break;
                            case 'u':
                                f_accesstime = true;
                                f_statustime = false;
                                break;
                            case 'F':
                                f_type = true;
                                break;
                            case 'L':
                                // Logical link handling (omitted here)
                                break;
                            case 'R':
                                f_recursive = true;
                                break;
                            case 'a':
                                f_listdot = true;
                                break;
                            case 'A':
                                f_listdot = true;
                                break;
                            case 'B':
                                f_nonprint = false;
                                f_octal = true;
                                f_octal_escape = false;
                                break;
                            case 'b':
                                f_nonprint = false;
                                f_octal = false;
                                f_octal_escape = true;
                                break;
                            case 'd':
                                f_listdir = true;
                                f_recursive = false;
                                break;
                            case 'f':
                                f_nosort = true;
                                break;
                            case 'i':
                                f_inode = true;
                                break;
                            case 'k':
                                blocksize = 1024;
                                kflag = 1;
                                f_humanize = false;
                                break;
                            case 'h':
                                f_humanize = true;
                                kflag = 0;
                                break;
                            case 'n':
                                f_numericonly = true;
                                f_longform = true;
                                f_column = f_columnacross = f_singlecol = f_stream = false;
                                break;
                            case 'o':
                                f_flags = true;
                                break;
                            case 'p':
                                f_typedir = true;
                                break;
                            case 'q':
                                f_nonprint = true;
                                f_octal = false;
                                f_octal_escape = false;
                                break;
                            case 'r':
                                f_reversesort = true;
                                break;
                            case 'S':
                                sortkey = BY_SIZE;
                                break;
                            case 's':
                                f_size = true;
                                break;
                            case 'T':
                                f_sectime = true;
                                break;
                            case 't':
                                sortkey = BY_TIME;
                                break;
                            case 'W':
                                f_whiteout = true;
                                break;
                            case 'w':
                                f_nonprint = false;
                                f_octal = false;
                                f_octal_escape = false;
                                break;
                            default:
                                Usage();
                                break;
                        }
                    }
                }
                else
                {
                    paths.Add(arg);
                }
            }

            // If no path specified, use the current directory.
            if (paths.Count == 0)
                paths.Add(".");

            // If the output format is column based and an environment variable "COLUMNS" is set, use it.
            string colEnv = Environment.GetEnvironmentVariable("COLUMNS");
            if ((f_column || f_columnacross || f_stream) && !string.IsNullOrEmpty(colEnv))
            {
                if (int.TryParse(colEnv, out int cols))
                    termwidth = cols;
            }

            // Select a sort function based on sortkey and reverse flag.
            if (f_reversesort)
            {
                switch (sortkey)
                {
                    case BY_NAME:
                        sortfcn = (a, b) => String.Compare(b.Name, a.Name, StringComparison.OrdinalIgnoreCase);
                        break;
                    case BY_SIZE:
                        sortfcn = (a, b) => b.Size.CompareTo(a.Size);
                        break;
                    case BY_TIME:
                        // Using LastWriteTime for demonstration.
                        sortfcn = (a, b) => DateTime.Compare(b.LastWriteTime, a.LastWriteTime);
                        break;
                }
            }
            else
            {
                switch (sortkey)
                {
                    case BY_NAME:
                        sortfcn = (a, b) => String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                        break;
                    case BY_SIZE:
                        sortfcn = (a, b) => a.Size.CompareTo(b.Size);
                        break;
                    case BY_TIME:
                        sortfcn = (a, b) => DateTime.Compare(a.LastWriteTime, b.LastWriteTime);
                        break;
                }
            }

            // Select a print function based on flags.
            if (f_singlecol)
                printfcn = PrintSingleColumn;
            else if (f_columnacross)
                printfcn = PrintAcrossColumns;
            else if (f_longform)
                printfcn = PrintLong;
            else if (f_stream)
                printfcn = PrintStream;
            else
                printfcn = PrintColumns;

            // Process each given path.
            foreach (string path in paths)
                Traverse(path);

            return rval;
        }

        // --- File traversal and display functions ---

        static void Traverse(string path)
        {
            // If f_listdir is set, list the directory entry itself.
            if (f_listdir)
            {
                List<FileSystemEntry> list = new List<FileSystemEntry>();
                FileSystemEntry entry = GetEntry(path);
                if (entry != null)
                    list.Add(entry);
                DisplayEntries(list, path);
            }
            else
            {
                FileAttributes attr;
                try
                {
                    attr = File.GetAttributes(path);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("{0}: {1}", path, ex.Message);
                    rval = 1;
                    return;
                }

                if ((attr & FileAttributes.Directory) != 0)
                {
                    Console.WriteLine("{0}:", path);
                    List<FileSystemEntry> entries = GetDirectoryEntries(path);
                    DisplayEntries(entries, path);

                    if (f_recursive)
                    {
                        foreach (var entry in entries)
                        {
                            if (!f_listdot && entry.Name.StartsWith("."))
                                continue;
                            try
                            {
                                if ((File.GetAttributes(entry.FullPath) & FileAttributes.Directory) != 0)
                                {
                                    Console.WriteLine();
                                    Traverse(entry.FullPath);
                                }
                            }
                            catch { }
                        }
                    }
                }
                else
                {
                    List<FileSystemEntry> list = new List<FileSystemEntry> { GetEntry(path) };
                    DisplayEntries(list, path);
                }
            }
        }

        static FileSystemEntry GetEntry(string path)
        {
            try
            {
                FileAttributes attr = File.GetAttributes(path);
                FileSystemEntry entry = new FileSystemEntry
                {
                    FullPath = Path.GetFullPath(path),
                    Name = Path.GetFileName(path)
                };
                if (string.IsNullOrEmpty(entry.Name))
                    entry.Name = path;  // For root directories

                entry.Attributes = attr;
                if ((attr & FileAttributes.Directory) != 0)
                {
                    entry.Size = 0;
                    entry.Mode = "d---------";  // Stub for directory permissions
                }
                else
                {
                    FileInfo fi = new FileInfo(path);
                    entry.Size = fi.Length;
                    entry.Mode = "----------";  // Stub for file permissions
                }
                entry.LastWriteTime = File.GetLastWriteTime(path);
                return entry;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("{0}: {1}", path, ex.Message);
                rval = 1;
                return null;
            }
        }

        static List<FileSystemEntry> GetDirectoryEntries(string path)
        {
            List<FileSystemEntry> list = new List<FileSystemEntry>();
            try
            {
                foreach (string entryPath in Directory.EnumerateFileSystemEntries(path))
                {
                    if (!f_listdot && Path.GetFileName(entryPath).StartsWith("."))
                        continue;
                    FileSystemEntry entry = GetEntry(entryPath);
                    if (entry != null)
                        list.Add(entry);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("{0}: {1}", path, ex.Message);
                rval = 1;
            }
            if (!f_nosort && sortfcn != null)
                list.Sort((a, b) => sortfcn(a, b));

            return list;
        }

        static void DisplayEntries(List<FileSystemEntry> list, string path)
        {
            if (list == null || list.Count == 0)
                return;

            Display d = new Display
            {
                List = list,
                Entries = list.Count,
                Btotal = list.Count,           // Stub: one block per entry.
                Stotal = list.Sum(e => e.Size)
            };

            printfcn(d);
        }

        // --- Printing routines ---

        static void PrintSingleColumn(Display d)
        {
            foreach (var entry in d.List)
            {
                Console.WriteLine(entry.Name);
            }
        }

        static void PrintAcrossColumns(Display d)
        {
            Console.WriteLine(string.Join("  ", d.List.Select(e => e.Name)));
        }

        static void PrintColumns(Display d)
        {
            PrintSingleColumn(d);
        }

        static void PrintStream(Display d)
        {
            Console.WriteLine(string.Join(", ", d.List.Select(e => e.Name)));
        }

        static void PrintLong(Display d)
        {
            Console.WriteLine("total {0}", d.Btotal);
            foreach (var entry in d.List)
            {
                Console.WriteLine("{0,11} {1,3} {2,8} {3,10} {4}",
                    entry.Mode,
                    entry.Nlink,
                    entry.Owner,
                    entry.Size,
                    entry.Name);
            }
        }

        static void Usage()
        {
            Console.Error.WriteLine("Usage: ls [-1Cglmxt...] [file ...]");
            Environment.Exit(1);
        }
    }

    // The Program class now contains the single entry point.
    public class Program
    {
        public static int Main(string[] args)
        {
            return LSProgram.Run(args);
        }
    }
}
