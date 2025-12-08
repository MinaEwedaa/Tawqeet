using System.IO;
using Microsoft.Data.Sqlite;
using System.Data;
using Tawqeet.App.Models;

namespace Tawqeet.App;

public static class DatabaseHelper
{
    private static readonly string DbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rfid.db");
    private static readonly string ConnectionString = $"Data Source={DbPath}";

    public static void Initialize()
    {
        if (!File.Exists(DbPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
        }

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var createUsers = connection.CreateCommand();
        createUsers.CommandText =
            """
            CREATE TABLE IF NOT EXISTS users (
                card_id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                department TEXT,
                status TEXT NOT NULL
            );
            """;
        createUsers.ExecuteNonQuery();

        using var createAttendance = connection.CreateCommand();
        createAttendance.CommandText =
            """
            CREATE TABLE IF NOT EXISTS attendance_logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                card_id TEXT NOT NULL,
                name TEXT NOT NULL,
                date TEXT NOT NULL,
                time_in TEXT NOT NULL,
                time_out TEXT,
                status TEXT NOT NULL
            );
            """;
        createAttendance.ExecuteNonQuery();
    }

    public static bool AddUser(User user, out string? error)
    {
        error = null;
        try
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO users (card_id, name, department, status)
                VALUES ($cardId, $name, $department, $status);
                """;
            cmd.Parameters.AddWithValue("$cardId", user.CardId);
            cmd.Parameters.AddWithValue("$name", user.Name);
            cmd.Parameters.AddWithValue("$department", user.Department);
            cmd.Parameters.AddWithValue("$status", user.Status);
            cmd.ExecuteNonQuery();
            return true;
        }
        catch (SqliteException ex)
        {
            error = ex.SqliteErrorCode == 19 ? "Card already registered." : ex.Message;
            return false;
        }
    }

    public static User? GetUserByCard(string cardId)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT card_id, name, department, status
            FROM users
            WHERE card_id = $cardId;
            """;
        cmd.Parameters.AddWithValue("$cardId", cardId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new User
        {
            CardId = reader.GetString(0),
            Name = reader.GetString(1),
            Department = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Status = reader.GetString(3)
        };
    }

    public static DataTable GetUsers(string? search = null)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT card_id AS CardId, name AS Name, department AS Department, status AS Status
            FROM users
            WHERE ($search IS NULL OR name LIKE $searchLike OR card_id LIKE $searchLike)
            ORDER BY name;
            """;
        // Explicitly set parameter values to avoid "Value must be set" when null
        cmd.Parameters.AddWithValue("$search", (object?)search ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$searchLike", search is null ? DBNull.Value : $"%{search}%");
        var table = new DataTable();
        using var reader = cmd.ExecuteReader();
        table.Load(reader);
        return table;
    }

    public static AttendanceRecord? GetLastLogForCard(string cardId, DateTime date)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, card_id, name, date, time_in, time_out, status
            FROM attendance_logs
            WHERE card_id = $cardId AND date = $date
            ORDER BY id DESC
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$cardId", cardId);
        cmd.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd"));
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new AttendanceRecord
        {
            Id = reader.GetInt32(0),
            CardId = reader.GetString(1),
            Name = reader.GetString(2),
            Date = reader.GetString(3),
            TimeIn = reader.GetString(4),
            TimeOut = reader.IsDBNull(5) ? null : reader.GetString(5),
            Status = reader.GetString(6)
        };
    }

    public static int InsertAttendance(AttendanceRecord record)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO attendance_logs (card_id, name, date, time_in, time_out, status)
            VALUES ($cardId, $name, $date, $timeIn, $timeOut, $status);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$cardId", record.CardId);
        cmd.Parameters.AddWithValue("$name", record.Name);
        cmd.Parameters.AddWithValue("$date", record.Date);
        cmd.Parameters.AddWithValue("$timeIn", record.TimeIn);
        cmd.Parameters.AddWithValue("$timeOut", (object?)record.TimeOut ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$status", record.Status);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public static void UpdateTimeOut(int logId, string timeOut)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            UPDATE attendance_logs
            SET time_out = $timeOut, status = 'OUT'
            WHERE id = $id;
            """;
        cmd.Parameters.AddWithValue("$timeOut", timeOut);
        cmd.Parameters.AddWithValue("$id", logId);
        cmd.ExecuteNonQuery();
    }

    public static DataTable GetLogs(DateTime? startDate = null, DateTime? endDate = null, bool todayOnly = false)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        if (todayOnly)
        {
            cmd.CommandText =
                """
                SELECT date AS Date, time_in AS TimeIn, time_out AS TimeOut, status AS Status, card_id AS CardId, name AS Name
                FROM attendance_logs
                WHERE date = $today
                ORDER BY id DESC;
                """;
            cmd.Parameters.AddWithValue("$today", DateTime.Now.ToString("yyyy-MM-dd"));
        }
        else
        {
            cmd.CommandText =
                """
                SELECT date AS Date, time_in AS TimeIn, time_out AS TimeOut, status AS Status, card_id AS CardId, name AS Name
                FROM attendance_logs
                WHERE ($start IS NULL OR date >= $start) AND ($end IS NULL OR date <= $end)
                ORDER BY date DESC, time_in DESC;
                """;
            cmd.Parameters.AddWithValue("$start", startDate?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$end", endDate?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
        }

        var table = new DataTable();
        using var reader = cmd.ExecuteReader();
        table.Load(reader);
        return table;
    }

    public static (int totalIns, int totalOuts) GetSummary(DateTime? startDate, DateTime? endDate)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT
                SUM(CASE WHEN status = 'IN' THEN 1 ELSE 0 END) AS totalIns,
                SUM(CASE WHEN status = 'OUT' THEN 1 ELSE 0 END) AS totalOuts
            FROM attendance_logs
            WHERE ($start IS NULL OR date >= $start) AND ($end IS NULL OR date <= $end);
            """;
        cmd.Parameters.AddWithValue("$start", startDate?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$end", endDate?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return (0, 0);
        }

        return (reader.IsDBNull(0) ? 0 : reader.GetInt32(0), reader.IsDBNull(1) ? 0 : reader.GetInt32(1));
    }
}

