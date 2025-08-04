namespace DelphiScanner.Winforms
{
    partial class MainForm
    {
        private System.Windows.Forms.Button SelectFolderButton;
        private System.Windows.Forms.Button ScanFolderButton;

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            SelectFolderButton = new Button();
            ScanFolderButton = new Button();
            SuspendLayout();
            // 
            // btnSelectFolder
            // 
            SelectFolderButton.Location = new Point(30, 30);
            SelectFolderButton.Name = "btnSelectFolder";
            SelectFolderButton.Size = new Size(120, 30);
            SelectFolderButton.TabIndex = 0;
            SelectFolderButton.Text = "Select Folder";
            SelectFolderButton.UseVisualStyleBackColor = true;
            SelectFolderButton.Click += SelectFolderButton_Click;
            // 
            // btnScanFolder
            // 
            ScanFolderButton.Location = new Point(30, 66);
            ScanFolderButton.Name = "btnScanFolder";
            ScanFolderButton.Size = new Size(120, 30);
            ScanFolderButton.TabIndex = 1;
            ScanFolderButton.Text = "Scan Folder";
            ScanFolderButton.UseVisualStyleBackColor = true;
            ScanFolderButton.Click += ScanFolderButton_Click;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(SelectFolderButton);
            Controls.Add(ScanFolderButton);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "MainForm";
            Text = "Form1";
            ResumeLayout(false);
        }
    }
}
