using System.IO;
using System.Windows;
using System.Windows.Threading;
using QuickStartup.Services;

namespace QuickStartup;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
