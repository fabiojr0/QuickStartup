using System.Runtime.InteropServices;

namespace QuickStartup.Helpers;

/// <summary>
/// Declarações P/Invoke para funções da user32.dll usadas no reposicionamento de janelas.
/// </summary>
public static class NativeMethods
{
    // ── SetWindowPos ─────────────────────────────────────────────────────────
    // Move e redimensiona uma janela. uFlags controla o comportamento.
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X, int Y,
        int cx, int cy,
        uint uFlags);

    // ── ShowWindow ────────────────────────────────────────────────────────────
    // Controla o estado de exibição da janela (maximizar, restaurar, etc.).
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    // ── EnumWindows ───────────────────────────────────────────────────────────
    // Itera por todas as janelas de nível superior para encontrar a janela de um processo
    // que pode ter criado múltiplos processos filhos (ex: Chrome, Discord).
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    // ── GetWindowThreadProcessId ──────────────────────────────────────────────
    // Obtém o PID do processo dono de uma janela — usado junto com EnumWindows.
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    // ── IsWindowVisible ───────────────────────────────────────────────────────
    // Verifica se a janela está visível (não minimizada ou oculta).
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    // ── GetWindowRect ─────────────────────────────────────────────────────────
    // Obtém as coordenadas atuais da janela na tela.
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    // ── IsIconic ──────────────────────────────────────────────────────────────
    // Retorna true se a janela está minimizada.
    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    // ── GetForegroundWindow ───────────────────────────────────────────────────
    // Usado para detectar apps UWP que já estavam rodando e só foram trazidos à frente
    // (nesse caso nenhuma janela "nova" aparece para o EnumWindows detectar).
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    // ── GetWindowText / GetWindowTextLength ───────────────────────────────────
    // Usado para filtrar janelas auxiliares sem título ao procurar a janela de um app UWP recém-aberto.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    // ── GetWindow / GetWindowLong ──────────────────────────────────────────────
    // Usados para descartar janelas "donas" (popups) e tool windows ao localizar
    // a janela recém-criada de um app UWP.
    [DllImport("user32.dll")]
    public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    public const uint GW_OWNER          = 4;
    public const int  GWL_EXSTYLE       = -20;
    public const int  WS_EX_TOOLWINDOW  = 0x00000080;

    // ── DwmGetWindowAttribute ─────────────────────────────────────────────────
    // No Windows 10/11, GetWindowRect inclui uma borda invisível ao redor de janelas
    // "restauradas" (reservada pelo DWM para sombra/redimensionamento) que não faz parte
    // da área realmente visível. DWMWA_EXTENDED_FRAME_BOUNDS dá o retângulo visível de
    // verdade — usado para compensar essa borda ao posicionar janelas (senão sobra um
    // "pedaço" de tela vazio nas bordas em vez da janela ocupar 100%).
    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    // ── Constantes SWP (SetWindowPos flags) ───────────────────────────────────
    public const uint SWP_NOZORDER    = 0x0004; // Mantém a ordem Z atual
    public const uint SWP_NOACTIVATE  = 0x0010; // Não ativa a janela
    public const uint SWP_SHOWWINDOW  = 0x0040; // Exibe a janela
    public const uint SWP_NOSIZE      = 0x0001; // Mantém o tamanho atual
    public const uint SWP_NOMOVE      = 0x0002; // Mantém a posição atual

    // ── Constantes ShowWindow ─────────────────────────────────────────────────
    public const int SW_RESTORE   = 9;
    public const int SW_MAXIMIZE  = 3;
    public const int SW_MINIMIZE  = 6;
    public const int SW_SHOW      = 5;

    // ── Delegate para EnumWindows ─────────────────────────────────────────────
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    // ── Estrutura RECT ────────────────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }
}
