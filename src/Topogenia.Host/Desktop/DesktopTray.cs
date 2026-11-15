// DesktopTray.cs
// In desktop mode without a console: the icon of the application near the clock, with « Open » and « Quit », and the clean stop at the end of the session of Windows (F-69, ADR 090).

using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;
using Topogenia.Host.Composition;
using Topogenia.Host.Startup;

namespace Topogenia.Host.Desktop;

/// <summary>A choice of the menu of the icon.</summary>
public enum TrayCommand
{
    /// <summary>Opens the browser on the application, with a new launch token, as a second start does (spec 6.1).</summary>
    Open = 1,

    /// <summary>Stops the application, as the command « Quit » of the interface (spec 5.2).</summary>
    Quit = 2,
}

/// <summary>
/// In desktop mode without a console (<see cref="StartupConsole.Windowed"/>, F-69, ADR 090): an icon in the notification
/// area of the taskbar, near the clock, named after the product. A click opens its menu: « Open » (the default) opens the
/// browser on the application with a new launch token, « Quit » stops it. The sentences kept by the start (a folder
/// readable by others, a restored backup) are shown once in a box of Windows. At the end of the session of Windows, the
/// application stops cleanly before the process ends (the data file closed, ADR 029). The icon lives on a thread of its
/// own, with a hidden window that receives its messages; the icon comes back when Explorer restarts. Without a shell to
/// hold it, the application runs on and « Quit » stays in the interface.
/// </summary>
public sealed partial class DesktopTrayService(IHostApplicationLifetime lifetime, IOptions<ProductOptions> options, DesktopAddress address,
    DesktopSession session, IBrowserLauncher browser, StartupConsole console, ILogger<DesktopTrayService> logger) : IHostedService, IDisposable
{
    private const uint Callback = 0x8001;
    private static readonly TimeSpan EndOfSession = TimeSpan.FromSeconds(10);

    private readonly ManualResetEventSlim ready = new();
    private readonly ManualResetEventSlim stopped = new();
    private Thread? thread;
    private IntPtr window;
    private IntPtr icon;
    private bool ownIcon;
    private uint taskbarCreated;
    private volatile bool ending;

    /// <summary>True once the icon is in the notification area.</summary>
    public bool Shown { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!console.Windowed || options.Value.Mode != HostMode.Desktop || !OperatingSystem.IsWindows())
            return Task.CompletedTask;
        lifetime.ApplicationStarted.Register(() =>
        {
            thread = new Thread(Run) { IsBackground = true, Name = "Notification area" };
            if (OperatingSystem.IsWindows())
                thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            ready.Wait(TimeSpan.FromSeconds(10));
        });
        lifetime.ApplicationStopped.Register(stopped.Set);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (thread is { } running && window != IntPtr.Zero)
        {
            NativeMethods.PostMessageW(window, NativeMethods.Close, IntPtr.Zero, IntPtr.Zero);
            // At the end of the session, the thread of the icon waits for the stop itself.
            if (!ending)
                running.Join(TimeSpan.FromSeconds(5));
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        ready.Dispose();
        stopped.Dispose();
    }

    /// <summary>Carries out a choice of the menu.</summary>
    public void Choose(TrayCommand command)
    {
        switch (command)
        {
            case TrayCommand.Open:
                browser.Open(address.LaunchAddress(session.Renew()));
                break;
            case TrayCommand.Quit:
                lifetime.StopApplication();
                break;
        }
    }

    /// <summary>The text of a choice in the language of the start.</summary>
    public string TextOf(TrayCommand command) =>
        console.Messages.Format(command == TrayCommand.Open ? "ICONE_OUVRIR" : "ICONE_QUITTER", console.Language,
            new Dictionary<string, object?> { ["product"] = ConsoleWindow.Title });

    private void Run()
    {
        var procedure = new NativeMethods.WindowProcedure(Receive);
        var instance = NativeMethods.GetModuleHandleW(null);
        var name = $"{ConsoleWindow.Title}.Tray.{Environment.ProcessId}";
        try
        {
            var windowClass = new NativeMethods.WindowClass
            {
                Size = Marshal.SizeOf<NativeMethods.WindowClass>(),
                Procedure = procedure,
                Instance = instance,
                ClassName = name,
            };
            if (NativeMethods.RegisterClassExW(ref windowClass) == 0)
            {
                LogNoIcon(logger, "class");
                return;
            }
            window = NativeMethods.CreateWindowExW(0, name, ConsoleWindow.Title, 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
            if (window == IntPtr.Zero)
            {
                LogNoIcon(logger, "window");
                return;
            }
            (icon, ownIcon) = Icon();
            taskbarCreated = NativeMethods.RegisterWindowMessageW("TaskbarCreated");
            Shown = Add();
            if (Shown)
                LogShown(logger);
            else
                LogNoIcon(logger, "shell");
            ready.Set();
            if (console.Window is { } notes)
                notes.Show(console.TakeKept(), problem: false);
            while (NativeMethods.GetMessageW(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                NativeMethods.TranslateMessage(ref message);
                NativeMethods.DispatchMessageW(ref message);
            }
        }
        finally
        {
            ready.Set();
            if (ownIcon)
                NativeMethods.DestroyIcon(icon);
            NativeMethods.UnregisterClassW(name, instance);
            GC.KeepAlive(procedure);
        }
    }

    /// <summary>The small icon of the executable (destroyed at the end), else the shared icon of an application of Windows.</summary>
    private static (IntPtr Icon, bool Own) Icon()
    {
        if (Environment.ProcessPath is { } path && NativeMethods.ExtractIconExW(path, 0, IntPtr.Zero, out var small, 1) > 0 && small != IntPtr.Zero)
            return (small, true);
        return (NativeMethods.LoadIconW(IntPtr.Zero, new IntPtr(32512)), false);
    }

    private NativeMethods.NotifyIconData Data(int flags) => new()
    {
        Size = Marshal.SizeOf<NativeMethods.NotifyIconData>(),
        Window = window,
        Id = 1,
        Flags = flags,
        CallbackMessage = (int)Callback,
        Icon = icon,
        Tip = ConsoleWindow.Title,
        Info = string.Empty,
        InfoTitle = string.Empty,
    };

    private bool Add()
    {
        var data = Data(NativeMethods.IconMessage | NativeMethods.IconImage | NativeMethods.IconTip | NativeMethods.IconShowTip);
        if (!NativeMethods.Shell_NotifyIconW(NativeMethods.AddIcon, ref data))
            return false;
        data.Version = NativeMethods.Version4;
        return NativeMethods.Shell_NotifyIconW(NativeMethods.SetVersion, ref data);
    }

    private IntPtr Receive(IntPtr target, uint message, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            switch (message)
            {
                case Callback:
                    var happened = (uint)(lParam.ToInt64() & 0xFFFF);
                    if (happened is NativeMethods.ContextMenu or NativeMethods.Select or NativeMethods.KeySelect)
                        Menu();
                    return IntPtr.Zero;
                case NativeMethods.QueryEndSession:
                    return new IntPtr(1);
                case NativeMethods.EndSession:
                    if (wParam != IntPtr.Zero)
                    {
                        ending = true;
                        lifetime.StopApplication();
                        stopped.Wait(EndOfSession);
                    }
                    return IntPtr.Zero;
                case NativeMethods.Close:
                    var data = Data(0);
                    NativeMethods.Shell_NotifyIconW(NativeMethods.DeleteIcon, ref data);
                    Shown = false;
                    NativeMethods.DestroyWindow(target);
                    return IntPtr.Zero;
                case NativeMethods.Destroy:
                    window = IntPtr.Zero;
                    NativeMethods.PostQuitMessage(0);
                    return IntPtr.Zero;
                default:
                    if (message == taskbarCreated && taskbarCreated != 0)
                        Shown = Add();
                    return NativeMethods.DefWindowProcW(target, message, wParam, lParam);
            }
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            // A failure never leaves the procedure of the window (it would end the process): the log keeps its type.
            LogFailed(logger, failure.GetType().FullName ?? failure.GetType().Name);
            return IntPtr.Zero;
        }
    }

    private void Menu()
    {
        var menu = NativeMethods.CreatePopupMenu();
        try
        {
            NativeMethods.AppendMenuW(menu, NativeMethods.MenuString, (UIntPtr)(uint)TrayCommand.Open, TextOf(TrayCommand.Open));
            NativeMethods.AppendMenuW(menu, NativeMethods.MenuSeparator, UIntPtr.Zero, null);
            NativeMethods.AppendMenuW(menu, NativeMethods.MenuString, (UIntPtr)(uint)TrayCommand.Quit, TextOf(TrayCommand.Quit));
            NativeMethods.SetMenuDefaultItem(menu, (uint)TrayCommand.Open, 0);
            NativeMethods.GetCursorPos(out var point);
            // The menu closes when the person clicks elsewhere only if its window is in the foreground (Shell_NotifyIcon).
            NativeMethods.SetForegroundWindow(window);
            var chosen = NativeMethods.TrackPopupMenuEx(menu, NativeMethods.ReturnCommand | NativeMethods.NoNotify | NativeMethods.RightButton,
                point.X, point.Y, window, IntPtr.Zero);
            NativeMethods.PostMessageW(window, 0, IntPtr.Zero, IntPtr.Zero);
            if (chosen is (int)TrayCommand.Open or (int)TrayCommand.Quit)
                Choose((TrayCommand)chosen);
        }
        finally
        {
            NativeMethods.DestroyMenu(menu);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The icon of the notification area is shown: the desktop mode runs without a console.")]
    private static partial void LogShown(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The icon of the notification area is not shown ({Step}); « Quit » stays in the interface.")]
    private static partial void LogNoIcon(ILogger logger, string step);

    [LoggerMessage(Level = LogLevel.Error, Message = "A message of the icon of the notification area failed: {Type}.")]
    private static partial void LogFailed(ILogger logger, string type);

    private static class NativeMethods
    {
        public const uint Destroy = 0x0002;
        public const uint Close = 0x0010;
        public const uint QueryEndSession = 0x0011;
        public const uint EndSession = 0x0016;
        public const uint ContextMenu = 0x007B;
        public const uint Select = 0x0400;
        public const uint KeySelect = 0x0401;
        public const uint AddIcon = 0;
        public const uint DeleteIcon = 2;
        public const uint SetVersion = 4;
        public const int Version4 = 4;
        public const int IconMessage = 0x01;
        public const int IconImage = 0x02;
        public const int IconTip = 0x04;
        public const int IconShowTip = 0x80;
        public const uint MenuString = 0x0000;
        public const uint MenuSeparator = 0x0800;
        public const uint RightButton = 0x0002;
        public const uint NoNotify = 0x0080;
        public const uint ReturnCommand = 0x0100;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate IntPtr WindowProcedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WindowClass
        {
            public int Size;
            public int Style;
            public WindowProcedure Procedure;
            public int ClassExtra;
            public int WindowExtra;
            public IntPtr Instance;
            public IntPtr Icon;
            public IntPtr Cursor;
            public IntPtr Background;
            public string? MenuName;
            public string ClassName;
            public IntPtr SmallIcon;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NotifyIconData
        {
            public int Size;
            public IntPtr Window;
            public int Id;
            public int Flags;
            public int CallbackMessage;
            public IntPtr Icon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Tip;
            public int State;
            public int StateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string Info;
            public int Version;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string InfoTitle;
            public int InfoFlags;
            public Guid Item;
            public IntPtr BalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Message
        {
            public IntPtr Window;
            public uint Id;
            public IntPtr WParam;
            public IntPtr LParam;
            public int Time;
            public int X;
            public int Y;
            public int Private;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern ushort RegisterClassExW(ref WindowClass windowClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterClassW(string name, IntPtr instance);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr CreateWindowExW(int extendedStyle, string className, string windowName, int style, int x, int y, int width,
            int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyWindow(IntPtr window);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr DefWindowProcW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern int GetMessageW(out Message message, IntPtr window, uint first, uint last);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool TranslateMessage(ref Message message);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr DispatchMessageW(ref Message message);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PostMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern void PostQuitMessage(int code);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern uint RegisterWindowMessageW(string name);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AppendMenuW(IntPtr menu, uint flags, UIntPtr id, string? text);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetMenuDefaultItem(IntPtr menu, uint item, uint byPosition);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr window, IntPtr parameters);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyMenu(IntPtr menu);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr LoadIconW(IntPtr instance, IntPtr name);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr icon);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern uint ExtractIconExW(string file, int index, IntPtr large, out IntPtr small, uint icons);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool Shell_NotifyIconW(uint message, ref NotifyIconData data);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr GetModuleHandleW(string? name);
    }
}
