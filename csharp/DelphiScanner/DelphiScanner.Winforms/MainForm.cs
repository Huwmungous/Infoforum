using Antlr4.Runtime;
using System;
using System.IO;
using System.Windows.Forms;

namespace DelphiScanner.Winforms
{
    public partial class MainForm : Form
    {
        private const string DefaultFolderPath = @"C:\Temp\PostgresFiddle\Delphi\PostgresFiddle";
        private string selectedFolderPath = DefaultFolderPath;

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

        private Dictionary<string, QueryInfo> QueryMap = new(StringComparer.OrdinalIgnoreCase);       

        private void ScanFolderButton_Click(object sender, EventArgs e)
        {
            if(string.IsNullOrEmpty(selectedFolderPath) || !Directory.Exists(selectedFolderPath))
            {
                MessageBox.Show("Please select a valid folder first.", "Error");
                return;
            }

            this.QueryMap = [];

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

                    var visitor = new DfmVisitor() { UnitFileName = dfmFile, QueryMap = this.QueryMap };
                    visitor.Visit(tree);
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Failed to parse {dfmFile}: {ex.Message}");
                }
            }

            // STEP 2: Parse all .pas files and update queryMap usage info
            foreach(var pasFile in pasFiles)
            {
                try
                {
                    var input = File.ReadAllText(pasFile);
                    var inputStream = new AntlrInputStream(input);
                    var lexer = new DelphiLexer(inputStream);
                    var tokens = new CommonTokenStream(lexer);
                    var parser = new DelphiParser(tokens);
                    var tree = parser.file();

                    var visitor = new DelphiVisitor() { 
                        Form=Path.GetFileNameWithoutExtension(pasFile), 
                        QueryMap = this.QueryMap 
                    };

                    visitor.Visit(tree);

                    var t = tree.ToStringTree(parser);
                    Console.WriteLine(t);
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Failed to parse {pasFile}: {ex.Message}");
                }
            }

            // OPTIONAL: show result in console or debug window
            foreach(var kvp in QueryMap)
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


/*
 
"(file (unit (unitHead unit (namespaceName (ident DataModule)) ;) (unitInterface interface (usesClause uses (namespaceNameList (namespaceName (ident System) . (ident SysUtils)) , (namespaceName (ident System) . (ident Classes)) , (namespaceName (ident DBAccess)) , (namespaceName (ident PgDacVcl)) , (namespaceName (ident Data) . (ident DB)) , (namespaceName (ident MemDS)) , (namespaceName (ident PgAccess)) , (namespaceName (ident Vcl) . (ident Dialogs)) , (namespaceName (ident VirtualDataSet)) , (namespaceName (ident Datasnap) . (ident Provider)) , (namespaceName (ident Datasnap) . (ident DBClient)) ;)) (interfaceDecl (typeSection type (typeDeclaration (genericTypeIdent (qualifiedIdent (ident TDM))) = (typeDecl (strucType (strucTypePart (classDecl (classTypeDecl class (classParent ( (genericTypeIdent (qualifiedIdent (ident TDataModule))) )) (classItem (classField (identList (ident PgConnection1)) : (typeDecl (typeId (namespacedQualifiedIdent (qualifiedIdent (ident TPgConnection))))) ;)) (classItem (classField (..." 

 */
