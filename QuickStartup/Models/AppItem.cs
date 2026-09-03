using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace QuickStartup.Models;

public class AppItem : INotifyPropertyChanged
{
    private string _name = "";
    private string _executablePath = "";
    private string _arguments = "";
    private int _monitorIndex;
    private bool _maximize;
    private int _x, _y, _width = 1024, _height = 768;
    private int _delayMs = 2000;
    private bool _usePercentSize;
    private double _xPercent, _yPercent, _widthPercent = 50, _heightPercent = 50;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string ExecutablePath
    {
        get => _executablePath;
        set { _executablePath = value; OnPropertyChanged(); }
    }

    public string Arguments
    {
        get => _arguments;
        set { _arguments = value; OnPropertyChanged(); }
    }

    public int MonitorIndex
    {
        get => _monitorIndex;
        set { _monitorIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(MonitorNumber)); }
    }

    /// <summary>Número exibido ao usuário (1-based) — mesma numeração usada no select/prévia de monitores.</summary>
    public int MonitorNumber => MonitorIndex + 1;

    public bool Maximize
    {
        get => _maximize;
        set { _maximize = value; OnPropertyChanged(); }
    }

    public int X
    {
        get => _x;
        set { _x = value; OnPropertyChanged(); }
    }

    public int Y
    {
        get => _y;
        set { _y = value; OnPropertyChanged(); }
    }

    public int Width
    {
        get => _width;
        set { _width = value; OnPropertyChanged(); }
    }

    public int Height
    {
        get => _height;
        set { _height = value; OnPropertyChanged(); }
    }

    public int DelayMs
    {
        get => _delayMs;
        set { _delayMs = value; OnPropertyChanged(); }
    }

    /// <summary>Se true, X/Y/Width/Height são ignorados e os campos *Percent são usados (relativos ao monitor).</summary>
    public bool UsePercentSize
    {
        get => _usePercentSize;
        set { _usePercentSize = value; OnPropertyChanged(); }
    }

    public double XPercent
    {
        get => _xPercent;
        set { _xPercent = value; OnPropertyChanged(); }
    }

    public double YPercent
    {
        get => _yPercent;
        set { _yPercent = value; OnPropertyChanged(); }
    }

    public double WidthPercent
    {
        get => _widthPercent;
        set { _widthPercent = value; OnPropertyChanged(); }
    }

    public double HeightPercent
    {
        get => _heightPercent;
        set { _heightPercent = value; OnPropertyChanged(); }
    }

    public AppItem Clone() => new()
    {
        Id             = Guid.NewGuid(),
        Name           = Name,
        ExecutablePath = ExecutablePath,
        Arguments      = Arguments,
        MonitorIndex   = MonitorIndex,
        Maximize       = Maximize,
        X              = X,
        Y              = Y,
        Width          = Width,
        Height         = Height,
        DelayMs        = DelayMs,
        UsePercentSize = UsePercentSize,
        XPercent       = XPercent,
        YPercent       = YPercent,
        WidthPercent   = WidthPercent,
        HeightPercent  = HeightPercent
    };

    public override string ToString()
    {
        if (!string.IsNullOrWhiteSpace(Name)) return Name;
        if (ExecutablePath.StartsWith(@"shell:AppsFolder\", StringComparison.OrdinalIgnoreCase))
            return ExecutablePath[(ExecutablePath.LastIndexOf('\\') + 1)..];
        return Path.GetFileNameWithoutExtension(ExecutablePath);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
