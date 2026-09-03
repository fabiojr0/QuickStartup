using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
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
            OpenEditorAction = profile =>
            {
                var editor = new ProfileEditorWindow(profile, profileService) { Owner = this };
                editor.ShowDialog();
            },
            OpenSettingsAction = () =>
            {
                var settings = new SettingsWindow(profileService) { Owner = this };
                settings.ShowDialog();
            }
        };

        DataContext = _vm;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_forceClose) return;
        // Minimiza para a bandeja ao fechar, em vez de encerrar
        e.Cancel = true;
        Hide();
        TrayIcon.ShowBalloonTip(
            "QuickStartup",
            "Rodando em segundo plano. Clique duplo no ícone para abrir.",
            BalloonIcon.Info);
    }

    public void ForceClose()
    {
        _forceClose = true;
        TrayIcon.Dispose();
        Close();
    }

    private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e) => ShowMainWindow();
    private void TrayOpen_Click(object sender, RoutedEventArgs e)               => ShowMainWindow();

    // Monta a lista de perfis toda vez que o menu é aberto, para refletir renomeações/
    // criações/exclusões feitas desde a última vez sem precisar observar cada mudança.
    private void TrayContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        TrayProfilesMenuItem.Items.Clear();

        if (_profileService.Profiles.Count == 0)
        {
            TrayProfilesMenuItem.Items.Add(new MenuItem { Header = "Nenhum perfil criado", IsEnabled = false });
            return;
        }

        foreach (var profile in _profileService.Profiles)
        {
            var header = profile.IsDefault ? $"{profile.Name} (Padrão)" : profile.Name;
            var item = new MenuItem { Header = header, IsEnabled = _vm.IsNotRunning };
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
