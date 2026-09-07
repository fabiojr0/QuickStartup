using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using QuickStartup.Helpers;
using QuickStartup.Models;
using QuickStartup.Services;

namespace QuickStartup.ViewModels;

public class ProfileEditorViewModel : INotifyPropertyChanged
{
    private readonly ProfileService _profileService;
    private AppItem? _selectedApp;
    private string _profileName;

    public Profile Profile { get; }

    public string ProfileName
    {
        get => _profileName;
        set
        {
            _profileName = value;
            Profile.Name = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<AppItem> Apps => Profile.Apps;

    public AppItem? SelectedApp
    {
        get => _selectedApp;
        set { _selectedApp = value; OnPropertyChanged(); }
    }

    public List<MonitorInfo> Monitors { get; }

    public List<MonitorPreviewItem> MonitorPreviewItems { get; }

    public List<AppSuggestion> AppSuggestions { get; }

    private AppSuggestion? _selectedAppSuggestion;
    public AppSuggestion? SelectedAppSuggestion
    {
        get => _selectedAppSuggestion;
        set
        {
            _selectedAppSuggestion = value;
            OnPropertyChanged();

            if (value is null || SelectedApp is null) return;

            SelectedApp.ExecutablePath = value.ExecutablePath;
            if (string.IsNullOrWhiteSpace(SelectedApp.Name) || SelectedApp.Name == "Novo App")
                SelectedApp.Name = value.Name;
            OnPropertyChanged(nameof(SelectedApp));
        }
    }

    public RelayCommand AddAppCommand         { get; }
    public RelayCommand RemoveAppCommand      { get; }
    public RelayCommand MoveUpCommand         { get; }
    public RelayCommand MoveDownCommand       { get; }
    public RelayCommand BrowseExeCommand      { get; }
    public RelayCommand PositionPresetCommand { get; }
    public RelayCommand SelectMonitorCommand  { get; }
    public RelayCommand SaveCommand           { get; }
    public RelayCommand CancelCommand         { get; }

    public Action? CloseAction          { get; set; }

    private const double PreviewCanvasWidth  = 436;
    private const double PreviewCanvasHeight = 66;

    public ProfileEditorViewModel(Profile profile, ProfileService profileService)
    {
        Profile          = profile;
        _profileService  = profileService;
        _profileName     = profile.Name;
        Monitors         = MonitorService.GetMonitors();

        MonitorPreviewItems = BuildMonitorPreview(Monitors, PreviewCanvasWidth, PreviewCanvasHeight);
        AppSuggestions      = BuildAppSuggestions(profileService);

        AddAppCommand         = new(AddApp);
        RemoveAppCommand      = new(RemoveApp,  () => SelectedApp is not null);
        MoveUpCommand         = new(MoveUp,     () => SelectedApp is not null && Apps.IndexOf(SelectedApp) > 0);
        MoveDownCommand       = new(MoveDown,   () => SelectedApp is not null && Apps.IndexOf(SelectedApp) < Apps.Count - 1);
        BrowseExeCommand      = new(BrowseExe,  () => SelectedApp is not null);
        PositionPresetCommand = new(param => ApplyPositionPreset(param as string), _ => SelectedApp is not null);
        SelectMonitorCommand  = new(param => SelectMonitor(param as int?), _ => SelectedApp is not null);
        SaveCommand           = new(Save);
        CancelCommand         = new(() => CloseAction?.Invoke());
    }

    private static List<MonitorPreviewItem> BuildMonitorPreview(
        List<MonitorInfo> monitors, double canvasWidth, double canvasHeight)
    {
        if (monitors.Count == 0) return new();

        int minX = monitors.Min(m => m.Bounds.X);
        int minY = monitors.Min(m => m.Bounds.Y);
        int maxX = monitors.Max(m => m.Bounds.X + m.Bounds.Width);
        int maxY = monitors.Max(m => m.Bounds.Y + m.Bounds.Height);

        double totalWidth  = Math.Max(1, maxX - minX);
        double totalHeight = Math.Max(1, maxY - minY);

        // Escala proporcional (mantém aspect ratio) para caber na área da prévia
        double scale = Math.Min(canvasWidth / totalWidth, canvasHeight / totalHeight);

        double offsetX = (canvasWidth  - totalWidth  * scale) / 2;
        double offsetY = (canvasHeight - totalHeight * scale) / 2;

        // Reflete a disposição real (X/Y absolutos) configurada nas Configurações do Windows
        return monitors.Select(m => new MonitorPreviewItem
        {
            Monitor = m,
            X       = offsetX + (m.Bounds.X - minX) * scale,
            Y       = offsetY + (m.Bounds.Y - minY) * scale,
            Width   = m.Bounds.Width  * scale,
            Height  = m.Bounds.Height * scale
        }).ToList();
    }

    private static List<AppSuggestion> BuildAppSuggestions(ProfileService profileService)
    {
        var suggestions = new Dictionary<string, AppSuggestion>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, path) in AppDiscoveryService.GetInstalledApps())
            suggestions[path] = new AppSuggestion { Name = name, ExecutablePath = path };

        // Apps da Microsoft Store (WhatsApp, Spotify, Xbox, etc.) — o caminho já vem pronto
        // no formato que o WindowService sabe iniciar; o usuário só vê o nome do app.
        foreach (var (name, shellPath) in AppDiscoveryService.GetInstalledUwpApps())
            suggestions[shellPath] = new AppSuggestion { Name = name, ExecutablePath = shellPath };

        // Apps já usados em outros perfis contam como "frequentes" e sobem no ranking
        var usage = profileService.Profiles
            .SelectMany(p => p.Apps)
            .Where(a => !string.IsNullOrWhiteSpace(a.ExecutablePath))
            .GroupBy(a => a.ExecutablePath, StringComparer.OrdinalIgnoreCase);

        foreach (var group in usage)
        {
            var count = group.Count();
            var name  = group.First(a => !string.IsNullOrWhiteSpace(a.Name)).Name;

            if (suggestions.TryGetValue(group.Key, out var existing))
                existing.UsageCount = count;
            else
                suggestions[group.Key] = new AppSuggestion { Name = name, ExecutablePath = group.Key, UsageCount = count };
        }

        return suggestions.Values
            .OrderByDescending(s => s.UsageCount)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void AddApp()
    {
        var item = new AppItem { Name = "Novo App", MonitorIndex = 0 };
        Apps.Add(item);
        SelectedApp = item;
    }

    private void RemoveApp()
    {
        if (SelectedApp is null) return;
        var idx = Apps.IndexOf(SelectedApp);
        Apps.Remove(SelectedApp);
        SelectedApp = Apps.Count > 0 ? Apps[Math.Max(0, idx - 1)] : null;
    }

    private void MoveUp()
    {
        if (SelectedApp is null) return;
        var idx = Apps.IndexOf(SelectedApp);
        if (idx <= 0) return;
        Apps.Move(idx, idx - 1);
        SelectedApp = Apps[idx - 1];
    }

    private void MoveDown()
    {
        if (SelectedApp is null) return;
        var idx = Apps.IndexOf(SelectedApp);
        if (idx >= Apps.Count - 1) return;
        Apps.Move(idx, idx + 1);
        SelectedApp = Apps[idx + 1];
    }

    private void BrowseExe()
    {
        if (SelectedApp is null) return;

        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Selecione o executável",
            Filter = "Executáveis (*.exe)|*.exe|Atalhos (*.lnk)|*.lnk|Todos os arquivos (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            SelectedApp.ExecutablePath = dlg.FileName;
            if (string.IsNullOrWhiteSpace(SelectedApp.Name))
                SelectedApp.Name = Path.GetFileNameWithoutExtension(dlg.FileName);

            OnPropertyChanged(nameof(SelectedApp));
        }
    }

    private void ApplyPositionPreset(string? preset)
    {
        if (SelectedApp is null || preset is null) return;

        SelectedApp.UsePercentSize = true;
        SelectedApp.Maximize       = false;

        (SelectedApp.XPercent, SelectedApp.YPercent, SelectedApp.WidthPercent, SelectedApp.HeightPercent) = preset switch
        {
            "Left"   => (0, 0, 50, 100),
            "Right"  => (50, 0, 50, 100),
            "Top"    => (0, 0, 100, 50),
            "Bottom" => (0, 50, 100, 50),
            _        => (SelectedApp.XPercent, SelectedApp.YPercent, SelectedApp.WidthPercent, SelectedApp.HeightPercent)
        };
    }

    private void SelectMonitor(int? monitorIndex)
    {
        if (SelectedApp is null || monitorIndex is null) return;
        SelectedApp.MonitorIndex = monitorIndex.Value;
    }

    private void Save()
    {
        _profileService.Save();
        CloseAction?.Invoke();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
