using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace QuickStartup.Services;

public record UpdateInfo(string Version, string DownloadUrl);

/// <summary>Checa por uma versão mais nova publicada como GitHub Release — forma gratuita
/// de distribuir atualizações sem precisar de servidor próprio nem assinatura de código.
/// A API de releases é pública e não exige autenticação para repositórios públicos.</summary>
public static class UpdateService
{
    private const string Owner = "fabiojr0";
    private const string Repo  = "QuickStartup";
    private static readonly Uri LatestReleaseApiUrl =
        new($"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");

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

            // Prefere linkar direto pro .exe anexado ao release; cai pra página do release se não achar.
            var downloadUrl = releaseUrl;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                    if (name is not null && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        && asset.TryGetProperty("browser_download_url", out var urlEl))
                    {
                        downloadUrl = urlEl.GetString() ?? releaseUrl;
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(downloadUrl)) return null;
            return new UpdateInfo(latestVersion.ToString(), downloadUrl);
        }
        catch
        {
            // Sem internet, limite de requisições da API, JSON inesperado etc. — checagem
            // de atualização nunca deve impedir o app de abrir normalmente.
            return null;
        }
    }
}
