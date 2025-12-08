using Tawqeet.App.Models;

namespace Tawqeet.App;

public class AttendanceResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public AttendanceRecord? Record { get; init; }
    public User? User { get; init; }
}

public static class AttendanceLogic
{
    public static AttendanceResult ProcessScan(string cardId, DateTime timestamp)
    {
        if (string.IsNullOrWhiteSpace(cardId))
        {
            return new AttendanceResult { Success = false, Message = "Empty card id." };
        }

        var user = DatabaseHelper.GetUserByCard(cardId.Trim());
        if (user is null)
        {
            return new AttendanceResult { Success = false, Message = "Unknown card.", Status = "UNKNOWN" };
        }

        if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return new AttendanceResult { Success = false, Message = "Card inactive.", Status = "INACTIVE", User = user };
        }

        var todayLast = DatabaseHelper.GetLastLogForCard(cardId, timestamp.Date);
        var dateString = timestamp.ToString("yyyy-MM-dd");
        var timeString = timestamp.ToString("HH:mm:ss");

        if (todayLast is null || todayLast.Status == "OUT")
        {
            var record = new AttendanceRecord
            {
                CardId = cardId,
                Name = user.Name,
                Date = dateString,
                TimeIn = timeString,
                Status = "IN"
            };
            record.Id = DatabaseHelper.InsertAttendance(record);
            return new AttendanceResult
            {
                Success = true,
                Message = "Clocked IN",
                Status = "IN",
                Record = record,
                User = user
            };
        }

        if (todayLast.Status == "IN" && string.IsNullOrWhiteSpace(todayLast.TimeOut))
        {
            DatabaseHelper.UpdateTimeOut(todayLast.Id, timeString);
            todayLast.TimeOut = timeString;
            todayLast.Status = "OUT";
            return new AttendanceResult
            {
                Success = true,
                Message = "Clocked OUT",
                Status = "OUT",
                Record = todayLast,
                User = user
            };
        }

        // Safety fallback: create new IN record
        var newRecord = new AttendanceRecord
        {
            CardId = cardId,
            Name = user.Name,
            Date = dateString,
            TimeIn = timeString,
            Status = "IN"
        };
        newRecord.Id = DatabaseHelper.InsertAttendance(newRecord);
        return new AttendanceResult
        {
            Success = true,
            Message = "Clocked IN",
            Status = "IN",
            Record = newRecord,
            User = user
        };
    }
}


