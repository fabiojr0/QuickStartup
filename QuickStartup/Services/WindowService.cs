using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using QuickStartup.Helpers;
using QuickStartup.Models;

namespace QuickStartup.Services;

public record AppLaunchResult(string AppName, bool Success, string Message);

public class WindowService
{
    private const int FindWindowTimeoutMs = 10_000;
    private const int PollIntervalMs      = 200;

    public event Action<AppLaunchResult>? AppLaunched;

    public Task ExecuteProfileAsync(
        Profile profile,
        IReadOnlyList<MonitorInfo> monitors,
        CancellationToken ct = default)
    {
        // Compartilhado entre todas as tasks lançadas em paralelo: garante que a mesma janela
        // nunca seja "reivindicada" por duas apps ao mesmo tempo (ver ClaimTracker).
        var claims = new ClaimTracker();

        // Todos os apps são lançados de uma vez — um app lento (ou que estoure o timeout
        // procurando a janela) não atrasa o início dos demais. O DelayMs de cada app continua
        // valendo, só que como uma espera independente antes de reposicionar aquele app específico.
        var tasks = profile.Apps.Select(async app =>
        {
            if (ct.IsCancellationRequested) return;
            var result = await LaunchAndPositionAsync(app, monitors, claims, ct);
            AppLaunched?.Invoke(result);
        });

        return Task.WhenAll(tasks);
    }

    /// <summary>Rastreia, entre todas as apps lançadas em paralelo de um mesmo perfil, quais
    /// janelas já foram atribuídas a alguma app. Sem isso, quando dois apps abrem ao mesmo tempo,
    /// a busca "achou uma janela nova/em foco" de uma pode encontrar a janela que na verdade
    /// pertence à outra (ainda não diferenciável por título/processo nesse instante), fazendo a
    /// janela errada ser movida para a posição configurada para outro app.</summary>
    private sealed class ClaimTracker
    {
        private readonly HashSet<IntPtr> _claimed = new();
        private readonly object _lock = new();

        // Tenta reivindicar hwnd para o chamador atual. Retorna false se outra app em paralelo
        // já reivindicou essa mesma janela primeiro.
        public bool TryClaim(IntPtr hwnd)
        {
            lock (_lock) return _claimed.Add(hwnd);
        }

        public bool IsClaimed(IntPtr hwnd)
        {
            lock (_lock) return _claimed.Contains(hwnd);
        }
    }

    private async Task<AppLaunchResult> LaunchAndPositionAsync(
        AppItem app,
        IReadOnlyList<MonitorInfo> monitors,
        ClaimTracker claims,
        CancellationToken ct)
    {
        var displayName = app.ToString();

        if (string.IsNullOrWhiteSpace(app.ExecutablePath))
            return new(displayName, false, "Caminho do executável não definido.");

        try
        {
            IntPtr hwnd = IsUwpShellPath(app.ExecutablePath)
                ? await LaunchUwpAndFindWindowAsync(app, claims, ct)
                : await LaunchWin32AndFindWindowAsync(app, claims, ct);

            if (hwnd == IntPtr.Zero)
                return new(displayName, false, "Janela não encontrada dentro do timeout.");

            PositionWindow(hwnd, app, monitors);
            return new(displayName, true, "OK");
        }
        catch (OperationCanceledException)
        {
            return new(displayName, false, "Cancelado.");
        }
        catch (Exception ex)
        {
            return new(displayName, false, ex.Message);
        }
    }

    /// <summary>Apps da Microsoft Store (WhatsApp, Spotify, Xbox, etc.) não são um .exe comum —
    /// o caminho vem como "shell:AppsFolder\{PackageFamilyName}!{AppId}" e precisa ser aberto
    /// via explorer.exe. O usuário não precisa saber dessa diferença: ela é só detectada aqui.</summary>
    private static bool IsUwpShellPath(string path) =>
        path.StartsWith(@"shell:AppsFolder\", StringComparison.OrdinalIgnoreCase);

    private static async Task<IntPtr> LaunchWin32AndFindWindowAsync(AppItem app, ClaimTracker claims, CancellationToken ct)
    {
        // Verifica se o processo já está rodando antes de abrir outro
        var existingHwnd = FindExistingWindow(app.ExecutablePath, claims);
        if (existingHwnd != IntPtr.Zero)
        {
            // Restaura caso esteja minimizado
            if (NativeMethods.IsIconic(existingHwnd))
                NativeMethods.ShowWindow(existingHwnd, NativeMethods.SW_RESTORE);
            return existingHwnd;
        }

        var psi = new ProcessStartInfo
        {
            FileName        = app.ExecutablePath,
            Arguments       = app.Arguments,
            UseShellExecute = true
        };

        var process = Process.Start(psi);
        if (process is null) return IntPtr.Zero;

        // Aguarda o delay configurado para o app ter tempo de abrir
        await Task.Delay(app.DelayMs, ct);

        // Tenta localizar o handle da janela principal
        return await WaitForWindowAsync(process, claims, ct);
    }

    private static async Task<IntPtr> LaunchUwpAndFindWindowAsync(AppItem app, ClaimTracker claims, CancellationToken ct)
    {
        // "explorer.exe shell:AppsFolder\...!App" ativa o app: abre uma instância nova se ele
        // não estiver rodando, ou só traz a janela existente para frente (mesmo comportamento
        // de clicar no ícone do app de novo). Por isso não há checagem prévia de "já está rodando".
        var before = SnapshotVisibleWindows();

        Process.Start(new ProcessStartInfo
        {
            FileName        = "explorer.exe",
            Arguments       = app.ExecutablePath,
            UseShellExecute = true
        });

        await Task.Delay(app.DelayMs, ct);
        return await WaitForUwpWindowAsync(before, claims, ct);
    }

    // Aguarda a janela do app UWP: ou uma janela nova aparece (primeira abertura),
    // ou o app já estava aberto e a janela existente foi trazida para frente.
    private static async Task<IntPtr> WaitForUwpWindowAsync(HashSet<IntPtr> before, ClaimTracker claims, CancellationToken ct)
    {
        var deadline  = DateTime.UtcNow.AddMilliseconds(FindWindowTimeoutMs);
        var firstPass = true;

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            var newWindow = FindNewVisibleWindow(before, claims);
            if (newWindow != IntPtr.Zero) return newWindow;

            if (!firstPass)
            {
                // Quando dois apps UWP abrem ao mesmo tempo, a janela em primeiro plano pode
                // pertencer à OUTRA app que está sendo lançada em paralelo — só aceita se ainda
                // não foi reivindicada por ela (claims.TryClaim é atômico entre as tasks).
                var fg = NativeMethods.GetForegroundWindow();
                if (fg != IntPtr.Zero && !IsOwnProcessWindow(fg)
                    && NativeMethods.IsWindowVisible(fg) && GetWindowTitle(fg).Length > 0
                    && claims.TryClaim(fg))
                    return fg;
            }

            firstPass = false;
            await Task.Delay(PollIntervalMs, ct);
        }

        return IntPtr.Zero;
    }

    private static HashSet<IntPtr> SnapshotVisibleWindows()
    {
        var set = new HashSet<IntPtr>();
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (NativeMethods.IsWindowVisible(hwnd)) set.Add(hwnd);
            return true;
        }, IntPtr.Zero);
        return set;
    }

    private static IntPtr FindNewVisibleWindow(HashSet<IntPtr> before, ClaimTracker claims)
    {
        IntPtr found = IntPtr.Zero;

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (before.Contains(hwnd)) return true;
            if (!NativeMethods.IsWindowVisible(hwnd)) return true;
            if (IsOwnProcessWindow(hwnd)) return true;
            // Ignora popups (têm "dono") e tool windows (tooltips, notificações)
            if (NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER) != IntPtr.Zero) return true;
            if ((NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE) & NativeMethods.WS_EX_TOOLWINDOW) != 0) return true;
            if (GetWindowTitle(hwnd).Length == 0) return true;
            // Outra app lançada em paralelo já reivindicou essa janela — continua procurando.
            if (!claims.TryClaim(hwnd)) return true;

            found = hwnd;
            return false;
        }, IntPtr.Zero);

        return found;
    }

    private static readonly uint OwnProcessId = (uint)Environment.ProcessId;

    private static bool IsOwnProcessWindow(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        return pid == OwnProcessId;
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        int len = NativeMethods.GetWindowTextLength(hwnd);
        if (len == 0) return "";
        var sb = new System.Text.StringBuilder(len + 1);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    // Busca uma janela visível de nível superior cujo processo-pai seja o executável.
    // Só considera janelas que já existiam ANTES desse lançamento (i.e. uma instância que já
    // estava aberta) — uma janela recém-criada por outra app do mesmo perfil, ainda não
    // reivindicada, não conta como "já rodando" aqui, senão a checagem "já está rodando" de uma
    // app pode roubar a janela que na verdade pertence a outra app do mesmo executável lançada
    // em paralelo (ex.: duas entradas apontando pro mesmo app).
    private static IntPtr FindExistingWindow(string executablePath, ClaimTracker claims)
    {
        var exeName = Path.GetFileNameWithoutExtension(executablePath).ToLowerInvariant();
        IntPtr found = IntPtr.Zero;

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd)) return true;
            if (claims.IsClaimed(hwnd)) return true;

            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            try
            {
                var proc = Process.GetProcessById((int)pid);
                if (proc.ProcessName.ToLowerInvariant() == exeName)
                {
                    // Reivindica atomicamente: evita que outra app concorrente pegue essa
                    // mesma janela entre a checagem e o uso.
                    if (!claims.TryClaim(hwnd)) return true;
                    found = hwnd;
                    return false; // Para a enumeração
                }
            }
            catch { /* processo pode ter terminado */ }

            return true;
        }, IntPtr.Zero);

        return found;
    }

    // Aguarda a janela principal do processo aparecer (com timeout)
    private static async Task<IntPtr> WaitForWindowAsync(Process process, ClaimTracker claims, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(FindWindowTimeoutMs);

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            // Tenta via MainWindowHandle (funciona para apps simples)
            try
            {
                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    claims.TryClaim(process.MainWindowHandle);
                    return process.MainWindowHandle;
                }
            }
            catch { /* processo pode não existir mais */ }

            // Fallback: EnumWindows para apps que criam processos filhos (Chrome, Discord)
            var hwnd = FindWindowByPid(process.Id);
            if (hwnd != IntPtr.Zero)
            {
                claims.TryClaim(hwnd);
                return hwnd;
            }

            await Task.Delay(PollIntervalMs, ct);
        }

        return IntPtr.Zero;
    }

    // Encontra uma janela visível pelo PID do processo
    private static IntPtr FindWindowByPid(int pid)
    {
        IntPtr found = IntPtr.Zero;

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd)) return true;
            NativeMethods.GetWindowThreadProcessId(hwnd, out uint wpid);
            if ((int)wpid == pid)
            {
                found = hwnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        return found;
    }

    private static void PositionWindow(IntPtr hwnd, AppItem app, IReadOnlyList<MonitorInfo> monitors)
    {
        var monitor = app.MonitorIndex < monitors.Count
            ? monitors[app.MonitorIndex]
            : monitors.FirstOrDefault();

        if (monitor is null) return;

        if (app.Maximize)
        {
            // Move para o monitor correto antes de maximizar
            NativeMethods.SetWindowPos(
                hwnd, IntPtr.Zero,
                monitor.Bounds.X, monitor.Bounds.Y,
                monitor.Bounds.Width, monitor.Bounds.Height,
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MAXIMIZE);
        }
        else
        {
            int width, height, absX, absY;

            if (app.UsePercentSize)
            {
                // WorkingArea (não Bounds) — exclui a barra de tarefas, então "Cima/Baixo/
                // Esquerda/Direita" (100% de altura ou largura) nunca ficam por baixo dela,
                // não importa em qual borda da tela ela esteja ancorada.
                var area = monitor.WorkingArea;
                width  = (int)Math.Round(area.Width  * Clamp01_100(app.WidthPercent)  / 100.0);
                height = (int)Math.Round(area.Height * Clamp01_100(app.HeightPercent) / 100.0);
                absX   = area.X + (int)Math.Round(area.Width  * Clamp01_100(app.XPercent) / 100.0);
                absY   = area.Y + (int)Math.Round(area.Height * Clamp01_100(app.YPercent) / 100.0);
            }
            else
            {
                width  = app.Width;
                height = app.Height;
                // Coordenadas absolutas na tela: posição do monitor + deslocamento local
                absX   = monitor.Bounds.X + app.X;
                absY   = monitor.Bounds.Y + app.Y;
            }

            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
            SetWindowBoundsCompensated(hwnd, absX, absY, width, height);
        }
    }

    /// <summary>Posiciona a janela para que sua área REALMENTE VISÍVEL fique exatamente no
    /// retângulo pedido. No Windows 10/11, SetWindowPos posiciona o retângulo "bruto" da janela,
    /// que inclui uma borda invisível reservada pelo DWM (sombra/redimensionamento) — sem
    /// compensar isso, a janela visível fica alguns pixels menor que o pedido e sobra um
    /// pedaço de tela vazio nas bordas (ex: em "Cima"/"Baixo" a largura não fica 100%).</summary>
    private static void SetWindowBoundsCompensated(IntPtr hwnd, int x, int y, int width, int height)
    {
        NativeMethods.SetWindowPos(
            hwnd, IntPtr.Zero, x, y, width, height,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_SHOWWINDOW);

        var hasOuter   = NativeMethods.GetWindowRect(hwnd, out var outer);
        var dwmSuccess = NativeMethods.DwmGetWindowAttribute(
            hwnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS, out var visible, Marshal.SizeOf<NativeMethods.RECT>());

        if (!hasOuter || dwmSuccess != 0) return;

        int marginLeft   = outer.Left   - visible.Left;
        int marginTop    = outer.Top    - visible.Top;
        int marginRight  = outer.Right  - visible.Right;
        int marginBottom = outer.Bottom - visible.Bottom;

        if (marginLeft == 0 && marginTop == 0 && marginRight == 0 && marginBottom == 0) return;

        NativeMethods.SetWindowPos(
            hwnd, IntPtr.Zero,
            x + marginLeft, y + marginTop,
            width  + (marginRight  - marginLeft),
            height + (marginBottom - marginTop),
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_SHOWWINDOW);
    }

    private static double Clamp01_100(double percent) => Math.Clamp(percent, 0, 100);
}
