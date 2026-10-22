using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace OracleApp;

public sealed class SystemInspector
{
    private readonly Dictionary<int, (TimeSpan Cpu, DateTime Time)> _previous = new();
    public SystemSnapshot Snapshot()
    {
        var (total, available) = PhysicalMemory.Read(); var used = Math.Max(0, total - available);
        var cpu = SystemCpu.ReadPercent();
        var os = RuntimeInformation.OSDescription;
        var up = TimeSpan.FromMilliseconds(Environment.TickCount64);
        return new(os, Environment.MachineName, $"{(int)up.TotalDays}d {up.Hours:00}h {up.Minutes:00}m", cpu is null ? "Unavailable" : $"{cpu:0}%", cpu?.ToString("0.0", CultureInfo.InvariantCulture) ?? "", total > 0 ? $"{FormatBytes(used)} / {FormatBytes(total)}" : "Unavailable", total > 0 ? (used * 100d / total).ToString("0.0", CultureInfo.InvariantCulture) : "", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
    }
    public IReadOnlyList<ProcessRow> Processes()
    {
        var now = DateTime.UtcNow;
        var rows = new List<ProcessRow>(); var inaccessible = 0;
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                try
                {
                    var pid = p.Id; var cpu = p.TotalProcessorTime; var mem = p.WorkingSet64;
                    string cpuText = "—";
                    if (_previous.TryGetValue(pid, out var old)) { var secs = (now - old.Time).TotalSeconds; if (secs > 0) cpuText = Math.Clamp((cpu - old.Cpu).TotalMilliseconds / (secs * Environment.ProcessorCount * 10), 0, 100).ToString("0.0") + "%"; }
                    _previous[pid] = (cpu, now);
                    string status; try { status = p.Responding ? "Running" : "Not responding"; } catch { status = "Running"; }
                    rows.Add(new(pid, p.ProcessName, cpuText, mem, FormatBytes(mem), status));
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { inaccessible++; }
            }
        }
        var live = rows.Select(r => r.Pid).ToHashSet(); foreach (var stale in _previous.Keys.Where(k => !live.Contains(k)).ToArray()) _previous.Remove(stale);
        if (inaccessible > 0) OracleLog.Add("WARN", $"{inaccessible} process entr(y/ies) changed or could not be read during refresh; unavailable entries were skipped.");
        return rows.OrderByDescending(r => r.MemoryBytes).ToArray();
    }
    public ProcessDetails ProcessInfo(int pid)
    {
        using var p = Process.GetProcessById(pid);
        var mem = "Unavailable"; var path = "Unavailable"; var publisher = "Not verified"; var status = "Unavailable";
        try { mem = FormatBytes(p.WorkingSet64); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { OracleLog.Add("WARN", $"Memory for process {pid} became unavailable during inspection."); }
        try { path = p.MainModule?.FileName ?? "Unavailable"; publisher = "Signature verification unavailable"; } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or System.Security.SecurityException) { path = "Access unavailable"; OracleLog.Add("WARN", $"Executable path for process {pid} is unavailable (it may have exited or access was denied)."); }
        try { status = p.Responding ? "Running" : "Not responding"; } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { OracleLog.Add("WARN", $"Status for process {pid} became unavailable during inspection."); }
        return new(p.ProcessName, pid, mem, path, publisher, status, "A process is a running program. Its name and resource use are observed facts; they do not establish intent or behavior.");
    }
    public (string Os, string Cpu, int Cores, string Architecture, string Memory, IReadOnlyList<VolumeInfo> Volumes) Atlas()
    {
        string cpu = "Unavailable"; try { using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"); cpu = key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? cpu; } catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException) { }
        var volumes = new List<VolumeInfo>();
        foreach (var d in DriveInfo.GetDrives()) try { if (!d.IsReady) { volumes.Add(new(d.Name, "—", "—", "—", "Not ready")); continue; } volumes.Add(new(d.Name, d.DriveFormat, FormatBytes(d.TotalSize), FormatBytes(d.AvailableFreeSpace), "Ready")); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { volumes.Add(new(d.Name, "—", "—", "—", "Unavailable")); }
        var (total, _) = PhysicalMemory.Read(); var memory = total > 0 ? FormatBytes(total) : "Unavailable";
        return (RuntimeInformation.OSDescription, cpu, Environment.ProcessorCount, RuntimeInformation.OSArchitecture.ToString(), memory, volumes);
    }
    public static string FormatBytes(long value) => value < 1024 ? $"{value} B" : string.Format(CultureInfo.InvariantCulture, "{0:0.0} {1}", value / Math.Pow(1024, Math.Min(4, (int)(Math.Log(value) / Math.Log(1024)))), new[] { "B", "KB", "MB", "GB", "TB" }[Math.Min(4, (int)(Math.Log(value) / Math.Log(1024)))]);
}
internal static class PhysicalMemory
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] private struct MEMORYSTATUSEX { public uint dwLength; public uint dwMemoryLoad; public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual; }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);
    public static (long Total, long Available) Read() { var s = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() }; return GlobalMemoryStatusEx(ref s) ? ((long)s.ullTotalPhys, (long)s.ullAvailPhys) : (0, 0); }
}
internal static class SystemCpu
{
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);
    [StructLayout(LayoutKind.Sequential)] private struct FILETIME { public uint Low; public uint High; public long Value => ((long)High << 32) | Low; }
    private static long? _idle, _total;
    public static double? ReadPercent() { if (!GetSystemTimes(out var i, out var k, out var u)) return null; var idle = i.Value; var total = k.Value + u.Value; if (_idle is null) { _idle = idle; _total = total; return null; } var di = idle - _idle.Value; var dt = total - _total!.Value; _idle = idle; _total = total; return dt > 0 ? Math.Clamp((dt - di) * 100d / dt, 0, 100) : null; }
}
