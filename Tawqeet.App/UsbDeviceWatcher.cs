using System.Management;

namespace Tawqeet.App;

/// <summary>
/// Monitors USB device plug/unplug events using WMI.
/// Raises events when devices are connected or disconnected.
/// </summary>
public class UsbDeviceWatcher : IDisposable
{
    private ManagementEventWatcher? _insertWatcher;
    private ManagementEventWatcher? _removeWatcher;
    private bool _disposed;

    /// <summary>
    /// Raised when a USB device is connected.
    /// </summary>
    public event EventHandler? DeviceConnected;

    /// <summary>
    /// Raised when a USB device is disconnected.
    /// </summary>
    public event EventHandler? DeviceDisconnected;

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
        // Check if it's a COM port device
        try
        {
            var instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            var name = instance["Name"]?.ToString() ?? "";
            var caption = instance["Caption"]?.ToString() ?? "";
            
            // Only trigger for COM port devices (serial/USB-serial adapters)
            if (name.Contains("COM") || caption.Contains("COM") || 
                caption.Contains("Serial") || caption.Contains("USB Serial"))
            {
                DeviceConnected?.Invoke(this, EventArgs.Empty);
            }
        }
        catch
        {
            // Still raise the event even if we can't check the device type
            DeviceConnected?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnDeviceRemoved(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            var name = instance["Name"]?.ToString() ?? "";
            var caption = instance["Caption"]?.ToString() ?? "";
            
            if (name.Contains("COM") || caption.Contains("COM") || 
                caption.Contains("Serial") || caption.Contains("USB Serial"))
            {
                DeviceDisconnected?.Invoke(this, EventArgs.Empty);
            }
        }
        catch
        {
            DeviceDisconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();
    }
}

