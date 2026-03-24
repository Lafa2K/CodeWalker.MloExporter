using CodeWalker.GameFiles;
using CodeWalker.Properties;
using CodeWalker.Tools;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CodeWalker.MloExporter
{
    public class MloExporterForm : Form
    {
        private GameFileCache GameFileCache;
        private readonly YtypPropExporter Exporter = new YtypPropExporter();

        private Button OpenButton;
        private CheckBox ExportTexturesCheckBox;
        private CheckBox OpenFolderCheckBox;
        private Label CacheStatusLabel;
        private Label InputPathLabel;
        private Label OutputPathLabel;
        private Label StatusLabel;
        private ProgressBar ExportProgressBar;
        private TextBox SummaryTextBox;
        private OpenFileDialog OpenFileDialog;

        private volatile bool CacheReady = false;
        private volatile bool ExportInProgress = false;
        private CancellationTokenSource CacheContentLoopCancellation;

        public MloExporterForm()
        {
            InitializeUi();
            AllowDrop = true;
            DragEnter += Form_DragEnter;
            DragDrop += Form_DragDrop;
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (!GTAFolder.UpdateGTAFolder(true))
            {
                Close();
                return;
            }

            GameFileCache = GameFileCacheFactory.Create();
            GTAFolder.UpdateEnhancedFormTitle(this);
            await InitializeCacheAsync();
        }

        private void InitializeUi()
        {
            Text = "CodeWalker MLO Exporter";
            Width = 760;
            Height = 420;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(760, 420);

            OpenFileDialog = new OpenFileDialog();
            OpenFileDialog.Filter = "YTYP files|*.ytyp;*.ytyp.xml|Binary YTYP|*.ytyp|YTYP XML|*.ytyp.xml";
            OpenFileDialog.Multiselect = false;
            OpenFileDialog.Title = "Open a YTYP or YTYP.XML";

            var titleLabel = new Label()
            {
                Left = 20,
                Top = 20,
                Width = 700,
                Height = 22,
                Text = "Open a YTYP or YTYP.XML and the tool exports all MLO props automatically."
            };
            Controls.Add(titleLabel);

            ExportTexturesCheckBox = new CheckBox()
            {
                Left = 20,
                Top = 55,
                Width = 260,
                Height = 24,
                Checked = true,
                Text = "Export related and shared textures"
            };
            Controls.Add(ExportTexturesCheckBox);

            OpenFolderCheckBox = new CheckBox()
            {
                Left = 300,
                Top = 55,
                Width = 220,
                Height = 24,
                Checked = true,
                Text = "Open output folder when done"
            };
            Controls.Add(OpenFolderCheckBox);

            OpenButton = new Button()
            {
                Left = 20,
                Top = 92,
                Width = 160,
                Height = 32,
                Text = "Open YTYP...",
                Enabled = false
            };
            OpenButton.Click += OpenButton_Click;
            Controls.Add(OpenButton);

            CacheStatusLabel = new Label()
            {
                Left = 200,
                Top = 99,
                Width = 520,
                Height = 20,
                Text = "Waiting to initialize GTA file cache..."
            };
            Controls.Add(CacheStatusLabel);

            var inputCaption = new Label()
            {
                Left = 20,
                Top = 142,
                Width = 90,
                Height = 20,
                Text = "Input:"
            };
            Controls.Add(inputCaption);

            InputPathLabel = new Label()
            {
                Left = 80,
                Top = 142,
                Width = 640,
                Height = 36,
                Text = "-"
            };
            Controls.Add(InputPathLabel);

            var outputCaption = new Label()
            {
                Left = 20,
                Top = 182,
                Width = 90,
                Height = 20,
                Text = "Output:"
            };
            Controls.Add(outputCaption);

            OutputPathLabel = new Label()
            {
                Left = 80,
                Top = 182,
                Width = 640,
                Height = 36,
                Text = "-"
            };
            Controls.Add(OutputPathLabel);

            ExportProgressBar = new ProgressBar()
            {
                Left = 20,
                Top = 230,
                Width = 700,
                Height = 22,
                Minimum = 0,
                Maximum = 1000,
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30
            };
            Controls.Add(ExportProgressBar);

            StatusLabel = new Label()
            {
                Left = 20,
                Top = 260,
                Width = 700,
                Height = 22,
                Text = "Select a YTYP when the cache is ready."
            };
            Controls.Add(StatusLabel);

            SummaryTextBox = new TextBox()
            {
                Left = 20,
                Top = 292,
                Width = 700,
                Height = 80,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical
            };
            Controls.Add(SummaryTextBox);
        }

        private async Task InitializeCacheAsync()
        {
            SetCacheStatus("Loading GTA keys...");
            try
            {
                await Task.Run(() =>
                {
                    GTA5Keys.LoadFromPath(GTAFolder.CurrentGTAFolder, GTAFolder.IsGen9, Settings.Default.Key);

                    GameFileCache.EnableDlc = true;
                    GameFileCache.EnableMods = true;
                    GameFileCache.LoadArchetypes = true;
                    GameFileCache.LoadVehicles = false;
                    GameFileCache.LoadPeds = false;
                    GameFileCache.LoadAudio = false;
                    GameFileCache.BuildExtendedJenkIndex = false;
                    GameFileCache.DoFullStringIndex = false;
                    GameFileCache.Init(UpdateStatusSafe, UpdateStatusSafe);
                });

                StartCacheContentLoop();
                CacheReady = true;
                OpenButton.Enabled = true;
                SetCacheStatus("Ready. Open a YTYP or drop one on this window.");
                UpdateStatusSafe("Ready to export.");
            }
            catch (Exception ex)
            {
                SetCacheStatus("Unable to initialize GTA files.");
                MessageBox.Show(this, "Failed to initialize the GTA file cache:\n" + ex, "CodeWalker MLO Exporter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void OpenButton_Click(object sender, EventArgs e)
        {
            if (ExportInProgress)
            {
                return;
            }

            if (OpenFileDialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            await ExportSelectedFileAsync(OpenFileDialog.FileName);
        }

        private void Form_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if ((files != null) && (files.Length == 1) && YtypPropExporter.SupportsInputPath(files[0]))
                {
                    e.Effect = DragDropEffects.Copy;
                    return;
                }
            }

            e.Effect = DragDropEffects.None;
        }

        private async void Form_DragDrop(object sender, DragEventArgs e)
        {
            if (ExportInProgress || !CacheReady)
            {
                return;
            }

            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if ((files == null) || (files.Length != 1))
            {
                return;
            }

            await ExportSelectedFileAsync(files[0]);
        }

        private async Task ExportSelectedFileAsync(string inputPath)
        {
            if (!CacheReady)
            {
                MessageBox.Show(this, "The GTA file cache is still loading. Please wait a moment and try again.", "CodeWalker MLO Exporter", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!YtypPropExporter.SupportsInputPath(inputPath))
            {
                MessageBox.Show(this, "Only .ytyp and .ytyp.xml files are supported.", "CodeWalker MLO Exporter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var outputFolder = YtypPropExporter.GetSuggestedOutputFolderPath(inputPath);
            InputPathLabel.Text = inputPath;
            OutputPathLabel.Text = outputFolder;
            SummaryTextBox.Clear();

            SetExportUiState(true);
            Exception exportException = null;
            YtypPropExportResult result = null;

            try
            {
                result = await Task.Run(() => Exporter.Export(
                    GameFileCache,
                    inputPath,
                    outputFolder,
                    ExportTexturesCheckBox.Checked,
                    UpdateProgressSafe,
                    UpdateStatusSafe));
            }
            catch (Exception ex)
            {
                exportException = ex;
            }
            finally
            {
                SetExportUiState(false);
            }

            if (exportException != null)
            {
                SummaryTextBox.Text = exportException.ToString();
                MessageBox.Show(this, "Export failed:\n" + exportException.Message, "CodeWalker MLO Exporter", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (result == null)
            {
                return;
            }

            var summary = BuildSummary(result, outputFolder);
            SummaryTextBox.Text = summary;
            UpdateStatusSafe("Export complete.");

            if (OpenFolderCheckBox.Checked && Directory.Exists(outputFolder))
            {
                try
                {
                    Process.Start("explorer", "\"" + outputFolder + "\"");
                }
                catch { }
            }
        }

        private string BuildSummary(YtypPropExportResult result, string outputFolder)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Output: " + outputFolder);
            sb.AppendLine(result.ExportedTargets.ToString() + " of " + result.TotalTargets.ToString() + " prop files exported.");

            if (result.ExportedTextures > 0)
            {
                sb.AppendLine(result.ExportedTextures.ToString() + " textures exported.");
            }
            if (result.MissingTextures > 0)
            {
                sb.AppendLine(result.MissingTextures.ToString() + " referenced textures were not found.");
            }
            if (result.MissingArchetypes > 0)
            {
                sb.AppendLine(result.MissingArchetypes.ToString() + " prop archetypes could not be resolved.");
            }
            if (result.MissingResources > 0)
            {
                sb.AppendLine(result.MissingResources.ToString() + " prop resources were not found.");
            }
            if (result.Errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Errors:");
                foreach (var error in result.Errors)
                {
                    sb.AppendLine(error);
                }
            }

            return sb.ToString();
        }

        private void SetExportUiState(bool exporting)
        {
            ExportInProgress = exporting;

            if (InvokeRequired)
            {
                BeginInvoke(new Action<bool>(SetExportUiState), exporting);
                return;
            }

            OpenButton.Enabled = CacheReady && !exporting;
            ExportTexturesCheckBox.Enabled = !exporting;
            OpenFolderCheckBox.Enabled = !exporting;

            if (exporting)
            {
                ExportProgressBar.Style = ProgressBarStyle.Marquee;
                ExportProgressBar.MarqueeAnimationSpeed = 30;
            }
            else
            {
                ExportProgressBar.Style = ProgressBarStyle.Continuous;
                ExportProgressBar.Value = 0;
            }
        }

        private void UpdateProgressSafe(YtypPropExportProgress progress)
        {
            if (IsDisposed || (progress == null))
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action<YtypPropExportProgress>(UpdateProgressSafe), progress);
                return;
            }

            StatusLabel.Text = progress.Status ?? string.Empty;
            if (progress.Total > 0)
            {
                ExportProgressBar.Style = ProgressBarStyle.Continuous;
                ExportProgressBar.Value = Math.Max(0, Math.Min((progress.Current * 1000) / progress.Total, 1000));
            }
            else
            {
                ExportProgressBar.Style = ProgressBarStyle.Marquee;
            }
        }

        private void UpdateStatusSafe(string text)
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(UpdateStatusSafe), text);
                return;
            }

            StatusLabel.Text = text;
        }

        private void SetCacheStatus(string text)
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(SetCacheStatus), text);
                return;
            }

            CacheStatusLabel.Text = text;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            CacheContentLoopCancellation?.Cancel();
            CacheContentLoopCancellation?.Dispose();
            CacheContentLoopCancellation = null;
            base.OnFormClosed(e);
        }

        private void StartCacheContentLoop()
        {
            CacheContentLoopCancellation?.Cancel();
            CacheContentLoopCancellation?.Dispose();
            CacheContentLoopCancellation = new CancellationTokenSource();
            var token = CacheContentLoopCancellation.Token;

            Task.Run(() =>
            {
                while (!token.IsCancellationRequested && !IsDisposed)
                {
                    if (GameFileCache?.IsInited == true)
                    {
                        GameFileCache.BeginFrame();
                        var itemsPending = GameFileCache.ContentThreadProc();
                        if (!itemsPending)
                        {
                            Thread.Sleep(10);
                        }
                    }
                    else
                    {
                        Thread.Sleep(50);
                    }
                }
            }, token);
        }
    }
}
