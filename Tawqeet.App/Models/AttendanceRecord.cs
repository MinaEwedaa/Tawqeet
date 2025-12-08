namespace Tawqeet.App.Models;

public class AttendanceRecord
{
    public int Id { get; set; }
    public string CardId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string TimeIn { get; set; } = string.Empty;
    public string? TimeOut { get; set; }
    public string Status { get; set; } = string.Empty;
}


