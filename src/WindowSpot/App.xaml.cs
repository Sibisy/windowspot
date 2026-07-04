using System;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using WindowSpot.Providers;
using WindowSpot.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace WindowSpot;

public partial class App : Application
{
    private Mutex? _mutex;
    private NotifyIcon? _trayIcon;
    private HotkeyManager? _hotkeyManager;
    private MainWindow? _mainWindow;
    private AppIndexer? _appIndexer;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, "WindowSpot.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("WindowSpot이 이미 실행 중입니다.", "WindowSpot",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _appIndexer = new AppIndexer();
        var searchEngine = new SearchEngine(new ISearchProvider[]
        {
            new CalculatorProvider(),
            new AppSearchProvider(_appIndexer),
            new FileSearchProvider(),
            new WebSearchProvider(),
        });

        _mainWindow = new MainWindow(searchEngine);

        RegisterHotkey();
        SetupTrayIcon();

        await _appIndexer.InitializeAsync();
    }

    private void RegisterHotkey()
    {
        try
        {
            _hotkeyManager = new HotkeyManager(_mainWindow!, ModifierKeys.Alt, Key.Space);
            _hotkeyManager.HotkeyPressed += () =>
            {
                if (_mainWindow!.IsVisible) _mainWindow.Hide();
                else _mainWindow.ShowAtCenter();
            };
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(
                $"전역 단축키(Alt+Space) 등록에 실패했습니다. 다른 프로그램이 이미 사용 중일 수 있습니다.\n\n{ex.Message}",
                "WindowSpot", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            Text = "WindowSpot (Alt+Space)",
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("열기 (Alt+Space)", null, (_, _) => _mainWindow!.ShowAtCenter());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => Shutdown());

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => _mainWindow!.ShowAtCenter();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyManager?.Dispose();
        _appIndexer?.Dispose();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _mutex?.ReleaseMutex();
        base.OnExit(e);
    }
}
