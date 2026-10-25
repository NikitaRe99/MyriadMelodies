using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MyriadMelodies
{
    public partial class MainForm
    {
        private Color idleStartColor;
        private bool settingsExpanded;
        private Point dragOffset;
        private bool dragging;
        private System.Windows.Forms.Timer captureSourceTimer;
        private Size captureWindowClientSize;
        private Size captureWindowFrameSize;
        private Point captureWindowClientOffset;
        private WindowCaptureTarget SelectedGameWindow => monitorSelector.SelectedItem as WindowCaptureTarget;
        private bool IsInDesigner => LicenseManager.UsageMode == LicenseUsageMode.Designtime || DesignMode;

        private sealed class MonitorOption
        {
            public string DeviceName { get; }
            private readonly int number,width,height;
            private readonly bool primary;
            public MonitorOption(Screen screen, int index)
            {
                DeviceName = screen.DeviceName;
                number=index+1;width=screen.Bounds.Width;height=screen.Bounds.Height;primary=screen.Primary;
            }
            public override string ToString() => UiText.F("Экран {0} · {1} × {2}","Display {0} · {1} × {2}",number,width,height)+(primary?(UiText.English?" · primary":" · основной"):"");
        }

        private Rectangle SelectedMonitorBounds
        {
            get
            {
                if(SelectedGameWindow is WindowCaptureTarget window)
                {
                    if(!window.IsAvailable) throw new InvalidOperationException(UiText.T("Окно игры закрыто. Выберите источник заново."));
                    return new Rectangle(Point.Empty,WindowCaptureGeometry.Read(window.Handle).ClientBounds.Size);
                }
                if (monitorSelector?.SelectedItem is MonitorOption option)
                    foreach (Screen screen in Screen.AllScreens)
                        if (screen.DeviceName == option.DeviceName) return screen.Bounds;
                return (Screen.PrimaryScreen ?? Screen.AllScreens[0]).Bounds;
            }
        }

        // Только данные и поведение. Размеры, стили и постоянные элементы
        // объявлены в Designer; здесь дерево контролов не перестраивается.
        private void InitializeRuntimeAppearance()
        {
            idleStartColor = btnStartStop.BackColor;
            RefreshMonitors();
            monitorSelector.DropDown += (s, e) => RefreshMonitors();
            monitorLabel.Text=UiText.T("Захват");
            overlayToolTip.SetToolTip(monitorSelector,UiText.T("Экран или окно Genshin. Запустите игру и раскройте список, чтобы обновить окна."));
            captureSourceTimer=new System.Windows.Forms.Timer(components){Interval=150};
            captureSourceTimer.Tick+=(s,e)=>ValidateWindowCapture();
            settingsExpanded = false;
            previewPlaceholder.Visible = false;
            DoubleBuffered = true;
            foreach(Control control in new Control[]{this,headerPanel,titleLabel,captureStatus,compactPanel,settingsPanel,previewPanel,demonstratPanel,
                monitorLabel,modeLabel,themeLabel,toleranceLabel,opacityLabel,captureGeometry,fullscreenHint,languageLabel,storageLabel})
            {
                control.MouseDown-=OverlayHeader_MouseDown;control.MouseDown+=OverlayHeader_MouseDown;
                control.MouseMove-=OverlayHeader_MouseMove;control.MouseMove+=OverlayHeader_MouseMove;
                control.MouseUp-=OverlayHeader_MouseUp;control.MouseUp+=OverlayHeader_MouseUp;
                control.MouseCaptureChanged+=(s,e)=>{if(!control.Capture)dragging=false;};
            }
            UpdateOverlayLayout();
            UpdateCaptureState();
        }

        private void RefreshMonitors()
        {
            string selected = (monitorSelector.SelectedItem as MonitorOption)?.DeviceName;
            IntPtr selectedWindow=SelectedGameWindow?.Handle ?? IntPtr.Zero;
            monitorSelector.BeginUpdate();
            try
            {
                monitorSelector.Items.Clear();
                Screen[] screens = Screen.AllScreens;
                int selectedIndex = 0;
                for (int i = 0; i < screens.Length; i++)
                {
                    monitorSelector.Items.Add(new MonitorOption(screens[i], i));
                    if (screens[i].DeviceName == selected || (selected == null && screens[i].Primary))
                        selectedIndex = i;
                }
                foreach(var window in WindowCaptureTarget.FindGameWindows())
                {
                    int index=monitorSelector.Items.Count;
                    monitorSelector.Items.Add(window);
                    if(window.Handle==selectedWindow) selectedIndex=index;
                }
                monitorSelector.SelectedIndex = selectedIndex;
            }
            finally { monitorSelector.EndUpdate(); }
        }

        private void ValidateWindowCapture()
        {
            if(!screenCapture.IsRunning) return;
            try
            {
                if(SelectedGameWindow is not WindowCaptureTarget window)
                {
                    Screen monitor=Array.Find(Screen.AllScreens,screen=>screen.DeviceName==captureMonitorDevice);
                    if(monitor==null)
                    {
                        screenCapture.Stop();UpdateCaptureState();captureStatus.Text=UiText.T("● Экран отключён");return;
                    }
                    if(monitor.Bounds!=captureMonitorBounds){StartGpuCapture();UpdateCaptureState();}
                    if(!CanSendInput())
                    {
                        lock(inputProcessingSync){keyInput.ReleaseAll();ResetShortPressLatches();}
                        captureStatus.Text=UiText.T("● Игра неактивна");
                    }
                    else UpdateCaptureState();
                    return;
                }
                if(!window.IsAvailable || WindowCaptureGeometry.IsIconic(window.Handle))
                {
                    screenCapture.Stop();keyInput.ReleaseAll();
                    UpdateCaptureState();
                    captureStatus.Text=window.IsAvailable?UiText.T("● Окно свёрнуто"):UiText.T("● Окно закрыто");
                    return;
                }
                var geometry=WindowCaptureGeometry.Read(window.Handle);
                var offset=new Point(geometry.ClientBounds.Left-geometry.FrameBounds.Left,geometry.ClientBounds.Top-geometry.FrameBounds.Top);
                if(geometry.ClientBounds.Size!=captureWindowClientSize || geometry.FrameBounds.Size!=captureWindowFrameSize || offset!=captureWindowClientOffset)
                {
                    keyInput.ReleaseAll();ResetShortPressLatches();
                    StartGpuCapture();UpdateCaptureState();
                }
                if(WindowCaptureGeometry.GetForegroundWindow()!=window.Handle)
                {
                    lock(inputProcessingSync){keyInput.ReleaseAll();ResetShortPressLatches();}
                    captureStatus.Text=UiText.T("● Игра неактивна");
                }
                else UpdateCaptureState();
            }
            catch(Exception exception)
            {
                screenCapture.Stop();keyInput.ReleaseAll();
                UpdateCaptureState();
                OverlayMessageDialog.ShowError(this,UiText.T("Захват окна остановлен"),exception);
            }
        }

        private void MonitorSelector_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index >= 0)
            {
                Rectangle textBounds = e.Bounds;
                textBounds.X += 10;
                textBounds.Width = Math.Max(0, textBounds.Width - 14);
                TextRenderer.DrawText(e.Graphics, monitorSelector.Items[e.Index].ToString(), e.Font,
                    textBounds, e.ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
            e.DrawFocusRectangle();
        }

        private void UpdateOverlayLayout()
        {
            if (IsInDesigner || compactPanel == null) return;
            SuspendLayout();
            settingsPanel.Visible = settingsExpanded;
            previewPanel.Visible = preview;
            int bottom = compactPanel.Bottom;
            settingsPanel.Top = bottom;
            if (settingsExpanded) bottom += settingsPanel.Height;
            previewPanel.Top = bottom;
            if (preview) bottom += previewPanel.Height;
            ClientSize = new Size(compactPanel.Width, bottom);
            btnSettings.BackColor = settingsExpanded ? Color.FromArgb(96, 108, 128) : Color.FromArgb(72, 83, 103);
            btnShow.BackColor = preview ? Color.FromArgb(96, 108, 128) : Color.FromArgb(72, 83, 103);
            ResumeLayout(true);
            LayoutPreviewAreas();
            // При раскрытии у края экрана оставляем всю панель доступной.
            if (IsHandleCreated)
            {
                Rectangle work = Screen.FromControl(this).WorkingArea;
                Location = new Point(Math.Clamp(Left, work.Left, Math.Max(work.Left, work.Right - Width)),
                    Math.Clamp(Top, work.Top, Math.Max(work.Top, work.Bottom - Height)));
            }
        }

        private void LayoutPreviewAreas()
        {
            if (captureAreas.Count == 0) return;
            float scale = DeviceDpi / 96f;
            int gap = (int)Math.Round(8 * scale);
            int available = demonstratPanel.ClientSize.Width;
            int baseWidth = Math.Max(1, (available - gap * 2) / 3);
            demonstratPanel.SuspendLayout();
            for (int i = 0; i < captureAreas.Count; i++)
            {
                int column = i % 3;
                int width = column == 2 ? available - baseWidth * 2 - gap * 2 : baseWidth;
                var area = captureAreas[i];
                area.Container.Margin = new Padding(0, 0, column == 2 ? 0 : gap, i < 3 ? gap : 0);
                area.ResizePreview(width, (int)Math.Round(100 * scale), scale);
                demonstratPanel.SetFlowBreak(area.Container, column == 2);
            }
            demonstratPanel.ResumeLayout(true);
        }

        private void BtnSettings_Click(object sender, EventArgs e)
        {
            settingsExpanded = !settingsExpanded;
            UpdateOverlayLayout();
        }

        private void BtnMinimize_Click(object sender, EventArgs e) => WindowState = FormWindowState.Minimized;
        private void BtnClose_Click(object sender, EventArgs e) => Close();
        private void PinCheckBox_CheckedChanged(object sender, EventArgs e) => TopMost = pinCheckBox.Checked;
        private void OpacityUpDown_ValueChanged(object sender, EventArgs e) => Opacity = (double)opacityUpDown.Value / 100;

        private void OverlayHeader_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            var control = (Control)sender;
            dragOffset = PointToClient(control.PointToScreen(e.Location));
            dragging = true;
            control.Capture = true;
        }

        private void OverlayHeader_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
                Location = new Point(Cursor.Position.X - dragOffset.X, Cursor.Position.Y - dragOffset.Y);
        }

        private void OverlayHeader_MouseUp(object sender, MouseEventArgs e)
        {
            dragging = false;
            ((Control)sender).Capture = false;
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (IsInDesigner || Width < 1 || Height < 1) return;
            int radius = Math.Max(8, (int)Math.Round(24 * DeviceDpi / 96.0));
            using (var path = new GraphicsPath())
            {
                path.AddArc(0, 0, radius, radius, 180, 90);
                path.AddArc(Width - radius, 0, radius, radius, 270, 90);
                path.AddArc(Width - radius, Height - radius, radius, radius, 0, 90);
                path.AddArc(0, Height - radius, radius, radius, 90, 90);
                path.CloseFigure();
                Region old = Region;
                Region = new Region(path);
                old?.Dispose();
            }
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            UpdateOverlayLayout();
        }

        private void UpdateCaptureState()
        {
            bool running = screenCapture.IsRunning || screenCapture.IsRecovering;
            bool sampling = System.Threading.Volatile.Read(ref customColorSamplingInProgress) != 0;
            if(captureSourceTimer!=null) captureSourceTimer.Enabled=running;
            btnStartStop.Text = running ? UiText.T("■  Стоп") : UiText.T("▶  Старт");
            btnStartStop.BackColor = running ? Color.FromArgb(218,153,141) : idleStartColor;
            btnStartStop.Enabled = !sampling;
            btnShow.Enabled = !running && !sampling;
            monitorSelector.Enabled = !running && !sampling;
            domainUpDownMode.Enabled = !running && !sampling;
            comboStyle.Enabled = !running && !sampling;
            btnAddCustomColor.Enabled = !running && !sampling;
            btnResetKeys.Enabled = !running && !sampling;
            storageSelector.Enabled=settingsStore.CanChangeLocation && !running && !sampling;
            foreach(var area in captureAreas) area.CanAssign=!running && !sampling;
            captureStatus.Text = sampling ? UiText.T("● Считывание") : screenCapture.IsRecovering ? UiText.T("● Восстановление") : running ? UiText.T("● В работе") : UiText.T("● Готово");
        }
    }
}
