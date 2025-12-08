# Tawqeet - RFID Attendance System

A modern WPF-based employee attendance tracking system that uses RFID card readers for clock-in/clock-out functionality.

![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4)
![WPF](https://img.shields.io/badge/WPF-Windows-0078D4)
![SQLite](https://img.shields.io/badge/SQLite-Database-003B57)

## Features

- **RFID Card Integration** - Supports serial port RFID readers with automatic USB device detection
- **Real-time Attendance Tracking** - Instant clock-in/clock-out with visual feedback
- **Employee Management** - Register, search, and manage employee records
- **Modern Dashboard** - Live statistics showing present, absent, late arrivals, and attendance rates
- **Reports & Analytics** - Filter attendance logs by date range, department, and status
- **Export Functionality** - Export reports to CSV format
- **Auto-Connect** - Automatically connects to RFID readers when plugged in
- **Keyboard Mode Support** - Works with RFID readers that emulate keyboard input

## Screenshots

The application features a clean, modern interface with:
- Header with real-time clock and reader status
- Card reader status panel with COM port selection
- Current scan display showing employee details
- Today's attendance statistics with visual progress
- Recent scans feed
- Quick action buttons for common tasks
- Weekly overview chart
- Tabbed interface for Attendance Logs, Employee Management, and Reports

## Requirements

- Windows 10/11
- .NET 8.0 Runtime
- RFID card reader (serial/COM port based)

## Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/Tawqeet.git
   ```

2. Open `Tawqeet.sln` in Visual Studio 2022

3. Restore NuGet packages:
   ```bash
   dotnet restore
   ```

4. Build and run:
   ```bash
   dotnet run --project Tawqeet.App
   ```

## Usage

### Connecting a Reader

1. Plug in your RFID reader
2. Select the COM port from the dropdown
3. Click **Connect** (or enable auto-connect in settings)

### Registering Employees

1. Scan an unregistered card
2. Expand the "Register New Card" panel
3. Enter the employee name and department
4. Click **Register**

### Tracking Attendance

- First scan of the day = **Clock IN**
- Second scan = **Clock OUT**
- Subsequent scans alternate between IN/OUT

### Exporting Data

- Use the **Export Today's Report** quick action
- Or navigate to the Attendance Logs tab and click **Excel** or **CSV**

## Project Structure

```
Tawqeet.App/
├── App.xaml              # Application resources and styles
├── MainWindow.xaml       # Main UI layout
├── MainWindow.xaml.cs    # Main window logic
├── RfidReader.cs         # Serial port RFID reader handler
├── DatabaseHelper.cs     # SQLite database operations
├── AttendanceLogic.cs    # Clock in/out business logic
├── SettingsService.cs    # Application settings persistence
├── UsbDeviceWatcher.cs   # USB device plug/unplug detection
├── Converters.cs         # XAML value converters
└── Models/
    ├── User.cs           # User entity
    ├── AttendanceRecord.cs # Attendance log entity
    └── AppSettings.cs    # Settings model
```

## Database

The application uses SQLite with two main tables:

- **users** - Employee card registrations
- **attendance_logs** - Clock in/out records

Database file is stored at: `bin/Debug/net8.0-windows/rfid.db`

## Dependencies

- [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite) - SQLite database access
- [System.IO.Ports](https://www.nuget.org/packages/System.IO.Ports) - Serial port communication
- [System.Management](https://www.nuget.org/packages/System.Management) - USB device detection

## License

MIT License - feel free to use and modify for your needs.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

