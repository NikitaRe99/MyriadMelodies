using System.Drawing;
using System.Windows.Forms;
using MyriadMelodies.Controls;

namespace MyriadMelodies
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        // Вся постоянная компоновка находится здесь и видна в WinForms Designer.
        // Конструктор показывает раскрытые панели; при запуске они сворачиваются.
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            compactPanel = new Panel();
            headerPanel = new Panel();
            captureStatus = new Label();
            titleLabel = new Label();
            btnMinimize = new OverlayButton();
            btnClose = new OverlayButton();
            monitorLabel = new Label();
            monitorSelector = new OverlayComboBox();
            modeLabel = new Label();
            domainUpDownMode = new OverlayComboBox();
            themeLabel = new Label();
            comboStyle = new OverlayComboBox();
            btnAddCustomColor = new OverlayButton();
            btnStartStop = new OverlayButton();
            btnShow = new OverlayButton();
            btnSettings = new OverlayButton();
            toleranceLabel = new Label();
            colorToleranceUpDown = new OverlayNumericUpDown();
            settingsPanel = new Panel();
            pinCheckBox = new CheckBox();
            opacityLabel = new Label();
            opacityUpDown = new OverlayNumericUpDown();
            captureGeometry = new Label();
            fullscreenHint = new Label();
            languageLabel = new Label();
            languageSelector = new OverlayComboBox();
            storageLabel = new Label();
            storageSelector = new OverlayComboBox();
            previewPanel = new Panel();
            demonstratPanel = new FlowLayoutPanel();
            previewPlaceholder = new Label();
            btnResetKeys = new OverlayButton();
            overlayToolTip = new ToolTip(components);
            compactPanel.SuspendLayout();
            headerPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)colorToleranceUpDown).BeginInit();
            settingsPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)opacityUpDown).BeginInit();
            previewPanel.SuspendLayout();
            SuspendLayout();
            // 
            // compactPanel
            // 
            resources.ApplyResources(compactPanel, "compactPanel");
            compactPanel.BackColor = Color.FromArgb(49, 59, 75);
            compactPanel.Controls.Add(headerPanel);
            compactPanel.Controls.Add(monitorLabel);
            compactPanel.Controls.Add(monitorSelector);
            compactPanel.Controls.Add(modeLabel);
            compactPanel.Controls.Add(domainUpDownMode);
            compactPanel.Controls.Add(themeLabel);
            compactPanel.Controls.Add(comboStyle);
            compactPanel.Controls.Add(btnAddCustomColor);
            compactPanel.Controls.Add(btnStartStop);
            compactPanel.Controls.Add(btnShow);
            compactPanel.Controls.Add(btnSettings);
            compactPanel.Name = "compactPanel";
            // 
            // headerPanel
            // 
            headerPanel.BackColor = Color.FromArgb(49, 59, 75);
            headerPanel.Controls.Add(captureStatus);
            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(btnMinimize);
            headerPanel.Controls.Add(btnClose);
            resources.ApplyResources(headerPanel, "headerPanel");
            headerPanel.Name = "headerPanel";
            headerPanel.MouseDown += OverlayHeader_MouseDown;
            headerPanel.MouseMove += OverlayHeader_MouseMove;
            headerPanel.MouseUp += OverlayHeader_MouseUp;
            // 
            // captureStatus
            // 
            resources.ApplyResources(captureStatus, "captureStatus");
            captureStatus.ForeColor = Color.FromArgb(153, 214, 198);
            captureStatus.Name = "captureStatus";
            // 
            // titleLabel
            // 
            resources.ApplyResources(titleLabel, "titleLabel");
            titleLabel.ForeColor = Color.FromArgb(237, 232, 218);
            titleLabel.Name = "titleLabel";
            overlayToolTip.SetToolTip(titleLabel, resources.GetString("titleLabel.ToolTip"));
            titleLabel.MouseDown += OverlayHeader_MouseDown;
            titleLabel.MouseMove += OverlayHeader_MouseMove;
            titleLabel.MouseUp += OverlayHeader_MouseUp;
            // 
            // btnMinimize
            // 
            resources.ApplyResources(btnMinimize, "btnMinimize");
            btnMinimize.BackColor = Color.FromArgb(49, 59, 75);
            btnMinimize.Cursor = Cursors.Hand;
            btnMinimize.FlatAppearance.BorderSize = 0;
            btnMinimize.ForeColor = Color.FromArgb(237, 232, 218);
            btnMinimize.Name = "btnMinimize";
            btnMinimize.TabStop = false;
            overlayToolTip.SetToolTip(btnMinimize, resources.GetString("btnMinimize.ToolTip"));
            btnMinimize.UseVisualStyleBackColor = false;
            btnMinimize.WindowGlyph = WindowButtonGlyph.Minimize;
            btnMinimize.Click += BtnMinimize_Click;
            // 
            // btnClose
            // 
            resources.ApplyResources(btnClose, "btnClose");
            btnClose.BackColor = Color.FromArgb(49, 59, 75);
            btnClose.Cursor = Cursors.Hand;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.ForeColor = Color.FromArgb(237, 232, 218);
            btnClose.Name = "btnClose";
            btnClose.TabStop = false;
            overlayToolTip.SetToolTip(btnClose, resources.GetString("btnClose.ToolTip"));
            btnClose.UseVisualStyleBackColor = false;
            btnClose.WindowGlyph = WindowButtonGlyph.Close;
            btnClose.Click += BtnClose_Click;
            // 
            // monitorLabel
            // 
            monitorLabel.ForeColor = Color.FromArgb(184, 193, 201);
            resources.ApplyResources(monitorLabel, "monitorLabel");
            monitorLabel.Name = "monitorLabel";
            // 
            // monitorSelector
            // 
            resources.ApplyResources(monitorSelector, "monitorSelector");
            monitorSelector.AccessibleRole = AccessibleRole.ComboBox;
            monitorSelector.BackColor = Color.FromArgb(237, 232, 218);
            monitorSelector.ForeColor = Color.FromArgb(61, 69, 84);
            monitorSelector.Name = "monitorSelector";
            monitorSelector.SelectedIndexChanged += DomainUpDownMode_SelectedItemChanged;
            monitorSelector.DrawItem += MonitorSelector_DrawItem;
            // 
            // modeLabel
            // 
            modeLabel.ForeColor = Color.FromArgb(184, 193, 201);
            resources.ApplyResources(modeLabel, "modeLabel");
            modeLabel.Name = "modeLabel";
            // 
            // domainUpDownMode
            // 
            resources.ApplyResources(domainUpDownMode, "domainUpDownMode");
            domainUpDownMode.AccessibleRole = AccessibleRole.ComboBox;
            domainUpDownMode.BackColor = Color.FromArgb(237, 232, 218);
            domainUpDownMode.ForeColor = Color.FromArgb(61, 69, 84);
            domainUpDownMode.Name = "domainUpDownMode";
            domainUpDownMode.SelectedIndexChanged += DomainUpDownMode_SelectedItemChanged;
            // 
            // themeLabel
            // 
            themeLabel.ForeColor = Color.FromArgb(184, 193, 201);
            resources.ApplyResources(themeLabel, "themeLabel");
            themeLabel.Name = "themeLabel";
            // 
            // comboStyle
            // 
            resources.ApplyResources(comboStyle, "comboStyle");
            comboStyle.AccessibleRole = AccessibleRole.ComboBox;
            comboStyle.BackColor = Color.FromArgb(237, 232, 218);
            comboStyle.ForeColor = Color.FromArgb(61, 69, 84);
            comboStyle.Name = "comboStyle";
            comboStyle.SelectedIndexChanged += ComboStyle_SelectedIndexChanged;
            // 
            // btnAddCustomColor
            // 
            resources.ApplyResources(btnAddCustomColor, "btnAddCustomColor");
            btnAddCustomColor.BackColor = Color.FromArgb(72, 83, 103);
            btnAddCustomColor.CornerRadius = 14;
            btnAddCustomColor.Cursor = Cursors.Hand;
            btnAddCustomColor.FlatAppearance.BorderSize = 0;
            btnAddCustomColor.ForeColor = Color.FromArgb(237, 232, 218);
            btnAddCustomColor.Name = "btnAddCustomColor";
            overlayToolTip.SetToolTip(btnAddCustomColor, resources.GetString("btnAddCustomColor.ToolTip"));
            btnAddCustomColor.UseVisualStyleBackColor = false;
            btnAddCustomColor.WindowGlyph = WindowButtonGlyph.Plus;
            btnAddCustomColor.Click += BtnAddCustomColor_Click;
            // 
            // btnStartStop
            // 
            btnStartStop.BackColor = Color.FromArgb(192, 255, 192);
            btnStartStop.Cursor = Cursors.Hand;
            btnStartStop.FlatAppearance.BorderSize = 0;
            resources.ApplyResources(btnStartStop, "btnStartStop");
            btnStartStop.ForeColor = Color.FromArgb(61, 69, 84);
            btnStartStop.Name = "btnStartStop";
            btnStartStop.UseVisualStyleBackColor = false;
            btnStartStop.Click += StartStopClicked;
            // 
            // btnShow
            // 
            btnShow.BackColor = Color.FromArgb(72, 83, 103);
            btnShow.Cursor = Cursors.Hand;
            btnShow.FlatAppearance.BorderSize = 0;
            resources.ApplyResources(btnShow, "btnShow");
            btnShow.ForeColor = Color.FromArgb(237, 232, 218);
            btnShow.Name = "btnShow";
            overlayToolTip.SetToolTip(btnShow, resources.GetString("btnShow.ToolTip"));
            btnShow.UseVisualStyleBackColor = false;
            btnShow.Click += BtnShow_Click;
            // 
            // btnSettings
            // 
            btnSettings.BackColor = Color.FromArgb(72, 83, 103);
            btnSettings.Cursor = Cursors.Hand;
            btnSettings.FlatAppearance.BorderSize = 0;
            resources.ApplyResources(btnSettings, "btnSettings");
            btnSettings.ForeColor = Color.FromArgb(237, 232, 218);
            btnSettings.Name = "btnSettings";
            overlayToolTip.SetToolTip(btnSettings, resources.GetString("btnSettings.ToolTip"));
            btnSettings.UseVisualStyleBackColor = false;
            btnSettings.Click += BtnSettings_Click;
            // 
            // toleranceLabel
            // 
            toleranceLabel.ForeColor = Color.FromArgb(184, 193, 201);
            resources.ApplyResources(toleranceLabel, "toleranceLabel");
            toleranceLabel.Name = "toleranceLabel";
            // 
            // colorToleranceUpDown
            // 
            resources.ApplyResources(colorToleranceUpDown, "colorToleranceUpDown");
            colorToleranceUpDown.BackColor = Color.FromArgb(237, 232, 218);
            colorToleranceUpDown.ForeColor = Color.FromArgb(61, 69, 84);
            colorToleranceUpDown.Maximum = new decimal(new int[] { 70, 0, 0, 0 });
            colorToleranceUpDown.Minimum = new decimal(new int[] { 10, 0, 0, 0 });
            colorToleranceUpDown.Name = "colorToleranceUpDown";
            colorToleranceUpDown.Value = new decimal(new int[] { 20, 0, 0, 0 });
            colorToleranceUpDown.ValueChanged += ColorToleranceUpDown_ValueChanged;
            // 
            // settingsPanel
            // 
            resources.ApplyResources(settingsPanel, "settingsPanel");
            settingsPanel.BackColor = Color.FromArgb(49, 59, 75);
            settingsPanel.Controls.Add(toleranceLabel);
            settingsPanel.Controls.Add(colorToleranceUpDown);
            settingsPanel.Controls.Add(pinCheckBox);
            settingsPanel.Controls.Add(opacityLabel);
            settingsPanel.Controls.Add(opacityUpDown);
            settingsPanel.Controls.Add(captureGeometry);
            settingsPanel.Controls.Add(fullscreenHint);
            settingsPanel.Controls.Add(languageLabel);
            settingsPanel.Controls.Add(languageSelector);
            settingsPanel.Controls.Add(storageLabel);
            settingsPanel.Controls.Add(storageSelector);
            settingsPanel.Name = "settingsPanel";
            // 
            // pinCheckBox
            // 
            pinCheckBox.Checked = true;
            pinCheckBox.CheckState = CheckState.Checked;
            resources.ApplyResources(pinCheckBox, "pinCheckBox");
            pinCheckBox.Name = "pinCheckBox";
            pinCheckBox.CheckedChanged += PinCheckBox_CheckedChanged;
            // 
            // opacityLabel
            // 
            opacityLabel.ForeColor = Color.FromArgb(184, 193, 201);
            resources.ApplyResources(opacityLabel, "opacityLabel");
            opacityLabel.Name = "opacityLabel";
            // 
            // opacityUpDown
            // 
            resources.ApplyResources(opacityUpDown, "opacityUpDown");
            opacityUpDown.BackColor = Color.FromArgb(237, 232, 218);
            opacityUpDown.ForeColor = Color.FromArgb(61, 69, 84);
            opacityUpDown.Minimum = new decimal(new int[] { 75, 0, 0, 0 });
            opacityUpDown.Name = "opacityUpDown";
            opacityUpDown.Value = new decimal(new int[] { 96, 0, 0, 0 });
            opacityUpDown.ValueChanged += OpacityUpDown_ValueChanged;
            // 
            // captureGeometry
            // 
            resources.ApplyResources(captureGeometry, "captureGeometry");
            captureGeometry.ForeColor = Color.FromArgb(184, 193, 201);
            captureGeometry.Name = "captureGeometry";
            // 
            // fullscreenHint
            // 
            fullscreenHint.ForeColor = Color.FromArgb(184, 193, 201);
            resources.ApplyResources(fullscreenHint, "fullscreenHint");
            fullscreenHint.Name = "fullscreenHint";
            // 
            // languageLabel
            // 
            languageLabel.ForeColor = Color.FromArgb(184, 193, 201);
            resources.ApplyResources(languageLabel, "languageLabel");
            languageLabel.Name = "languageLabel";
            // 
            // languageSelector
            // 
            languageSelector.AccessibleRole = AccessibleRole.ComboBox;
            languageSelector.BackColor = Color.FromArgb(237, 232, 218);
            languageSelector.ForeColor = Color.FromArgb(61, 69, 84);
            languageSelector.Items.AddRange(new object[] { "Русский", "English" });
            resources.ApplyResources(languageSelector, "languageSelector");
            languageSelector.Name = "languageSelector";
            languageSelector.SelectedIndexChanged += LanguageSelector_Changed;
            // storageLabel
            storageLabel.ForeColor = Color.FromArgb(184, 193, 201);
            storageLabel.Location = new Point(12, 51);
            storageLabel.Size = new Size(60, 20);
            storageLabel.Text = "Хранение";
            storageLabel.Name = "storageLabel";
            // storageSelector
            storageSelector.Location = new Point(72, 46);
            storageSelector.Size = new Size(256, 30);
            storageSelector.Items.AddRange(new object[] { "LocalAppData", "Рядом с EXE" });
            storageSelector.Name = "storageSelector";
            storageSelector.TabIndex = 1;
            storageSelector.SelectedIndexChanged += StorageSelector_Changed;
            overlayToolTip.SetToolTip(storageSelector,"Где сохранять настройки, темы и клавиши. При выборе текущее состояние копируется автоматически.");
            // 
            // previewPanel
            // 
            resources.ApplyResources(previewPanel, "previewPanel");
            previewPanel.BackColor = Color.FromArgb(49, 59, 75);
            previewPanel.Controls.Add(demonstratPanel);
            previewPanel.Controls.Add(previewPlaceholder);
            previewPanel.Controls.Add(btnResetKeys);
            previewPanel.Name = "previewPanel";
            // 
            // demonstratPanel
            // 
            demonstratPanel.BackColor = Color.FromArgb(49, 59, 75);
            resources.ApplyResources(demonstratPanel, "demonstratPanel");
            demonstratPanel.Name = "demonstratPanel";
            // 
            // previewPlaceholder
            // 
            previewPlaceholder.ForeColor = Color.FromArgb(184, 193, 201);
            resources.ApplyResources(previewPlaceholder, "previewPlaceholder");
            previewPlaceholder.Name = "previewPlaceholder";
            // 
            // btnResetKeys
            // 
            resources.ApplyResources(btnResetKeys, "btnResetKeys");
            btnResetKeys.BackColor = Color.FromArgb(72, 83, 103);
            btnResetKeys.ForeColor = Color.FromArgb(237, 232, 218);
            btnResetKeys.Name = "btnResetKeys";
            overlayToolTip.SetToolTip(btnResetKeys, resources.GetString("btnResetKeys.ToolTip"));
            btnResetKeys.UseVisualStyleBackColor = false;
            btnResetKeys.Click += BtnResetKeys_Click;
            // 
            // MainForm
            // 
            resources.ApplyResources(this, "$this");
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(49, 59, 75);
            Controls.Add(compactPanel);
            Controls.Add(settingsPanel);
            Controls.Add(previewPanel);
            ForeColor = Color.FromArgb(237, 232, 218);
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            Name = "MainForm";
            Opacity = 0.96D;
            TopMost = true;
            compactPanel.ResumeLayout(false);
            headerPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)colorToleranceUpDown).EndInit();
            settingsPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)opacityUpDown).EndInit();
            previewPanel.ResumeLayout(false);
            ResumeLayout(false);
        }

        private Panel compactPanel;
        private Panel headerPanel;
        private Label titleLabel;
        private Label captureStatus;
        private OverlayButton btnMinimize;
        private OverlayButton btnClose;
        private OverlayComboBox comboStyle;
        private OverlayButton btnStartStop;
        private OverlayButton btnShow;
        private OverlayButton btnSettings;
        private Panel settingsPanel;
        private Label monitorLabel;
        private OverlayComboBox monitorSelector;
        private Label modeLabel;
        private Label themeLabel;
        private OverlayComboBox domainUpDownMode;
        private Label toleranceLabel;
        private OverlayNumericUpDown colorToleranceUpDown;
        private OverlayButton btnAddCustomColor;
        private CheckBox pinCheckBox;
        private Label opacityLabel;
        private OverlayNumericUpDown opacityUpDown;
        private Label captureGeometry;
        private Label fullscreenHint;
        private Panel previewPanel;
        private FlowLayoutPanel demonstratPanel;
        private Label previewPlaceholder;
        private OverlayButton btnResetKeys;
        private ToolTip overlayToolTip;
        private Label languageLabel;
        private OverlayComboBox languageSelector;
        private Label storageLabel;
        private OverlayComboBox storageSelector;
    }
}
