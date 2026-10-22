namespace OracleApp;
public sealed record SystemSnapshot(string Os, string Machine, string Uptime, string Cpu, string CpuValue, string Memory, string MemoryValue, string Refreshed);
public sealed record ProcessRow(int Pid, string Name, string Cpu, long MemoryBytes, string Memory, string Status);
public sealed record ProcessDetails(string Name, int Pid, string Memory, string Path, string Publisher, string Status, string Explanation);
public sealed record VolumeInfo(string Name, string Format, string Total, string Free, string Status);
public sealed record FileFinding(string Label, string Value);
public sealed class OracleSettings { public int RefreshSeconds { get; set; } = 5; }
