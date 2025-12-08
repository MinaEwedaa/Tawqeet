using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Media;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Tawqeet.App.Models;

namespace Tawqeet.App;

public partial class MainWindow : Window
{
    private readonly RfidReader _rfidReader = new();
    private readonly DispatcherTimer _clockTimer = new();
    private readonly UsbDeviceWatcher _usbWatcher = new();
    private AppSettings _settings = new();
    private readonly ObservableCollection<RecentScanItem> _recentScans = new();
    private readonly ObservableCollection<AttendanceLogItem> _attendanceLogs = new();
    private readonly List<char> _keyboardBuffer = new();
    private DateTime _lastScanTime = DateTime.MinValue;
    private string[] _previousPorts = Array.Empty<string>();

    public MainWindow()
    {
        InitializeComponent();
        
        _rfidReader.CardScanned += RfidReader_CardScanned;
        
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += ClockTimer_Tick;
        _clockTimer.Start();
        
        // Subscribe to USB device events
        _usbWatcher.DeviceConnected += UsbWatcher_DeviceConnected;
        _usbWatcher.DeviceDisconnected += UsbWatcher_DeviceDisconnected;
        
        lstRecentScans.ItemsSource = _recentScans;
        dgLogs.ItemsSource = _attendanceLogs;
        
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSettings();
        RefreshPorts();
        _previousPorts = _rfidReader.GetAvailablePorts();
        LoadTodayData();
        LoadEmployees();
        LoadReport();
        
        dpFilterDate.SelectedDate = DateTime.Today;
        dpReportFrom.SelectedDate = DateTime.Today;
        dpReportTo.SelectedDate = DateTime.Today;
        cmbDepartment.SelectedIndex = 0;
        
        if (_settings.AutoConnectOnStartup && cmbComPort.Items.Count > 0)
        {
            ConnectReader();
        }
        
        // Start monitoring for USB device changes
        _usbWatcher.Start();
        
        UpdateClock();
    }

    private void ClockTimer_Tick(object? sender, EventArgs e)
    {
        UpdateClock();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        txtHeaderDate.Text = now.ToString("dddd, MMMM d, yyyy");
        txtHeaderTime.Text = now.ToString("h:mm:ss tt");
    }

    private void LoadSettings()
    {
        _settings = SettingsService.Load();
    }

    private void RefreshPorts()
    {
        var ports = _rfidReader.GetAvailablePorts();
        var previousSelection = cmbComPort.SelectedItem?.ToString();
        
        cmbComPort.Items.Clear();
        foreach (var port in ports)
        {
            cmbComPort.Items.Add(port);
        }
        
        // Try to restore previous selection, otherwise select first
        if (previousSelection != null && ports.Contains(previousSelection))
        {
            cmbComPort.SelectedItem = previousSelection;
        }
        else if (ports.Length > 0)
        {
            cmbComPort.SelectedIndex = 0;
        }
    }

    private void UsbWatcher_DeviceConnected(object? sender, EventArgs e)
    {
        // Run on UI thread with a small delay to let the port initialize
        Dispatcher.BeginInvoke(async () =>
        {
            await Task.Delay(500); // Wait for COM port to register
            
            var currentPorts = _rfidReader.GetAvailablePorts();
            var newPorts = currentPorts.Except(_previousPorts).ToArray();
            
            RefreshPorts();
            _previousPorts = currentPorts;
            
            // If we found new ports and auto-connect is enabled
            if (newPorts.Length > 0 && _settings.AutoConnectOnDevicePlug && !_rfidReader.IsConnected)
            {
                // Select the new port
                cmbComPort.SelectedItem = newPorts[0];
                
                // Auto-connect to the new reader
                ConnectReader();
                
                // Show notification
                ShowDeviceNotification($"Reader detected on {newPorts[0]} - Connected automatically");
            }
            else if (newPorts.Length > 0)
            {
                ShowDeviceNotification($"New reader detected on {newPorts[0]}");
            }
        });
    }

    private void UsbWatcher_DeviceDisconnected(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(async () =>
        {
            await Task.Delay(300);
            
            var currentPorts = _rfidReader.GetAvailablePorts();
            var removedPorts = _previousPorts.Except(currentPorts).ToArray();
            
            // Check if our connected port was removed
            if (_rfidReader.IsConnected && removedPorts.Length > 0)
            {
                var connectedPort = cmbComPort.SelectedItem?.ToString();
                if (connectedPort != null && removedPorts.Contains(connectedPort))
                {
                    // The connected reader was unplugged
                    _rfidReader.Disconnect();
                    UpdateConnectionStatus(false);
                    ShowDeviceNotification($"Reader on {connectedPort} disconnected");
                }
            }
            
            RefreshPorts();
            _previousPorts = currentPorts;
        });
    }

    private void ShowDeviceNotification(string message)
    {
        // Update the status temporarily
        var originalStatus = txtConnectionStatus.Text;
        var originalBrush = txtConnectionStatus.Foreground;
        
        txtConnectionStatus.Text = message;
        txtConnectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(59, 130, 246)); // Blue
        
        // Reset after 3 seconds
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            UpdateConnectionStatus(_rfidReader.IsConnected, cmbComPort.SelectedItem?.ToString());
        };
        timer.Start();
    }

    private void ConnectReader()
    {
        if (_rfidReader.IsConnected)
        {
            _rfidReader.Disconnect();
            UpdateConnectionStatus(false);
            return;
        }

        var port = cmbComPort.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(port))
        {
            MessageBox.Show("Select a COM port first.", "Connect", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _rfidReader.Connect(port, _settings.BaudRate);
            UpdateConnectionStatus(true, port);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to connect: {ex.Message}", "RFID", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateConnectionStatus(false);
        }
    }

    private void UpdateConnectionStatus(bool connected, string? port = null)
    {
        if (connected)
        {
            txtConnectionStatus.Text = "READER CONNECTED";
            txtConnectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            connectionIcon.Fill = new SolidColorBrush(Color.FromRgb(220, 252, 231));
            btnConnect.Content = "Disconnect";
            btnConnect.Style = (Style)FindResource("DangerButtonStyle");
            
            readerStatusDot.Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            txtReaderStatus.Text = $"Active Reader: {port}";
            borderReaderStatus.Background = new SolidColorBrush(Color.FromRgb(34, 197, 94));
        }
        else
        {
            txtConnectionStatus.Text = "READER DISCONNECTED";
            txtConnectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            connectionIcon.Fill = new SolidColorBrush(Color.FromRgb(243, 244, 246));
            btnConnect.Content = "Connect";
            btnConnect.Style = (Style)FindResource("SuccessButtonStyle");
            
            readerStatusDot.Fill = new SolidColorBrush(Color.FromRgb(107, 114, 128));
            txtReaderStatus.Text = "No Reader";
            borderReaderStatus.Background = new SolidColorBrush(Color.FromRgb(55, 65, 81));
        }
    }

    private void RfidReader_CardScanned(object? sender, string cardId)
    {
        Dispatcher.Invoke(() => HandleScan(cardId));
    }

    private void HandleScan(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            return;

        cardId = cardId.Trim();
        var now = DateTime.Now;
        _lastScanTime = now;
        
        txtCardId.Text = cardId;
        txtScanTime.Text = $"Scan Time: {now:h:mm:ss tt}";
        txtLastScan.Text = $"Last scan: {GetTimeAgo(now)}";
        
        var result = AttendanceLogic.ProcessScan(cardId, now);
        
        if (result.User != null)
        {
            txtEmployeeName.Text = result.User.Name;
            txtDepartment.Text = result.User.Department;
            borderDepartment.Visibility = string.IsNullOrEmpty(result.User.Department) 
                ? Visibility.Collapsed : Visibility.Visible;
        }
        else
        {
            txtEmployeeName.Text = "-";
            borderDepartment.Visibility = Visibility.Collapsed;
        }

        if (result.Success)
        {
            txtScanStatus.Text = result.Status == "IN" ? "Clocked IN ✓" : "Clocked OUT ✗";
            borderScanStatus.Background = result.Status == "IN"
                ? new SolidColorBrush(Color.FromRgb(220, 252, 231))
                : new SolidColorBrush(Color.FromRgb(254, 226, 226));
            txtScanStatus.Foreground = result.Status == "IN"
                ? new SolidColorBrush(Color.FromRgb(34, 197, 94))
                : new SolidColorBrush(Color.FromRgb(239, 68, 68));
            
            // Add to recent scans
            _recentScans.Insert(0, new RecentScanItem
            {
                Time = now,
                Name = result.User?.Name ?? "Unknown",
                Status = result.Status
            });
            
            // Keep only last 20 scans
            while (_recentScans.Count > 20)
                _recentScans.RemoveAt(_recentScans.Count - 1);
            
            if (_settings.PlaySoundOnScan)
            {
                SystemSounds.Asterisk.Play();
            }
            
            LoadTodayData();
        }
        else
        {
            txtScanStatus.Text = result.Message;
            borderScanStatus.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199));
            txtScanStatus.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
        }
    }

    private static string GetTimeAgo(DateTime time)
    {
        var diff = DateTime.Now - time;
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} minutes ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} hours ago";
        return time.ToString("MMM d, h:mm tt");
    }

    private void LoadTodayData()
    {
        var logs = DatabaseHelper.GetLogs(todayOnly: true);
        var summary = DatabaseHelper.GetSummary(DateTime.Today, DateTime.Today);
        var totalUsers = GetTotalUserCount();
        
        txtTotalPresent.Text = summary.totalIns.ToString();
        txtTotalAbsent.Text = Math.Max(0, totalUsers - summary.totalIns).ToString();
        txtPending.Text = "0";
        txtLateArrivals.Text = "0";
        
        // Update header badge
        txtHeaderPresent.Text = $"Today: {summary.totalIns}/{totalUsers} Present";
        
        // Update attendance rate
        var rate = totalUsers > 0 ? (summary.totalIns * 100.0 / totalUsers) : 0;
        txtAttendanceRate.Text = $"{rate:F0}%";
        txtAvgAttendance.Text = $"{rate:F0}%";
        progressAttendance.Width = (progressAttendance.Parent as Border)?.ActualWidth * rate / 100 ?? 0;
        
        // Update attendance logs
        _attendanceLogs.Clear();
        foreach (DataRow row in logs.Rows)
        {
            var user = DatabaseHelper.GetUserByCard(row["CardId"]?.ToString() ?? "");
            var timeIn = row["TimeIn"]?.ToString() ?? "";
            var timeOut = row["TimeOut"]?.ToString() ?? "";
            
            _attendanceLogs.Add(new AttendanceLogItem
            {
                Date = row["Date"]?.ToString() ?? "",
                TimeIn = timeIn,
                TimeOut = timeOut,
                Name = row["Name"]?.ToString() ?? "",
                Department = user?.Department ?? "",
                Status = row["Status"]?.ToString() ?? "",
                DisplayStatus = string.IsNullOrEmpty(timeOut) ? "Present" : "Present"
            });
        }
        
        txtPagination.Text = $"Showing 1 to {_attendanceLogs.Count} of {_attendanceLogs.Count} entries";
    }

    private static int GetTotalUserCount()
    {
        var users = DatabaseHelper.GetUsers();
        return users.Rows.Count;
    }

    private void LoadEmployees(string? search = null)
    {
        var users = DatabaseHelper.GetUsers(search);
        var employees = new List<EmployeeItem>();
        
        foreach (DataRow row in users.Rows)
        {
            employees.Add(new EmployeeItem
            {
                CardId = row["CardId"]?.ToString() ?? "",
                Name = row["Name"]?.ToString() ?? "",
                Department = row["Department"]?.ToString() ?? "",
                Status = row["Status"]?.ToString() ?? ""
            });
        }
        
        dgEmployees.ItemsSource = employees;
    }

    private void LoadReport()
    {
        var start = dpReportFrom.SelectedDate ?? DateTime.Today;
        var end = dpReportTo.SelectedDate ?? DateTime.Today;
        
        var logs = DatabaseHelper.GetLogs(start, end);
        var summary = DatabaseHelper.GetSummary(start, end);
        
        dgReport.ItemsSource = logs.DefaultView;
        txtReportSummary.Text = $"Summary: IN={summary.totalIns}, OUT={summary.totalOuts}, Rows={logs.Rows.Count}";
    }

    // Event Handlers
    private void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        ConnectReader();
    }

    private void BtnRegister_Click(object sender, RoutedEventArgs e)
    {
        var cardId = txtCardId.Text.Trim();
        var name = txtRegisterName.Text.Trim();
        var department = (cmbDepartment.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

        if (string.IsNullOrWhiteSpace(cardId) || cardId == "-")
        {
            MessageBox.Show("Scan a card first to fill Card ID.", "Registration", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Enter a name.", "Registration", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var user = new User
        {
            CardId = cardId,
            Name = name,
            Department = department,
            Status = "Active"
        };

        if (DatabaseHelper.AddUser(user, out var error))
        {
            MessageBox.Show("User registered successfully!", "Registration", MessageBoxButton.OK, MessageBoxImage.Information);
            txtRegisterName.Text = "";
            LoadEmployees();
            LoadTodayData();
        }
        else
        {
            MessageBox.Show(error ?? "Failed to register.", "Registration", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnTestScan_Click(object sender, RoutedEventArgs e)
    {
        var cardId = txtCardId.Text.Trim();
        if (string.IsNullOrWhiteSpace(cardId) || cardId == "-")
        {
            // Generate a test card ID
            cardId = $"TEST{DateTime.Now:HHmmss}";
        }
        HandleScan(cardId);
    }

    private void BtnSearchEmployee_Click(object sender, RoutedEventArgs e)
    {
        LoadEmployees(txtSearchEmployee.Text.Trim());
    }

    private void TxtSearchEmployee_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            LoadEmployees(txtSearchEmployee.Text.Trim());
        }
    }

    private void BtnFilterReport_Click(object sender, RoutedEventArgs e)
    {
        LoadReport();
    }

    private void BtnExportReport_Click(object sender, RoutedEventArgs e)
    {
        ExportToCsv(dgReport);
    }

    private void BtnExportToday_Click(object sender, RoutedEventArgs e)
    {
        ExportToCsv(dgLogs);
    }

    private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
    {
        ExportToCsv(dgLogs);
    }

    private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
    {
        ExportToCsv(dgLogs);
    }

    private void ViewFullLog_Click(object sender, MouseButtonEventArgs e)
    {
        tabMain.SelectedIndex = 0;
    }

    private static void ExportToCsv(DataGrid grid)
    {
        if (grid.ItemsSource == null)
        {
            MessageBox.Show("No data to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"attendance_{DateTime.Now:yyyyMMddHHmmss}.csv"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var lines = new List<string>();
            
            // Get headers
            var headers = grid.Columns.Select(c => c.Header?.ToString() ?? "").ToList();
            lines.Add(string.Join(",", headers));
            
            // Get data
            if (grid.ItemsSource is DataView dataView)
            {
                foreach (DataRowView row in dataView)
                {
                    var values = row.Row.ItemArray.Select(v => $"\"{v?.ToString()?.Replace("\"", "\"\"")}\"");
                    lines.Add(string.Join(",", values));
                }
            }
            else if (grid.ItemsSource is IEnumerable<AttendanceLogItem> logItems)
            {
                foreach (var item in logItems)
                {
                    lines.Add($"\"{item.Date}\",\"{item.TimeIn}\",\"{item.TimeOut}\",\"{item.Name}\",\"{item.Department}\",\"{item.Status}\"");
                }
            }
            
            File.WriteAllLines(dialog.FileName, lines);
            MessageBox.Show("Export completed successfully!", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "Export", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // Handle keyboard input for RFID readers that emulate keyboard
        if (e.Key == Key.Enter)
        {
            if (_keyboardBuffer.Count > 0)
            {
                var cardId = new string(_keyboardBuffer.ToArray());
                _keyboardBuffer.Clear();
                HandleScan(cardId.Trim());
            }
        }
        else if (e.Key >= Key.D0 && e.Key <= Key.D9)
        {
            _keyboardBuffer.Add((char)('0' + (e.Key - Key.D0)));
        }
        else if (e.Key >= Key.A && e.Key <= Key.Z)
        {
            _keyboardBuffer.Add((char)('A' + (e.Key - Key.A)));
        }
        else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
        {
            _keyboardBuffer.Add((char)('0' + (e.Key - Key.NumPad0)));
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _usbWatcher.Dispose();
        _rfidReader.Dispose();
        _clockTimer.Stop();
        base.OnClosing(e);
    }
}

// Data Models for UI
public class RecentScanItem : INotifyPropertyChanged
{
    public DateTime Time { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    
    public string TimeDisplay => $"[{Time:HH:mm}]";
    public string StatusDisplay => Status == "IN" ? "Clocked IN ✓" : "Clocked OUT ✗";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class AttendanceLogItem : INotifyPropertyChanged
{
    public string Date { get; set; } = "";
    public string TimeIn { get; set; } = "";
    public string TimeOut { get; set; } = "";
    public string Name { get; set; } = "";
    public string Department { get; set; } = "";
    public string Status { get; set; } = "";
    public string DisplayStatus { get; set; } = "Present";
    
    public string TotalHours
    {
        get
        {
            if (string.IsNullOrEmpty(TimeIn) || string.IsNullOrEmpty(TimeOut))
                return "-";
            
            if (TimeSpan.TryParse(TimeIn, out var tIn) && TimeSpan.TryParse(TimeOut, out var tOut))
            {
                var diff = tOut - tIn;
                return $"{(int)diff.TotalHours}h {diff.Minutes}m";
            }
            return "-";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class EmployeeItem : INotifyPropertyChanged
{
    public string CardId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Department { get; set; } = "";
    public string Status { get; set; } = "";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

