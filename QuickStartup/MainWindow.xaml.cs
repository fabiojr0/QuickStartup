using System.Windows;
using System.Windows.Controls;
using QuickStartup.Models;
using QuickStartup.Services;
using QuickStartup.ViewModels;
using QuickStartup.Views;

namespace QuickStartup;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly ProfileService _profileService;
    private bool _forceClose;

    public MainWindow(ProfileService profileService, WindowService windowService)
    {
        InitializeComponent();

        _profileService = profileService;

        _vm = new MainViewModel(profileService, windowService)
        {
            OpenSettingsAction = () =>
            {
                var settings = new SettingsWindow(profileService) { Owner = this };
                settings.ShowDialog();
            },
            ConfirmDeleteAction = profile => System.Windows.MessageBox.Show(
                this,
                $"Tem certeza que deseja excluir o perfil \"{profile.Name}\"?",
                "Excluir perfil",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes,
            RequestAppExitAction = ForceClose
        };

        DataContext = _vm;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_forceClose) return;
        // Minimiza para a bandeja ao fechar, em vez de encerrar (sem notificação/balão)
        e.Cancel = true;
        Hide();
    }

    public void ForceClose()
    {
        _forceClose = true;
        TrayIcon.Dispose();
        Close();
        // ShutdownMode="OnExplicitShutdown" (App.xaml) significa que fechar a janela sozinho
        // não encerra o processo — precisa deste chamado explícito.
        System.Windows.Application.Current.Shutdown();
    }

    private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e) => ShowMainWindow();
    private void TrayOpen_Click(object sender, RoutedEventArgs e)               => ShowMainWindow();

    /// <summary>Chamado quando uma segunda cópia do app é aberta (ver App.xaml.cs) para trazer
    /// esta janela existente para frente em vez de rodar duas instâncias ao mesmo tempo.</summary>
    public void BringToFront() => ShowMainWindow();

    /// <summary>Chamado pelo App.xaml.cs quando a checagem de atualização (GitHub Releases)
    /// encontra uma versão mais nova — exibe o aviso na janela principal.</summary>
    public void ShowUpdateAvailable(string version, string? installerUrl, string releaseUrl) =>
        _vm.SetUpdateAvailable(version, installerUrl, releaseUrl);

    // Monta a lista de perfis toda vez que o menu é aberto, para refletir renomeações/
    // criações/exclusões feitas desde a última vez sem precisar observar cada mudança.
    private void TrayContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        TrayProfilesMenuItem.Items.Clear();
        var itemStyle = (Style)Resources["TrayMenuItemStyle"];

        if (_profileService.Profiles.Count == 0)
        {
            TrayProfilesMenuItem.Items.Add(new MenuItem { Header = "Nenhum perfil criado", IsEnabled = false, Style = itemStyle });
            return;
        }

        foreach (var profile in _profileService.Profiles)
        {
            var header = profile.IsDefault ? $"{profile.Name} (Padrão)" : profile.Name;
            var item = new MenuItem { Header = header, IsEnabled = _vm.IsNotRunning, Style = itemStyle };
            item.Click += (_, _) => RunProfileFromTray(profile);
            TrayProfilesMenuItem.Items.Add(item);
        }
    }

    private void RunProfileFromTray(Profile profile)
    {
        _vm.SelectedProfile = profile;
        _vm.ExecuteProfile();
    }

    private void TrayExit_Click(object sender, RoutedEventArgs e) => ForceClose();

    private void ShowMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
}
