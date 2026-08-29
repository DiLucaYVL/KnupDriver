using System;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using HidSharp;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using Nefarius.Drivers.HidHide;
using SharpDX.DirectInput;
using SharpDX;


namespace EmuladorKnup360
{
    public class EmulatorService : IDisposable
    {
        private int currentVid = 0;
        private int currentPid = 0;

        private ViGEmClient? client;
        private IXbox360Controller? xboxController;
        private DirectInput? directInput;
        private Joystick? joystick;
        private HidDevice? hidDevice;
        private Thread? loopThread;

        private volatile bool isRunning;
        private IHidHideControlService? hidHide;
        private bool isDisposed;
        private bool isKnupConnected;
        public bool IsKnupConnected => isKnupConnected;

        public ButtonMapping Config { get; set; }
        public event Action<string>? OnLog;
        public event Action<int>? OnJoystickButtonReady;
        public event Action<bool>? OnConnectionChanged;

        public int LeftX { get; private set; } = 32767;
        public int LeftY { get; private set; } = 32767;
        public int RightX { get; private set; } = 32767;
        public int RightY { get; private set; } = 32767;

        public bool DPadUp { get; private set; }
        public bool DPadDown { get; private set; }
        public bool DPadLeft { get; private set; }
        public bool DPadRight { get; private set; }

        public bool[] ButtonStates { get; private set; } = new bool[128];
        public bool IsWaitingForMap = false;

        public EmulatorService(ButtonMapping config)
        {
            Config = config;
            try { client = new ViGEmClient(); }
            catch (Exception ex) { Log("❌ Erro ViGEm: " + ex.Message); }

            try
            {
                var hh = new HidHideControlService();
                if (hh.IsInstalled)
                {
                    hidHide = hh;
                    string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        try { hidHide.AddApplicationPath(exePath); } catch { }
                    }
                }
            }
            catch { hidHide = null; }
        }

        private void Log(string msg)
        {
            if (!isRunning && isDisposed) return;
            try { OnLog?.Invoke(msg); } catch { }
        }

        public bool IsHidHideAvailable => hidHide != null && hidHide.IsInstalled;

        public void EnableHidHide()
        {
            if (hidHide == null || !hidHide.IsInstalled) return;
            try
            {
                var instances = GetControllerInstanceIds();
                foreach (var inst in instances)
                {
                    try { hidHide.AddBlockedInstanceId(inst); } catch { }
                }

                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    try { hidHide.AddApplicationPath(exePath); } catch { }
                }

                hidHide.IsActive = true;
                Log("✔ HidHide Ativo — Knup oculto para jogos (apenas Xbox 360 visível).");
            }
            catch (Exception ex) { Log("❌ Erro HidHide: " + ex.Message); }
        }

        public void DisableHidHide()
        {
            if (hidHide == null || !hidHide.IsInstalled) return;
            try
            {
                var instances = GetControllerInstanceIds();
                foreach (var inst in instances)
                {
                    try { hidHide.RemoveBlockedInstanceId(inst); } catch { }
                }
                hidHide.IsActive = false;
                Log("ℹ HidHide Desativado — controle físico visível.");
            }
            catch (Exception ex) { Log("❌ Erro HidHide: " + ex.Message); }
        }

        private List<string> GetControllerInstanceIds()
        {
            var list = new List<string>();
            try
            {
                var allHids = DeviceList.Local.GetHidDevices().ToList();
                foreach (var dev in allHids)
                {
                    try
                    {
                        // Ignora dispositivos virtuais da Microsoft (Xbox 360)
                        if (dev.VendorID == 0x045E) continue;

                        string path = dev.DevicePath;
                        int start = path.IndexOf("hid#", StringComparison.OrdinalIgnoreCase);
                        if (start >= 0)
                        {
                            int guidStart = path.LastIndexOf('{');
                            string mid = guidStart > start ? path[start..guidStart].TrimEnd('#', '\\') : path[start..];
                            string inst = mid.Replace('#', '\\').ToUpperInvariant();
                            if (!list.Contains(inst)) list.Add(inst);
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return list;
        }

        public void Start()
        {
            if (isRunning) return;
            isRunning = true;

            try { directInput = new DirectInput(); } catch { }

            loopThread = new Thread(Loop) { IsBackground = true, Name = "KnupEmulatorLoop" };
            loopThread.Start();
        }

        private DeviceInstance? FindPhysicalKnupDevice()
        {
            if (directInput == null)
            {
                try { directInput = new DirectInput(); } catch { return null; }
            }

            try
            {
                var devs = directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AllDevices);
                foreach (var d in devs)
                {
                    string name = d.ProductName.Trim();
                    // Ignora qualquer controle virtual ou do Xbox
                    if (name.Contains("Xbox", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("360", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("ViGEm", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Extrai dinamicamente VID e PID dos bytes do ProductGuid
                    byte[] guidBytes = d.ProductGuid.ToByteArray();
                    if (guidBytes.Length >= 4)
                    {
                        // Em DirectInput, Data1 (GUID) armazena VID nos bytes 0-1 e PID nos bytes 2-3 (little-endian)
                        currentVid = guidBytes[0] | (guidBytes[1] << 8);
                        currentPid = guidBytes[2] | (guidBytes[3] << 8);
                    }

                    return d;
                }
            }
            catch { }
            return null;
        }

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_SetOutputReport(SafeFileHandle HidDeviceObject, byte[] lpReportBuffer, int ReportBufferLength);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;

        private SafeFileHandle? hidSafeHandle;

        private void TryConnectPhysicalController()
        {
            if (joystick != null) return;

            var dev = FindPhysicalKnupDevice();
            if (dev == null) return;

            try
            {
                joystick = new Joystick(directInput, dev.InstanceGuid);
                joystick.Properties.BufferSize = 128;
                joystick.Acquire();

                // Conecta o canal de vibração USB HID nativo (Win32 HID SetOutputReport)
                try
                {
                    List<HidDevice> hidDevices = new();
                    if (currentVid != 0 && currentPid != 0)
                    {
                        hidDevices = DeviceList.Local.GetHidDevices(currentVid, currentPid).ToList();
                    }
                    
                    if (hidDevices.Count == 0)
                    {
                        hidDevices = DeviceList.Local.GetHidDevices(0x0810, 0x0001).ToList();
                    }

                    if (hidDevices.Count == 0)
                    {
                        hidDevices = DeviceList.Local.GetHidDevices().Where(h => h.VendorID != 0x045E).ToList();
                    }

                    if (hidDevices.Count > 0)
                    {
                        hidDevice = hidDevices.First();
                        hidSafeHandle = CreateFile(
                            hidDevice.DevicePath,
                            GENERIC_READ | GENERIC_WRITE,
                            FILE_SHARE_READ | FILE_SHARE_WRITE,
                            IntPtr.Zero,
                            OPEN_EXISTING,
                            0,
                            IntPtr.Zero);

                        if (!hidSafeHandle.IsInvalid)
                        {
                            Log($"⚡ Canal de vibração ativo (HID 0x{hidDevice.VendorID:X4}:0x{hidDevice.ProductID:X4})");
                        }
                    }
                }
                catch { }


                // Cria o controle Xbox 360 Virtual no Windows
                if (client != null && xboxController == null)
                {
                    try
                    {
                        xboxController = client.CreateXbox360Controller();
                        xboxController.FeedbackReceived += XboxController_FeedbackReceived;
                        xboxController.Connect();
                    }
                    catch (Exception ex) { Log("❌ Erro Xbox 360 Virtual: " + ex.Message); }
                }

                isKnupConnected = true;
                Log($"✔ Controle Conectado: {dev.ProductName.Trim()} (VID 0x{currentVid:X4}, PID 0x{currentPid:X4}) → Emulando Xbox 360");
                OnConnectionChanged?.Invoke(true);
            }
            catch (Exception ex)
            {
                DisconnectPhysicalController();
                Log($"❌ Erro ao conectar controle: {ex.Message}");
            }
        }

        private void DisconnectPhysicalController()
        {
            isKnupConnected = false;
            try
            {
                if (joystick != null)
                {
                    try { joystick.Unacquire(); } catch { }
                    try { joystick.Dispose(); } catch { }
                    joystick = null;
                }
            }
            catch { }

            try
            {
                if (hidSafeHandle != null && !hidSafeHandle.IsInvalid && !hidSafeHandle.IsClosed)
                {
                    byte[] stopReport = new byte[5] { 0x01, 0x00, 0x00, 0x00, 0x00 };
                    try { HidD_SetOutputReport(hidSafeHandle, stopReport, 5); } catch { }
                    try { hidSafeHandle.Dispose(); } catch { }
                    hidSafeHandle = null;
                }
            }
            catch { }

            try
            {
                if (xboxController != null)
                {
                    xboxController.FeedbackReceived -= XboxController_FeedbackReceived;
                    try { xboxController.Disconnect(); } catch { }
                    xboxController = null;
                }
            }
            catch { }

            OnConnectionChanged?.Invoke(false);
        }

        private void XboxController_FeedbackReceived(object sender, Xbox360FeedbackReceivedEventArgs e)
        {
            SendVibration(e.LargeMotor, e.SmallMotor);
        }

        public void SendVibration(byte largeMotor, byte smallMotor)
        {
            if (hidSafeHandle == null || hidSafeHandle.IsInvalid || hidSafeHandle.IsClosed) return;
            try
            {
                // Padrão Knup/Twin USB:
                // Byte 0 = Report ID (0x01)
                // Byte 1 = Motor Esquerdo / Forte (0-255)
                // Byte 2 = Motor Direito / Fraco (0-255)
                // Byte 3 = 0x00
                // Byte 4 = Flag de Ativação (0xFF quando ativo, 0x00 para parar)
                byte enable = (largeMotor > 0 || smallMotor > 0) ? (byte)0xFF : (byte)0x00;
                byte[] report = new byte[5] { 0x01, largeMotor, smallMotor, 0x00, enable };
                HidD_SetOutputReport(hidSafeHandle, report, 5);
            }
            catch { }
        }

        public void SendVibration(byte intensity)
        {
            SendVibration(intensity, intensity);
        }



        private void Loop()
        {
            while (isRunning)
            {
                try
                {
                    if (joystick == null)
                    {
                        TryConnectPhysicalController();
                        if (joystick == null)
                        {
                            Thread.Sleep(500);
                            continue;
                        }
                    }

                    joystick.Poll();
                    var state = joystick.GetCurrentState();

                    LeftX = state.X;
                    LeftY = state.Y;
                    RightX = state.RotationZ;
                    RightY = state.Z;

                    int pov = state.PointOfViewControllers.Length > 0 ? state.PointOfViewControllers[0] : -1;
                    bool dUp = false, dDown = false, dLeft = false, dRight = false;
                    if (pov >= 0)
                    {
                        dUp    = (pov >= 31500 || pov <= 4500);
                        dRight = (pov >= 4500  && pov <= 13500);
                        dDown  = (pov >= 13500 && pov <= 22500);
                        dLeft  = (pov >= 22500 && pov <= 31500);
                    }

                    var btns = state.Buttons;
                    ButtonStates = btns;

                    if (Config.Buttons.TryGetValue("DUp",    out int du) && du < btns.Length)    dUp    |= btns[du];
                    if (Config.Buttons.TryGetValue("DDown",  out int dd) && dd < btns.Length)    dDown  |= btns[dd];
                    if (Config.Buttons.TryGetValue("DLeft",  out int dl) && dl < btns.Length)    dLeft  |= btns[dl];
                    if (Config.Buttons.TryGetValue("DRight", out int dr) && dr < btns.Length)    dRight |= btns[dr];

                    DPadUp = dUp;
                    DPadDown = dDown;
                    DPadLeft = dLeft;
                    DPadRight = dRight;

                    if (IsWaitingForMap)
                    {
                        for (int i = 0; i < btns.Length; i++)
                        {
                            if (btns[i])
                            {
                                OnJoystickButtonReady?.Invoke(i);
                                IsWaitingForMap = false;
                                break;
                            }
                        }
                    }
                    else if (xboxController != null)
                    {
                        void MapBtn(string name, Xbox360Button btn)
                        {
                            bool pressed = Config.Buttons.TryGetValue(name, out int id) && id < btns.Length && btns[id];
                            xboxController.SetButtonState(btn, pressed);
                        }

                        MapBtn("A",     Xbox360Button.A);
                        MapBtn("B",     Xbox360Button.B);
                        MapBtn("X",     Xbox360Button.X);
                        MapBtn("Y",     Xbox360Button.Y);
                        MapBtn("LB",    Xbox360Button.LeftShoulder);
                        MapBtn("RB",    Xbox360Button.RightShoulder);
                        MapBtn("Back",  Xbox360Button.Back);
                        MapBtn("Start", Xbox360Button.Start);
                        MapBtn("L3",    Xbox360Button.LeftThumb);
                        MapBtn("R3",    Xbox360Button.RightThumb);

                        xboxController.SetSliderValue(Xbox360Slider.LeftTrigger,
                            Config.Buttons.TryGetValue("LT", out int ltId) && ltId < btns.Length && btns[ltId] ? (byte)255 : (byte)0);
                        xboxController.SetSliderValue(Xbox360Slider.RightTrigger,
                            Config.Buttons.TryGetValue("RT", out int rtId) && rtId < btns.Length && btns[rtId] ? (byte)255 : (byte)0);

                        xboxController.SetButtonState(Xbox360Button.Up,    dUp);
                        xboxController.SetButtonState(Xbox360Button.Down,  dDown);
                        xboxController.SetButtonState(Xbox360Button.Left,  dLeft);
                        xboxController.SetButtonState(Xbox360Button.Right, dRight);

                        xboxController.SetAxisValue(Xbox360Axis.LeftThumbX,  NormalizeAxis(state.X, false));
                        xboxController.SetAxisValue(Xbox360Axis.LeftThumbY,  NormalizeAxis(state.Y, true));
                        xboxController.SetAxisValue(Xbox360Axis.RightThumbX, NormalizeAxis(state.RotationZ, false));
                        xboxController.SetAxisValue(Xbox360Axis.RightThumbY, NormalizeAxis(state.Z, true));

                        xboxController.SubmitReport();
                    }
                }
                catch (SharpDXException)
                {
                    DisconnectPhysicalController();
                    Log("⚠ Controle Knup desconectado da USB. Aguardando reconexão...");
                }
                catch
                {
                    if (!isRunning) break;
                }
                Thread.Sleep(8);
            }
        }

        private static short NormalizeAxis(int rawValue, bool invert)
        {
            rawValue = Math.Clamp(rawValue, 0, 65535);
            int normalized = rawValue - 32768;
            if (invert) normalized = -normalized;
            return (short)Math.Clamp(normalized, short.MinValue, short.MaxValue);
        }

        public void Stop()
        {
            if (!isRunning && isDisposed) return;
            isRunning = false;
            DisconnectPhysicalController();

            try { loopThread?.Join(300); } catch { }
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;
            Stop();

            try { client?.Dispose(); client = null; } catch { }
            try { directInput?.Dispose(); directInput = null; } catch { }
        }
    }
}
