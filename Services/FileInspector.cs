using System.Text;
using System.Text.RegularExpressions;
using System.IO;
namespace OracleApp;
public sealed record FileReport(string Target, string Kind, string Size, string Encoding, string Preview, IReadOnlyList<FileFinding> Findings, string Note);
public static class FileInspector
{
    private static readonly string[] Extensions = [".cs", ".txt", ".md", ".json", ".xml", ".yaml", ".yml", ".html", ".htm", ".css", ".js", ".ts", ".py", ".ps1", ".bat", ".cmd", ".log", ".ini", ".config", ".sql", ".java", ".cpp", ".h", ".csv"];
    private static readonly string[] Terms = ["TODO", "FIXME", "password", "secret", "api_key", "http://", "https://", "Invoke-Expression", "eval("];
    public static IReadOnlyList<string> FolderFiles(string folder) => Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly).Take(500).ToArray();
    public static FileReport Inspect(string path)
    {
        var info = new FileInfo(path); if (!info.Exists) throw new FileNotFoundException("The selected file no longer exists.", path);
        if (info.Length > 2_000_000) return new(path, Kind(path), SystemInspector.FormatBytes(info.Length), "Not read", "Preview unavailable: files over 2 MB are not loaded.", [], "Static inspection only. This is not a full malware scanner; the file was not executed.");
        var findings = new List<FileFinding> { new("File type", Kind(path)), new("Size", SystemInspector.FormatBytes(info.Length)), new("Modified", info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")) };
        if (Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase))
        {
            try { var v = System.Diagnostics.FileVersionInfo.GetVersionInfo(path); findings.Add(new("Product", string.IsNullOrWhiteSpace(v.ProductName) ? "Unavailable" : v.ProductName)); findings.Add(new("Company metadata", string.IsNullOrWhiteSpace(v.CompanyName) ? "Unavailable" : v.CompanyName)); findings.Add(new("File version", string.IsNullOrWhiteSpace(v.FileVersion) ? "Unavailable" : v.FileVersion)); } catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or UnauthorizedAccessException) { findings.Add(new("Executable metadata", "Unavailable")); }
            findings.Add(new("Authenticode signature", "Verification unavailable in this version"));
            return new(path, Kind(path), SystemInspector.FormatBytes(info.Length), "Binary", "Executable content is not previewed or run.", findings, "Metadata only. This is not a full malware scanner; the file was not executed.");
        }
        if (!Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase) || info.Length == 0) return new(path, Kind(path), SystemInspector.FormatBytes(info.Length), "Unknown", "No text preview available (unsupported, empty, or binary file).", findings, "Static inspection only. This is not a full malware scanner; the file was not executed.");
        var bytes = File.ReadAllBytes(path); var (encoding, text) = Decode(bytes); findings.Add(new("Detected encoding", encoding));
        foreach (var term in Terms) { var count = Regex.Matches(text, Regex.Escape(term), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count; if (count > 0) findings.Add(new("Text match: " + term, $"{count} occurrence(s)")); }
        return new(path, Kind(path), SystemInspector.FormatBytes(info.Length), encoding, text.Length > 12000 ? text[..12000] + "\n… preview truncated …" : text, findings, "Keyword matches are simple text matches and may be false positives. This is not a full malware scanner; the file was not executed.");
    }
    private static (string, string) Decode(byte[] bytes) { if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return ("UTF-8 BOM", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3)); if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return ("UTF-16 LE", Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2)); if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) return ("UTF-16 BE", Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2)); try { return ("UTF-8", new UTF8Encoding(false, true).GetString(bytes)); } catch (DecoderFallbackException) { return ("Latin-1 fallback", Encoding.Latin1.GetString(bytes)); } }
    private static string Kind(string path) => Path.GetExtension(path).ToLowerInvariant() switch { ".exe" => "Windows executable", ".dll" => "Dynamic library", ".cs" => "C# source", ".ps1" => "PowerShell source", ".py" => "Python source", ".json" => "JSON text", ".md" => "Markdown text", ".txt" => "Plain text", _ => string.IsNullOrEmpty(Path.GetExtension(path)) ? "File" : Path.GetExtension(path).TrimStart('.').ToUpperInvariant() + " file" };
}
