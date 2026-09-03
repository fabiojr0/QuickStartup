namespace QuickStartup.Models;

/// <summary>Retângulo de um monitor já escalado para caber na prévia visual,
/// preservando a posição relativa real configurada no Windows.</summary>
public class MonitorPreviewItem
{
    public MonitorInfo Monitor { get; set; } = null!;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}
