using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace FFClient.Avalonia.Views;

public partial class FFClientControl : UserControl
{
    public static readonly StyledProperty<int> VideoWidthProperty =
            AvaloniaProperty.Register<FFClientControl, int>(nameof(VideoWidth), 0);

    public static readonly StyledProperty<int> VideoHeightProperty =
            AvaloniaProperty.Register<FFClientControl, int>(nameof(VideoHeight), 0);

    public static readonly StyledProperty<int> VolumeProperty =
           AvaloniaProperty.Register<FFClientControl, int>(nameof(Volume), 100);

    public static readonly StyledProperty<string?> VideoSourceProperty =
            AvaloniaProperty.Register<FFClientControl, string?>(nameof(VideoSource));

    public static readonly StyledProperty<string?> ClientPathProperty =
            AvaloniaProperty.Register<FFClientControl, string?>(nameof(ClientPath));

    public int VideoWidth
    {
        get { return GetValue(VideoWidthProperty); }
        set { SetValue(VideoWidthProperty, value); }
    }

    public int VideoHeight
    {
        get { return GetValue(VideoHeightProperty); }
        set { SetValue(VideoHeightProperty, value); }
    }

    public int Volume
    {
        get { return GetValue(VolumeProperty); }
        set { SetValue(VolumeProperty, value); }
    }

    public string? VideoSource
    {
        get { return GetValue(VideoSourceProperty); }
        set { SetValue(VideoSourceProperty, value); }
    }

    public string? ClientPath
    {
        get { return GetValue(ClientPathProperty); }
        set { SetValue(ClientPathProperty, value); }
    }

    private VideoDisplay? _handel;
    private WriteableBitmap? _bitmap;
    private TopLevel? level;

    public bool IsStarted { get; private set; }

    public FFClientControl()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == VolumeProperty)
        {
            if (Volume > 100 || Volume < 0)
            {
                Volume = 100;
                return;
            }
            _handel?.SetVolume(Volume);
        }
    }

    public void Play()
    {
        var source = VideoSource;
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentNullException(nameof(VideoSource), "Source is null");
        }

        if (_handel != null)
        {
            throw new Exception("video is started");
        }

        _handel = new(source, VideoWidth, VideoHeight, VideoLoad, ClientPath, Volume);

        level = TopLevel.GetTopLevel(this);

        IsStarted = true;
    }

    public void Stop()
    {
        if (_handel == null)
        {
            throw new Exception("video is not started");
        }

        IsStarted = false;

        _handel.Stop();
        Image1.Source = null;
        _bitmap?.Dispose();
        _handel = null;
    }

    private void VideoLoad()
    {
        _bitmap?.Dispose();
        _bitmap = new(new PixelSize(_handel!.Width, _handel.Height), new Vector(96, 96),
            PixelFormat.Bgra8888, AlphaFormat.Opaque);
        
        Dispatcher.UIThread.Post(() =>
        {
            Image1.Source = _bitmap;
            level?.RequestAnimationFrame((t) =>
            {
                Render();
            });
        });
    }

    private void Render()
    {
        
        unsafe
        {
            using var locked = _bitmap!.Lock();
            Unsafe.CopyBlock(locked.Address.ToPointer(), _handel!.Ptr.ToPointer(),
                    (uint)(_handel.Width * _handel.Height * 4));
        }

        Image1.InvalidateVisual();

        level?.RequestAnimationFrame((t) =>
        {
            Render();
        });
    }
}

public partial class LinuxHook
{
    [LibraryImport("libc", EntryPoint = "shmget")]
    public static partial int Shmget(int key, ulong size, int shmflg);

    [LibraryImport("libc", EntryPoint = "shmat")]
    public static partial IntPtr Shmat(int shm_id, IntPtr shm_addr, int shmflg);

    [LibraryImport("libc", EntryPoint = "shmdt")]
    public static partial int Shmdt(IntPtr shm_addr);

    [LibraryImport("libc", EntryPoint = "shmctl")]
    public static partial int Shmctl(int shmid, int cmd, IntPtr buf);
}

public partial class Win32Hook
{
    public const uint PAGE_READWRITE = 0x04;
    public const uint FILE_MAP_ALL_ACCESS = 0xF001F;
    public const int INVALID_HANDLE_VALUE = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenFileMapping(uint dwDesiredAccess, bool bInheritHandle, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateFileMapping(IntPtr hFile, IntPtr lpFileMappingAttributes,
        uint flProtect, uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess,
       uint dwFileOffsetHigh, uint dwFileOffsetLow, UIntPtr dwNumberOfBytesToMap);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);
}

public class VideoDisplay
{
    public const string Client = "ffclient";

    public static string GetNewFromTag()
    {
        return Guid.NewGuid().ToString().ToLower();
    }

    /// <summary>
    /// 获取所有正在使用的端口
    /// </summary>
    /// <returns>端口列表</returns>
    private static List<int> PortIsUsed()
    {
        var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
        var ipsTCP = ipGlobalProperties.GetActiveTcpListeners();
        var ipsUDP = ipGlobalProperties.GetActiveUdpListeners();
        var tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();

        var allPorts = new List<int>();
        foreach (var ep in ipsTCP) allPorts.Add(ep.Port);
        foreach (var ep in ipsUDP) allPorts.Add(ep.Port);
        foreach (var conn in tcpConnInfoArray) allPorts.Add(conn.LocalEndPoint.Port);

        return allPorts;
    }

    /// <summary>
    /// 获取一个没有使用的端口
    /// </summary>
    /// <returns>端口</returns>
    private static int GetFirstAvailablePort()
    {
        var portUsed = PortIsUsed();
        if (portUsed.Count > 5000)
        {
            return -1;
        }
        var random = new Random();
        do
        {
            int temp = random.Next() % 65535;
            if (!portUsed.Contains(temp))
            {
                return temp;
            }
        }
        while (true);
    }

    private static int ToInt(byte[] temp, int start)
    {
        return temp[start + 3] << 24 | temp[start + 2] << 16 | temp[start + 1] << 8 | temp[start];
    }

    public int Width { get; private set; }
    public int Height { get; private set; }
    public IntPtr Ptr { get; private set; }

    private int _shmid = -1;

    private readonly Process _process;
    private readonly string _mem;
    private readonly Action _action;
    private readonly bool _windows;
    private readonly Socket _socket;

    private Socket? _client;

    private IntPtr _handel;

    private bool _output = true;
    private bool _stop = false;

    public VideoDisplay(string url, int img_width, int img_height, 
        Action action, string? clientpath, int volume)
    {
        _windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        Width = img_width;
        Height = img_height;
        _action = action;

        string path;
        if (!_windows)
        {
            path = $"/tmp/{GetNewFromTag()[..8]}.sock";
            //path = "/tmp/video.sock";
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
            _socket.Bind(new UnixDomainSocketEndPoint(path));
            Console.WriteLine($"Socket start in {path}");
        }
        else
        {
            var port = GetFirstAvailablePort();
            //var port = 666;
            path = port.ToString();
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            _socket.Bind(new IPEndPoint(IPAddress.Any, port));
            Console.WriteLine($"Socket start in :{port}");
        }
        _socket.Listen();
        _socket.BeginAccept(Accept, null);
        ProcessStartInfo info;
        string pex = _windows ? ".exe" : "";
        if (string.IsNullOrWhiteSpace(clientpath))
        {
            info = new ProcessStartInfo(Client + pex)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
        }
        else
        {
            info = new ProcessStartInfo(Path.GetFullPath(clientpath + "/" + Client + pex))
            { 
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
        }

        _mem = new Random().Next(65535).ToString();
        //mem_name = "1234";

        _process = new Process
        {
            StartInfo = info,
            EnableRaisingEvents = true,
        };
        info.ArgumentList.Add("-input");
        info.ArgumentList.Add(url);
        info.ArgumentList.Add(Width.ToString());
        info.ArgumentList.Add(Height.ToString());
        info.ArgumentList.Add(path);
        info.ArgumentList.Add(_mem);
        info.ArgumentList.Add("-volume");
        info.ArgumentList.Add(volume.ToString());
        _process.Exited += Process1_Exited;
        _process.OutputDataReceived += Process_OutputDataReceived;
        _process.ErrorDataReceived += Process_ErrorDataReceived;
        _process.Start();
        _process.BeginErrorReadLine();
        _process.BeginOutputReadLine();
    }

    private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (_output)
        {
            Console.WriteLine(e.Data);
        }
    }

    private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (_output)
        {
            Console.WriteLine(e.Data);
        }
    }

    private void Process1_Exited(object? sender, EventArgs e)
    {
        
    }

    private void Accept(IAsyncResult result)
    {
        try
        {
            _client = _socket.EndAccept(result);
            if (_stop)
            {
                return;
            }
            _socket.BeginAccept(Accept, null);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            if (!_stop)
            {
                _socket.BeginAccept(Accept, null);
            }
            return;
        }
        new Thread(() =>
        {
            byte[] temp = new byte[32];
            try
            {
                _client.Receive(temp, 16, SocketFlags.None);
                if (temp[0] == 0xff && temp[1] == 0x54)
                {
                    _output = false;

                    Width = ToInt(temp, 2);
                    Height = ToInt(temp, 6);
                    _shmid = ToInt(temp, 10);

                    Console.WriteLine($"Get decoder {Width}x{Height} shmid:{_shmid}");

                    if (_windows)
                    {
                        _handel = Win32Hook.OpenFileMapping(Win32Hook.FILE_MAP_ALL_ACCESS, false, _mem);
                        Ptr = Win32Hook.MapViewOfFile(_handel, Win32Hook.FILE_MAP_ALL_ACCESS, 0, 0, 0);
                    }
                    else
                    {
                        Ptr = LinuxHook.Shmat(_shmid, 0, 0);
                    }

                    temp[0] = 0xcf;
                    temp[1] = 0x1f;
                    temp[2] = 0xe4;
                    temp[3] = 0x98;
                    _client.Send(temp, 4, SocketFlags.None);

                    _action();
                }
            }
            catch
            {
                Close();
                if (!_stop)
                {
                    _socket.BeginAccept(Accept, null);
                }
            }
        }).Start();
    }

    private void Close()
    {
        if (_windows)
        {
            if (Ptr != IntPtr.Zero)
            {
                Win32Hook.UnmapViewOfFile(Ptr);

                Ptr = IntPtr.Zero;
            }
            if (_handel != 0)
            {
                Win32Hook.CloseHandle(_handel);

                _handel = 0;
            }
        }
        else
        {
            if (Ptr != IntPtr.Zero)
            {
                LinuxHook.Shmdt(Ptr);

                Ptr = IntPtr.Zero;
            }
            if (_shmid != -1)
            {
                LinuxHook.Shmctl(_shmid, 0, 0);

                _shmid = 0;
            }
        }
    }

    public void Stop()
    {
        _stop = true;

        Close();

        var temp = new byte[4];
        temp[0] = 0xcf;
        temp[1] = 0x1f;
        temp[2] = 0x98;
        temp[3] = 0x31;

        try
        {
            if (_client != null && _client.Connected)
            {
                _client.Send(temp, 4, SocketFlags.None);
                _client.Close();
                _client.Dispose();
            }

            _socket.Close();
            _socket.Dispose();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public void SetVolume(int volume)
    {
        if (_client != null && _client.Connected)
        {
            var temp = new byte[4];
            temp[0] = 0x35;
            temp[1] = 0x67;
            temp[2] = 0xA7;
            temp[3] = (byte)volume;

            _client.Send(temp, 4, SocketFlags.None);
        }
    }
}
