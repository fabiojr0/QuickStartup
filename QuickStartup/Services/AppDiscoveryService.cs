using System.IO;
using System.Reflection;
using System.Security.Principal;
using Windows.ApplicationModel.Core;
using Windows.Management.Deployment;

namespace QuickStartup.Services;

/// <summary>Descobre apps instalados — tanto atalhos do Menu Iniciar (.exe tradicionais)
/// quanto pacotes UWP/Microsoft Store — para sugerir como opções rápidas ao configurar um perfil.</summary>
public static class AppDiscoveryService
{
    /// <summary>Apps da Microsoft Store/UWP (WhatsApp, Spotify, Xbox, etc.) não têm um .exe
    /// que possa ser iniciado diretamente. O caminho retornado aqui já vem no formato
    /// "shell:AppsFolder\{PackageFamilyName}!{AppId}", que o WindowService sabe iniciar
    /// via "explorer.exe" — o usuário nunca precisa saber dessa diferença.</summary>
    public static List<(string Name, string ShellPath)> GetInstalledUwpApps()
    {
        var results = new List<(string Name, string ShellPath)>();

        try
        {
            var sid = WindowsIdentity.GetCurrent().User?.Value;
            if (string.IsNullOrEmpty(sid)) return results;

            var packageManager = new PackageManager();
            foreach (var package in packageManager.FindPackagesForUser(sid))
            {
                if (package.IsFramework || package.IsResourcePackage) continue;

                IReadOnlyList<AppListEntry> entries;
                try { entries = package.GetAppListEntries(); }
                catch { continue; }

                foreach (var entry in entries)
                {
                    string name;
                    try { name = entry.DisplayInfo.DisplayName; }
                    catch { continue; }

                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (string.IsNullOrWhiteSpace(entry.AppUserModelId)) continue;

                    results.Add((name, $@"shell:AppsFolder\{entry.AppUserModelId}"));
                }
            }
        }
        catch
        {
            // PackageManager indisponível/sem permissão nesta máquina — segue sem sugestões UWP.
        }

        return results;
    }

    public static List<(string Name, string Path)> GetInstalledApps()
    {
        var byPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        object? shell;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return new();
            shell = Activator.CreateInstance(shellType);
            if (shell is null) return new();
        }
        catch
        {
            // WSH indisponível — segue sem sugestões do sistema.
            return new();
        }

        var startMenuDirs = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
        };

        foreach (var dir in startMenuDirs)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;

            IEnumerable<string> shortcuts;
            try { shortcuts = Directory.EnumerateFiles(dir, "*.lnk", SearchOption.AllDirectories); }
            catch { continue; }

            foreach (var lnk in shortcuts)
            {
                var name = Path.GetFileNameWithoutExtension(lnk);
                if (name.Contains("uninstall", StringComparison.OrdinalIgnoreCase)) continue;

                var target = ResolveShortcutTarget(shell, lnk);
                if (string.IsNullOrWhiteSpace(target)) continue;
                if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                if (!File.Exists(target)) continue;

                byPath[target] = name;
            }
        }

        return byPath
            .Select(kv => (Name: kv.Value, Path: kv.Key))
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? ResolveShortcutTarget(object shell, string lnkPath)
    {
        try
        {
            var shellType = shell.GetType();
            var shortcut = shellType.InvokeMember(
                "CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
            if (shortcut is null) return null;

            var shortcutType = shortcut.GetType();
            return shortcutType.InvokeMember(
                "TargetPath", BindingFlags.GetProperty, null, shortcut, null) as string;
        }
        catch
        {
            // Atalho quebrado/inacessível — ignora.
            return null;
        }
    }
}
