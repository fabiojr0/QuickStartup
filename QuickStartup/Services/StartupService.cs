using Microsoft.Win32;

namespace QuickStartup.Services;

public static class StartupService
{
    private const string RegistryKey   = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppValueName  = "QuickStartup";

    public static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, writable: false);
        return key?.GetValue(AppValueName) is not null;
    }

    public static void SetStartup(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, writable: true)
            ?? throw new InvalidOperationException("Não foi possível abrir a chave de registro de inicialização.");

        if (enable)
        {
            // Registra o executável atual para iniciar com o Windows
            var exePath = Environment.ProcessPath
                ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                ?? throw new InvalidOperationException("Não foi possível obter o caminho do executável.");

            // --startup flag indica ao app que foi iniciado automaticamente (minimizar na bandeja)
            key.SetValue(AppValueName, $"\"{exePath}\" --startup");
        }
        else
        {
            key.DeleteValue(AppValueName, throwOnMissingValue: false);
        }
    }
}
