using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MyriadMelodies
{
    internal static class KeyboardInputSender
    {
        private const uint InputKeyboard = 1;
        private const uint KeyEventKeyUp = 0x0002;
        private const uint KeyEventExtended = 0x0001;
        private static readonly int InputSize = Marshal.SizeOf<Input>();

        [StructLayout(LayoutKind.Sequential)]
        private struct Input
        {
            public uint Type;
            public InputUnion Data;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public KeyboardInput Keyboard;

            // MOUSEINPUT задаёт правильный размер union: INPUT должен занимать
            // 40 байт на x64 и 28 байт на x86.
            [FieldOffset(0)]
            public MouseInput Mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            public int X;
            public int Y;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            public ushort VirtualKey;
            public ushort ScanCode;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint inputCount, ref Input inputs, int inputSize);

        [DllImport("user32.dll")]
        private static extern void keybd_event(
            byte virtualKey,
            byte scanCode,
            uint flags,
            UIntPtr extraInfo);

        public static void Send(Keys key, bool isDown)
        {
            uint flags=GetFlags(key,isDown);
            var input = new Input
            {
                Type = InputKeyboard,
                Data = new InputUnion
                {
                    Keyboard = new KeyboardInput
                    {
                        VirtualKey = (ushort)key,
                        Flags = flags
                    }
                }
            };

            if (SendInput(1, ref input, InputSize) == 0)
            {
                keybd_event(
                    (byte)key,
                    0,
                    flags,
                    UIntPtr.Zero);
            }
        }
        internal static uint GetFlags(Keys key,bool isDown)
        {
            bool extended=key==Keys.Left || key==Keys.Right || key==Keys.Up || key==Keys.Down ||
                key==Keys.Home || key==Keys.End || key==Keys.Prior || key==Keys.Next ||
                key==Keys.Insert || key==Keys.Delete || key==Keys.Divide || key==Keys.NumLock ||
                key==Keys.RControlKey || key==Keys.RMenu || key==Keys.LWin || key==Keys.RWin || key==Keys.Apps;
            return (extended?KeyEventExtended:0u) | (isDown?0u:KeyEventKeyUp);
        }
    }

    /// <summary>
    /// Отправляет события сразу, а отпускание коротких нажатий и cooldown
    /// обслуживает одним постоянным потоком без Task на каждое событие.
    /// </summary>
    internal sealed class KeyInputDispatcher : IDisposable
    {
        private sealed class KeyState
        {
            public Keys Key;
            public bool IsDown;
            public bool Pending;
            public long ReleaseAt;
            public long CooldownEndsAt;
        }

        private static readonly double StopwatchTicksPerMillisecond =
            Stopwatch.Frequency / 1000.0;
        private readonly object sync = new object();
        private readonly KeyState[] states;
        private readonly Action<Keys, bool> send;
        private readonly Thread schedulerThread;
        private bool disposed;

        public KeyInputDispatcher(int keyCount, Action<Keys, bool> sendKeyEvent)
        {
            send = sendKeyEvent ?? throw new ArgumentNullException(nameof(sendKeyEvent));
            states = new KeyState[keyCount];
            for (int i = 0; i < states.Length; i++)
                states[i] = new KeyState();

            schedulerThread = new Thread(SchedulerLoop)
            {
                IsBackground = true,
                Name = "Key input scheduler",
                Priority = ThreadPriority.AboveNormal
            };
            schedulerThread.Start();
        }

        public bool IsPressed(int index)
        {
            lock (sync)
                return states[index].IsDown;
        }

        /// <summary>
        /// На свободной клавише начинает короткое нажатие, а уже
        /// зажатую клавишу отпускает. Проверка и действие выполняются
        /// под одной блокировкой.
        /// </summary>
        public void TapOrRelease(int index, Keys key, int durationMilliseconds)
        {
            KeyState state = states[index];
            lock (sync)
            {
                if (disposed)
                    return;

                if (state.IsDown)
                {
                    ReleaseCore(state);
                    Monitor.Pulse(sync);
                    return;
                }

                if (state.Pending)
                    return;
                state.Key = key;
                state.IsDown = true;
                state.Pending = true;
                state.ReleaseAt = GetDeadline(durationMilliseconds);
                state.CooldownEndsAt = 0;
                send(key, true);
                Monitor.Pulse(sync);
            }
        }

        public void Press(int index, Keys key)
        {
            KeyState state = states[index];
            lock (sync)
            {
                if (disposed || state.IsDown)
                    return;

                state.Key = key;
                state.Pending = false;
                state.ReleaseAt = 0;
                state.CooldownEndsAt = 0;
                send(key, true);
                state.IsDown = true;
            }
        }

        public void Release(int index)
        {
            KeyState state = states[index];
            lock (sync)
            {
                state.Pending = false;
                state.ReleaseAt = 0;
                state.CooldownEndsAt = 0;
                if (!state.IsDown)
                    return;

                send(state.Key, false);
                state.IsDown = false;
                Monitor.Pulse(sync);
            }
        }

        /// <summary>
        /// Зажимает свободную клавишу, а для уже зажатой повторяет
        /// key-up/key-down не чаще заданного интервала.
        /// </summary>
        public void PressOrRetrigger(int index, Keys key, int cooldownMilliseconds)
        {
            KeyState state = states[index];
            lock (sync)
            {
                if (disposed)
                    return;

                state.Key = key;
                if (!state.IsDown)
                {
                    state.Pending = false;
                    state.ReleaseAt = 0;
                    state.CooldownEndsAt = 0;
                    send(key, true);
                    state.IsDown = true;
                    return;
                }

                if (state.Pending)
                    return;

                send(key, false);
                send(key, true);
                state.Pending = true;
                state.ReleaseAt = 0;
                state.CooldownEndsAt = GetDeadline(cooldownMilliseconds);
                Monitor.Pulse(sync);
            }
        }

        public void ReleaseAll()
        {
            lock (sync)
            {
                if (disposed)
                    return;
                ReleaseAllCore();
                Monitor.Pulse(sync);
            }
        }

        private static long GetDeadline(int milliseconds)
        {
            return Stopwatch.GetTimestamp() +
                (long)Math.Ceiling(Math.Max(0, milliseconds) * StopwatchTicksPerMillisecond);
        }

        private void ReleaseAllCore()
        {
            foreach (KeyState state in states)
            {
                ReleaseCore(state);
            }
        }

        private void ReleaseCore(KeyState state)
        {
            state.Pending = false;
            state.ReleaseAt = 0;
            state.CooldownEndsAt = 0;
            if (state.IsDown)
                send(state.Key, false);
            state.IsDown = false;
        }

        private void SchedulerLoop()
        {
            lock (sync)
            {
                while (!disposed)
                {
                    long now = Stopwatch.GetTimestamp();
                    long nextDeadline = long.MaxValue;

                    foreach (KeyState state in states)
                    {
                        if (state.ReleaseAt != 0)
                        {
                            if (state.ReleaseAt <= now)
                            {
                                if (state.IsDown)
                                    send(state.Key, false);
                                state.IsDown = false;
                                state.Pending = false;
                                state.ReleaseAt = 0;
                            }
                            else
                            {
                                nextDeadline = Math.Min(nextDeadline, state.ReleaseAt);
                            }
                        }

                        if (state.CooldownEndsAt != 0)
                        {
                            if (state.CooldownEndsAt <= now)
                            {
                                state.Pending = false;
                                state.CooldownEndsAt = 0;
                            }
                            else
                            {
                                nextDeadline = Math.Min(nextDeadline, state.CooldownEndsAt);
                            }
                        }
                    }

                    if (nextDeadline == long.MaxValue)
                    {
                        Monitor.Wait(sync);
                    }
                    else
                    {
                        long remainingTicks = Math.Max(0, nextDeadline - Stopwatch.GetTimestamp());
                        int waitMilliseconds = Math.Max(
                            1,
                            (int)Math.Min(
                                int.MaxValue,
                                Math.Ceiling(remainingTicks / StopwatchTicksPerMillisecond)));
                        Monitor.Wait(sync, waitMilliseconds);
                    }
                }
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                    return;
                ReleaseAllCore();
                disposed = true;
                Monitor.PulseAll(sync);
            }

            if (Thread.CurrentThread != schedulerThread)
                schedulerThread.Join(1000);
        }
    }
}
