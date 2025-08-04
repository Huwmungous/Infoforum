// ------------------- Ingestor.Forms/MainForm.Designer.cs -------------------
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ingestor.Forms;

partial class MainForm
{
    private Button loadButton;
    private ListView chunkListView;
    private TextBox previewBox;
    private TextBox requestBox;
    private Button generateButton;

    private void InitializeComponent()
    {
        loadButton = new Button();
        chunkListView = new ListView();
        previewBox = new TextBox();
        requestBox = new TextBox();
        generateButton = new Button();
        SuspendLayout();
        // 
        // loadButton
        // 
        loadButton.Location = new Point(10, 10);
        loadButton.Name = "loadButton";
        loadButton.Size = new Size(75, 23);
        loadButton.TabIndex = 0;
        loadButton.Text = "Load Codebase";
        loadButton.Click += LoadButton_Click;
        // 
        // chunkListView
        // 
        chunkListView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        chunkListView.Location = new Point(10, 40);
        chunkListView.Name = "chunkListView";
        chunkListView.Size = new Size(400, 790);
        chunkListView.TabIndex = 1;
        chunkListView.UseCompatibleStateImageBehavior = false;
        chunkListView.SelectedIndexChanged += ChunkListView_SelectedIndexChanged;
        // 
        // previewBox
        // 
        previewBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        previewBox.Location = new Point(420, 40);
        previewBox.Multiline = true;
        previewBox.Name = "previewBox";
        previewBox.ScrollBars = ScrollBars.Both;
        previewBox.Size = new Size(832, 790);
        previewBox.TabIndex = 2;
        // 
        // requestBox
        // 
        requestBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        requestBox.Location = new Point(12, 836);
        requestBox.Multiline = true;
        requestBox.Name = "requestBox";
        requestBox.Size = new Size(1147, 137);
        requestBox.TabIndex = 3;
        requestBox.Text = "Please locate any SQL.Strings you find and generate a corresponding ADO.NET method for each";
        // 
        // generateButton
        // 
        generateButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        generateButton.Location = new Point(1165, 950);
        generateButton.Name = "generateButton";
        generateButton.Size = new Size(75, 23);
        generateButton.TabIndex = 4;
        generateButton.Text = "Generate API";
        generateButton.Click += GenerateButton_Click;
        // 
        // MainForm
        // 
        ClientSize = new Size(1264, 985);
        Controls.Add(loadButton);
        Controls.Add(chunkListView);
        Controls.Add(previewBox);
        Controls.Add(requestBox);
        Controls.Add(generateButton);
        Name = "MainForm";
        Text = "Code Ingestor & Generator";
        ResumeLayout(false);
        PerformLayout();
    }
}
