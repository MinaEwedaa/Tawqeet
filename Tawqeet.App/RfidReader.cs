using System.IO.Ports;

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


