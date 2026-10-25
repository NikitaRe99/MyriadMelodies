using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace MyriadMelodies
{
    internal sealed class WindowCaptureTarget
    {
        private static readonly object processCacheSync=new object();
        private static int cachedProcess;
        private static bool cachedIsGame;
        public static bool IsGameWindow(IntPtr handle)
        {
            if(handle==IntPtr.Zero) return false;
            int id=GetProcessId(handle);
            if(id==0) return false;
            lock(processCacheSync)
            {
                if(id==cachedProcess) return cachedIsGame;
                bool game=false;
                try {using var process=Process.GetProcessById(id);game=IsGameProcessName(process.ProcessName);}
                catch(ArgumentException) { }
                catch(InvalidOperationException) { }
                catch(System.ComponentModel.Win32Exception) { }
                cachedProcess=id;cachedIsGame=game;return game;
            }
        }
        private static bool IsGameProcessName(string name)=>name.Equals("GenshinImpact",StringComparison.OrdinalIgnoreCase) || name.Equals("YuanShen",StringComparison.OrdinalIgnoreCase);
        public IntPtr Handle { get; }
        public int ProcessId { get; }
        public string ProcessName { get; }
        public string Title { get; }
        public WindowCaptureTarget(IntPtr handle,int processId,string processName,string title)
        {Handle=handle;ProcessId=processId;ProcessName=processName;Title=title;}
        public bool IsAvailable => WindowCaptureGeometry.IsWindow(Handle) && GetProcessId(Handle)==ProcessId;
        public override string ToString()
        {
            if(!IsAvailable) return UiText.T("Genshin · окно закрыто");
            try
            {
                Size size=WindowCaptureGeometry.Read(Handle).ClientBounds.Size;
                return UiText.F("Genshin · окно · {0} × {1}","Genshin · window · {0} × {1}",size.Width,size.Height);
            }
            catch(InvalidOperationException){return UiText.T("Genshin · окно недоступно");}
        }
        public static IReadOnlyList<WindowCaptureTarget> FindGameWindows()
        {
            var windows=new List<WindowCaptureTarget>();
            var gameProcesses=new Dictionary<int,string>();
            foreach(string processName in new[]{"GenshinImpact","YuanShen"})
            {
                Process[] matches;
                try {matches=Process.GetProcessesByName(processName);}
                catch(System.ComponentModel.Win32Exception){continue;}
                catch(InvalidOperationException){continue;}
                foreach(var process in matches)
                    using(process)
                    {
                        try {gameProcesses[process.Id]=processName;}
                        catch(InvalidOperationException) { }
                    }
            }
            EnumWindows((handle,param)=>
            {
                if(!IsWindowVisible(handle)) return true;
                int id=GetProcessId(handle);
                if(!gameProcesses.TryGetValue(id,out string name)) return true;
                try
                {
                    if(GetWindow(handle,4)!=IntPtr.Zero) return true; // Только основное, не дочернее окно.
                    var text=new StringBuilder(512);GetWindowText(handle,text,text.Capacity);
                    windows.Add(new WindowCaptureTarget(handle,id,name,text.ToString()));
                }
                catch(ArgumentException) { }
                catch(InvalidOperationException) { }
                catch(System.ComponentModel.Win32Exception) { }
                return true;
            },IntPtr.Zero);
            return windows;
        }
        private static int GetProcessId(IntPtr handle){GetWindowThreadProcessId(handle,out uint id);return (int)id;}
        private delegate bool EnumWindowProc(IntPtr handle,IntPtr param);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowProc callback,IntPtr param);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr handle,uint command);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetWindowText(IntPtr handle,StringBuilder text,int count);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle,out uint processId);
    }

    internal readonly struct WindowCaptureGeometry
    {
        public Rectangle ClientBounds {get;}
        public Rectangle FrameBounds {get;}
        public Rectangle OuterBounds {get;}
        private WindowCaptureGeometry(Rectangle client,Rectangle frame,Rectangle outer)
        {ClientBounds=client;FrameBounds=frame;OuterBounds=outer;}
        public Rectangle ClientInCapture(Size capturedSize)
        {
            if(capturedSize==FrameBounds.Size) return new Rectangle(new Point(ClientBounds.Left-FrameBounds.Left,ClientBounds.Top-FrameBounds.Top),ClientBounds.Size);
            if(capturedSize==OuterBounds.Size) return new Rectangle(new Point(ClientBounds.Left-OuterBounds.Left,ClientBounds.Top-OuterBounds.Top),ClientBounds.Size);
            if(capturedSize==ClientBounds.Size) return new Rectangle(Point.Empty,ClientBounds.Size);
            throw new InvalidOperationException(UiText.T("Размер кадра окна изменился. Повторите запуск захвата."));
        }
        public static WindowCaptureGeometry Read(IntPtr handle)
        {
            if(!IsWindow(handle)) throw new InvalidOperationException(UiText.T("Окно игры закрыто. Выберите его заново."));
            if(!GetClientRect(handle,out Rect client) || !GetWindowRect(handle,out Rect outer))
                throw new InvalidOperationException(UiText.T("Не удалось определить размеры окна игры."));
            var origin=new NativePoint();
            if(!ClientToScreen(handle,ref origin)) throw new InvalidOperationException(UiText.T("Не удалось определить игровую область."));
            Rect frame=outer;
            if(DwmGetWindowAttribute(handle,9,out Rect extended,Marshal.SizeOf<Rect>())>=0) frame=extended;
            return new WindowCaptureGeometry(new Rectangle(origin.X,origin.Y,client.Right,client.Bottom),frame.Rectangle,outer.Rectangle);
        }
        [StructLayout(LayoutKind.Sequential)] private struct Rect
        {
            public int Left,Top,Right,Bottom;
            public Rectangle Rectangle=>Rectangle.FromLTRB(Left,Top,Right,Bottom);
        }
        [StructLayout(LayoutKind.Sequential)] private struct NativePoint {public int X,Y;}
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr handle);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr handle);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern IntPtr MonitorFromWindow(IntPtr handle,uint flags);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr handle,out Rect rect);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle,out Rect rect);
        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr handle,ref NativePoint point);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr handle,int attribute,out Rect rect,int size);
    }
}
