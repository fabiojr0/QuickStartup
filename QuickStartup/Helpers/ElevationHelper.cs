using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace QuickStartup.Helpers;

public static class ElevationHelper
{
    // Nomes das tarefas agendadas usadas para relançar elevado sem prompt de UAC repetido
    // (ver TryRelaunchElevatedSilently/EnsureScheduledTasksRegistered abaixo).
    private const string ManualTaskName  = "QuickStartup-Elevated";
    private const string StartupTaskName = "QuickStartup-ElevatedStartup";

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

    /// <summary>Tenta relançar o app já elevado SEM o prompt do UAC, disparando uma das tarefas
    /// agendadas registradas por EnsureScheduledTasksRegistered. Uma tarefa criada com privilégio
    /// "Highest" por um processo já elevado dispensa novo consentimento do UAC quando executada
    /// via "schtasks /run" — é a técnica padrão para apps que precisam elevar automaticamente a
    /// cada início sem incomodar o usuário toda vez. Retorna false se a tarefa não existe (nunca
    /// foi registrada, foi removida, ou o usuário não é administrador) ou falhar por outro motivo;
    /// quem chamar deve cair para TryRelaunchElevated (UAC normal) nesse caso.</summary>
    public static bool TryRelaunchElevatedSilently(bool isAutoStartup)
    {
        var taskName = isAutoStartup ? StartupTaskName : ManualTaskName;
        return RunSchtasks($"/Run /TN \"{taskName}\"");
    }

    /// <summary>Registra (ou atualiza) as tarefas agendadas usadas por TryRelaunchElevatedSilently,
    /// apontando para o caminho atual do executável. Só funciona quando chamado de dentro de um
    /// processo já elevado — criar uma tarefa com /RL HIGHEST exige token de administrador; sem
    /// elevação, a criação falha silenciosamente (best effort) e o próximo início cai de volta
    /// para o UAC normal via TryRelaunchElevated. Chamado a cada início elevado (idempotente),
    /// não só ao ligar a opção, para autocorrigir o caminho caso o app tenha sido movido/atualizado.
    /// /SC ONCE com uma data já passada é o jeito documentado de criar uma tarefa que nunca
    /// dispara sozinha, só sob demanda via "schtasks /run".</summary>
    public static void EnsureScheduledTasksRegistered()
    {
        var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (exePath is null) return;

        CreateTask(ManualTaskName, exePath, extraArgs: null);
        CreateTask(StartupTaskName, exePath, extraArgs: "--startup");
    }

    /// <summary>Remove as tarefas agendadas — chamado ao desligar "Prioridade alta". Best effort:
    /// se as tarefas nunca existiram (nunca chegou a rodar elevado) não há nada a fazer.</summary>
    public static void RemoveScheduledTasks()
    {
        RunSchtasks($"/Delete /TN \"{ManualTaskName}\" /F");
        RunSchtasks($"/Delete /TN \"{StartupTaskName}\" /F");
    }

    private static void CreateTask(string name, string exePath, string? extraArgs)
    {
        // Aspas simples ao redor do caminho dentro do valor de /TR: forma documentada pelo
        // schtasks para caminhos com espaço, já que ele não lida bem com aspas duplas aninhadas.
        var target = extraArgs is null ? $"'{exePath}'" : $"'{exePath}' {extraArgs}";
        RunSchtasks(
            $"/Create /F /TN \"{name}\" /TR \"{target}\" /SC ONCE /ST 00:00 /SD 01/01/2020 /RL HIGHEST");
    }

    private static bool RunSchtasks(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe", arguments)
            {
                UseShellExecute       = false,
                CreateNoWindow        = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true
            };
            using var process = Process.Start(psi);
            if (process is null) return false;
            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
