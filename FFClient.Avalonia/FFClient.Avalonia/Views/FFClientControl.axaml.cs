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

    private VideoDisplay _handel;
    private WriteableBitmap _bitmap;
    private TopLevel? level;

    public FFClientControl()
    {
        InitializeComponent();
    }

    public void Play()
    {
        var source = VideoSource;
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentNullException(nameof(VideoSource), "Source is null");
        }

        _handel = new(source, VideoWidth, VideoHeight, VideoUpdate, ClientPath);

        level = TopLevel.GetTopLevel(this);
    }

    public void Stop()
    {
        _handel?.Stop();
        Image1.Source = null;
        _bitmap?.Dispose();
        _handel = null!;
    }

    private bool Run;
    private int VWidth;
    private int VHeight;
    private IntPtr Ptr;

    private void VideoUpdate(int width, int height, IntPtr ptr)
    {
        Ptr = ptr;
        VWidth = width;
        VHeight = height;

        Run = true;

        Dispatcher.UIThread.Post(() =>
        {
            level?.RequestAnimationFrame((t) =>
            {
                Render();
            });
        });
    }

    private void Render()
    {
        if (!Run)
        {
            return;
        }

        if (Ptr == 0 || _bitmap == null || _bitmap.Size.Width != VWidth
            || _bitmap.Size.Height != VHeight)
        {
            Image1.Source = null;
            _bitmap?.Dispose();
            _bitmap = new(new PixelSize(VWidth, VHeight), new Vector(96, 96),
                PixelFormat.Bgra8888, AlphaFormat.Opaque);
            Image1.Source = _bitmap;
        }
        if (Ptr != 0)
        {
            using var locked = _bitmap.Lock();
            unsafe
            {
                Unsafe.CopyBlock(locked.Address.ToPointer(), Ptr.ToPointer(),
                        (uint)(VWidth * VHeight * 4));
            }

            Image1.InvalidateVisual();
        }

        level?.RequestAnimationFrame((t) =>
        {
            Render();
        });
    }
}

public class LinuxHook
{
    [DllImport("libc", EntryPoint = "shmget")]
    public static extern int Shmget(int key, ulong size, int shmflg);

    [DllImport("libc", EntryPoint = "shmat")]
    public static extern IntPtr Shmat(int shm_id, IntPtr shm_addr, int shmflg);

    [DllImport("libc", EntryPoint = "shmdt")]
    public static extern int Shmdt(IntPtr shm_addr);

    [DllImport("libc", EntryPoint = "shmctl")]
    public static extern int Shmctl(int shmid, int cmd, IntPtr buf);
}

public class Win32Hook
{
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenFileMapping(uint dwDesiredAccess, bool bInheritHandle, string lpName);

    // 定义CreateFileMapping函数
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateFileMapping(IntPtr hFile, IntPtr lpFileMappingAttributes, 
        uint flProtect, uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);

    // 定义MapViewOfFile函数
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess,
        uint dwFileOffsetHigh, uint dwFileOffsetLow, UIntPtr dwNumberOfBytesToMap);

    // 定义MapViewOfFile函数
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

    // 定义CloseHandle函数
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr hObject);

    public const uint PAGE_READWRITE = 0x04;
    public const uint FILE_MAP_ALL_ACCESS = 0xF001F;
    public const int INVALID_HANDLE_VALUE = -1;
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

    private int _width;
    private int _height;
    private IntPtr _handel;

    private Process _process;
    
    private Socket _socket;
    private int shmid = -1;
    private string mem_name;
    private Action<int, int, IntPtr> _action;
    private bool windows;
  
    private bool output = true;
    private bool stop = false;

    public VideoDisplay(string url, int img_width,
        int img_height, Action<int, int, IntPtr> action, string? clientpath)
    {
        windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        _width = img_width;
        _height = img_height;
        _action = action;

        string path;
        if (!windows)
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
        string pex = windows ? ".exe" : "";
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

        mem_name = new Random().Next(65535).ToString();
        //mem_name = "1234";

        _process = new Process
        {
            StartInfo = info,
            EnableRaisingEvents = true,
        };
        info.ArgumentList.Add("-input");
        info.ArgumentList.Add(url);
        info.ArgumentList.Add(_width.ToString());
        info.ArgumentList.Add(_height.ToString());
        info.ArgumentList.Add(path);
        info.ArgumentList.Add(mem_name);
        _process.Exited += Process1_Exited;
        _process.OutputDataReceived += Process_OutputDataReceived;
        _process.ErrorDataReceived += Process_ErrorDataReceived;
        _process.Start();
        _process.BeginErrorReadLine();
        _process.BeginOutputReadLine();
    }

    private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (output)
        {
            Console.WriteLine(e.Data);
        }
    }

    private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (output)
        {
            Console.WriteLine(e.Data);
        }
    }

    private void Process1_Exited(object? sender, EventArgs e)
    {
        
    }

    private int ToInt(byte[] temp, int start)
    {
        return temp[start + 3] << 24 | temp[start + 2] << 16 | temp[start + 1] << 8 | temp[start];
    }

    private void Accept(IAsyncResult result)
    {
        var client = _socket.EndAccept(result);
        if (client == null)
        {
            _socket.BeginAccept(Accept, null);
            return;
        }

        new Thread(() =>
        {
            byte[] temp = new byte[32];
            IntPtr ptr = 0;
            try
            {
                client.Receive(temp, 16, SocketFlags.None);
                if (temp[0] == 0xff && temp[1] == 0x54)
                {
                    output = false;

                    _width = ToInt(temp, 2);
                    _height = ToInt(temp, 6);
                    shmid = ToInt(temp, 10);

                    Console.WriteLine($"Get decoder {_width}x{_height} shmid:{shmid}");

                    _action(_width, _height, 0);

                    if (windows)
                    {
                        _handel = Win32Hook.OpenFileMapping(Win32Hook.FILE_MAP_ALL_ACCESS, false, mem_name);
                        ptr = Win32Hook.MapViewOfFile(_handel, Win32Hook.FILE_MAP_ALL_ACCESS, 0, 0, 0);
                    }
                    else
                    {
                        ptr = LinuxHook.Shmat(shmid, 0, 0);
                    }

                    temp[0] = 0xcf;
                    temp[1] = 0x1f;
                    temp[2] = 0xe4;
                    temp[3] = 0x98;
                    client.Send(temp, 4, SocketFlags.None);
                }

                while (true)
                {
                    _action(_width, _height, ptr);
                    Thread.Sleep(20);
                    if (stop)
                    {
                        temp[0] = 0xcf;
                        temp[1] = 0x1f;
                        temp[2] = 0x98;
                        temp[3] = 0x31;

                        client.Send(temp, 4, SocketFlags.None);
                        client.Close();
                        client.Dispose();

                        return;
                    }
                }
            }
            catch
            {
                if (windows)
                {
                    if (ptr != IntPtr.Zero)
                    {
                        Win32Hook.UnmapViewOfFile(ptr);

                        ptr = IntPtr.Zero;
                    }
                    if (_handel != 0)
                    {
                        Win32Hook.CloseHandle(_handel);

                        _handel = 0;
                    }
                }
                else
                {
                    if (ptr != IntPtr.Zero)
                    {
                        LinuxHook.Shmdt(ptr);

                        ptr = IntPtr.Zero;
                    }
                    if (shmid != -1)
                    {
                        LinuxHook.Shmctl(shmid, 0, 0);

                        shmid = 0;
                    }
                }
                if (!stop)
                {
                    _socket.BeginAccept(Accept, null);
                }
            }
        }).Start();
    }

    public void Stop()
    {
        stop = true;
        _socket.Close();
        _socket.Dispose();
    }
}
