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
    private FavoritesStore? _favoritesStore;
    private UsageStore? _usageStore;
    private SettingsStore? _settingsStore;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"예상치 못한 오류가 발생했습니다:\n\n{args.Exception}",
                "WindowSpot 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            MessageBox.Show($"복구할 수 없는 오류가 발생해 종료됩니다:\n\n{args.ExceptionObject}",
                "WindowSpot 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        try
        {
            try
            {
                _mutex = new Mutex(true, "WindowSpot.SingleInstance", out bool createdNew);
                if (!createdNew)
                {
                    MessageBox.Show("WindowSpot이 이미 실행 중입니다.", "WindowSpot",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown();
                    return;
                }
            }
            catch (UnauthorizedAccessException)
            {
                // 다른 보안 컨텍스트(예: 백신의 사전 실행 검사용 샌드박스)가 이미 같은 이름의
                // 뮤텍스를 만들어놔서 이 프로세스 권한으로는 열 수 없는 경우. 중복 실행
                // 여부를 확인할 수 없을 뿐, 실제로 이미 실행 중인 것은 아니므로 계속 진행한다.
                _mutex = null;
            }

            _appIndexer = new AppIndexer();
            _usageStore = new UsageStore();
            _favoritesStore = new FavoritesStore();
            _settingsStore = new SettingsStore();

            var searchEngine = new SearchEngine(new ISearchProvider[]
            {
                new CalculatorProvider(),
                new ExchangeRateProvider(),
                new UrlProvider(),
                new AppSearchProvider(_appIndexer, _usageStore),
                new FileSearchProvider(),
                new WebSearchProvider(),
            });

            _mainWindow = new MainWindow(searchEngine, _appIndexer, _favoritesStore, _usageStore, _settingsStore);

            RegisterHotkey();
            SetupTrayIcon();

            await _appIndexer.InitializeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"시작 중 오류가 발생했습니다:\n\n{ex}",
                "WindowSpot 시작 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
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
        menu.Items.Add("AI 설정...", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => Shutdown());

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => _mainWindow!.ShowAtCenter();
    }

    private void OpenSettings()
    {
        var settingsWindow = new SettingsWindow(_settingsStore!);
        if (settingsWindow.ShowDialog() == true)
        {
            _mainWindow!.RefreshAiEnabled();
        }
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
