namespace Tawqeet.App.Models;

public class AppSettings
{
    public bool AutoConnectOnStartup { get; set; }
    public bool AutoConnectOnDevicePlug { get; set; } = true;
    public bool PlaySoundOnScan { get; set; } = true;
    public string DateFormat { get; set; } = "yyyy-MM-dd";
    public int BaudRate { get; set; } = 115200; // Default to ESP32 common baud rate
    public bool IsEsp32Device { get; set; } = true; // Assume ESP32 by default
    public string? LastConnectedPort { get; set; } // Remember last connected port
}


