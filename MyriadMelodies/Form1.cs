using MyriadMelodies;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace MyriadMelodies
{


    public partial class MainForm : Form
    {
        private enum ColorSignal
        {
            None,
            ShortPress,
            LongPress,
            LongPressFlash,
            Neutral
        }

        private readonly FormElementCreator elementCreator = new FormElementCreator();
        private readonly List<PreviewAreaView> captureAreas = new List<PreviewAreaView>(6);
        private readonly GpuScreenCapture screenCapture = new GpuScreenCapture();
        private readonly KeyInputDispatcher keyInput;
        private Rectangle screenBounds => SelectedMonitorBounds;
        Action<Color, int> compareMethod;
        long lastPreviewInfoTimestamp;
        int previewInfoUpdatePending;
        readonly object inputProcessingSync = new object();
        int suppressKeyInput;
        readonly int[] shortPressLatched = new int[6];
        TaskCompletionSource<Color[]> pendingColorReady;
        int customColorSamplingInProgress;
        readonly CancellationTokenSource lifetimeCancellation = new CancellationTokenSource();
        IntPtr inputTargetWindow;
        IntPtr inputMonitor;
        private string captureMonitorDevice;
        private Rectangle captureMonitorBounds;
        private readonly Func<IntPtr,bool> gameWindowCheck;


        bool preview;
        private readonly Keys[] AreaKeys = LaneBindings.CreateDefaults();

        private int colorTolerance = 20; // Допуск цвета




        public MainForm() : this(new OverlaySettingsStore()) { }

        internal MainForm(OverlaySettingsStore settingsStore) : this(settingsStore,KeyboardInputSender.Send,WindowCaptureTarget.IsGameWindow) { }

        internal MainForm(OverlaySettingsStore settingsStore,Action<Keys,bool> sendKey,Func<IntPtr,bool> gameWindowCheck)
        {
            this.settingsStore = settingsStore;
            this.gameWindowCheck=gameWindowCheck;
            OverlayPreferences saved = null;
            if(System.ComponentModel.LicenseManager.UsageMode!=System.ComponentModel.LicenseUsageMode.Designtime)
            {
                saved=settingsStore.Load();
                UiText.SetLanguage(saved.Language);
            }
            InitializeComponent();
            if (IsInDesigner) return;
            keyInput = new KeyInputDispatcher(AreaKeys.Length, sendKey);
            InitializeRuntimeAppearance();
            InitializeCapture();
            MainInfo();
            RestoreOverlayPreferences(saved);
            InitializeStorageChoice();
            int languageIndex=UiText.English?1:0;
            if(languageSelector.SelectedIndex==languageIndex) ApplyLanguage();
            else languageSelector.SelectedIndex=languageIndex;

        }

        private void AreaAdd()
        {
            byte areaCounter = (byte)(captureAreas.Count+1);

            PreviewAreaView previewArea = elementCreator.CreatePreviewArea(
                LaneBindings.DisplayName(AreaKeys[areaCounter - 1]),
                areaCounter, DeviceDpi / 96f);
            captureAreas.Add(previewArea);
            previewArea.SetKeyName(LaneBindings.DisplayName(AreaKeys[areaCounter-1]));
            previewArea.AssignmentRequested+=(s,e)=>EditAreaKey(previewArea.AreaNumber-1);
            previewArea.CanAssign=!screenCapture.IsRunning && Volatile.Read(ref customColorSamplingInProgress)==0;
            foreach(Control control in previewArea.Container.Controls)
                overlayToolTip.SetToolTip(control,UiText.T("Нажмите, чтобы изменить клавишу этой зоны."));
            overlayToolTip.SetToolTip(previewArea.Container,UiText.T("Нажмите, чтобы изменить клавишу этой зоны."));
            demonstratPanel.Controls.Add(previewArea.Container);
        }

        private void InitializeCapture()
        {
            screenCapture.FrameCaptured += ScreenCapture_FrameCaptured;
            screenCapture.CaptureFailed += ScreenCapture_CaptureFailed;
            screenCapture.WindowClosed += ScreenCapture_WindowClosed;
            screenCapture.CaptureInterrupted += ScreenCapture_Interrupted;
        }
        private void ScreenCapture_Interrupted(object sender,EventArgs e)
        {
            lock(inputProcessingSync)
            {
                keyInput.ReleaseAll();ResetShortPressLatches();
            }
            Interlocked.Exchange(ref pendingColorReady,null)?.TrySetException(new InvalidOperationException(UiText.T("Захват прерван. Повторите считывание цветов.")));
            if(IsDisposed || Disposing || !IsHandleCreated) return;
            if(!InvokeRequired) UpdateCaptureState();
            else
            {
                try {BeginInvoke((MethodInvoker)(()=>{if(!IsDisposed && !Disposing) UpdateCaptureState();}));}
                catch(InvalidOperationException) { }
            }
        }
        private void ScreenCapture_WindowClosed(object sender,EventArgs e)
        {
            if(IsDisposed || Disposing || !IsHandleCreated) return;
            if(InvokeRequired)
            {
                try {BeginInvoke((MethodInvoker)(()=>ScreenCapture_WindowClosed(sender,e)));}
                catch(InvalidOperationException) { }
                return;
            }
            keyInput.ReleaseAll();
            Interlocked.Exchange(ref pendingColorReady,null)?.TrySetException(new InvalidOperationException(UiText.T("Окно игры закрыто.")));
            UpdateCaptureState();captureStatus.Text=UiText.T("● Окно закрыто");
        }
        private void MainInfo()
        {
            ConfigureColorStyleComboBox();
            PopulateColorStyles();
            comboStyle.SelectedIndex = ColorPaletteCatalog.GetLastUsedStyleIndex();
            domainUpDownMode.Items.Clear();
            domainUpDownMode.Items.AddRange(new object[] { UiText.T("Вертикальный режим"), UiText.T("Режим распространения") });
            domainUpDownMode.SelectedIndex = 0;
            // Явно переопределяем возможное старое значение из ресурсов Designer.
            btnAddCustomColor.Enabled = true;

        }

        private void ConfigureColorStyleComboBox()
        {
            comboStyle.ItemHeight = 22;
            comboStyle.DropDownWidth = 430;
            comboStyle.DrawItem += ComboStyle_DrawItem;
            comboStyle.ItemAction += (s, e) => EditTheme(e.Index);
            comboStyle.AllowItemReorder=true;
            comboStyle.ItemMoveRequested+=(s,e)=>
            {
                try {ColorPaletteCatalog.MoveStyle(e.From,e.To);comboStyle.MoveItem(e.From,e.To);}
                catch(Exception exception){OverlayMessageDialog.ShowError(this,UiText.T("Не удалось изменить порядок тем"),exception);}
            };
            overlayToolTip.SetToolTip(comboStyle, UiText.T("Карандаш или F2 — изменить. Перетащите тему или Alt+↑/↓ — порядок."));
        }

        private void ComboStyle_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= comboStyle.Items.Count ||
                !(comboStyle.Items[e.Index] is ColorPaletteStyle paletteStyle))
            {
                return;
            }

            const int circleDiameter = 13;
            const int circleGap = 4;
            const int rightPadding = 6;
            int circlesWidth = circleDiameter * 3 + circleGap * 2;
            int circlesLeft = e.Bounds.Right - rightPadding - circlesWidth;
            var textBounds = new Rectangle(
                e.Bounds.Left + 10,
                e.Bounds.Top,
                Math.Max(0, circlesLeft - e.Bounds.Left - 14),
                e.Bounds.Height);
            TextRenderer.DrawText(
                e.Graphics,
                ColorPaletteCatalog.GetDisplayName(paletteStyle),
                e.Font,
                textBounds,
                e.ForeColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);

            using (var outline = new Pen(Color.FromArgb(80, 80, 80)))
            {
                int y = e.Bounds.Top + (e.Bounds.Height - circleDiameter) / 2;
                DrawColorCircle(e.Graphics, outline, paletteStyle.ShortPressColor,
                    circlesLeft, y, circleDiameter);
                DrawColorCircle(e.Graphics, outline, paletteStyle.LongPressColor,
                    circlesLeft + circleDiameter + circleGap, y, circleDiameter);
                DrawColorCircle(e.Graphics, outline, paletteStyle.LongPressFlashColor,
                    circlesLeft + (circleDiameter + circleGap) * 2, y, circleDiameter);
            }

            e.DrawFocusRectangle();
        }

        private static void DrawColorCircle(
            Graphics graphics,
            Pen outline,
            Color color,
            int x,
            int y,
            int diameter)
        {
            using (var fill = new SolidBrush(color))
                graphics.FillEllipse(fill, x, y, diameter, diameter);
            graphics.DrawEllipse(outline, x, y, diameter, diameter);
        }

        private void PopulateColorStyles()
        {
            comboStyle.BeginUpdate();
            try
            {
                comboStyle.Items.Clear();
                foreach (ColorPaletteStyle paletteStyle in ColorPaletteCatalog.Styles)
                    comboStyle.Items.Add(paletteStyle);
            }
            finally
            {
                comboStyle.EndUpdate();
            }
        }

        private void StartGpuCapture(bool renderPreview = true)
        {
            // Проверяем состояние окна до расчёта геометрии: у свёрнутого
            // клиентская область может иметь нулевую высоту.
            if(SelectedGameWindow is WindowCaptureTarget selectedWindow)
            {
                if(!selectedWindow.IsAvailable) throw new CaptureSourceUnavailableException(UiText.T("Окно игры закрыто. Запустите игру и выберите её окно в списке «Захват»."));
                if(WindowCaptureGeometry.IsIconic(selectedWindow.Handle))
                    throw new CaptureSourceUnavailableException(UiText.T("Окно игры свёрнуто. Разверните его и нажмите «Старт» ещё раз."));
            }
            Rectangle field=screenBounds;
            if(field.Width<=0 || field.Height<=0)
                throw new CaptureSourceUnavailableException(UiText.T("Игровая область пока недоступна. Разверните окно игры и повторите запуск."));
            Rectangle[] regions=CaptureLayout.CreateRegions(field,domainUpDownMode.SelectedIndex==1);
            RecalculateCaptureRegions();
            IntPtr[] previewHandles = Array.Empty<IntPtr>();
            if (preview && renderPreview)
            {
                previewHandles = new IntPtr[captureAreas.Count];
                for (int i = 0; i < captureAreas.Count; i++)
                    previewHandles[i] = captureAreas[i].GpuPreview.Handle;
            }
            lastPreviewInfoTimestamp = 0;
            ResetShortPressLatches();
            if(SelectedGameWindow is WindowCaptureTarget window)
            {
                if(!window.IsAvailable) throw new InvalidOperationException(UiText.T("Окно игры закрыто. Выберите источник заново."));
                captureWindowClientSize=field.Size;
                var geometry=WindowCaptureGeometry.Read(window.Handle);
                captureWindowFrameSize=geometry.FrameBounds.Size;
                captureWindowClientOffset=new Point(geometry.ClientBounds.Left-geometry.FrameBounds.Left,geometry.ClientBounds.Top-geometry.FrameBounds.Top);
                Volatile.Write(ref inputTargetWindow,window.Handle);
                screenCapture.StartWindow(window.Handle,regions,previewHandles);
            }
            else
            {
                Volatile.Write(ref inputTargetWindow,IntPtr.Zero);
                Screen monitor=Screen.FromRectangle(field);
                captureMonitorDevice=monitor.DeviceName;captureMonitorBounds=field;
                Volatile.Write(ref inputMonitor,CaptureItemInterop.GetMonitorHandle(monitor));
                screenCapture.Start(regions, previewHandles);
            }
        }

        private void ScreenCapture_FrameCaptured(object sender, GpuFrameCapturedEventArgs e)
        {
            if (!screenCapture.IsRunning || IsDisposed || Disposing || !IsHandleCreated)
                return;

            int capturedCount = Math.Min(e.Colors.Count, AreaKeys.Length);
            lock (inputProcessingSync)
            {
                if(!screenCapture.IsRunning || e.SessionId!=screenCapture.SessionId) return;
                if(capturedCount==AreaKeys.Length)
                {
                    var request=Interlocked.Exchange(ref pendingColorReady,null);
                    if(request!=null)
                    {
                        var sample=new Color[capturedCount];for(int i=0;i<capturedCount;i++)sample[i]=e.Colors[i];
                        request.TrySetResult(sample);
                    }
                }
                if(!CanSendInput())
                {
                    keyInput.ReleaseAll();ResetShortPressLatches();
                }
                else if (suppressKeyInput == 0)
                {
                    for (int i = 0; i < capturedCount; i++)
                        compareMethod(e.Colors[i], i);
                }
            }

            if (preview && ShouldUpdatePreviewInfo() &&
                Interlocked.CompareExchange(ref previewInfoUpdatePending, 1, 0) == 0)
            {
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        try
                        {
                            if (!preview || !screenCapture.IsRunning || e.SessionId!=screenCapture.SessionId)
                                return;

                            int count = Math.Min(e.Colors.Count, captureAreas.Count);
                            for (int i = 0; i < count; i++)
                            {
                                captureAreas[i].Update(
                                    e.Colors[i],
                                    keyInput.IsPressed(i));
                            }
                        }
                        finally
                        {
                            Volatile.Write(ref previewInfoUpdatePending, 0);
                        }
                    });
                }
                catch (InvalidOperationException)
                {
                    Volatile.Write(ref previewInfoUpdatePending, 0);
                }
            }
        }

        private bool CanSendInput()
        {
            IntPtr target=Volatile.Read(ref inputTargetWindow);
            IntPtr foreground=WindowCaptureGeometry.GetForegroundWindow();
            return target!=IntPtr.Zero ? foreground==target : gameWindowCheck(foreground) &&
                WindowCaptureGeometry.MonitorFromWindow(foreground,2)==Volatile.Read(ref inputMonitor);
        }

        private bool ShouldUpdatePreviewInfo()
        {
            long now = Stopwatch.GetTimestamp();
            // Текстовые RGB-индикаторы не влияют на распознавание; 5 Гц
            // достаточно для наблюдения и заметно сокращает WinForms-работу.
            long interval = Stopwatch.Frequency / 5;
            if (lastPreviewInfoTimestamp != 0 && now - lastPreviewInfoTimestamp < interval)
                return false;
            lastPreviewInfoTimestamp = now;
            return true;
        }

        private void ScreenCapture_CaptureFailed(object sender, CaptureFailedEventArgs e)
        {
            int failedSession=screenCapture.SessionId;
            TaskCompletionSource<Color[]> colorReadyRequest =
                Interlocked.Exchange(ref pendingColorReady, null);
            bool colorSamplingWasActive = colorReadyRequest != null || Volatile.Read(ref customColorSamplingInProgress)!=0;
            if (colorReadyRequest != null)
                colorReadyRequest.TrySetException(e.Exception);

            keyInput.ReleaseAll();
            if(IsDisposed || Disposing || !IsHandleCreated) return;
            try {BeginInvoke((MethodInvoker)delegate
            {
                if(IsDisposed || Disposing || failedSession!=screenCapture.SessionId) return;
                UpdateCaptureState();
                if (!colorSamplingWasActive)
                {
                    OverlayMessageDialog.ShowError(this,UiText.T("Ошибка GPU-захвата"),e.Exception);
                }
            });}
            catch(InvalidOperationException) { }
        }


        private void ProcessCapturedColor(
            Color averageColor,
            ColorPaletteStyle paletteStyle,
            bool usesStandardInput,
            int areaIndex)
        {
            ColorSignal signal = DetectColorSignal(averageColor, paletteStyle);
            if (signal == ColorSignal.None)
                return;

            if (signal == ColorSignal.ShortPress)
            {
                if (Interlocked.CompareExchange(
                    ref shortPressLatched[areaIndex], 1, 0) != 0)
                {
                    return;
                }
            }
            else if (signal == ColorSignal.LongPressFlash || signal == ColorSignal.Neutral)
            {
                Volatile.Write(ref shortPressLatched[areaIndex], 0);
            }

            DispatchColorSignal(AreaKeys[areaIndex], signal, usesStandardInput, areaIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ColorSignal DetectColorSignal(Color color, ColorPaletteStyle paletteStyle)
        {
            ColorSignal best=ColorSignal.None;
            int bestDistance=int.MaxValue;
            int tolerance=Volatile.Read(ref colorTolerance);
            int red=color.R,green=color.G,blue=color.B;
            void Consider(Color reference,ColorSignal signal)
            {
                int r=red-reference.R,g=green-reference.G,b=blue-reference.B;
                if(Math.Abs(r)>=tolerance || Math.Abs(g)>=tolerance || Math.Abs(b)>=tolerance) return;
                int distance=r*r+g*g+b*b;
                if(distance<bestDistance){bestDistance=distance;best=signal;}
            }
            Consider(paletteStyle.ShortPressColor,ColorSignal.ShortPress);
            Consider(paletteStyle.LongPressColor,ColorSignal.LongPress);
            Consider(paletteStyle.LongPressFlashColor,ColorSignal.LongPressFlash);
            Consider(ColorPaletteCatalog.NeutralColor,ColorSignal.Neutral);
            return best;
        }

        private void ResetShortPressLatches()
        {
            for (int i = 0; i < shortPressLatched.Length; i++)
                Volatile.Write(ref shortPressLatched[i], 0);
        }





        private void DispatchColorSignal(
            Keys key,
            ColorSignal signal,
            bool usesStandardInput,
            int areaIndex)
        {
            switch (signal)
            {
                case ColorSignal.ShortPress:
                    keyInput.TapOrRelease(areaIndex, key, 10);
                    break;

                case ColorSignal.LongPress when usesStandardInput:
                    keyInput.PressOrRetrigger(areaIndex, key, 50);
                    break;

                case ColorSignal.LongPress:
                    keyInput.Press(areaIndex, key);
                    break;

                case ColorSignal.LongPressFlash when !usesStandardInput:
                case ColorSignal.Neutral:
                    keyInput.Release(areaIndex);
                    break;
            }
        }

        void StartStopClicked(object sender, EventArgs e)
        {
            if (screenCapture.IsRunning || screenCapture.IsRecovering)
            {
                screenCapture.Stop();
                keyInput.ReleaseAll();
                UpdateCaptureState();

            }
            else
            {
                try
                {
                    StartGpuCapture();
                    UpdateCaptureState();
                }
                catch (Exception exception)
                {
                    screenCapture.Stop();keyInput.ReleaseAll();
                    UpdateCaptureState();
                    OverlayMessageDialog.ShowError(this,UiText.T("Не удалось запустить захват"),exception);
                }

            }
        }

        private void ComboStyle_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!(comboStyle.SelectedItem is ColorPaletteStyle selectedStyle))
                return;

            ResetShortPressLatches();
            bool usesStandardInput = selectedStyle.UsesStandardInput;
            compareMethod = (averageColor, areaIndex) => ProcessCapturedColor(
                averageColor,
                selectedStyle,
                usesStandardInput,
                areaIndex);


        }

        private void EditTheme(int index)
        {
            if(screenCapture.IsRunning || screenCapture.IsRecovering || index<0 || index>=comboStyle.Items.Count || Volatile.Read(ref customColorSamplingInProgress)!=0) return;
            var style=(ColorPaletteStyle)comboStyle.Items[index];
            if(Interlocked.Exchange(ref customColorSamplingInProgress,1)!=0) return;
            lock(inputProcessingSync){suppressKeyInput=1;keyInput.ReleaseAll();}
            UpdateCaptureState();
            try
            {
                using var editor=new ThemeEditDialog(style,async owner=>
                {
                    try {return await ReadPreparedThemeColorsAsync(owner);}
                    catch(Exception) when(lifetimeCancellation.IsCancellationRequested){return null;}
                    catch(Exception exception)
                    {
                        MessageBox.Show(owner,UiText.T(exception.Message),UiText.T("Не удалось считать цвета"),MessageBoxButtons.OK,MessageBoxIcon.Error);
                        return null;
                    }
                });
                DialogResult result=editor.ShowDialog(this);
                if(IsDisposed || Disposing) return;
                int selected=comboStyle.SelectedIndex;
                if(result==DialogResult.Retry && style.IsCustom)
                {
                    using var confirmation=new DeleteThemeDialog(style.Name);
                    if(confirmation.ShowDialog(this)!=DialogResult.OK || !ColorPaletteCatalog.RemoveCustomStyle(style)) return;
                    if(selected==index) selected=ColorPaletteCatalog.GetDefaultStyleIndex();
                    else if(selected>index) selected--;
                }
                else if(result==DialogResult.Ignore && !style.IsCustom) ColorPaletteCatalog.ResetBuiltInStyle(style);
                else if(result==DialogResult.OK)
                    ColorPaletteCatalog.UpdateStyleFromEditor(style,editor.ThemeName,editor.ShortColor,editor.LongColor,editor.FlashColor,editor.UsesStandardInput);
                else return;
                PopulateColorStyles();
                comboStyle.SelectedIndex=selected;
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, UiText.T(exception.Message), UiText.T("Не удалось изменить тему"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                lock(inputProcessingSync) suppressKeyInput=0;
                Interlocked.Exchange(ref customColorSamplingInProgress,0);
                if(!IsDisposed && !Disposing) UpdateCaptureState();
            }
        }

        private async Task<(Color Short,Color Long)?> ReadPreparedThemeColorsAsync(IWin32Window owner)
        {
            CancellationToken cancellation=lifetimeCancellation.Token;
            cancellation.ThrowIfCancellationRequested();
            using(var preparation=new ThemeCapturePreparationDialog(domainUpDownMode.Text))
                if(preparation.ShowDialog(owner)!=DialogResult.OK) return null;
            cancellation.ThrowIfCancellationRequested();
            bool temporaryCapture=!screenCapture.IsRunning;
            var request=new TaskCompletionSource<Color[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            Interlocked.Exchange(ref pendingColorReady,request);
            try
            {
                if(temporaryCapture) StartGpuCapture(renderPreview:false);
                Color[] colors;
                try {colors=await request.Task.WaitAsync(TimeSpan.FromSeconds(2),cancellation);}
                catch(TimeoutException exception)
                {throw new TimeoutException(UiText.T("Не удалось быстро получить цвета областей."),exception);}
                cancellation.ThrowIfCancellationRequested();
                return (colors[4],colors[1]);
            }
            finally
            {
                Interlocked.CompareExchange(ref pendingColorReady,null,request);
                // Stop also cancels a queued recovery when no frame is currently running.
                if(temporaryCapture){screenCapture.Stop();keyInput.ReleaseAll();}
            }
        }

        private async void BtnAddCustomColor_Click(object sender, EventArgs e)
        {
            if(screenCapture.IsRunning || screenCapture.IsRecovering) return;
            if (Interlocked.Exchange(ref customColorSamplingInProgress, 1) != 0)
                return;

            lock (inputProcessingSync)
            {
                suppressKeyInput = 1;
                keyInput.ReleaseAll();
            }
            btnAddCustomColor.Enabled = false;
            UpdateCaptureState();
            try
            {
                var colors=await ReadPreparedThemeColorsAsync(this);
                if(!colors.HasValue) return;
                Color shortPressColor=colors.Value.Short,longPressColor=colors.Value.Long;

                using (var dialog = new ThemeNameDialog(shortPressColor, longPressColor))
                {
                    DialogResult dialogResult = dialog.ShowDialog(this);
                    if (dialogResult != DialogResult.OK)
                        return;

                    if (dialog.ThemeName.Length == 0)
                    {
                        MessageBox.Show(
                            this,
                            UiText.T("Введите название темы."),
                            UiText.T("Название не задано"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        ColorPaletteStyle newStyle = ColorPaletteCatalog.AddCustomStyle(
                            dialog.ThemeName,
                            shortPressColor,
                            longPressColor,
                            dialog.FlashMode);
                        comboStyle.Items.Add(newStyle);
                        comboStyle.SelectedItem = newStyle;
                    }
                    catch (Exception exception)
                    {
                        MessageBox.Show(
                            this,
                            UiText.T(exception.Message),
                            UiText.T("Не удалось добавить тему"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception) when(lifetimeCancellation.IsCancellationRequested) { }
            catch (Exception exception)
            {
                MessageBox.Show(
                    this,
                    UiText.T(exception.Message),
                    UiText.T("Не удалось считать цвета"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                lock (inputProcessingSync)
                    suppressKeyInput = 0;
                if (!IsDisposed && !Disposing)
                    btnAddCustomColor.Enabled = true;
                Interlocked.Exchange(ref customColorSamplingInProgress, 0);
                if (!IsDisposed && !Disposing)
                    UpdateCaptureState();
            }
        }

        private void BtnShow_Click(object sender, EventArgs e)
        {
            if (!preview)
            {
                btnShow.Text = UiText.T("Зоны");
                demonstratPanel.Visible = true;
                previewPlaceholder.Visible = false;
                preview = true;
                for (int i = 0; i < 6; i++)
                {
                    AreaAdd();
                }
            }
            else if (preview)
            {
                btnShow.Text = UiText.T("Зоны");
                demonstratPanel.Visible = false;
                previewPlaceholder.Visible = false;
                preview = false;
                foreach (var area in captureAreas)
                {
                    demonstratPanel.Controls.Remove(area.Container);
                    area.Dispose();
                }
                captureAreas.Clear();
            }
            UpdateOverlayLayout();
        }

        private void DomainUpDownMode_SelectedItemChanged(object sender, EventArgs e)
        {
            RecalculateCaptureRegions();
            if (screenCapture.IsRunning)
            {
                try
                {
                    StartGpuCapture();
                }
                catch (Exception exception)
                {
                    screenCapture.Stop();
                    keyInput.ReleaseAll();
                    UpdateCaptureState();
                    OverlayMessageDialog.ShowError(this,UiText.T("Не удалось изменить захват"),exception);
                }
            }
        }

        private void RecalculateCaptureRegions()
        {
            if(domainUpDownMode.SelectedIndex<0) return;
            if(SelectedGameWindow is WindowCaptureTarget window)
            {
                if(!window.IsAvailable){captureGeometry.Text=UiText.T("Окно игры закрыто");return;}
                if(WindowCaptureGeometry.IsIconic(window.Handle)){captureGeometry.Text=UiText.T("Игра свёрнута · разверните окно");return;}
            }
            Rectangle bounds;
            try {bounds=screenBounds;}
            catch(InvalidOperationException){captureGeometry.Text=UiText.T("Игровая область пока недоступна");return;}
            if(bounds.Width<=0 || bounds.Height<=0){captureGeometry.Text=UiText.T("Игровая область пока недоступна");return;}
            int side = CaptureLayout.GetAreaSize(bounds.Size);
            int mode = domainUpDownMode.SelectedIndex;
            if (mode < 0) return;
            if (captureGeometry != null)
                captureGeometry.Text = bounds.Width / (double)bounds.Height < 4.0 / 3 - 0.001
                    ? UiText.T("Пропорции уже 4:3 пока не проверены")
                    : UiText.F("{0} × {1} · 6 зон по {2} px","{0} × {1} · 6 zones of {2} px",bounds.Width,bounds.Height,side);
        }

        private void ColorToleranceUpDown_ValueChanged(object sender, EventArgs e)
        {
            colorTolerance = Convert.ToInt32(colorToleranceUpDown.Value);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            lifetimeCancellation.Cancel();
            screenCapture.FrameCaptured -= ScreenCapture_FrameCaptured;
            screenCapture.CaptureFailed -= ScreenCapture_CaptureFailed;
            screenCapture.WindowClosed -= ScreenCapture_WindowClosed;
            screenCapture.CaptureInterrupted -= ScreenCapture_Interrupted;
            screenCapture.Dispose();
            keyInput?.Dispose();
            lifetimeCancellation.Dispose();
            base.OnFormClosed(e);
        }
    }

}
