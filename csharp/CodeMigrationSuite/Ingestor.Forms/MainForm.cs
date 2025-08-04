using Generator.WebAPI;
using Ingestor.Core;
using Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace Ingestor.Forms;

public partial class MainForm : Form
{
    private List<CodeChunk> _chunks = [];

    public MainForm()
    {
        InitializeComponent();
        chunkListView.View = View.Details;
        chunkListView.Columns.Add("Unit Name");
        chunkListView.Columns.Add("Language");
        chunkListView.Columns.Add("Type");
        chunkListView.FullRowSelect = true;
    }

    private void LoadButton_Click(object sender, EventArgs e)
    {
        using var fbd = new FolderBrowserDialog();
        if(fbd.ShowDialog() == DialogResult.OK)
        {
            var path = fbd.SelectedPath;
            _chunks = CodeIngestor.IngestDirectory(path);
            CodeIngestor.SaveChunksToJson(_chunks, "ingested.json");
            UpdateChunkList();
        }
    }

    private void UpdateChunkList()
    {
        chunkListView.Items.Clear();
        foreach(var chunk in _chunks)
        {
            var item = new ListViewItem([chunk.UnitName, chunk.Language, chunk.ChunkType])
            {
                Tag = chunk
            };
            chunkListView.Items.Add(item);
        }
    }

    private void ChunkListView_SelectedIndexChanged(object sender, EventArgs e)
    {
        if(chunkListView.SelectedItems.Count == 1)
        {
            var chunk = (CodeChunk)chunkListView.SelectedItems[0].Tag;
            previewBox.Text = chunk.Content;
        }
    }

    private async void GenerateButton_Click(object sender, EventArgs e)
    {
        var request = requestBox.Text;
        var selected = chunkListView.SelectedItems.Count > 0
            ? chunkListView.SelectedItems.Cast<ListViewItem>().Select(i => (CodeChunk)i.Tag).ToList()
            : _chunks;

        var instruction = "You are a code generation assistant.  Please Consider the following \n\n";

        var prompt = instruction + PromptBuilder.BuildPrompt(selected, request);
        File.WriteAllText("debug_prompt.txt", prompt);

        var client = new LlmClient();
        var result = await client.CallLlmAsync(prompt);
        previewBox.Text = result;
        File.WriteAllText("generated.cs", result);
        MessageBox.Show("Generation complete.");
    }
}