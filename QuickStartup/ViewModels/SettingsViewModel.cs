using System.ComponentModel;
using System.Runtime.CompilerServices;
using QuickStartup.Helpers;
using QuickStartup.Models;
using QuickStartup.Services;

namespace QuickStartup.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ProfileService _profileService;
    private bool _startWithWindows;
    private Profile? _defaultProfile;

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            _startWithWindows = value;
            OnPropertyChanged();
        }
    }

    public Profile? DefaultProfile
    {
        get => _defaultProfile;
        set
        {
            _defaultProfile = value;
            OnPropertyChanged();
        }
    }

    public IEnumerable<Profile> Profiles => _profileService.Profiles;

    // Mesma versão que UpdateService compara contra o GitHub Releases pra checar atualização.
    public string VersionText => $"Versão {UpdateService.GetCurrentVersion()}";

    public RelayCommand SaveCommand  { get; }
    public Action? CloseAction       { get; set; }

    public SettingsViewModel(ProfileService profileService)
    {
        _profileService    = profileService;
        _startWithWindows  = StartupService.IsStartupEnabled();
        _defaultProfile    = profileService.GetDefault();

        SaveCommand = new(Save);
    }

    private void Save()
    {
        try { StartupService.SetStartup(_startWithWindows); }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Erro ao salvar configuração de inicialização:\n{ex.Message}",
                "QuickStartup", System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }

        _profileService.SetDefault(_defaultProfile);
        CloseAction?.Invoke();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
