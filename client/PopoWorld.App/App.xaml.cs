using System.Windows;
using System.Windows.Threading;
using PopoWorld.App.Services;
using Serilog;

namespace PopoWorld.App;

/// <summary>단일 인스턴스, 로그, 트레이, 시작.</summary>
public partial class App : Application
{
    private Mutex? _mutex;
    private WorldController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, @"Local\PopoWorld.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("포포월드가 이미 실행 중이에요. 트레이 아이콘을 확인하세요.", "포포월드", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        AppPaths.EnsureDirectories();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(Path.Combine(AppPaths.LogDirectory, "app-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        Log.Information("PopoWorld 시작 v{Version}", typeof(App).Assembly.GetName().Version);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log.Fatal(args.ExceptionObject as Exception, "처리되지 않은 예외");
        TaskScheduler.UnobservedTaskException += (_, args) => { Log.Error(args.Exception, "관찰되지 않은 Task 예외"); args.SetObserved(); };

        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        try
        {
            var settingsStore = new SettingsStore();
            settingsStore.Load();

            AppConfig config;
            try { config = AppConfig.Load(); }
            catch (Exception ex)
            {
                Log.Fatal(ex, "설정 파일 오류");
                MessageBox.Show(ex.Message, "포포월드", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }
            if (config.IsPlaceholder)
            {
                MessageBox.Show("appsettings.json 에 SUPABASE_URL 과 SUPABASE_ANON_KEY 를 넣어 주세요.", "포포월드", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            var sessionStore = new SessionStore();
            var supabase = new SupabaseService(config, sessionStore);
            await supabase.InitializeAsync();

            var popo = new PopoService(supabase);
            var realtime = new RealtimeService(supabase);
            var presence = new PresenceService();
            var sound = new SoundService(settingsStore);
            var assets = new AssetCache();
            var hotkeys = new HotkeyService();
            var tray = new TrayIcon();

            _controller = new WorldController(settingsStore, supabase, popo, realtime, presence, sound, assets, hotkeys, tray);
            if (!await _controller.StartAsync())
            {
                Log.Information("로그인하지 않아 종료");
                _controller.Dispose();
                Shutdown();
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "시작 실패");
            MessageBox.Show("포포월드를 시작하지 못했어요. 로그를 확인하세요.\n" + ex.Message, "포포월드", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "UI 스레드 예외");
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("PopoWorld 종료");
        Log.CloseAndFlush();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
