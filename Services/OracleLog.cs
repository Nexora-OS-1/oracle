using System.Collections.ObjectModel;
namespace OracleApp;
public sealed record EventEntry(string Timestamp, string Level, string Message);
public static class OracleLog
{
    public static ObservableCollection<EventEntry> Entries { get; } = [];
    public static event Action? Changed;
    public static void Add(string level, string message) { Entries.Insert(0, new(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), level, message)); while (Entries.Count > 1000) Entries.RemoveAt(Entries.Count - 1); Changed?.Invoke(); }
    public static void Clear() { Entries.Clear(); Changed?.Invoke(); }
}
