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

    /// <summary>Chave que identifica um atalho de forma única: caminho + argumentos. Necessário
    /// porque launchers como Riot Client/Steam/Ubisoft Connect criam vários atalhos apontando para
    /// o MESMO .exe do launcher, diferindo só nos argumentos (ex.: "--launch-product=league_of_legends")
    /// — deduplicar só por caminho faria um atalho sobrescrever silenciosamente o outro.</summary>
    private readonly record struct ShortcutKey(string Path, string Arguments)
    {
        public static ShortcutKey Of(string path, string arguments) =>
            new(path.ToLowerInvariant(), arguments.ToLowerInvariant());
    }

    public static List<(string Name, string Path, string Arguments)> GetInstalledApps()
    {
        var byKey = new Dictionary<ShortcutKey, (string Name, string Path, string Arguments)>();

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

            foreach (var lnk in EnumerateShortcutsSafe(dir))
            {
                var name = Path.GetFileNameWithoutExtension(lnk);
                if (name.Contains("uninstall", StringComparison.OrdinalIgnoreCase)) continue;

                var (target, arguments) = ResolveShortcutTarget(shell, lnk);
                if (string.IsNullOrWhiteSpace(target)) continue;
                if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                if (!File.Exists(target)) continue;

                byKey[ShortcutKey.Of(target, arguments)] = (name, target, arguments);
            }
        }

        return byKey.Values
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Percorre atalhos .lnk recursivamente, pulando subpastas sem permissão de acesso
    /// em vez de abortar a busca inteira. Directory.EnumerateFiles(..., AllDirectories) não serve
    /// aqui: é avaliado de forma preguiçosa, então um UnauthorizedAccessException numa subpasta
    /// (ex.: "Startup" com ACL restrita nesta máquina) estoura no meio do foreach do chamador,
    /// não na chamada em si — por isso força a lista com ToList() dentro do try.</summary>
    private static IEnumerable<string> EnumerateShortcutsSafe(string dir)
    {
        List<string> files;
        try { files = Directory.EnumerateFiles(dir, "*.lnk").ToList(); }
        catch { files = new List<string>(); }

        foreach (var file in files)
            yield return file;

        List<string> subDirs;
        try { subDirs = Directory.EnumerateDirectories(dir).ToList(); }
        catch { subDirs = new List<string>(); }

        foreach (var subDir in subDirs)
            foreach (var file in EnumerateShortcutsSafe(subDir))
                yield return file;
    }

    private static (string? Target, string Arguments) ResolveShortcutTarget(object shell, string lnkPath)
    {
        try
        {
            var shellType = shell.GetType();
            var shortcut = shellType.InvokeMember(
                "CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
            if (shortcut is null) return (null, "");

            var shortcutType = shortcut.GetType();
            var target = shortcutType.InvokeMember(
                "TargetPath", BindingFlags.GetProperty, null, shortcut, null) as string;
            var arguments = shortcutType.InvokeMember(
                "Arguments", BindingFlags.GetProperty, null, shortcut, null) as string;

            return (target, arguments ?? "");
        }
        catch
        {
            // Atalho quebrado/inacessível — ignora.
            return (null, "");
        }
    }
}
