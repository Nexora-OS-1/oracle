using System.IO;
namespace OracleApp;
internal static class CoreChecks
{
    public static void Run()
    {
        Require(SystemInspector.FormatBytes(0) == "0 B", "zero byte formatting");
        Require(SystemInspector.FormatBytes(1024) == "1.0 KB", "kilobyte formatting");
        var file = Path.Combine(Path.GetTempPath(), "oracle-check-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            File.WriteAllText(file, "TODO: remove this\npassword=sample\nhello");
            var report = FileInspector.Inspect(file);
            Require(report.Encoding == "UTF-8", "UTF-8 detection");
            Require(report.Findings.Any(x => x.Label == "Text match: TODO"), "TODO keyword finding");
            Require(report.Findings.Any(x => x.Label == "Text match: password"), "password keyword finding");
            Require(report.Note.Contains("not a full malware scanner", StringComparison.OrdinalIgnoreCase), "static analysis limitation disclosure");
            File.WriteAllBytes(file, new byte[2_000_001]);
            Require(FileInspector.Inspect(file).Preview.Contains("over 2 MB", StringComparison.Ordinal), "large-file limit");
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }
    private static void Require(bool condition, string check) { if (!condition) throw new InvalidOperationException("Failed: " + check); }
}
