namespace QuickStartup.Models;

/// <summary>Um app conhecido (instalado no Windows e/ou já usado em outros perfis)
/// oferecido como sugestão rápida no campo de executável.</summary>
public class AppSuggestion
{
    public string Name { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public int UsageCount { get; set; }

    public override string ToString() => Name;
}
