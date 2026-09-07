using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using QuickStartup.Helpers;
using QuickStartup.Models;
using QuickStartup.Services;

namespace QuickStartup.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ProfileService _profileService;
    private readonly WindowService  _windowService;
    private Profile? _selectedProfile;
    private bool _isRunning;
    private string _statusText = "Pronto.";
    private CancellationTokenSource? _cts;

    public ObservableCollection<Profile> Profiles => _profileService.Profiles;

    public ObservableCollection<AppLaunchResult> LaunchResults { get; } = new();

    public Profile? SelectedProfile
    {
        get => _selectedProfile;
        set { _selectedProfile = value; OnPropertyChanged(); }
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotRunning)); }
    }

    public bool IsNotRunning => !_isRunning;

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    private bool _updateAvailable;
    private string _updateVersionText = "";
    private string? _updateInstallerUrl;
    private string _updateReleaseUrl = "";
    private string _updateButtonText = "Atualizar";
    private bool _isUpdating;

    public bool UpdateAvailable
    {
        get => _updateAvailable;
        private set { _updateAvailable = value; OnPropertyChanged(); }
    }

    public string UpdateVersionText
    {
        get => _updateVersionText;
        private set { _updateVersionText = value; OnPropertyChanged(); }
    }

    public string UpdateButtonText
    {
        get => _updateButtonText;
        private set { _updateButtonText = value; OnPropertyChanged(); }
    }

    public bool IsUpdating
    {
        get => _isUpdating;
        private set { _isUpdating = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotUpdating)); }
    }

    public bool IsNotUpdating => !_isUpdating;

    public RelayCommand OpenUpdateCommand    { get; }
    public RelayCommand DismissUpdateCommand { get; }

    // Chamada para encerrar o app de verdade (janela + tray + processo) antes de instalar a
    // atualização — injetada pela View, já que a VM não deve depender de detalhes da janela.
    public Action? RequestAppExitAction { get; set; }

    public RelayCommand ExecuteProfileCommand { get; }
    public RelayCommand NewProfileCommand     { get; }
    public RelayCommand EditProfileCommand    { get; }
    public RelayCommand DuplicateProfileCommand { get; }
    public RelayCommand DeleteProfileCommand  { get; }
    public RelayCommand CancelCommand         { get; }
    public RelayCommand OpenSettingsCommand   { get; }

    // Ações que abrem janelas — injetadas pela View para manter a VM testável
    public Action<Profile>? OpenEditorAction   { get; set; }
    public Action? OpenSettingsAction          { get; set; }

    // Confirmação antes de excluir — injetada pela View (MessageBox). Se não for
    // definida (ex: em testes), a exclusão prossegue sem pedir confirmação.
    public Func<Profile, bool>? ConfirmDeleteAction { get; set; }

    public MainViewModel(ProfileService profileService, WindowService windowService)
    {
        _profileService = profileService;
        _windowService  = windowService;

        _windowService.AppLaunched += OnAppLaunched;

        ExecuteProfileCommand   = new(ExecuteProfile,   () => SelectedProfile is not null && IsNotRunning);
        NewProfileCommand       = new(NewProfile,       () => IsNotRunning);
        EditProfileCommand      = new(EditProfile,      () => SelectedProfile is not null && IsNotRunning);
        DuplicateProfileCommand = new(DuplicateProfile, () => SelectedProfile is not null && IsNotRunning);
        DeleteProfileCommand    = new(DeleteProfile,    () => SelectedProfile is not null && IsNotRunning);
        CancelCommand           = new(Cancel,           () => IsRunning);
        OpenSettingsCommand     = new(OpenSettings);
        OpenUpdateCommand       = new(InstallUpdate, () => IsNotUpdating);
        DismissUpdateCommand    = new(() => UpdateAvailable = false, () => IsNotUpdating);
    }

    public void SetUpdateAvailable(string version, string? installerUrl, string releaseUrl)
    {
        _updateInstallerUrl = installerUrl;
        _updateReleaseUrl   = releaseUrl;
        UpdateVersionText   = version;
        UpdateButtonText    = "Atualizar";
        UpdateAvailable     = true;
    }

    private async void InstallUpdate()
    {
        if (string.IsNullOrWhiteSpace(_updateInstallerUrl))
        {
            // Release sem instalador anexado nesse formato — melhor abrir a página do
            // release manualmente do que rodar um .exe qualquer com flags de instalação silenciosa.
            Process.Start(new ProcessStartInfo(_updateReleaseUrl) { UseShellExecute = true });
            return;
        }

        IsUpdating       = true;
        UpdateButtonText = "Baixando...";

        var installerPath = await UpdateService.DownloadInstallerAsync(_updateInstallerUrl);
        if (installerPath is null)
        {
            UpdateButtonText = "Falha no download";
            IsUpdating        = false;
            return;
        }

        UpdateButtonText = "Instalando...";
        UpdateService.RunInstallerSilently(installerPath);

        // O instalador (/VERYSILENT) fecha esta instância via AppMutex e reabre sozinho ao
        // terminar (ver installer/setup.iss) — encerra aqui também pra não deixar o processo
        // antigo pendurado enquanto o instalador ainda está de pé.
        RequestAppExitAction?.Invoke();
    }

    public async void ExecuteProfile()
    {
        if (SelectedProfile is null) return;

        IsRunning = true;
        LaunchResults.Clear();
        StatusText = $"Executando perfil \"{SelectedProfile.Name}\"...";
        _cts = new CancellationTokenSource();

        var monitors = MonitorService.GetMonitors();
        await _windowService.ExecuteProfileAsync(SelectedProfile, monitors, _cts.Token);

        IsRunning = false;
        StatusText = _cts.Token.IsCancellationRequested
            ? "Execução cancelada."
            : "Execução concluída.";
    }

    private void OnAppLaunched(AppLaunchResult result)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            LaunchResults.Add(result);
            StatusText = $"[{(result.Success ? "OK" : "ERRO")}] {result.AppName}: {result.Message}";
        });
    }

    private void NewProfile()
    {
        var profile = new Profile();
        _profileService.Add(profile);
        SelectedProfile = profile;
        OpenEditorAction?.Invoke(profile);
    }

    private void EditProfile()
    {
        if (SelectedProfile is null) return;
        OpenEditorAction?.Invoke(SelectedProfile);
        _profileService.Save();
    }

    private void DuplicateProfile()
    {
        if (SelectedProfile is null) return;
        _profileService.Duplicate(SelectedProfile);
    }

    private void DeleteProfile()
    {
        if (SelectedProfile is null) return;
        if (ConfirmDeleteAction?.Invoke(SelectedProfile) == false) return;

        _profileService.Remove(SelectedProfile);
        SelectedProfile = Profiles.FirstOrDefault();
    }

    private void Cancel() => _cts?.Cancel();

    private void OpenSettings() => OpenSettingsAction?.Invoke();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
