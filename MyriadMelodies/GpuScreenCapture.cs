using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Foundation.Metadata;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using WinRT;
using static Vortice.Direct3D11.D3D11;
using Box = Vortice.Mathematics.Box;

namespace MyriadMelodies
{
    /// <summary>
    /// Захватывает области монитора или клиентской части окна через Windows Graphics Capture.
    /// Экранный кадр и анализ пикселей остаются на GPU; в системную память читаются
    /// только шесть готовых RGB-результатов, без Bitmap-захвата.
    /// </summary>
    public sealed class GpuScreenCapture : IDisposable
    {
        private readonly object sync = new object();
        private readonly object stateSync = new object();
        private ID3D11Device device;
        private ID3D11DeviceContext context;
        private Direct3D11CaptureFramePool framePool;
        private GraphicsCaptureSession session;
        private GraphicsCaptureItem item;
        private GpuColorAnalyzer colorAnalyzer;
        private IDXGISwapChain1[] previewSwapChains = Array.Empty<IDXGISwapChain1>();
        private IntPtr[] previewHandles = Array.Empty<IntPtr>();
        private Color[] analyzedColors = Array.Empty<Color>();
        private Rectangle[] regionsInMonitor = Array.Empty<Rectangle>();
        private Rectangle[] screenRegions = Array.Empty<Rectangle>();
        private SynchronizationContext ownerContext;
        private int generation;
        private int recoveryPending;
        private int recoveryAttempts;
        private int frameProcessing;
        private long lastFrameTimestamp;
        private long lastPreviewFrameTimestamp;
        private TimeSpan minimumFrameInterval = TimeSpan.FromMilliseconds(8);
        private TimeSpan minimumPreviewFrameInterval = TimeSpan.FromMilliseconds(60);
        private long minimumFrameIntervalTicks = ToStopwatchTicks(TimeSpan.FromMilliseconds(8));
        private long minimumPreviewFrameIntervalTicks = ToStopwatchTicks(TimeSpan.FromMilliseconds(60));
        private bool disposed;
        private IntPtr targetWindow;
        private Size capturedSize;
        private int captureActive;
        private int recovering;
        private IDirect3DDevice captureDevice;
        private long lastAnalysisTicks,lastReadbackTicks;
        private int deviceCreationCount;

        public bool IsRunning => Volatile.Read(ref captureActive)!=0;
        public bool IsRecovering => Volatile.Read(ref recovering)!=0;
        public int SessionId => Volatile.Read(ref generation);
        public int DeviceCreationCount => Volatile.Read(ref deviceCreationCount);
        public double LastAnalysisMilliseconds => Volatile.Read(ref lastAnalysisTicks)*1000.0/Stopwatch.Frequency;
        public double LastReadbackMilliseconds => Volatile.Read(ref lastReadbackTicks)*1000.0/Stopwatch.Frequency;
        public bool AutomaticRecovery { get; set; } = true;
        // 8 мс не отбрасывают кадры на 60 Гц, но ограничивают лишнюю обработку
        // на мониторах с высокой частотой обновления примерно до 72–120 кадров/с.
        public TimeSpan MinimumFrameInterval
        {
            get { return minimumFrameInterval; }
            set
            {
                minimumFrameInterval = value;
                Volatile.Write(ref minimumFrameIntervalTicks, ToStopwatchTicks(value));
            }
        }

        // Предпросмотр не участвует в распознавании, поэтому 15–17 FPS достаточно.
        public TimeSpan MinimumPreviewFrameInterval
        {
            get { return minimumPreviewFrameInterval; }
            set
            {
                minimumPreviewFrameInterval = value;
                Volatile.Write(ref minimumPreviewFrameIntervalTicks, ToStopwatchTicks(value));
            }
        }

        public string AdapterName { get; private set; } = string.Empty;

        public event EventHandler<GpuFrameCapturedEventArgs> FrameCaptured;
        public event EventHandler<CaptureFailedEventArgs> CaptureFailed;
        public event EventHandler WindowClosed;
        public event EventHandler CaptureInterrupted;

        /// <summary>
        /// Начинает захват. Все области задаются в физических экранных координатах
        /// и должны целиком находиться на одном мониторе.
        /// </summary>
        public void Start(
            IReadOnlyList<Rectangle> regions,
            IReadOnlyList<IntPtr> previewOutputHandles = null)
            => StartCapture(regions,previewOutputHandles,IntPtr.Zero);

        /// <summary>Области в физических координатах клиентской части окна, от (0,0).</summary>
        public void StartWindow(IntPtr window,IReadOnlyList<Rectangle> regions,IReadOnlyList<IntPtr> previewOutputHandles = null)
        {
            if(window==IntPtr.Zero || !WindowCaptureGeometry.IsWindow(window)) throw new ArgumentException(UiText.T("Окно игры не найдено."),nameof(window));
            if(WindowCaptureGeometry.IsIconic(window)) throw new InvalidOperationException(UiText.T("Разверните окно игры перед запуском захвата."));
            StartCapture(regions,previewOutputHandles,window);
        }

        private void StartCapture(IReadOnlyList<Rectangle> regions,IReadOnlyList<IntPtr> previewOutputHandles,IntPtr window)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(GpuScreenCapture));
            if (!GraphicsCaptureSession.IsSupported())
                throw new PlatformNotSupportedException(UiText.T("Windows Graphics Capture не поддерживается этой системой."));
            if (regions == null || regions.Count == 0)
                throw new ArgumentException(UiText.T("Нужна хотя бы одна область захвата."), nameof(regions));

            Rectangle[] requestedRegions = new Rectangle[regions.Count];
            for (int i = 0; i < regions.Count; i++)
            {
                if (regions[i].Width <= 0 || regions[i].Height <= 0)
                    throw new ArgumentOutOfRangeException(nameof(regions), UiText.T("Размеры областей должны быть положительными."));
                requestedRegions[i] = regions[i];
            }

            if (previewOutputHandles != null &&
                previewOutputHandles.Count != 0 &&
                previewOutputHandles.Count != requestedRegions.Length)
            {
                throw new ArgumentException(
                    UiText.T("Количество preview-контролов должно совпадать с количеством областей."),
                    nameof(previewOutputHandles));
            }

            Screen monitor = window==IntPtr.Zero ? Screen.FromRectangle(requestedRegions[0]) : Screen.FromHandle(window);
            Rectangle allowed = window==IntPtr.Zero ? monitor.Bounds : new Rectangle(Point.Empty,WindowCaptureGeometry.Read(window).ClientBounds.Size);
            foreach (Rectangle region in requestedRegions)
            {
                if (!allowed.Contains(region))
                    throw new ArgumentException(window==IntPtr.Zero ? UiText.T("Все области должны целиком находиться на одном мониторе.") : UiText.T("Области должны находиться внутри игрового окна."), nameof(regions));
            }

            bool reuseDevice=window!=IntPtr.Zero && targetWindow==window && device!=null && device.DeviceRemovedReason.Success;
            StopCore(!reuseDevice);
            targetWindow = window;
            screenRegions = requestedRegions;
            previewHandles = CopyHandles(previewOutputHandles);
            ownerContext = SynchronizationContext.Current;
            recoveryAttempts = 0;
            lastFrameTimestamp = 0;
            lastPreviewFrameTimestamp = 0;
            int currentGeneration = Interlocked.Increment(ref generation);

            try
            {
                StartCore(monitor);
            }
            catch
            {
                CleanupResources();
                Volatile.Write(ref captureActive,0);
                if (currentGeneration == generation)
                    Interlocked.Increment(ref generation);
                throw;
            }
        }

        private void StartCore(Screen monitor)
        {
            WindowCaptureGeometry geometry=default;
            Rectangle clientInFrame=default;
            if(targetWindow!=IntPtr.Zero)
            {
                if(WindowCaptureGeometry.IsIconic(targetWindow)) throw new InvalidOperationException(UiText.T("Окно игры свёрнуто."));
                geometry=WindowCaptureGeometry.Read(targetWindow);
            }
            IntPtr monitorHandle = CaptureItemInterop.GetMonitorHandle(monitor);
            if(device==null) CreateD3DDevice(monitorHandle);
            item = targetWindow==IntPtr.Zero ? CaptureItemInterop.CreateForMonitor(monitor) : CaptureItemInterop.CreateForWindow(targetWindow);
            capturedSize=new Size(item.Size.Width,item.Size.Height);
            if(targetWindow!=IntPtr.Zero) clientInFrame=geometry.ClientInCapture(capturedSize);
            regionsInMonitor = new Rectangle[screenRegions.Length];
            for (int i = 0; i < screenRegions.Length; i++)
            {
                Rectangle region = screenRegions[i];
                regionsInMonitor[i] = new Rectangle(
                    targetWindow==IntPtr.Zero ? region.X - monitor.Bounds.X : region.X+clientInFrame.X,
                    targetWindow==IntPtr.Zero ? region.Y - monitor.Bounds.Y : region.Y+clientInFrame.Y,
                    region.Width,
                    region.Height);
            }

            CreateGpuProcessingResources();
            CreatePreviewSwapChains();
            item.Closed += OnCaptureItemClosed;

            captureDevice ??= CreateWinRtDevice(device);
            framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                captureDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                item.Size);
            framePool.FrameArrived += OnFrameArrived;

            session = framePool.CreateCaptureSession(item);
            session.IsCursorCaptureEnabled = false;
            DisableSystemCaptureBorder(session);
            Volatile.Write(ref captureActive,1);
            session.StartCapture();
        }

        private static unsafe void DisableSystemCaptureBorder(GraphicsCaptureSession captureSession)
        {
            const string sessionType = "Windows.Graphics.Capture.GraphicsCaptureSession";
            if (!ApiInformation.IsPropertyPresent(sessionType, "IsBorderRequired"))
                return;

            // IGraphicsCaptureSession3 содержит IsBorderRequired. Целевой TFM 19041
            // не публикует это свойство в C#, поэтому обращаемся к доступному в ОС ABI.
            Guid session3Iid = new Guid("F2CDD966-22AE-5EA1-9596-3A289344C3BE");
            // GraphicsCaptureSession — runtime class, а не интерфейс. Попытка
            // маршалить её через MarshalInterface запрашивала неверный IID и
            // приводила к InvalidCastException ещё до StartCapture().
            using (IObjectReference sessionReference =
                MarshalInspectable<GraphicsCaptureSession>.CreateMarshaler(captureSession))
            {
                int queryResult = Marshal.QueryInterface(
                    sessionReference.ThisPtr,
                    in session3Iid,
                    out IntPtr session3Pointer);
                if (queryResult < 0)
                {
                    Debug.WriteLine($"Capture border interface is unavailable: 0x{queryResult:X8}");
                    return;
                }

                try
                {
                    IntPtr* vtable = *(IntPtr**)session3Pointer;
                    var setIsBorderRequired =
                        (delegate* unmanaged[Stdcall]<IntPtr, byte, int>)vtable[7];
                    // Оформление рамки необязательно. Если Windows или политика
                    // запрещает его менять, продолжаем захват с системной рамкой.
                    int borderResult = setIsBorderRequired(session3Pointer, 0);
                    if (borderResult < 0)
                        Debug.WriteLine($"Capture border preference was rejected: 0x{borderResult:X8}");
                }
                finally
                {
                    Marshal.Release(session3Pointer);
                }
            }
        }

        public void Stop()
            => StopCore(true);

        private void StopCore(bool releaseDevice)
        {
            bool wasActive;
            lock(stateSync)
            {
                wasActive=Interlocked.Exchange(ref captureActive,0)!=0 || IsRecovering || session!=null;
                Interlocked.Increment(ref generation);
                Volatile.Write(ref recovering,0);
                Interlocked.Exchange(ref recoveryPending,0);
            }
            if(wasActive) CaptureInterrupted?.Invoke(this,EventArgs.Empty);
            CleanupResources(releaseDevice);
        }

        private void CleanupResources(bool releaseDevice=true)
        {
            lock (sync)
            {
                if (framePool != null)
                    framePool.FrameArrived -= OnFrameArrived;
                if (item != null)
                    item.Closed -= OnCaptureItemClosed;

                if (session != null)
                    session.Dispose();
                if (framePool != null)
                    framePool.Dispose();
                item = null;
                session = null;
                framePool = null;

                if (colorAnalyzer != null)
                    colorAnalyzer.Dispose();
                colorAnalyzer = null;

                foreach (IDXGISwapChain1 swapChain in previewSwapChains)
                {
                    if (swapChain != null)
                        swapChain.Dispose();
                }
                previewSwapChains = Array.Empty<IDXGISwapChain1>();
                analyzedColors = Array.Empty<Color>();

                if(releaseDevice)
                {
                    (captureDevice as IDisposable)?.Dispose();captureDevice=null;
                    context?.Dispose();device?.Dispose();
                    context=null;device=null;
                }
            }
        }

        private void CreateD3DDevice(IntPtr monitorHandle)
        {
            using (IDXGIFactory1 factory = Vortice.DXGI.DXGI.CreateDXGIFactory1<IDXGIFactory1>())
            {
                for (uint adapterIndex = 0; ; adapterIndex++)
                {
                    IDXGIAdapter1 adapter;
                    if (factory.EnumAdapters1(adapterIndex, out adapter).Failure)
                        break;

                    using (adapter)
                    {
                        for (uint outputIndex = 0; ; outputIndex++)
                        {
                            IDXGIOutput output;
                            if (adapter.EnumOutputs(outputIndex, out output).Failure)
                                break;

                            using (output)
                            {
                                if (output.Description.Monitor != monitorHandle)
                                    continue;

                                CreateD3DDevice(adapter, DriverType.Unknown);
                                AdapterName = adapter.Description1.Description;
                                return;
                            }
                        }
                    }
                }
            }

            CreateD3DDevice(null, DriverType.Hardware);
            using (IDXGIDevice dxgiDevice = device.QueryInterface<IDXGIDevice>())
            using (IDXGIAdapter fallbackAdapter = dxgiDevice.GetAdapter())
                AdapterName = fallbackAdapter.Description.Description;
        }

        private void CreateD3DDevice(IDXGIAdapter adapter, DriverType driverType)
        {
            D3D11CreateDevice(
                adapter,
                driverType,
                DeviceCreationFlags.BgraSupport,
                new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0 },
                out device,
                out _,
                out context).CheckError();
            Interlocked.Increment(ref deviceCreationCount);
        }

        private void CreateGpuProcessingResources()
        {
            colorAnalyzer = new GpuColorAnalyzer(device, context, regionsInMonitor);
            analyzedColors = new Color[regionsInMonitor.Length];
        }

        private void CreatePreviewSwapChains()
        {
            if (previewHandles.Length == 0)
            {
                previewSwapChains = Array.Empty<IDXGISwapChain1>();
                return;
            }

            previewSwapChains = new IDXGISwapChain1[previewHandles.Length];
            using (IDXGIDevice dxgiDevice = device.QueryInterface<IDXGIDevice>())
            using (IDXGIAdapter adapter = dxgiDevice.GetAdapter())
            using (IDXGIFactory2 factory = adapter.GetParent<IDXGIFactory2>())
            {
                for (int i = 0; i < previewHandles.Length; i++)
                {
                    Rectangle region = regionsInMonitor[i];
                    var description = new SwapChainDescription1
                    {
                        Width = (uint)region.Width,
                        Height = (uint)region.Height,
                        Format = Format.B8G8R8A8_UNorm,
                        SampleDescription = new SampleDescription(1, 0),
                        BufferUsage = Usage.RenderTargetOutput,
                        BufferCount = 2,
                        Scaling = Scaling.Stretch,
                        SwapEffect = SwapEffect.FlipDiscard,
                        AlphaMode = AlphaMode.Ignore
                    };

                    previewSwapChains[i] = factory.CreateSwapChainForHwnd(
                        device,
                        previewHandles[i],
                        description);
                    factory.MakeWindowAssociation(
                        previewHandles[i],
                        WindowAssociationFlags.IgnoreAltEnter);
                }
            }
        }

        private static IDirect3DDevice CreateWinRtDevice(ID3D11Device d3dDevice)
        {
            using (IDXGIDevice dxgiDevice = d3dDevice.QueryInterface<IDXGIDevice>())
            {
                NativeMethods.CreateDirect3D11DeviceFromDXGIDevice(
                    dxgiDevice.NativePointer,
                    out IntPtr inspectable).ThrowIfFailed();
                try
                {
                    return MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
                }
                finally
                {
                    Marshal.Release(inspectable);
                }
            }
        }

        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            if (Interlocked.Exchange(ref frameProcessing, 1) != 0)
            {
                Exception discardFailure=null;int discardGeneration=SessionId;
                lock(sync)
                {
                    if(!ReferenceEquals(sender,framePool) || !IsRunning) return;
                    discardGeneration=SessionId;
                    try {using var skipped=sender.TryGetNextFrame();}
                    catch(Exception exception){discardFailure=AddDeviceRemovedReason(exception);}
                }
                if(discardFailure!=null) QueueRecoveryForSession(discardFailure,discardGeneration);
                return;
            }

            int frameGeneration=SessionId;
            GpuFrameCapturedEventArgs snapshot=null;
            try
            {
                try
                {
                    lock (sync)
                    {
                        if (disposed || !IsRunning || !ReferenceEquals(sender,framePool) || context == null || colorAnalyzer == null)
                            return;
                        frameGeneration=SessionId;

                        using (Direct3D11CaptureFrame frame = sender.TryGetNextFrame())
                        {
                            if (frame == null || !ShouldProcessFrame())
                                return;
                            if(frame.ContentSize.Width!=capturedSize.Width || frame.ContentSize.Height!=capturedSize.Height) return;

                            if(targetWindow!=IntPtr.Zero &&
                                (!WindowCaptureGeometry.IsWindow(targetWindow) || WindowCaptureGeometry.IsIconic(targetWindow))) return;

                            using (ID3D11Texture2D source = Direct3DInterop.GetTexture(frame.Surface))
                            {
                                if (!RegionsFitFrame(frame.ContentSize.Width, frame.ContentSize.Height))
                                    return;

                                long analysisStarted=Stopwatch.GetTimestamp();
                                colorAnalyzer.Analyze(source, analyzedColors);
                                Volatile.Write(ref lastAnalysisTicks,Stopwatch.GetTimestamp()-analysisStarted);
                                Volatile.Write(ref lastReadbackTicks,colorAnalyzer.LastReadbackTicks);
                                RenderPreviews(source);
                                snapshot=new GpuFrameCapturedEventArgs((Color[])analyzedColors.Clone(),frameGeneration);
                            }
                        }
                        Interlocked.Exchange(ref recoveryAttempts,0);
                    }
                }
                catch (Exception exception)
                {
                    QueueRecoveryForSession(AddDeviceRemovedReason(exception),frameGeneration);
                    return;
                }

                EventHandler<GpuFrameCapturedEventArgs> handler = FrameCaptured;
                if (handler != null && snapshot != null && IsRunning && frameGeneration==SessionId)
                    handler(this, snapshot);
            }
            finally
            {
                Interlocked.Exchange(ref frameProcessing, 0);
            }
        }

        private bool ShouldProcessFrame()
        {
            long minimumTicks = Volatile.Read(ref minimumFrameIntervalTicks);
            if (minimumTicks <= 0)
                return true;

            long now = Stopwatch.GetTimestamp();
            if (lastFrameTimestamp != 0 && now - lastFrameTimestamp < minimumTicks)
                return false;
            lastFrameTimestamp = now;
            return true;
        }

        private bool RegionsFitFrame(int frameWidth, int frameHeight)
        {
            foreach (Rectangle region in regionsInMonitor)
            {
                if (region.Left < 0 || region.Top < 0 ||
                    region.Right > frameWidth || region.Bottom > frameHeight)
                    return false;
            }
            return true;
        }

        private void RenderPreviews(ID3D11Texture2D source)
        {
            if (previewSwapChains.Length == 0 || !ShouldRenderPreviewFrame())
                return;

            for (int i = 0; i < previewSwapChains.Length; i++)
            {
                Rectangle region = regionsInMonitor[i];
                using (ID3D11Texture2D destination =
                    previewSwapChains[i].GetBuffer<ID3D11Texture2D>(0))
                {
                    var sourceBox = new Box(
                        region.Left,
                        region.Top,
                        0,
                        region.Right,
                        region.Bottom,
                        1);
                    context.CopySubresourceRegion(
                        destination,
                        0,
                        0,
                        0,
                        0,
                        source,
                        0,
                        sourceBox);
                }
                previewSwapChains[i].Present(0, PresentFlags.DoNotWait);
            }
        }

        private bool ShouldRenderPreviewFrame()
        {
            long minimumTicks = Volatile.Read(ref minimumPreviewFrameIntervalTicks);
            if (minimumTicks <= 0)
                return true;

            long now = Stopwatch.GetTimestamp();
            if (lastPreviewFrameTimestamp != 0 &&
                now - lastPreviewFrameTimestamp < minimumTicks)
            {
                return false;
            }

            lastPreviewFrameTimestamp = now;
            return true;
        }

        private static IntPtr[] CopyHandles(IReadOnlyList<IntPtr> handles)
        {
            if (handles == null || handles.Count == 0)
                return Array.Empty<IntPtr>();

            var result = new IntPtr[handles.Count];
            for (int i = 0; i < result.Length; i++)
                result[i] = handles[i];
            return result;
        }

        private static long ToStopwatchTicks(TimeSpan interval)
        {
            if (interval <= TimeSpan.Zero)
                return 0;

            double ticks = interval.TotalSeconds * Stopwatch.Frequency;
            if (ticks >= long.MaxValue)
                return long.MaxValue;
            return Math.Max(1L, (long)ticks);
        }

        private void OnCaptureItemClosed(GraphicsCaptureItem sender, object args)
        {
            if(sender!=null && !ReferenceEquals(sender,item)) return;
            if(targetWindow!=IntPtr.Zero)
            {
                StopClosedWindow(SessionId,sender);
                return;
            }
            QueueRecovery(new InvalidOperationException(targetWindow==IntPtr.Zero ? UiText.T("Захватываемый монитор был отключён или изменён.") : UiText.T("Захватываемое окно было закрыто или изменено.")));
        }

        private void StopClosedWindow(int expectedGeneration,GraphicsCaptureItem closedItem)
        {
            SendOrPostCallback stop=state=>
            {
                if(disposed || expectedGeneration!=SessionId) return;
                if(closedItem!=null && !ReferenceEquals(closedItem,item)) return;
                Stop();WindowClosed?.Invoke(this,EventArgs.Empty);
            };
            if(ownerContext!=null) ownerContext.Post(stop,null);
            else ThreadPool.QueueUserWorkItem(state=>stop(state));
        }

        private Exception AddDeviceRemovedReason(Exception exception)
        {
            lock(sync)
            {
                if (device == null) return exception;
                var reason = device.DeviceRemovedReason;
                return reason.Failure
                    ? new InvalidOperationException(UiText.F("Устройство Direct3D потеряно: {0}","Direct3D device lost: {0}",reason), exception)
                    : exception;
            }
        }

        private void QueueRecovery(Exception exception)
            => QueueRecoveryForSession(exception,SessionId);

        private void QueueRecoveryForSession(Exception exception,int expectedGeneration)
        {
            if(expectedGeneration!=SessionId) return;
            if(targetWindow!=IntPtr.Zero && !WindowCaptureGeometry.IsWindow(targetWindow))
            {StopClosedWindow(expectedGeneration,null);return;}
            int currentGeneration;
            lock(stateSync)
            {
                if(disposed || expectedGeneration!=SessionId || Interlocked.CompareExchange(ref recoveryPending,1,0)!=0) return;
                Volatile.Write(ref captureActive,0);
                Volatile.Write(ref recovering,1);
                currentGeneration=Interlocked.Increment(ref generation);
            }
            CaptureInterrupted?.Invoke(this,EventArgs.Empty);
            int attempts=0;
            SendOrPostCallback recover=null;
            void PostRecovery()
            {
                if(disposed || currentGeneration!=SessionId) return;
                if(ownerContext!=null) ownerContext.Post(recover,null);
                else ThreadPool.QueueUserWorkItem(state=>recover(state));
            }
            void Fail(Exception failure)
            {
                CleanupResources();
                Volatile.Write(ref captureActive,0);
                Volatile.Write(ref recovering,0);
                Interlocked.Exchange(ref recoveryPending,0);
                CaptureFailed?.Invoke(this,new CaptureFailedEventArgs(failure));
            }
            recover=delegate
            {
                if(disposed || currentGeneration!=SessionId) return;
                if(!AutomaticRecovery){Fail(exception);return;}
                try
                {
                    attempts=Interlocked.Increment(ref recoveryAttempts);
                    if(attempts>3){Fail(new InvalidOperationException(UiText.T("Не удалось восстановить GPU-захват после трёх попыток."),exception));return;}
                    CleanupResources();
                    Screen monitor = targetWindow==IntPtr.Zero ? Screen.FromRectangle(screenRegions[0]) : Screen.FromHandle(targetWindow);
                    StartCore(monitor);
                    Volatile.Write(ref recovering,0);
                    Interlocked.Exchange(ref recoveryPending,0);
                }
                catch (Exception recoveryException)
                {
                    Volatile.Write(ref captureActive,0);
                    CleanupResources();
                    if(attempts>=3) Fail(new InvalidOperationException(UiText.T("Не удалось восстановить GPU-захват после трёх попыток."),recoveryException));
                    else _=Task.Delay(100).ContinueWith(_=>PostRecovery(),TaskScheduler.Default);
                }
            };
            PostRecovery();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            Stop();
            disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public sealed class GpuFrameCapturedEventArgs : EventArgs
    {
        internal GpuFrameCapturedEventArgs(Color[] colors) : this(colors,0) { }
        internal GpuFrameCapturedEventArgs(Color[] colors,int sessionId)
        {
            Colors = Array.AsReadOnly(colors);
            SessionId=sessionId;
        }

        public IReadOnlyList<Color> Colors { get; private set; }
        public int SessionId {get;}
    }

    public sealed class CaptureFailedEventArgs : EventArgs
    {
        public CaptureFailedEventArgs(Exception exception)
        {
            Exception = exception;
        }

        public Exception Exception { get; private set; }
    }

    internal static class NativeMethods
    {
        [DllImport("d3d11.dll")]
        public static extern int CreateDirect3D11DeviceFromDXGIDevice(
            IntPtr dxgiDevice,
            out IntPtr graphicsDevice);
    }

    internal static class Direct3DInterop
    {
        private static readonly Guid DxgiInterfaceAccessIid = new Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1");

        public static unsafe ID3D11Texture2D GetTexture(IDirect3DSurface surface)
        {
            using (IObjectReference surfaceReference = MarshalInterface<IDirect3DSurface>.CreateMarshaler(surface))
            {
                Guid accessIid = DxgiInterfaceAccessIid;
                Marshal.QueryInterface(surfaceReference.ThisPtr, in accessIid, out IntPtr accessPointer)
                    .ThrowIfFailed();

                try
                {
                    Guid textureIid = typeof(ID3D11Texture2D).GUID;
                    IntPtr texturePointer = IntPtr.Zero;
                    IntPtr* vtable = *(IntPtr**)accessPointer;
                    var getInterface = (delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)vtable[3];
                    int hresult = getInterface(accessPointer, &textureIid, &texturePointer);
                    hresult.ThrowIfFailed();
                    return new ID3D11Texture2D(texturePointer);
                }
                finally
                {
                    Marshal.Release(accessPointer);
                }
            }
        }
    }

    internal static class CaptureItemInterop
    {
        private static readonly Guid GraphicsCaptureItemIid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
        private static readonly Guid InteropIid = new Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");

        public static unsafe GraphicsCaptureItem CreateForMonitor(Screen monitor)
            => CreateItem(GetMonitorHandle(monitor),false);

        public static unsafe GraphicsCaptureItem CreateForWindow(IntPtr window)
            => CreateItem(window,true);

        private static unsafe GraphicsCaptureItem CreateItem(IntPtr handle,bool window)
        {
            IntPtr className = IntPtr.Zero;
            IntPtr factory = IntPtr.Zero;
            IntPtr captureItem = IntPtr.Zero;
            try
            {
                const string runtimeClass = "Windows.Graphics.Capture.GraphicsCaptureItem";
                WindowsCreateString(runtimeClass, runtimeClass.Length, out className).ThrowIfFailed();
                Guid interopIid = InteropIid;
                RoGetActivationFactory(className, ref interopIid, out factory).ThrowIfFailed();

                Guid itemIid = GraphicsCaptureItemIid;
                IntPtr* vtable = *(IntPtr**)factory;
                var createForMonitor =
                    (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Guid*, IntPtr*, int>)vtable[window?3:4];
                createForMonitor(factory, handle, &itemIid, &captureItem).ThrowIfFailed();
                return MarshalInterface<GraphicsCaptureItem>.FromAbi(captureItem);
            }
            finally
            {
                if (captureItem != IntPtr.Zero) Marshal.Release(captureItem);
                if (factory != IntPtr.Zero) Marshal.Release(factory);
                if (className != IntPtr.Zero) WindowsDeleteString(className);
            }
        }

        public static IntPtr GetMonitorHandle(Screen monitor)
        {
            return MonitorFromPoint(
                new NativePoint(monitor.Bounds.Left, monitor.Bounds.Top),
                2);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public NativePoint(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X;
            public int Y;
        }

        [DllImport("combase.dll")]
        private static extern int WindowsCreateString(
            [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
            int length,
            out IntPtr hstring);

        [DllImport("combase.dll")]
        private static extern int WindowsDeleteString(IntPtr hstring);

        [DllImport("combase.dll")]
        private static extern int RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);
    }

    internal static class HResultExtensions
    {
        public static void ThrowIfFailed(this int hresult)
        {
            Marshal.ThrowExceptionForHR(hresult);
        }
    }
}
