namespace Tawqeet.App.Models;

public class AppSettings
{
    public bool AutoConnectOnStartup { get; set; }
    public bool AutoConnectOnDevicePlug { get; set; } = true;
    public bool PlaySoundOnScan { get; set; } = true;
    public string DateFormat { get; set; } = "yyyy-MM-dd";
    public int BaudRate { get; set; } = 9600;
}


