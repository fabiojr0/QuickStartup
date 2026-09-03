using QuickStartup.Models;
using WinForms = System.Windows.Forms;

namespace QuickStartup.Services;

public static class MonitorService
{
    public static List<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();
        var screens = WinForms.Screen.AllScreens;

        for (int i = 0; i < screens.Length; i++)
        {
            var s = screens[i];
            monitors.Add(new MonitorInfo
            {
                Index       = i,
                DeviceName  = s.DeviceName,
                Bounds      = s.Bounds,
                WorkingArea = s.WorkingArea,
                IsPrimary   = s.Primary
            });
        }

        // Garante que o monitor principal aparece primeiro
        monitors.Sort((a, b) => a.IsPrimary ? -1 : b.IsPrimary ? 1 : a.Index.CompareTo(b.Index));

        // Re-indexa após ordenar
        for (int i = 0; i < monitors.Count; i++)
            monitors[i].Index = i;

        return monitors;
    }

    public static MonitorInfo? GetMonitor(int index)
    {
        var monitors = GetMonitors();
        return index >= 0 && index < monitors.Count ? monitors[index] : monitors.FirstOrDefault();
    }
}
