using System.Drawing;

namespace QuickStartup.Models;

public class MonitorInfo
{
    public int Index { get; set; }
    public string DeviceName { get; set; } = "";
    public Rectangle Bounds { get; set; }
    public Rectangle WorkingArea { get; set; }
    public bool IsPrimary { get; set; }

    /// <summary>Número exibido ao usuário (1-based) — usado tanto no select quanto na prévia visual.</summary>
    public int Number => Index + 1;

    public string DisplayName =>
        $"Monitor {Index + 1}{(IsPrimary ? " (Principal)" : "")} — {Bounds.Width}×{Bounds.Height}";

    public override string ToString() => DisplayName;
}
