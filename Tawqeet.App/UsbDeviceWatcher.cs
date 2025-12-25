using System.Management;

namespace Tawqeet.App;

/// <summary>
/// Monitors USB device plug/unplug events using WMI.
/// Raises events when devices are connected or disconnected.
/// Specifically enhanced for ESP32 device detection.
/// </summary>
public class UsbDeviceWatcher : IDisposable
{
    private ManagementEventWatcher? _insertWatcher;
    private ManagementEventWatcher? _removeWatcher;
    private bool _disposed;

    /// <summary>
    /// Raised when a USB device is connected. Event args contain device info.
    /// </summary>
    public event EventHandler<DeviceInfoEventArgs>? DeviceConnected;

    /// <summary>
    /// Raised when a USB device is disconnected. Event args contain device info.
    /// </summary>
    public event EventHandler<DeviceInfoEventArgs>? DeviceDisconnected;

    /// <summary>
    /// Checks if a device is likely an ESP32 based on its description.
    /// </summary>
    public static bool IsLikelyEsp32(string? deviceName, string? deviceCaption, string? pnpDeviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceName) && string.IsNullOrWhiteSpace(deviceCaption) && string.IsNullOrWhiteSpace(pnpDeviceId))
            return false;

        var combined = $"{deviceName} {deviceCaption} {pnpDeviceId}".ToUpperInvariant();

        // Common ESP32 USB-to-Serial chip identifiers
        var esp32Indicators = new[]
        {
            "CP210",      // Silicon Labs CP210x (very common for ESP32)
            "CH340",      // WCH CH340 (common for ESP32)
            "CH341",      // WCH CH341
            "FT232",      // FTDI FT232 (sometimes used)
            "ESP32",      // Direct ESP32 mention
            "ESP32-S2",   // ESP32-S2
            "ESP32-S3",   // ESP32-S3
            "USB SERIAL", // Generic USB Serial (could be ESP32)
            "USB\\VID_10C4&PID_EA60", // CP2102 VID/PID
            "USB\\VID_1A86&PID_7523", // CH340 VID/PID
            "USB\\VID_0403&PID_6001", // FT232 VID/PID
        };

        return esp32Indicators.Any(indicator => combined.Contains(indicator));
    }

    /// <summary>
    /// Starts monitoring for USB device changes.
    /// </summary>
    public void Start()
    {
        Stop(); // Ensure no duplicate watchers

        try
        {
            // Watch for device insertion
            var insertQuery = new WqlEventQuery(
                "SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'");
            _insertWatcher = new ManagementEventWatcher(insertQuery);
            _insertWatcher.EventArrived += OnDeviceInserted;
            _insertWatcher.Start();

            // Watch for device removal
            var removeQuery = new WqlEventQuery(
                "SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'");
            _removeWatcher = new ManagementEventWatcher(removeQuery);
            _removeWatcher.EventArrived += OnDeviceRemoved;
            _removeWatcher.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to start USB watcher: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops monitoring for USB device changes.
    /// </summary>
    public void Stop()
    {
        if (_insertWatcher != null)
        {
            _insertWatcher.EventArrived -= OnDeviceInserted;
            _insertWatcher.Stop();
            _insertWatcher.Dispose();
            _insertWatcher = null;
        }

        if (_removeWatcher != null)
        {
            _removeWatcher.EventArrived -= OnDeviceRemoved;
            _removeWatcher.Stop();
            _removeWatcher.Dispose();
            _removeWatcher = null;
        }
    }

    private void OnDeviceInserted(object sender, EventArrivedEventArgs e)
    {
        // Check if it's a COM port device, especially ESP32
        try
        {
            var instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            var name = instance["Name"]?.ToString() ?? "";
            var caption = instance["Caption"]?.ToString() ?? "";
            var pnpDeviceId = instance["PNPDeviceID"]?.ToString() ?? "";
            
            // Check if it's a COM port device
            var isComPort = name.Contains("COM") || caption.Contains("COM") || 
                          caption.Contains("Serial") || caption.Contains("USB Serial");
            
            if (isComPort)
            {
                var isEsp32 = IsLikelyEsp32(name, caption, pnpDeviceId);
                var deviceInfo = new DeviceInfoEventArgs
                {
                    DeviceName = name,
                    DeviceCaption = caption,
                    PnpDeviceId = pnpDeviceId,
                    IsEsp32 = isEsp32,
                    ComPort = ExtractComPort(name, caption)
                };
                
                DeviceConnected?.Invoke(this, deviceInfo);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnDeviceInserted: {ex.Message}");
            // Still raise the event with minimal info
            DeviceConnected?.Invoke(this, new DeviceInfoEventArgs { IsEsp32 = false });
        }
    }

    private void OnDeviceRemoved(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            var name = instance["Name"]?.ToString() ?? "";
            var caption = instance["Caption"]?.ToString() ?? "";
            var pnpDeviceId = instance["PNPDeviceID"]?.ToString() ?? "";
            
            var isComPort = name.Contains("COM") || caption.Contains("COM") || 
                          caption.Contains("Serial") || caption.Contains("USB Serial");
            
            if (isComPort)
            {
                var isEsp32 = IsLikelyEsp32(name, caption, pnpDeviceId);
                var deviceInfo = new DeviceInfoEventArgs
                {
                    DeviceName = name,
                    DeviceCaption = caption,
                    PnpDeviceId = pnpDeviceId,
                    IsEsp32 = isEsp32,
                    ComPort = ExtractComPort(name, caption)
                };
                
                DeviceDisconnected?.Invoke(this, deviceInfo);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnDeviceRemoved: {ex.Message}");
            DeviceDisconnected?.Invoke(this, new DeviceInfoEventArgs { IsEsp32 = false });
        }
    }

    private static string? ExtractComPort(string name, string caption)
    {
        // Try to extract COM port number from device name/caption
        var text = $"{name} {caption}";
        var match = System.Text.RegularExpressions.Regex.Match(text, @"COM(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Value : null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();
    }
}

/// <summary>
/// Event arguments containing device information.
/// </summary>
public class DeviceInfoEventArgs : EventArgs
{
    public string? DeviceName { get; set; }
    public string? DeviceCaption { get; set; }
    public string? PnpDeviceId { get; set; }
    public bool IsEsp32 { get; set; }
    public string? ComPort { get; set; }
}






