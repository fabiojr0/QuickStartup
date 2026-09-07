using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using QuickStartup.Services;

namespace QuickStartup;

public partial class App : System.Windows.Application
{
    // GUID fixo só para dar um nome único ao mutex/evento entre processos — não representa nada além disso.
    private const string SingleInstanceId = "QuickStartup-9f1e6b0a-3f0f-4a3f-a2b0-11f4d2a5c111";

    // Mantidos como campos para não serem coletados pelo GC enquanto o app roda
    // (isso derrubaria a posse do mutex / o handle do evento).
    private static Mutex? _instanceMutex;
    private static EventWaitHandle? _showRequestedEvent;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceMutex = new Mutex(true, SingleInstanceId, out bool isFirstInstance);
        _showRequestedEvent = new EventWaitHandle(false, EventResetMode.AutoReset, SingleInstanceId + "-Show");

        if (!isFirstInstance)
        {
            // Já existe uma instância rodando (provavelmente na bandeja) — pede pra ela
            // se mostrar e encerra esta segunda cópia em vez de rodar duas ao mesmo tempo.
            _showRequestedEvent.Set();
            Shutdown();
            return;
        }

        RaiseProcessPriority();

        DispatcherUnhandledException += (_, args) =>
        {
            LogException(args.Exception);
            System.Windows.MessageBox.Show(
                args.Exception.ToString(),
                "QuickStartup — Erro inesperado",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                LogException(ex);
        };

        var profileService = new ProfileService();
        var windowService  = new WindowService();
        var mainWindow     = new MainWindow(profileService, windowService);

        // Escuta pedidos de outras cópias do app iniciadas enquanto esta já está rodando
        // (ver bloco de instância única acima) e traz a janela existente para frente.
        new Thread(() =>
        {
            while (_showRequestedEvent!.WaitOne())
                Dispatcher.Invoke(mainWindow.BringToFront);
        })
        { IsBackground = true }.Start();

        // --startup indica que o app foi aberto pelo Windows automaticamente
        bool isAutoStartup = e.Args.Contains("--startup");

        if (isAutoStartup)
        {
            // O ícone da bandeja vive dentro do XAML da MainWindow e só é criado quando
            // ela é exibida pelo menos uma vez — por isso não dá pra simplesmente nunca
            // chamar Show(). Para evitar qualquer flash visual na tela, mostramos a janela
            // fora da área visível e totalmente transparente, escondemos em seguida, e só
            // então restauramos a posição/opacidade normais para quando o usuário abrir
            // a janela manualmente pela bandeja.
            mainWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            mainWindow.Left         = -32000;
            mainWindow.Top          = -32000;
            mainWindow.ShowInTaskbar = false;
            mainWindow.Opacity      = 0;

            mainWindow.Show();
            mainWindow.Hide();

            mainWindow.Opacity       = 1;
            mainWindow.ShowInTaskbar = true;
            mainWindow.WindowState   = WindowState.Normal;
            mainWindow.Left = (SystemParameters.PrimaryScreenWidth  - mainWindow.Width)  / 2;
            mainWindow.Top  = (SystemParameters.PrimaryScreenHeight - mainWindow.Height) / 2;

            var defaultProfile = profileService.GetDefault();
            if (defaultProfile is not null)
            {
                var monitors = MonitorService.GetMonitors();
                _ = windowService.ExecuteProfileAsync(defaultProfile, monitors);
            }
        }
        else
        {
            mainWindow.Show();
        }

        // Checagem de atualização via GitHub Releases — assíncrona e silenciosa, nunca
        // atrasa nem interrompe a abertura do app (ver UpdateService para detalhes).
        _ = CheckForUpdatesAsync(mainWindow);
    }

    private static async Task CheckForUpdatesAsync(MainWindow mainWindow)
    {
        var update = await UpdateService.CheckForUpdateAsync();
        if (update is null) return;

        mainWindow.Dispatcher.Invoke(() =>
            mainWindow.ShowUpdateAvailable(update.Version, update.InstallerUrl, update.ReleasePageUrl));
    }

    /// <summary>Sobe a prioridade do processo para AboveNormal para que a janela e o
    /// posicionamento dos apps do perfil padrão sejam processados mais rápido, mesmo
    /// concorrendo com outros programas que sobem junto no boot do Windows. Não usa
    /// High/RealTime para não competir com processos críticos do sistema.</summary>
    private static void RaiseProcessPriority()
    {
        try
        {
            using var current = Process.GetCurrentProcess();
            current.PriorityClass = ProcessPriorityClass.AboveNormal;
        }
        catch { /* sem permissão ou processo já finalizando — segue com a prioridade padrão */ }
    }

    private static void LogException(Exception ex)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickStartup");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { /* nada a fazer se nem o log funcionar */ }
    }
}
