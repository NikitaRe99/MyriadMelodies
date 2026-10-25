using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace MyriadMelodies
{
    /// <summary>
    /// Вычисляет средний цвет круглых областей compute-шейдером. На CPU
    /// возвращаются только готовые RGB-значения — экранные пиксели не читаются.
    /// </summary>
    internal sealed class GpuColorAnalyzer : IDisposable
    {
        private const int MaximumRegionCount = 6;
        private const int ResultStride = 16;
        private static readonly Lazy<byte[]> ShaderBytecode = new Lazy<byte[]>(
            () => CompileShader(ComputeShaderSource, "CSMain", "cs_5_0"));

        [StructLayout(LayoutKind.Sequential)]
        private struct RegionConstants
        {
            public uint X;
            public uint Y;
            public uint Width;
            public uint Height;
        }

        private readonly ID3D11Device device;
        private readonly ID3D11DeviceContext context;
        private readonly int regionCount;
        private readonly ID3D11ComputeShader shader;
        private readonly ID3D11Buffer regionBuffer;
        private readonly ID3D11Buffer gpuResultBuffer;
        private readonly ID3D11UnorderedAccessView resultView;
        private readonly ID3D11Buffer readbackBuffer;
        private readonly Dictionary<IntPtr, ID3D11ShaderResourceView> sourceViews =
            new Dictionary<IntPtr, ID3D11ShaderResourceView>();
        private bool disposed;
        public long LastReadbackTicks {get;private set;}

        public GpuColorAnalyzer(
            ID3D11Device d3dDevice,
            ID3D11DeviceContext d3dContext,
            Rectangle[] regions)
        {
            if (regions == null || regions.Length == 0 || regions.Length > MaximumRegionCount)
                throw new ArgumentOutOfRangeException(nameof(regions));

            device = d3dDevice;
            context = d3dContext;
            regionCount = regions.Length;

            try
            {
                shader = device.CreateComputeShader(ShaderBytecode.Value, null);

                var constants = new RegionConstants[MaximumRegionCount];
                for (int i = 0; i < regions.Length; i++)
                {
                    constants[i] = new RegionConstants
                    {
                        X = (uint)regions[i].X,
                        Y = (uint)regions[i].Y,
                        Width = (uint)regions[i].Width,
                        Height = (uint)regions[i].Height
                    };
                }

                regionBuffer = device.CreateBuffer(
                    constants,
                    BindFlags.ConstantBuffer,
                    ResourceUsage.Immutable,
                    CpuAccessFlags.None,
                    ResourceOptionFlags.None,
                    (uint)(MaximumRegionCount * Marshal.SizeOf<RegionConstants>()),
                    0);

                uint resultSize = (uint)(MaximumRegionCount * ResultStride);
                gpuResultBuffer = device.CreateBuffer(
                    resultSize,
                    BindFlags.UnorderedAccess,
                    ResourceUsage.Default,
                    CpuAccessFlags.None,
                    ResourceOptionFlags.BufferStructured,
                    ResultStride);

                var viewDescription = new UnorderedAccessViewDescription(
                    gpuResultBuffer,
                    Format.Unknown,
                    0,
                    MaximumRegionCount,
                    BufferUnorderedAccessViewFlags.None);
                resultView = device.CreateUnorderedAccessView(gpuResultBuffer, viewDescription);

                readbackBuffer = device.CreateBuffer(
                    resultSize,
                    BindFlags.None,
                    ResourceUsage.Staging,
                    CpuAccessFlags.Read,
                    ResourceOptionFlags.None,
                    0);
            }
            catch
            {
                // При ошибке конструктора освобождаем уже созданные GPU-ресурсы.
                Dispose();
                throw;
            }
        }

        public void Analyze(ID3D11Texture2D source, Color[] colors)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(GpuColorAnalyzer));
            if (colors == null || colors.Length < regionCount)
                throw new ArgumentException(UiText.T("Недостаточно элементов для результатов."), nameof(colors));

            ID3D11ShaderResourceView sourceView = GetSourceView(source);
            context.CSSetShader(shader);
            context.CSSetConstantBuffer(0, regionBuffer);
            context.CSSetShaderResource(0, sourceView);
            context.CSSetUnorderedAccessView(0, resultView, 0);
            context.Dispatch((uint)regionCount, 1, 1);

            // Ресурсы отвязываются до копирования результата, чтобы драйвер
            // не держал UAV в состоянии записи.
            context.CSSetUnorderedAccessView(0, null, 0);
            context.CSSetShaderResource(0, null);
            context.CSSetConstantBuffer(0, null);
            context.CSSetShader(null);

            context.CopyResource(readbackBuffer, gpuResultBuffer);
            // Для управления клавишами нужен результат именно текущего кадра.
            // Map синхронизирует только крошечный буфер из шести uint4 (96 байт),
            // зато не добавляет задержку в один экранный кадр.
            ReadColors(readbackBuffer, colors);
        }

        private ID3D11ShaderResourceView GetSourceView(ID3D11Texture2D source)
        {
            IntPtr resourcePointer = source.NativePointer;
            if (sourceViews.TryGetValue(resourcePointer, out ID3D11ShaderResourceView view))
                return view;

            // FramePool создан с двумя буферами. Ограничение защищает от роста
            // кэша, если система когда-либо пересоздаст поверхности захвата.
            if (sourceViews.Count >= 4)
            {
                foreach (ID3D11ShaderResourceView cachedView in sourceViews.Values)
                    cachedView.Dispose();
                sourceViews.Clear();
            }

            view = device.CreateShaderResourceView(source, null);
            sourceViews.Add(resourcePointer, view);
            return view;
        }

        private unsafe void ReadColors(ID3D11Buffer readbackBuffer, Color[] colors)
        {
            long started=Stopwatch.GetTimestamp();
            MappedSubresource mapped = context.Map(
                readbackBuffer,
                0,
                MapMode.Read,
                Vortice.Direct3D11.MapFlags.None);
            LastReadbackTicks=Stopwatch.GetTimestamp()-started;
            try
            {
                uint* values = (uint*)mapped.DataPointer;
                for (int i = 0; i < regionCount; i++)
                {
                    int offset = i * 4;
                    colors[i] = Color.FromArgb(
                        (int)Math.Min(values[offset], 255u),
                        (int)Math.Min(values[offset + 1], 255u),
                        (int)Math.Min(values[offset + 2], 255u));
                }
            }
            finally
            {
                context.Unmap(readbackBuffer, 0);
            }
        }

        private static unsafe byte[] CompileShader(string source, string entryPoint, string target)
        {
            IntPtr codeBlob = IntPtr.Zero;
            IntPtr errorBlob = IntPtr.Zero;
            try
            {
                int result = NativeShaderCompiler.D3DCompile(
                    source,
                    (UIntPtr)Encoding.ASCII.GetByteCount(source),
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    entryPoint,
                    target,
                    0,
                    0,
                    out codeBlob,
                    out errorBlob);

                if (result < 0)
                {
                    string message = errorBlob == IntPtr.Zero
                        ? UiText.T("Не удалось скомпилировать compute shader.")
                        : ReadBlobString(errorBlob);
                    throw new InvalidOperationException(message, Marshal.GetExceptionForHR(result));
                }

                return ReadBlobBytes(codeBlob);
            }
            finally
            {
                if (errorBlob != IntPtr.Zero)
                    Marshal.Release(errorBlob);
                if (codeBlob != IntPtr.Zero)
                    Marshal.Release(codeBlob);
            }
        }

        private static unsafe byte[] ReadBlobBytes(IntPtr blob)
        {
            IntPtr* vtable = *(IntPtr**)blob;
            var getBufferPointer = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr>)vtable[3];
            var getBufferSize = (delegate* unmanaged[Stdcall]<IntPtr, UIntPtr>)vtable[4];
            IntPtr pointer = getBufferPointer(blob);
            int length = checked((int)getBufferSize(blob));
            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return bytes;
        }

        private static unsafe string ReadBlobString(IntPtr blob)
        {
            IntPtr* vtable = *(IntPtr**)blob;
            var getBufferPointer = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr>)vtable[3];
            var getBufferSize = (delegate* unmanaged[Stdcall]<IntPtr, UIntPtr>)vtable[4];
            IntPtr pointer = getBufferPointer(blob);
            int length = checked((int)getBufferSize(blob));
            return Marshal.PtrToStringAnsi(pointer, length) ?? UiText.T("Ошибка компиляции compute shader.");
        }

        public void Dispose()
        {
            if (disposed)
                return;
            foreach (ID3D11ShaderResourceView sourceView in sourceViews.Values)
                sourceView.Dispose();
            sourceViews.Clear();
            readbackBuffer?.Dispose();
            resultView?.Dispose();
            gpuResultBuffer?.Dispose();
            regionBuffer?.Dispose();
            shader?.Dispose();
            disposed = true;
        }

        private const string ComputeShaderSource = @"
Texture2D<float4> FrameTexture : register(t0);
RWStructuredBuffer<uint4> AverageColors : register(u0);

cbuffer RegionBuffer : register(b0)
{
    uint4 Regions[6];
};

groupshared uint SharedR[256];
groupshared uint SharedG[256];
groupshared uint SharedB[256];
groupshared uint SharedCount[256];

[numthreads(256, 1, 1)]
void CSMain(uint3 groupId : SV_GroupID, uint groupIndex : SV_GroupIndex)
{
    uint regionIndex = groupId.x;
    uint4 region = Regions[regionIndex];
    uint pixelCount = region.z * region.w;
    float2 center = float2(region.z, region.w) * 0.5f;
    float radius = min(center.x, center.y);
    float radiusSquared = radius * radius;

    uint localR = 0;
    uint localG = 0;
    uint localB = 0;
    uint localCount = 0;

    for (uint pixelIndex = groupIndex; pixelIndex < pixelCount; pixelIndex += 256)
    {
        uint x = pixelIndex % region.z;
        uint y = pixelIndex / region.z;
        float2 delta = float2(x, y) - center;
        if (dot(delta, delta) <= radiusSquared)
        {
            float4 color = FrameTexture.Load(int3(region.x + x, region.y + y, 0));
            localR += (uint)round(saturate(color.r) * 255.0f);
            localG += (uint)round(saturate(color.g) * 255.0f);
            localB += (uint)round(saturate(color.b) * 255.0f);
            localCount++;
        }
    }

    SharedR[groupIndex] = localR;
    SharedG[groupIndex] = localG;
    SharedB[groupIndex] = localB;
    SharedCount[groupIndex] = localCount;
    GroupMemoryBarrierWithGroupSync();

    for (uint stride = 128; stride > 0; stride >>= 1)
    {
        if (groupIndex < stride)
        {
            SharedR[groupIndex] += SharedR[groupIndex + stride];
            SharedG[groupIndex] += SharedG[groupIndex + stride];
            SharedB[groupIndex] += SharedB[groupIndex + stride];
            SharedCount[groupIndex] += SharedCount[groupIndex + stride];
        }
        GroupMemoryBarrierWithGroupSync();
    }

    if (groupIndex == 0)
    {
        uint count = SharedCount[0];
        AverageColors[regionIndex] = count == 0
            ? uint4(0, 0, 0, 0)
            : uint4(SharedR[0] / count, SharedG[0] / count, SharedB[0] / count, count);
    }
}";
    }

    internal static class NativeShaderCompiler
    {
        [DllImport("d3dcompiler_47.dll", CharSet = CharSet.Ansi)]
        internal static extern int D3DCompile(
            [MarshalAs(UnmanagedType.LPStr)] string sourceData,
            UIntPtr sourceDataSize,
            IntPtr sourceName,
            IntPtr defines,
            IntPtr include,
            [MarshalAs(UnmanagedType.LPStr)] string entryPoint,
            [MarshalAs(UnmanagedType.LPStr)] string target,
            uint flags1,
            uint flags2,
            out IntPtr code,
            out IntPtr errorMessages);
    }
}
