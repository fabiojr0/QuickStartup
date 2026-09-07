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
    private string _updateUrl = "";

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

    public RelayCommand OpenUpdateCommand    { get; }
    public RelayCommand DismissUpdateCommand { get; }

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
        OpenUpdateCommand       = new(OpenUpdate);
        DismissUpdateCommand    = new(() => UpdateAvailable = false);
    }

    public void SetUpdateAvailable(string version, string url)
    {
        _updateUrl        = url;
        UpdateVersionText = version;
        UpdateAvailable   = true;
    }

    private void OpenUpdate()
    {
        if (string.IsNullOrWhiteSpace(_updateUrl)) return;
        Process.Start(new ProcessStartInfo(_updateUrl) { UseShellExecute = true });
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
