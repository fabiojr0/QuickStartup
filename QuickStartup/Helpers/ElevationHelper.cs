using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace QuickStartup.Helpers;

public static class ElevationHelper
{
    public static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>Relança o executável atual com o verbo "runas", disparando o prompt de
    /// elevação (UAC) do Windows. Retorna false se o usuário cancelar o prompt ou se o
    /// relançamento falhar por qualquer outro motivo.</summary>
    public static bool TryRelaunchElevated(string[] args)
    {
        try
        {
            var exePath = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (exePath is null) return false;

            var psi = new ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                Verb            = "runas",
                Arguments       = string.Join(' ', args.Select(a => $"\"{a}\""))
            };
            Process.Start(psi);
            return true;
        }
        catch (Win32Exception)
        {
            // Usuário clicou em "Não" no prompt do UAC (ERROR_CANCELLED)
            return false;
        }
    }
}
