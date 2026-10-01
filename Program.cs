using System;
using System.IO;
using System.IO.Ports;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Microsoft.Win32;

namespace TeslaFanController
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MonitorContext());
        }
    }

    public class MonitorContext : ApplicationContext
    {
        private const string NvmlDll = "nvml.dll";

        [DllImport(NvmlDll, EntryPoint = "nvmlInit_v2", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NvmlInit();

        [DllImport(NvmlDll, EntryPoint = "nvmlShutdown", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NvmlShutdown();

        [DllImport(NvmlDll, EntryPoint = "nvmlDeviceGetHandleByIndex_v2", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NvmlDeviceGetHandleByIndex(uint index, out IntPtr device);

        [DllImport(NvmlDll, EntryPoint = "nvmlDeviceGetTemperature", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NvmlDeviceGetTemperature(IntPtr device, int sensorType, out uint temp);

        [DllImport(NvmlDll, EntryPoint = "nvmlDeviceGetUtilizationRates", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NvmlDeviceGetUtilizationRates(IntPtr device, out NvmlUtilization utilization);

        [DllImport(NvmlDll, EntryPoint = "nvmlDeviceGetMemoryInfo", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NvmlDeviceGetMemoryInfo(IntPtr device, out NvmlMemory memory);

        [DllImport(NvmlDll, EntryPoint = "nvmlDeviceGetName", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NvmlDeviceGetName(IntPtr device, System.Text.StringBuilder name, uint length);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct NvmlUtilization
        {
            public uint Gpu;
            public uint Memory;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NvmlMemory
        {
            public ulong Total;
            public ulong Free;
            public ulong Used;
        }

        private const string RegistryRunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppAppName = "TeslaFanController";

        private SerialPort? _serialPort;
        private System.Windows.Forms.Timer _monitorTimer;
        private const string ConfigFile = "config.txt";

        private string _currentPort = "COM3";
        private int _tempMinPwm = 40;
        private int _tempMaxPwm = 80;
        private int _minPwmPercent = 30;
        private string _iconMode = "load";

        private IntPtr _gpuHandle = IntPtr.Zero;
        private bool _isNvmlInitialized = false;

        private string _gpuName = "NVIDIA GPU";
        private string _gpuTemp = "N/A";
        private string _fanRpm = "N/A";
        private string _gpuLoad = "N/A";
        private string _gpuMem = "N/A";
        private string _calculatedPwm = "N/A";

        private int _rawTemp = 0;
        private int _rawLoad = 0;
        private int _rawRpm = 0;

        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _contextMenuStrip;
        private IntPtr _currentIconHandle = IntPtr.Zero;

        private SettingsForm? _settingsForm = null;
        private GraphForm? _graphForm = null;

        public MonitorContext()
        {
            LoadConfig();

            _contextMenuStrip = new ContextMenuStrip();
            BuildContextMenu();

            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                ContextMenuStrip = _contextMenuStrip,
                Visible = true,
                Text = "Initializing NVIDIA Temp Monitor..."
            };

            _notifyIcon.DoubleClick += NotifyIcon_DoubleClick;

            try
            {
                if (NvmlInit() == 0 && NvmlDeviceGetHandleByIndex(0, out _gpuHandle) == 0)
                {
                    _isNvmlInitialized = true;
                    var nameBuilder = new System.Text.StringBuilder(64);
                    if (NvmlDeviceGetName(_gpuHandle, nameBuilder, 64) == 0)
                    {
                        _gpuName = nameBuilder.ToString().Trim();
                    }
                }
            }
            catch
            {
                _gpuTemp = "NVML Error";
            }

            InitSerialPort();

            _monitorTimer = new System.Windows.Forms.Timer();
            _monitorTimer.Interval = 2000;
            _monitorTimer.Tick += MonitorTimer_Tick;
            _monitorTimer.Start();
        }

        private void NotifyIcon_DoubleClick(object? sender, EventArgs e)
        {
            if (_graphForm != null && !_graphForm.IsDisposed)
            {
                _graphForm.Activate();
                return;
            }

            _graphForm = new GraphForm(_gpuName);
            _graphForm.Show();
        }

        /// <summary>
        /// Loads configuration from config.txt with robust parsing.
        /// Dynamically searches for the COM port line to avoid issues
        /// caused by empty lines or whitespace between sections.
        /// </summary>
        private void LoadConfig()
        {
            if (File.Exists(ConfigFile))
            {
                try
                {
                    string[] lines = File.ReadAllLines(ConfigFile);
                    List<string> validValues = new List<string>();

                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        // Filter out empty lines and comments
                        if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("#"))
                        {
                            validValues.Add(trimmed);
                        }
                    }

                    // Dynamically find the COM port by searching for "COM" prefix
                    foreach (string val in validValues)
                    {
                        if (val.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
                        {
                            _currentPort = val;
                            break;
                        }
                    }

                    // Parse remaining parameters by index, skipping the COM port line
                    if (validValues.Count >= 4)
                    {
                        int.TryParse(validValues[1], out _tempMinPwm);
                        int.TryParse(validValues[2], out _tempMaxPwm);
                        int.TryParse(validValues[3], out _minPwmPercent);
                    }
                    if (validValues.Count >= 5)
                    {
                        string mode = validValues[4].ToLower();
                        if (mode == "load" || mode == "temp" || mode == "fan") _iconMode = mode;
                    }
                }
                catch { }
            }
            else
            {
                SaveConfig(_currentPort);
            }
        }

        private void SaveConfig(string port)
        {
            try
            {
                string configText =
                    "# NVIDIA Tesla Cooling Configuration\n" +
                    "# ===================================\n\n" +
                    "# Arduino COM Port name\n" +
                    $"{port}\n\n" +
                    "# Temperature below which fan runs at minimum\n" +
                    $"{_tempMinPwm}\n\n" +
                    "# Temperature above which fan runs at 100%\n" +
                    $"{_tempMaxPwm}\n\n" +
                    "# Minimum fan speed in PWM percentage (0 to 100)\n" +
                    $"{_minPwmPercent}\n\n" +
                    "# Tray icon display mode (load, temp, fan)\n" +
                    $"{_iconMode}\n";

                File.WriteAllText(ConfigFile, configText);
            }
            catch { }
        }

        private void InitSerialPort()
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.DataReceived -= SerialPort_DataReceived;
                    _serialPort.Close();
                }

                _serialPort = new SerialPort(_currentPort, 9600);
                _serialPort.Open();
                _serialPort.DataReceived += SerialPort_DataReceived;
                _fanRpm = "Connecting...";
            }
            catch
            {
                _fanRpm = "Port Error";
            }
        }

        private void BuildContextMenu()
        {
            _contextMenuStrip.Items.Clear();

            var settingsItem = new ToolStripMenuItem("Settings Panel...", null, (s, e) =>
            {
                ShowSettingsWindow();
            });
            settingsItem.Font = new Font(settingsItem.Font, FontStyle.Bold);
            _contextMenuStrip.Items.Add(settingsItem);

            _contextMenuStrip.Items.Add(new ToolStripSeparator());

            var displayMenu = new ToolStripMenuItem("Tray Icon Display");
            var loadItem = new ToolStripMenuItem("GPU Load") { Checked = (_iconMode == "load") };
            loadItem.Click += (s, e) => { ChangeIconMode("load"); };
            var tempItem = new ToolStripMenuItem("GPU Temperature") { Checked = (_iconMode == "temp") };
            tempItem.Click += (s, e) => { ChangeIconMode("temp"); };
            var fanItem = new ToolStripMenuItem("Fan Speed (RPM)") { Checked = (_iconMode == "fan") };
            fanItem.Click += (s, e) => { ChangeIconMode("fan"); };
            displayMenu.DropDownItems.Add(loadItem);
            displayMenu.DropDownItems.Add(tempItem);
            displayMenu.DropDownItems.Add(fanItem);
            _contextMenuStrip.Items.Add(displayMenu);

            var startupItem = new ToolStripMenuItem("Start with Windows") { Checked = IsInStartup() };
            startupItem.Click += (s, e) =>
            {
                ToggleStartup(!startupItem.Checked);
                BuildContextMenu();
            };
            _contextMenuStrip.Items.Add(startupItem);

            _contextMenuStrip.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("Exit", null, (s, e) =>
            {
                _monitorTimer.Stop();
                if (_serialPort != null && _serialPort.IsOpen) _serialPort.Close();
                if (_isNvmlInitialized) NvmlShutdown();
                if (_currentIconHandle != IntPtr.Zero) DestroyIcon(_currentIconHandle);
                if (_graphForm != null && !_graphForm.IsDisposed) _graphForm.Close();
                _notifyIcon.Visible = false;
                Application.Exit();
            });
            _contextMenuStrip.Items.Add(exitItem);
        }

        private void ShowSettingsWindow()
        {
            if (_settingsForm != null && !_settingsForm.IsDisposed)
            {
                _settingsForm.Activate();
                return;
            }
            _settingsForm = new SettingsForm(_currentPort, _tempMinPwm, _tempMaxPwm, _minPwmPercent);
            _settingsForm.OnSettingsSaved = (newPort, minT, maxT, minP) =>
            {
                bool portChanged = (_currentPort != newPort);
                _currentPort = newPort;
                _tempMinPwm = minT;
                _tempMaxPwm = maxT;
                _minPwmPercent = minP;
                SaveConfig(_currentPort);
                if (portChanged) InitSerialPort();
                BuildContextMenu();
            };
            _settingsForm.Show();
        }

        private void ChangeIconMode(string newMode)
        {
            _iconMode = newMode;
            SaveConfig(_currentPort);
            BuildContextMenu();
            UpdateTrayTextAndIcon();
        }

        private bool IsInStartup()
        {
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryRunPath, false))
            {
                if (key != null)
                {
                    object? value = key.GetValue(AppAppName);
                    return value != null && value.ToString() == Application.ExecutablePath;
                }
            }
            return false;
        }

        private void ToggleStartup(bool enable)
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryRunPath, true))
                {
                    if (key != null)
                    {
                        if (enable) key.SetValue(AppAppName, Application.ExecutablePath);
                        else key.DeleteValue(AppAppName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Auto-startup error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MonitorTimer_Tick(object? sender, EventArgs e)
        {
            UpdateGpuDataViaNvml();
            UpdateTrayTextAndIcon();

            if (_graphForm != null && !_graphForm.IsDisposed && _isNvmlInitialized)
            {
                int currentPwmVal = 0;
                if (int.TryParse(_calculatedPwm.Replace("%", ""), out int parsedPwm))
                {
                    currentPwmVal = parsedPwm;
                }
                _graphForm.AddData(_rawTemp, currentPwmVal);
            }
        }

        private void UpdateGpuDataViaNvml()
        {
            if (!_isNvmlInitialized || _gpuHandle == IntPtr.Zero) return;

            if (NvmlDeviceGetTemperature(_gpuHandle, 0, out uint temp) == 0)
            {
                _rawTemp = (int)temp;
                _gpuTemp = $"{temp}°C";
                int targetPwm = CalculateTargetPwm(_rawTemp);
                _calculatedPwm = $"{targetPwm}%";

                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.WriteLine(targetPwm.ToString());
                }
            }

            if (NvmlDeviceGetUtilizationRates(_gpuHandle, out NvmlUtilization utilization) == 0)
            {
                _rawLoad = (int)utilization.Gpu;
                _gpuLoad = $"{utilization.Gpu}%";
            }

            if (NvmlDeviceGetMemoryInfo(_gpuHandle, out NvmlMemory memory) == 0)
            {
                ulong usedMb = memory.Used / 1048576;
                ulong totalMb = memory.Total / 1048576;
                _gpuMem = $"{usedMb} / {totalMb} MB";
            }
        }

        private int CalculateTargetPwm(int temp)
        {
            if (_tempMaxPwm <= _tempMinPwm) return 100;
            if (temp <= _tempMinPwm) return _minPwmPercent;
            if (temp >= _tempMaxPwm) return 100;

            double ratio = (double)(temp - _tempMinPwm) / (_tempMaxPwm - _tempMinPwm);
            int pwm = _minPwmPercent + (int)Math.Round(ratio * (100 - _minPwmPercent));
            return Math.Clamp(pwm, _minPwmPercent, 100);
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    string data = _serialPort.ReadLine().Trim();
                    if (data.StartsWith("RPM:"))
                    {
                        string rpmStr = data.Replace("RPM:", "");
                        _fanRpm = rpmStr + " RPM";
                        int.TryParse(rpmStr, out _rawRpm);
                    }
                }
            }
            catch { }
        }

        private void UpdateTrayTextAndIcon()
        {
            string tooltip = $"{_gpuName} Monitor ({_currentPort})\n" +
                              $"Temp: {_gpuTemp} (PWM: {_calculatedPwm})\n" +
                              $"Fan Speed: {_fanRpm}\n" +
                              $"GPU Load: {_gpuLoad}\n" +
                              $"Memory: {_gpuMem}";

            if (tooltip.Length > 127) tooltip = tooltip.Substring(0, 127);
            _notifyIcon.Text = tooltip;

            try
            {
                int percentage = 0;
                Color barColor = Color.FromArgb(17, 125, 187);

                if (_iconMode == "load")
                {
                    percentage = _rawLoad;
                }
                else if (_iconMode == "temp")
                {
                    percentage = Math.Clamp((int)Math.Round((_rawTemp - 30) / 60.0 * 100), 0, 100);
                    if (_rawTemp < 60) barColor = Color.FromArgb(37, 175, 76);
                    else if (_rawTemp < 78) barColor = Color.FromArgb(242, 156, 17);
                    else barColor = Color.FromArgb(217, 83, 79);
                }
                else if (_iconMode == "fan")
                {
                    percentage = Math.Clamp((int)Math.Round((_rawRpm / 4500.0) * 100), 0, 100);
                    barColor = Color.FromArgb(155, 89, 182);
                }

                using (Bitmap bmp = new Bitmap(16, 16))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.FromArgb(29, 39, 41));

                    using (Pen gridPen = new Pen(Color.FromArgb(50, 65, 68), 1))
                    {
                        g.DrawLine(gridPen, 4, 0, 4, 16);
                        g.DrawLine(gridPen, 8, 0, 8, 16);
                        g.DrawLine(gridPen, 12, 0, 12, 16);
                        g.DrawLine(gridPen, 0, 4, 16, 4);
                        g.DrawLine(gridPen, 0, 8, 16, 8);
                        g.DrawLine(gridPen, 0, 12, 16, 12);
                    }

                    int fillHeight = (int)Math.Round((percentage / 100.0) * 16);
                    if (fillHeight > 0)
                    {
                        int yCoord = 16 - fillHeight;
                        using (Brush brush = new SolidBrush(barColor))
                        {
                            g.FillRectangle(brush, 0, yCoord, 16, fillHeight);
                        }
                    }

                    using (Pen borderPen = new Pen(Color.FromArgb(85, 105, 110), 1))
                    {
                        g.DrawRectangle(borderPen, 0, 0, 15, 15);
                    }

                    IntPtr hIcon = bmp.GetHicon();
                    _notifyIcon.Icon = Icon.FromHandle(hIcon);

                    if (_currentIconHandle != IntPtr.Zero)
                    {
                        DestroyIcon(_currentIconHandle);
                    }
                    _currentIconHandle = hIcon;
                }
            }
            catch { }
        }
    }
}
