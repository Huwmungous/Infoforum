using Antlr4.Runtime;
using System;
using System.IO;
using System.Windows.Forms;

namespace DelphiScanner.Winforms
{
    public partial class MainForm : Form
    {
        private string selectedFolderPath = string.Empty;

        public MainForm()
        {
            InitializeComponent();
        }

        private void SelectFolderButton_Click(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog();
            dialog.Description = "Select a directory";
            dialog.UseDescriptionForTitle = true;

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                selectedFolderPath = dialog.SelectedPath;
                MessageBox.Show($"Selected folder: {selectedFolderPath}", "Folder Selected");
            }
        }

        private void ScanFolderButton_Click(object sender, EventArgs e)
        {
            if(string.IsNullOrEmpty(selectedFolderPath) || !Directory.Exists(selectedFolderPath))
            {
                MessageBox.Show("Please select a valid folder first.", "Error");
                return;
            }

            var queryMap = new Dictionary<string, QueryInfo>(StringComparer.OrdinalIgnoreCase);
            var dfmFiles = Directory.GetFiles(selectedFolderPath, "*.dfm", SearchOption.AllDirectories);
            var pasFiles = Directory.GetFiles(selectedFolderPath, "*.pas", SearchOption.AllDirectories);

            // STEP 1: Parse all .dfm files
            foreach(var dfmFile in dfmFiles)
            {
                try
                {
                    var input = File.ReadAllText(dfmFile);
                    var inputStream = new AntlrInputStream(input);
                    var lexer = new DelphiDfmLexer(inputStream);
                    var tokens = new CommonTokenStream(lexer);
                    var parser = new DelphiDfmParser(tokens);
                    var tree = parser.dfmFile();

                    var visitor = new DfmQueryExtractorVisitor();
                    // var visitor = new DebugVisitor();
                    visitor.Visit(tree);

                    //foreach(var kvp in visitor.Queries)
                    //{
                    //    queryMap[kvp.Key] = kvp.Value;
                    //}
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Failed to parse {dfmFile}: {ex.Message}");
                }
            }

            // STEP 2: Parse all .pas files and update queryMap
            //foreach(var pasFile in pasFiles)
            //{
            //    try
            //    {
            //        var input = File.ReadAllText(pasFile);
            //        var inputStream = new AntlrInputStream(input);
            //        var lexer = new DelphiLexer(inputStream);
            //        var tokens = new CommonTokenStream(lexer);
            //        var parser = new DelphiParser(tokens);
            //        var tree = parser.compilationUnit(); // or entry rule you use

            //        var visitor = new QueryUsageCollector(queryMap);
            //        visitor.Visit(tree);
            //    }
            //    catch(Exception ex)
            //    {
            //        Console.WriteLine($"Failed to parse {pasFile}: {ex.Message}");
            //    }
            //}

            // OPTIONAL: show result in console or debug window
            foreach(var kvp in queryMap)
            {
                Console.WriteLine($"Query: {kvp.Key}");
                Console.WriteLine("  SQL:");
                foreach(var line in kvp.Value.SqlText)
                    Console.WriteLine($"    {line}");

                Console.WriteLine("  Fields:");
                foreach(var field in kvp.Value.Fields)
                    Console.WriteLine($"    {field.FieldName} : {field.FieldType}");

                //Console.WriteLine("  Methods:");
                //foreach(var method in kvp.Value.Methods)
                //    Console.WriteLine($"    {method}");
            }

            MessageBox.Show("Scan complete.", "Done");
        }


    }
}
