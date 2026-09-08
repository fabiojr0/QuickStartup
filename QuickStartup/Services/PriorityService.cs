using System.Diagnostics;
using Microsoft.Win32;

namespace QuickStartup.Services;

/// <summary>Guarda a preferência de prioridade alta do processo. A leitura em
/// App.xaml.cs decide entre ProcessPriorityClass.High (exige elevação como
/// administrador) e AboveNormal (padrão, sem elevação) a cada início do app.</summary>
public static class PriorityService
{
    private const string RegistryKey = @"SOFTWARE\QuickStartup";
    private const string ValueName   = "HighPriority";

    public static bool IsHighPriorityEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, writable: false);
        return key?.GetValue(ValueName) is int value && value != 0;
    }

    public static void SetHighPriorityEnabled(bool enable)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryKey, writable: true)
            ?? throw new InvalidOperationException("Não foi possível abrir a chave de registro de configurações.");

        key.SetValue(ValueName, enable ? 1 : 0, RegistryValueKind.DWord);
    }

    /// <summary>Aplica a prioridade no processo atual imediatamente (sem esperar o
    /// próximo início do app). Falha silenciosamente sem elevação, já que High exige
    /// administrador.</summary>
    public static void ApplyToCurrentProcess(bool enable)
    {
        try
        {
            using var current = Process.GetCurrentProcess();
            current.PriorityClass = enable ? ProcessPriorityClass.High : ProcessPriorityClass.AboveNormal;
        }
        catch { /* sem permissão ou processo já finalizando — segue com a prioridade padrão */ }
    }
}
