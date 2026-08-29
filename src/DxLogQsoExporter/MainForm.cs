using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DxLogQsoExporter.Export;

namespace DxLogQsoExporter
{
    public sealed partial class MainForm : Form
    {
        private readonly ExportCoordinator _exportCoordinator;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _outputPathIsAutomatic = true;
        private bool _settingOutputPath;

        public MainForm()
        {
            _exportCoordinator = new ExportCoordinator();
            InitializeComponent();
            SetTimingDefaults();
            UpdateValidation();
        }

        private void BrowseLogButton_Click(object? sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Select the DXLog log";
                dialog.Filter = "DXLog logs (*.dxn)|*.dxn|All files (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                _logPathTextBox.Text = dialog.FileName;
                SetAutomaticOutputPath();
            }
        }

        private void BrowseRecordingButton_Click(object? sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select the folder containing DXLog MP3 recordings.";
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _recordingFolderTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void BrowseOutputButton_Click(object? sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select the folder for exported QSO clips.";
                dialog.ShowNewFolderButton = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _outputPathIsAutomatic = false;
                    _outputFolderTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void LogPathTextBox_TextChanged(object? sender, EventArgs e)
        {
            if (_outputPathIsAutomatic && !_settingOutputPath)
            {
                SetAutomaticOutputPath();
            }

            UpdateValidation();
        }

        private void OutputFolderTextBox_TextChanged(object? sender, EventArgs e)
        {
            if (!_settingOutputPath)
            {
                _outputPathIsAutomatic = false;
            }

            UpdateValidation();
        }

        private void Timing_ValueChanged(object? sender, EventArgs e)
        {
            UpdateValidation();
        }

        private void ResetButton_Click(object? sender, EventArgs e)
        {
            SetTimingDefaults();
            UpdateValidation();
        }

        private async void ExportButton_Click(object? sender, EventArgs e)
        {
            UpdateValidation();
            if (!_exportButton.Enabled)
            {
                return;
            }

            var options = new ExportOptions(
                _logPathTextBox.Text.Trim(),
                _recordingFolderTextBox.Text.Trim(),
                _outputFolderTextBox.Text.Trim(),
                TimeSpan.FromSeconds((double)_timeBeforeNumericUpDown.Value),
                TimeSpan.FromSeconds((double)_durationNumericUpDown.Value),
                _extractRadioChannelsCheckBox.Checked);
            _cancellationTokenSource = new CancellationTokenSource();
            SetExportActive(true);
            _statusLabel.Text = "Starting export...";
            _progressBar.Value = 0;
            _summaryTextBox.ForeColor = SystemColors.WindowText;
            _summaryTextBox.Clear();

            var progress = new Progress<ExportProgress>(UpdateProgress);
            try
            {
                var result = await Task.Run(
                    () => _exportCoordinator.Run(options, _cancellationTokenSource.Token, progress),
                    CancellationToken.None);
                ShowResult(result);
            }
            catch (Exception exception)
            {
                _statusLabel.Text = "Export failed.";
                MessageBox.Show(
                    this,
                    exception.Message,
                    "DXLog QSO Exporter",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
                SetExportActive(false);
                UpdateValidation();
            }
        }

        private void CancelButton_Click(object? sender, EventArgs e)
        {
            if (_cancellationTokenSource == null)
            {
                return;
            }

            _cancelButton.Enabled = false;
            _statusLabel.Text = "Cancelling...";
            _cancellationTokenSource.Cancel();
        }

        private void OpenOutputButton_Click(object? sender, EventArgs e)
        {
            var outputFolder = _outputFolderTextBox.Text.Trim();
            if (!Directory.Exists(outputFolder))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = outputFolder,
                    UseShellExecute = true
                });
            }
            catch (Exception exception) when (exception is Win32Exception || exception is FileNotFoundException)
            {
                MessageBox.Show(
                    this,
                    "Could not open the export folder in Windows Explorer:\r\n\r\n" + exception.Message,
                    "DXLog QSO Exporter",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (_cancellationTokenSource == null)
            {
                return;
            }

            _cancellationTokenSource.Cancel();
            e.Cancel = true;
            _statusLabel.Text = "Cancelling export. Close the window again when it finishes.";
        }

        private void SetAutomaticOutputPath()
        {
            var logPath = _logPathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(logPath))
            {
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(logPath);
                var name = Path.GetFileNameWithoutExtension(fullPath);
                if (string.IsNullOrWhiteSpace(name))
                {
                    return;
                }

                _settingOutputPath = true;
                _outputFolderTextBox.Text = Path.Combine(
                    Path.GetDirectoryName(fullPath) ?? string.Empty,
                    name + "_QSO_Audio");
                _settingOutputPath = false;
            }
            catch (ArgumentException)
            {
            }
        }

        private void SetTimingDefaults()
        {
            _timeBeforeNumericUpDown.Value = 30;
            _durationNumericUpDown.Value = 60;
        }

        private void UpdateValidation()
        {
            if (_cancellationTokenSource != null)
            {
                return;
            }

            var timingValid = _durationNumericUpDown.Value > _timeBeforeNumericUpDown.Value;
            _errorProvider.SetError(
                _durationNumericUpDown,
                timingValid ? string.Empty : "Total clip duration must be greater than the lead-in.");
            var pathsPresent = !string.IsNullOrWhiteSpace(_logPathTextBox.Text)
                && !string.IsNullOrWhiteSpace(_recordingFolderTextBox.Text)
                && !string.IsNullOrWhiteSpace(_outputFolderTextBox.Text);
            var canExport = timingValid
                && pathsPresent
                && File.Exists(_logPathTextBox.Text.Trim())
                && Directory.Exists(_recordingFolderTextBox.Text.Trim());
            _exportButton.Enabled = canExport;
            _closeDxLogLabel.Visible = canExport;
        }

        private void SetExportActive(bool active)
        {
            _logPathTextBox.ReadOnly = active;
            _recordingFolderTextBox.ReadOnly = active;
            _outputFolderTextBox.ReadOnly = active;
            _browseLogButton.Enabled = !active;
            _browseRecordingButton.Enabled = !active;
            _browseOutputButton.Enabled = !active;
            _timeBeforeNumericUpDown.Enabled = !active;
            _durationNumericUpDown.Enabled = !active;
            _resetButton.Enabled = !active;
            _extractRadioChannelsCheckBox.Enabled = !active;
            _exportButton.Enabled = !active && _durationNumericUpDown.Value > _timeBeforeNumericUpDown.Value;
            _closeDxLogLabel.Visible = active || _exportButton.Enabled;
            _cancelButton.Visible = active;
            _cancelButton.Enabled = active;
            _openOutputButton.Enabled = !active && Directory.Exists(_outputFolderTextBox.Text.Trim());
        }

        private void UpdateProgress(ExportProgress progress)
        {
            var total = Math.Max(progress.Total, 1);
            _progressBar.Maximum = total;
            _progressBar.Value = Math.Min(progress.Completed, total);
            _statusLabel.Text = progress.Operation;
        }

        private void ShowResult(ExportResult result)
        {
            if (result.Cancelled)
            {
                _statusLabel.Text = "Export cancelled.";
            }
            else if (result.Aborted)
            {
                _statusLabel.Text = "Export aborted.";
            }
            else
            {
                _statusLabel.Text = "Export complete.";
            }

            _summaryTextBox.ForeColor = SystemColors.WindowText;
            _summaryTextBox.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Total QSOs: {0}\r\nExported: {1}\r\nShortened: {2}\r\nSkipped: {3}\r\nFailed: {4}\r\nXQSOs: {5}\r\nReport: {6}",
                result.TotalQsoRows,
                result.ExportedCount,
                result.ShortenedCount,
                result.SkippedCount,
                result.FailedCount,
                result.XqsoCount,
                result.ReportPath ?? "not written");
            if (!string.IsNullOrWhiteSpace(result.FailureMessage))
            {
                MessageBox.Show(
                    this,
                    result.FailureMessage,
                    result.Cancelled ? "Export cancelled" : "Export status",
                    MessageBoxButtons.OK,
                    result.Aborted ? MessageBoxIcon.Error : MessageBoxIcon.Information);
            }
        }
    }
}
