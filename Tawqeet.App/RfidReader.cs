using System.IO.Ports;
using System.Management;

namespace Tawqeet.App;

public class RfidReader : IDisposable
{
    private readonly SerialPort _serialPort = new();
    private readonly SynchronizationContext? _syncContext = SynchronizationContext.Current;
    private readonly List<char> _keyboardBuffer = new();
    private bool _disposed;

    public event EventHandler<string>? CardScanned;
    public bool IsConnected => _serialPort.IsOpen;

    public string[] GetAvailablePorts() => SerialPort.GetPortNames().OrderBy(p => p).ToArray();

    /// <summary>
    /// Gets available COM ports with their device information, specifically identifying ESP32 devices.
    /// </summary>
    public Dictionary<string, PortInfo> GetPortsWithInfo()
    {
        var ports = new Dictionary<string, PortInfo>();
        var portNames = GetAvailablePorts();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, Caption, PNPDeviceID FROM Win32_PnPEntity WHERE Name LIKE '%COM%'");

            foreach (ManagementObject device in searcher.Get())
            {
                var name = device["Name"]?.ToString() ?? "";
                var caption = device["Caption"]?.ToString() ?? "";
                var pnpDeviceId = device["PNPDeviceID"]?.ToString() ?? "";

                // Extract COM port number
                var comMatch = System.Text.RegularExpressions.Regex.Match(name, @"COM(\d+)", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                if (comMatch.Success)
                {
                    var comPort = comMatch.Value;
                    if (portNames.Contains(comPort))
                    {
                        var isEsp32 = UsbDeviceWatcher.IsLikelyEsp32(name, caption, pnpDeviceId);
                        ports[comPort] = new PortInfo
                        {
                            PortName = comPort,
                            DeviceName = name,
                            DeviceCaption = caption,
                            IsEsp32 = isEsp32
                        };
                    }
                }
            }
        }
        catch
        {
            // Fallback: just return port names without device info
            foreach (var port in portNames)
            {
                if (!ports.ContainsKey(port))
                {
                    ports[port] = new PortInfo { PortName = port, IsEsp32 = false };
                }
            }
        }

        // Add any ports that weren't found in WMI
        foreach (var port in portNames)
        {
            if (!ports.ContainsKey(port))
            {
                ports[port] = new PortInfo { PortName = port, IsEsp32 = false };
            }
        }

        return ports;
    }

    /// <summary>
    /// Finds the first available ESP32 device port.
    /// </summary>
    public string? FindEsp32Port()
    {
        var ports = GetPortsWithInfo();
        return ports.Values.FirstOrDefault(p => p.IsEsp32)?.PortName;
    }

    public void Connect(string portName, int baudRate)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            throw new ArgumentException("Port name is required", nameof(portName));
        }

        if (_serialPort.IsOpen)
        {
            _serialPort.Close();
        }

        _serialPort.PortName = portName;
        _serialPort.BaudRate = baudRate;
        _serialPort.DataReceived -= OnDataReceived;
        _serialPort.DataReceived += OnDataReceived;
        _serialPort.Open();
    }

    public void Disconnect()
    {
        if (_serialPort.IsOpen)
        {
            _serialPort.DataReceived -= OnDataReceived;
            _serialPort.Close();
        }
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            var data = _serialPort.ReadExisting();
            if (string.IsNullOrWhiteSpace(data))
            {
                return;
            }

            foreach (var line in data.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                RaiseCardScanned(line.Trim());
            }
        }
        catch
        {
            // swallow errors to keep listener alive
        }
    }

    public void HandleKeyboardChar(char keyChar)
    {
        if (keyChar == '\r' || keyChar == '\n')
        {
            if (_keyboardBuffer.Count == 0)
            {
                return;
            }

            var cardId = new string(_keyboardBuffer.ToArray());
            _keyboardBuffer.Clear();
            RaiseCardScanned(cardId.Trim());
        }
        else
        {
            _keyboardBuffer.Add(keyChar);
        }
    }

    private void RaiseCardScanned(string cardId)
    {
        void Raise() => CardScanned?.Invoke(this, cardId);
        if (_syncContext != null)
        {
            _syncContext.Post(_ => Raise(), null);
        }
        else
        {
            Raise();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Disconnect();
        _serialPort.Dispose();
    }
}

/// <summary>
/// Information about a serial port.
/// </summary>
public class PortInfo
{
    public string PortName { get; set; } = "";
    public string? DeviceName { get; set; }
    public string? DeviceCaption { get; set; }
    public bool IsEsp32 { get; set; }
}







