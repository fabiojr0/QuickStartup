using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace QuickStartup.Services;

/// <param name="InstallerUrl">
/// Asset do instalador Inno Setup anexado ao release (ver installer/setup.iss), null se o
/// release não tiver um asset nesse formato — nesse caso só dá pra abrir <see cref="ReleasePageUrl"/>
/// manualmente, já que não é seguro rodar qualquer outro .exe com flags de instalador silencioso.
/// </param>
public record UpdateInfo(string Version, string? InstallerUrl, string ReleasePageUrl);

/// <summary>Checa por uma versão mais nova publicada como GitHub Release — forma gratuita
/// de distribuir atualizações sem precisar de servidor próprio nem assinatura de código.
/// A API de releases é pública e não exige autenticação para repositórios públicos.</summary>
public static class UpdateService
{
    private const string Owner = "fabiojr0";
    private const string Repo  = "QuickStartup";
    private static readonly Uri LatestReleaseApiUrl =
        new($"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");

    // Prefixo do asset gerado pelo Inno Setup (ver OutputBaseFilename em installer/setup.iss) —
    // usado pra diferenciar o instalador do .exe portátil que também vai no mesmo release.
    private const string InstallerAssetPrefix = "QuickStartup-Setup-";

    /// <summary>Lê a versão do próprio app a partir do &lt;Version&gt; do .csproj (embutido
    /// como AssemblyInformationalVersion), para comparar com a tag do release no mesmo formato.</summary>
    public static Version GetCurrentVersion()
    {
        var info = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // dotnet às vezes anexa "+<hash-do-commit>" — descarta, pois só a versão importa aqui.
        var clean = info?.Split('+')[0];
        return Version.TryParse(clean, out var v) ? v : new Version(0, 0, 0);
    }

    public static async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            // A API do GitHub exige um User-Agent identificável, senão rejeita a requisição.
            http.DefaultRequestHeaders.UserAgent.ParseAdd("QuickStartup-UpdateChecker");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            using var response = await http.GetAsync(LatestReleaseApiUrl, ct);
            if (!response.IsSuccessStatusCode) return null; // sem releases, repo privado, sem internet, etc.

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            var tagName = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(tagName)) return null;

            var latestVersionText = tagName.TrimStart('v', 'V');
            if (!Version.TryParse(latestVersionText, out var latestVersion)) return null;
            if (latestVersion <= GetCurrentVersion()) return null;

            var releaseUrl = root.TryGetProperty("html_url", out var htmlUrlEl) ? htmlUrlEl.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(releaseUrl)) return null;

            string? installerUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                    if (name is not null
                        && name.StartsWith(InstallerAssetPrefix, StringComparison.OrdinalIgnoreCase)
                        && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        && asset.TryGetProperty("browser_download_url", out var urlEl))
                    {
                        installerUrl = urlEl.GetString();
                        break;
                    }
                }
            }

            return new UpdateInfo(latestVersion.ToString(), installerUrl, releaseUrl);
        }
        catch
        {
            // Sem internet, limite de requisições da API, JSON inesperado etc. — checagem
            // de atualização nunca deve impedir o app de abrir normalmente.
            return null;
        }
    }

    /// <summary>Baixa o instalador para uma pasta temporária. Retorna o caminho local do
    /// arquivo baixado, ou null se o download falhar por qualquer motivo.</summary>
    public static async Task<string?> DownloadInstallerAsync(string installerUrl, CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("QuickStartup-UpdateChecker");

            using var response = await http.GetAsync(installerUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) return null;

            var fileName = Path.GetFileName(new Uri(installerUrl).LocalPath);
            if (string.IsNullOrWhiteSpace(fileName)) fileName = "QuickStartup-Update.exe";
            var destPath = Path.Combine(Path.GetTempPath(), fileName);

            await using (var fileStream = File.Create(destPath))
            await using (var httpStream = await response.Content.ReadAsStreamAsync(ct))
            {
                await httpStream.CopyToAsync(fileStream, ct);
            }

            return destPath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Roda o instalador silenciosamente, sem mostrar o assistente. O app deve se
    /// encerrar logo em seguida (ver RequestAppExitAction em MainViewModel) para que o
    /// instalador consiga sobrescrever o .exe em uso; AppMutex em setup.iss serve de
    /// segurança extra caso o encerramento não tenha sido rápido o bastante.</summary>
    public static void RunInstallerSilently(string installerPath)
    {
        Process.Start(new ProcessStartInfo(installerPath)
        {
            Arguments       = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
            UseShellExecute = true
        });
    }
}
