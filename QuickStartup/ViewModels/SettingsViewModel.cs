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
    private bool _highPriority;
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

    public bool HighPriority
    {
        get => _highPriority;
        set
        {
            if (_highPriority == value) return;

            // Ligar exige administrador — se ainda não estamos elevados, pede o UAC agora
            // (em vez de esperar o próximo início do app) e só marca o checkbox se aceito.
            if (value && !ElevationHelper.IsRunningAsAdministrator())
            {
                RequestElevationForHighPriority();
                return;
            }

            if (!TrySetHighPriority(value)) return;
            _highPriority = value;
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
        _highPriority      = PriorityService.IsHighPriorityEnabled();
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

        // Prioridade alta já foi salva e aplicada imediatamente quando o checkbox
        // foi marcado/desmarcado (ver HighPriority acima) — nada a fazer aqui.

        _profileService.SetDefault(_defaultProfile);
        CloseAction?.Invoke();
    }

    /// <summary>Persiste a preferência e aplica a prioridade no processo atual na hora,
    /// sem esperar o próximo início do app.</summary>
    private static bool TrySetHighPriority(bool enable)
    {
        try
        {
            PriorityService.SetHighPriorityEnabled(enable);
            PriorityService.ApplyToCurrentProcess(enable);
            return true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Erro ao salvar configuração de prioridade:\n{ex.Message}",
                "QuickStartup", System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return false;
        }
    }

    /// <summary>Chamado ao marcar "Prioridade alta" quando o app ainda não está elevado:
    /// salva o restante das configurações pendentes (serão perdidas no relançamento) e
    /// pede o UAC. Se aceito, o app relança elevado e esta instância encerra; a nova
    /// instância já inicia com prioridade alta aplicada. Se cancelado, desfaz e mantém
    /// o checkbox desmarcado.</summary>
    private void RequestElevationForHighPriority()
    {
        try { StartupService.SetStartup(_startWithWindows); } catch { /* reportado no Save normalmente */ }
        _profileService.SetDefault(_defaultProfile);

        if (!TrySetHighPriority(true)) return;

        if (ElevationHelper.TryRelaunchElevated(Array.Empty<string>()))
        {
            _highPriority = true;
            OnPropertyChanged(nameof(HighPriority));
            System.Windows.Application.Current.Shutdown();
        }
        else
        {
            // Usuário cancelou o prompt do UAC — desfaz e mantém desmarcado.
            TrySetHighPriority(false);
            _highPriority = false;
            OnPropertyChanged(nameof(HighPriority));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
