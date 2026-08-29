using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace DxLogQsoExporter
{
    public sealed partial class MainForm
    {
        private IContainer components = null!;
        private Label _closeDxLogLabel = null!;
        private GroupBox _filesGroupBox = null!;
        private GroupBox _settingsGroupBox = null!;
        private Label _logPathLabel = null!;
        private TextBox _logPathTextBox = null!;
        private Button _browseLogButton = null!;
        private Label _recordingFolderLabel = null!;
        private TextBox _recordingFolderTextBox = null!;
        private Button _browseRecordingButton = null!;
        private Label _outputFolderLabel = null!;
        private TextBox _outputFolderTextBox = null!;
        private Button _browseOutputButton = null!;
        private Label _timeBeforeLabel = null!;
        private NumericUpDown _timeBeforeNumericUpDown = null!;
        private Label _durationLabel = null!;
        private NumericUpDown _durationNumericUpDown = null!;
        private Button _resetButton = null!;
        private CheckBox _extractRadioChannelsCheckBox = null!;
        private Button _exportButton = null!;
        private Button _cancelButton = null!;
        private ProgressBar _progressBar = null!;
        private Label _statusLabel = null!;
        private TextBox _summaryTextBox = null!;
        private Button _openOutputButton = null!;
        private ErrorProvider _errorProvider = null!;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this._closeDxLogLabel = new System.Windows.Forms.Label();
            this._filesGroupBox = new System.Windows.Forms.GroupBox();
            this._browseOutputButton = new System.Windows.Forms.Button();
            this._outputFolderTextBox = new System.Windows.Forms.TextBox();
            this._outputFolderLabel = new System.Windows.Forms.Label();
            this._browseRecordingButton = new System.Windows.Forms.Button();
            this._recordingFolderTextBox = new System.Windows.Forms.TextBox();
            this._recordingFolderLabel = new System.Windows.Forms.Label();
            this._browseLogButton = new System.Windows.Forms.Button();
            this._logPathTextBox = new System.Windows.Forms.TextBox();
            this._logPathLabel = new System.Windows.Forms.Label();
            this._settingsGroupBox = new System.Windows.Forms.GroupBox();
            this._extractRadioChannelsCheckBox = new System.Windows.Forms.CheckBox();
            this._resetButton = new System.Windows.Forms.Button();
            this._durationNumericUpDown = new System.Windows.Forms.NumericUpDown();
            this._durationLabel = new System.Windows.Forms.Label();
            this._timeBeforeNumericUpDown = new System.Windows.Forms.NumericUpDown();
            this._timeBeforeLabel = new System.Windows.Forms.Label();
            this._exportButton = new System.Windows.Forms.Button();
            this._cancelButton = new System.Windows.Forms.Button();
            this._progressBar = new System.Windows.Forms.ProgressBar();
            this._statusLabel = new System.Windows.Forms.Label();
            this._summaryTextBox = new System.Windows.Forms.TextBox();
            this._openOutputButton = new System.Windows.Forms.Button();
            this._errorProvider = new System.Windows.Forms.ErrorProvider(this.components);
            this._filesGroupBox.SuspendLayout();
            this._settingsGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._durationNumericUpDown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._timeBeforeNumericUpDown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._errorProvider)).BeginInit();
            this.SuspendLayout();
            // 
            // _closeDxLogLabel
            // 
            this._closeDxLogLabel.AutoSize = true;
            this._closeDxLogLabel.ForeColor = System.Drawing.SystemColors.GrayText;
            this._closeDxLogLabel.Location = new System.Drawing.Point(252, 322);
            this._closeDxLogLabel.Name = "_closeDxLogLabel";
            this._closeDxLogLabel.Size = new System.Drawing.Size(167, 15);
            this._closeDxLogLabel.TabIndex = 6;
            this._closeDxLogLabel.Text = "Close DXLog before exporting.";
            this._closeDxLogLabel.Visible = false;
            // 
            // _filesGroupBox
            // 
            this._filesGroupBox.Controls.Add(this._browseOutputButton);
            this._filesGroupBox.Controls.Add(this._outputFolderTextBox);
            this._filesGroupBox.Controls.Add(this._outputFolderLabel);
            this._filesGroupBox.Controls.Add(this._browseRecordingButton);
            this._filesGroupBox.Controls.Add(this._recordingFolderTextBox);
            this._filesGroupBox.Controls.Add(this._recordingFolderLabel);
            this._filesGroupBox.Controls.Add(this._browseLogButton);
            this._filesGroupBox.Controls.Add(this._logPathTextBox);
            this._filesGroupBox.Controls.Add(this._logPathLabel);
            this._filesGroupBox.Location = new System.Drawing.Point(24, 24);
            this._filesGroupBox.Name = "_filesGroupBox";
            this._filesGroupBox.Size = new System.Drawing.Size(672, 146);
            this._filesGroupBox.TabIndex = 2;
            this._filesGroupBox.TabStop = false;
            this._filesGroupBox.Text = "Files";
            // 
            // _browseOutputButton
            // 
            this._browseOutputButton.AccessibleName = "Browse for output folder";
            this._browseOutputButton.Location = new System.Drawing.Point(581, 98);
            this._browseOutputButton.Name = "_browseOutputButton";
            this._browseOutputButton.Size = new System.Drawing.Size(75, 25);
            this._browseOutputButton.TabIndex = 8;
            this._browseOutputButton.Text = "Browse...";
            this._browseOutputButton.UseVisualStyleBackColor = true;
            this._browseOutputButton.Click += new System.EventHandler(this.BrowseOutputButton_Click);
            // 
            // _outputFolderTextBox
            // 
            this._outputFolderTextBox.AccessibleName = "Output folder path";
            this._outputFolderTextBox.Location = new System.Drawing.Point(126, 99);
            this._outputFolderTextBox.Name = "_outputFolderTextBox";
            this._outputFolderTextBox.Size = new System.Drawing.Size(440, 23);
            this._outputFolderTextBox.TabIndex = 7;
            this._outputFolderTextBox.TextChanged += new System.EventHandler(this.OutputFolderTextBox_TextChanged);
            // 
            // _outputFolderLabel
            // 
            this._outputFolderLabel.AutoSize = true;
            this._outputFolderLabel.Location = new System.Drawing.Point(16, 103);
            this._outputFolderLabel.Name = "_outputFolderLabel";
            this._outputFolderLabel.Size = new System.Drawing.Size(74, 15);
            this._outputFolderLabel.TabIndex = 6;
            this._outputFolderLabel.Text = "Export folder";
            // 
            // _browseRecordingButton
            // 
            this._browseRecordingButton.AccessibleName = "Browse for recording folder";
            this._browseRecordingButton.Location = new System.Drawing.Point(581, 62);
            this._browseRecordingButton.Name = "_browseRecordingButton";
            this._browseRecordingButton.Size = new System.Drawing.Size(75, 25);
            this._browseRecordingButton.TabIndex = 5;
            this._browseRecordingButton.Text = "Browse...";
            this._browseRecordingButton.UseVisualStyleBackColor = true;
            this._browseRecordingButton.Click += new System.EventHandler(this.BrowseRecordingButton_Click);
            // 
            // _recordingFolderTextBox
            // 
            this._recordingFolderTextBox.AccessibleName = "Recording folder path";
            this._recordingFolderTextBox.Location = new System.Drawing.Point(126, 63);
            this._recordingFolderTextBox.Name = "_recordingFolderTextBox";
            this._recordingFolderTextBox.Size = new System.Drawing.Size(440, 23);
            this._recordingFolderTextBox.TabIndex = 4;
            this._recordingFolderTextBox.TextChanged += new System.EventHandler(this.RecordingFolderTextBox_TextChanged);
            // 
            // _recordingFolderLabel
            // 
            this._recordingFolderLabel.AutoSize = true;
            this._recordingFolderLabel.Location = new System.Drawing.Point(16, 67);
            this._recordingFolderLabel.Name = "_recordingFolderLabel";
            this._recordingFolderLabel.Size = new System.Drawing.Size(90, 15);
            this._recordingFolderLabel.TabIndex = 3;
            this._recordingFolderLabel.Text = "MP3 recordings";
            // 
            // _browseLogButton
            // 
            this._browseLogButton.AccessibleName = "Browse for DXN log file";
            this._browseLogButton.Location = new System.Drawing.Point(581, 26);
            this._browseLogButton.Name = "_browseLogButton";
            this._browseLogButton.Size = new System.Drawing.Size(75, 25);
            this._browseLogButton.TabIndex = 2;
            this._browseLogButton.Text = "Browse...";
            this._browseLogButton.UseVisualStyleBackColor = true;
            this._browseLogButton.Click += new System.EventHandler(this.BrowseLogButton_Click);
            // 
            // _logPathTextBox
            // 
            this._logPathTextBox.AccessibleName = "DXN log file path";
            this._logPathTextBox.Location = new System.Drawing.Point(126, 27);
            this._logPathTextBox.Name = "_logPathTextBox";
            this._logPathTextBox.Size = new System.Drawing.Size(440, 23);
            this._logPathTextBox.TabIndex = 1;
            this._logPathTextBox.TextChanged += new System.EventHandler(this.LogPathTextBox_TextChanged);
            // 
            // _logPathLabel
            // 
            this._logPathLabel.AutoSize = true;
            this._logPathLabel.Location = new System.Drawing.Point(16, 31);
            this._logPathLabel.Name = "_logPathLabel";
            this._logPathLabel.Size = new System.Drawing.Size(70, 15);
            this._logPathLabel.TabIndex = 0;
            this._logPathLabel.Text = "DXN log file";
            // 
            // _settingsGroupBox
            // 
            this._settingsGroupBox.Controls.Add(this._extractRadioChannelsCheckBox);
            this._settingsGroupBox.Controls.Add(this._resetButton);
            this._settingsGroupBox.Controls.Add(this._durationNumericUpDown);
            this._settingsGroupBox.Controls.Add(this._durationLabel);
            this._settingsGroupBox.Controls.Add(this._timeBeforeNumericUpDown);
            this._settingsGroupBox.Controls.Add(this._timeBeforeLabel);
            this._settingsGroupBox.Location = new System.Drawing.Point(24, 194);
            this._settingsGroupBox.Name = "_settingsGroupBox";
            this._settingsGroupBox.Size = new System.Drawing.Size(672, 100);
            this._settingsGroupBox.TabIndex = 3;
            this._settingsGroupBox.TabStop = false;
            this._settingsGroupBox.Text = "Clip settings";
            // 
            // _extractRadioChannelsCheckBox
            // 
            this._extractRadioChannelsCheckBox.AccessibleName = "Extract one channel per radio";
            this._extractRadioChannelsCheckBox.AutoSize = true;
            this._extractRadioChannelsCheckBox.Location = new System.Drawing.Point(16, 64);
            this._extractRadioChannelsCheckBox.Name = "_extractRadioChannelsCheckBox";
            this._extractRadioChannelsCheckBox.Size = new System.Drawing.Size(410, 19);
            this._extractRadioChannelsCheckBox.TabIndex = 5;
            this._extractRadioChannelsCheckBox.Text = "Extract one channel from stereo recording per radio (R1 = left, R2 = right)";
            this._extractRadioChannelsCheckBox.UseVisualStyleBackColor = true;
            // 
            // _resetButton
            // 
            this._resetButton.AccessibleName = "Reset timing to DXLog defaults";
            this._resetButton.Location = new System.Drawing.Point(490, 25);
            this._resetButton.Name = "_resetButton";
            this._resetButton.Size = new System.Drawing.Size(166, 27);
            this._resetButton.TabIndex = 4;
            this._resetButton.Text = "Reset defaults";
            this._resetButton.UseVisualStyleBackColor = true;
            this._resetButton.Click += new System.EventHandler(this.ResetButton_Click);
            // 
            // _durationNumericUpDown
            // 
            this._durationNumericUpDown.AccessibleName = "Total clip duration in seconds";
            this._durationNumericUpDown.Location = new System.Drawing.Point(385, 27);
            this._durationNumericUpDown.Maximum = new decimal(new int[] {
            86400,
            0,
            0,
            0});
            this._durationNumericUpDown.Name = "_durationNumericUpDown";
            this._durationNumericUpDown.Size = new System.Drawing.Size(82, 23);
            this._durationNumericUpDown.TabIndex = 3;
            this._durationNumericUpDown.ValueChanged += new System.EventHandler(this.Timing_ValueChanged);
            // 
            // _durationLabel
            // 
            this._durationLabel.AutoSize = true;
            this._durationLabel.Location = new System.Drawing.Point(250, 31);
            this._durationLabel.Name = "_durationLabel";
            this._durationLabel.Size = new System.Drawing.Size(119, 15);
            this._durationLabel.TabIndex = 2;
            this._durationLabel.Text = "Total clip duration (s)";
            // 
            // _timeBeforeNumericUpDown
            // 
            this._timeBeforeNumericUpDown.AccessibleName = "Time before QSO in seconds";
            this._timeBeforeNumericUpDown.Location = new System.Drawing.Point(140, 27);
            this._timeBeforeNumericUpDown.Maximum = new decimal(new int[] {
            86400,
            0,
            0,
            0});
            this._timeBeforeNumericUpDown.Name = "_timeBeforeNumericUpDown";
            this._timeBeforeNumericUpDown.Size = new System.Drawing.Size(82, 23);
            this._timeBeforeNumericUpDown.TabIndex = 1;
            this._timeBeforeNumericUpDown.ValueChanged += new System.EventHandler(this.Timing_ValueChanged);
            // 
            // _timeBeforeLabel
            // 
            this._timeBeforeLabel.AutoSize = true;
            this._timeBeforeLabel.Location = new System.Drawing.Point(16, 31);
            this._timeBeforeLabel.Name = "_timeBeforeLabel";
            this._timeBeforeLabel.Size = new System.Drawing.Size(114, 15);
            this._timeBeforeLabel.TabIndex = 0;
            this._timeBeforeLabel.Text = "Time before QSO (s)";
            // 
            // _exportButton
            // 
            this._exportButton.AccessibleName = "Export QSO clips";
            this._exportButton.Location = new System.Drawing.Point(24, 314);
            this._exportButton.Name = "_exportButton";
            this._exportButton.Size = new System.Drawing.Size(100, 30);
            this._exportButton.TabIndex = 4;
            this._exportButton.Text = "Export";
            this._exportButton.UseVisualStyleBackColor = true;
            this._exportButton.Click += new System.EventHandler(this.ExportButton_Click);
            // 
            // _cancelButton
            // 
            this._cancelButton.AccessibleName = "Cancel export";
            this._cancelButton.Location = new System.Drawing.Point(132, 314);
            this._cancelButton.Name = "_cancelButton";
            this._cancelButton.Size = new System.Drawing.Size(100, 30);
            this._cancelButton.TabIndex = 5;
            this._cancelButton.Text = "Cancel";
            this._cancelButton.UseVisualStyleBackColor = true;
            this._cancelButton.Visible = false;
            this._cancelButton.Click += new System.EventHandler(this.CancelButton_Click);
            // 
            // _progressBar
            // 
            this._progressBar.Location = new System.Drawing.Point(24, 380);
            this._progressBar.Name = "_progressBar";
            this._progressBar.Size = new System.Drawing.Size(672, 18);
            this._progressBar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this._progressBar.TabIndex = 7;
            // 
            // _statusLabel
            // 
            this._statusLabel.AutoEllipsis = true;
            this._statusLabel.ForeColor = System.Drawing.SystemColors.GrayText;
            this._statusLabel.Location = new System.Drawing.Point(24, 355);
            this._statusLabel.Name = "_statusLabel";
            this._statusLabel.Size = new System.Drawing.Size(672, 20);
            this._statusLabel.TabIndex = 6;
            this._statusLabel.Text = "Ready";
            // 
            // _summaryTextBox
            // 
            this._summaryTextBox.AccessibleName = "Export completion summary";
            this._summaryTextBox.BackColor = System.Drawing.SystemColors.Window;
            this._summaryTextBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this._summaryTextBox.ForeColor = System.Drawing.SystemColors.GrayText;
            this._summaryTextBox.Location = new System.Drawing.Point(24, 412);
            this._summaryTextBox.Multiline = true;
            this._summaryTextBox.Name = "_summaryTextBox";
            this._summaryTextBox.ReadOnly = true;
            this._summaryTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._summaryTextBox.Size = new System.Drawing.Size(672, 105);
            this._summaryTextBox.TabIndex = 8;
            this._summaryTextBox.Text = "No export has been run.";
            // 
            // _openOutputButton
            // 
            this._openOutputButton.AccessibleName = "Open output folder";
            this._openOutputButton.Enabled = false;
            this._openOutputButton.Location = new System.Drawing.Point(546, 314);
            this._openOutputButton.Name = "_openOutputButton";
            this._openOutputButton.Size = new System.Drawing.Size(150, 30);
            this._openOutputButton.TabIndex = 6;
            this._openOutputButton.Text = "Open export folder";
            this._openOutputButton.UseVisualStyleBackColor = true;
            this._openOutputButton.Click += new System.EventHandler(this.OpenOutputButton_Click);
            // 
            // _errorProvider
            // 
            this._errorProvider.ContainerControl = this;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(720, 532);
            this.Controls.Add(this._openOutputButton);
            this.Controls.Add(this._summaryTextBox);
            this.Controls.Add(this._progressBar);
            this.Controls.Add(this._statusLabel);
            this.Controls.Add(this._cancelButton);
            this.Controls.Add(this._exportButton);
            this.Controls.Add(this._settingsGroupBox);
            this.Controls.Add(this._filesGroupBox);
            this.Controls.Add(this._closeDxLogLabel);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "DXLog QSO Exporter";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.filesGroupBox_Layout();
            this.settingsGroupBox_Layout();
            ((System.ComponentModel.ISupportInitialize)(this._durationNumericUpDown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._timeBeforeNumericUpDown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._errorProvider)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void filesGroupBox_Layout()
        {
            this._filesGroupBox.ResumeLayout(false);
            this._filesGroupBox.PerformLayout();
        }

        private void settingsGroupBox_Layout()
        {
            this._settingsGroupBox.ResumeLayout(false);
            this._settingsGroupBox.PerformLayout();
        }
    }
}
