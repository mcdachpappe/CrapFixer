﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CFixer.Views
{
    public partial class ViveView : UserControl
    {
        private List<ViveFeature> featureList;
        private string viveToolPath;

        public ViveView()
        {
            InitializeComponent();
            IsViveToolAvailable();
            InitFeatureList();
            LoadFeaturesToGrid();

            // Fire and forget async call
            _ = UpdateFeatureStatusFromSystem();
        }

        private void IsViveToolAvailable()
        {
            string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");

            var viveFolder = Directory.GetDirectories(pluginsDir)
                                      .FirstOrDefault(dir => Path.GetFileName(dir).ToLower().StartsWith("vive"));

            if (viveFolder == null)
            {
                viveToolPath = null;
                btnDescription.Text = "Enable experimental and hidden features (Disabled)";
                return;
            }

            string exePath = Path.Combine(viveFolder, "ViVeTool.exe");
            if (File.Exists(exePath))
            {
                viveToolPath = exePath;
                btnDescription.Text = "Enable experimental and hidden features (Enabled)";
            }
        }

        /// <summary>
        /// Initializes hardcoded feature list with names, IDs and default states.
        /// </summary>
        private void InitFeatureList()
        {
            featureList = new List<ViveFeature>
            {
                new ViveFeature
                {
                    Ids = new List<int> {47205210, 49221331, 49381526, 49402389, 49820095, 55495322, 48433719},
                    Name = "Enable the redesigned Windows 11 Start menu",
                    InfoUrl = "https://www.neowin.net/guides/how-to-enable-the-redesigned-windows-11-start-menu/",
                    Enabled = false
                },
                new ViveFeature
                {
                    Ids = new List<int> {52467192,53079680},
                    Name = "Enable Text extractor in Snipping Tool",
                    InfoUrl = "https://blogs.windows.com/windows-insider/2025/04/15/text-extractor-in-snipping-tool-begins-rolling-out-to-windows-insiders/",
                    Enabled = false
                }
                    ,new ViveFeature
                {
                    Ids = new List<int> {45624564},
                    Name = "Enable Drag Tray Share UI",
                    InfoUrl = "https://www.neowin.net/news/windows-11-is-getting-a-quirky-new-way-to-share-files/",
                    Enabled = false
                }
            };
        }

        /// <summary>
        /// Loads the feature list into the DataGridView UI.
        /// </summary>
        private void LoadFeaturesToGrid()
        {
            dataGridView.Rows.Clear();

            foreach (var feature in featureList)
            {
                int rowIndex = dataGridView.Rows.Add();
                var row = dataGridView.Rows[rowIndex];

                row.Cells["EnabledColumn"].Value = feature.Enabled;
                row.Cells["NameColumn"].Value = feature.Name;
                row.Cells["IdColumn"].Value = feature.IdsAsString;
                row.Cells["InfoColumn"].Value = feature.InfoUrl;
                row.Cells["StatusColumn"].Value = "Unknown";
            }
        }

        /// <summary>
        /// Executes ViVeTool with given feature IDs and state.
        /// </summary>
        private void ApplyFeature(List<int> ids, bool enable)
        {
            if (string.IsNullOrEmpty(viveToolPath))
            {
                MessageBox.Show("ViVeTool not found. Please ensure it is installed in the plugins folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string action = enable ? "/enable" : "/disable";
            string idArg = string.Join(",", ids);
            string args = $"{action} /id:{idArg}";

            Process.Start(new ProcessStartInfo
            {
                FileName = viveToolPath,
                Arguments = args,
                UseShellExecute = true,
                CreateNoWindow = true,
                Verb = "runas" // Run as administrator
            });
        }

        /// <summary>
        /// Applies the currently selected states from the DataGridView to the system using ViVeTool.
        /// </summary>
        private async void btnViveApply_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < dataGridView.Rows.Count; i++)
            {
                var row = dataGridView.Rows[i];
                if (row.IsNewRow) continue;

                bool isEnabled = Convert.ToBoolean(row.Cells["EnabledColumn"].Value);
                string idText = row.Cells["IdColumn"].Value.ToString();
                List<int> ids = idText.Split(',').Select(s => int.Parse(s.Trim())).ToList();

                ApplyFeature(ids, isEnabled);
            }

            Task.Delay(1000).Wait();
            await UpdateFeatureStatusFromSystem(); // Refresh the status after applying changes

            MessageBox.Show("Features have been applied.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Queries the current system state using ViVeTool and returns a map of ID => isEnabled.
        /// </summary>
        // Pro ID ein Query machen, um den Status exakt zu ermitteln
        private Dictionary<int, bool> QueryCurrentFeatureStates()
        {
            var statusMap = new Dictionary<int, bool>();

            if (string.IsNullOrEmpty(viveToolPath))
            {
                MessageBox.Show("ViVeTool not found. Please ensure it is installed in the plugins folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return statusMap;
            }

            var allIds = featureList.SelectMany(f => f.Ids).Distinct();

            foreach (int id in allIds)
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = viveToolPath,
                    Arguments = $"/query /id:{id}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (Process proc = Process.Start(psi))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();

                    bool enabled = output.Contains("Enabled");
                    statusMap[id] = enabled;
                }
            }

            return statusMap;
        }

        /// <summary>
        /// Asynchronously updates the grid checkboxes and status labels to reflect actual feature states on the system.
        /// </summary>
        private async Task UpdateFeatureStatusFromSystem()
        {
            // Run QueryCurrentFeatureStates on a background thread
            var systemStatus = await Task.Run(() => QueryCurrentFeatureStates());

            // Update UI on the UI thread
            if (dataGridView.InvokeRequired)
            {
                dataGridView.Invoke(new Action(() =>
                {
                    UpdateGridWithStatus(systemStatus);
                }));
            }
            else
            {
                UpdateGridWithStatus(systemStatus);
            }
        }

        /// <summary>
        /// Updates the grid rows with the given status dictionary.
        /// </summary>
        private void UpdateGridWithStatus(Dictionary<int, bool> systemStatus)
        {
            foreach (DataGridViewRow row in dataGridView.Rows)
            {
                if (row.IsNewRow) continue;

                string idText = row.Cells["IdColumn"].Value.ToString();
                var ids = idText.Split(',').Select(s => int.Parse(s.Trim())).ToList();

                int enabledCount = ids.Count(id => systemStatus.ContainsKey(id) && systemStatus[id]);

                string status = "Disabled";
                if (enabledCount == ids.Count)
                    status = "All Enabled";
                else if (enabledCount > 0)
                    status = "Partially Enabled";

                row.Cells["StatusColumn"].Value = status;
            }
        }

        private void dataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == dataGridView.Columns["InfoColumn"].Index && e.RowIndex >= 0)
            {
                var url = dataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString();
                if (!string.IsNullOrEmpty(url))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to open link:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        /// <summary>
        /// Represents a single ViVe feature group.
        /// </summary>
        public class ViveFeature
        {
            public List<int> Ids { get; set; }
            public string Name { get; set; }
            public bool Enabled { get; set; }
            public string InfoUrl { get; set; }

            public string IdsAsString => string.Join(",", Ids);
        }

        private void linkPluginUsage_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            MessageBox.Show("This plugin uses ViVeTool to enable hidden Windows features.\n" +
    "Please download ViVeTool (e.g. 'ViVeTool-v0.3.x-IntelAmd') from:\n" +
    "https://github.com/thebookisclosed/ViVe/releases\n" +
    "Extract it and place the contents into a subfolder inside the 'plugins' directory.\n\n");
        }

        private void btnApplyCustom_Click(object sender, EventArgs e)
        {
            string input = txtCustomIds.Text;

            if (string.IsNullOrWhiteSpace(input))
            {
                MessageBox.Show("Please enter one or more feature IDs.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Parse input (e.g., "123,456,789")
            var idList = new List<int>();
            var parts = input.Split(',');

            foreach (var part in parts)
            {
                if (int.TryParse(part.Trim(), out int id))
                {
                    idList.Add(id);
                }
                else
                {
                    MessageBox.Show($"Invalid ID: '{part}'", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            if (idList.Count == 0)
            {
                MessageBox.Show("No valid IDs found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Ask whether to enable or disable
            var result = MessageBox.Show(
             $"Do you want to ENABLE these features?\n\n{string.Join(", ", idList)}\n\n" +
             "Yes = Enable\nNo = Disable\nCancel = Abort",
             "Confirm Action",
             MessageBoxButtons.YesNoCancel,
             MessageBoxIcon.Question);

            if (result == DialogResult.Cancel) return;

            bool enable = (result == DialogResult.Yes);

            ApplyFeature(idList, enable);

            MessageBox.Show("Custom feature action sent to ViVeTool.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}