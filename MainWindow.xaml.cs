using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace OracleApp;

public partial class MainWindow : Window
{
    private readonly SystemInspector _system = new();
    private readonly DispatcherTimer _timer = new();
    private OracleSettings _settings = new();
    private string _active = "DASHBOARD";
    private ProcessRow? _selectedProcess;
    private bool _refreshing;
    private readonly ObservableCollection<ProcessRow> _processRows = [];
    private readonly ObservableCollection<EventEntry> _eventRows = OracleLog.Entries;
    private TextBox? _labText;
    private TextBlock? _labPath;
    private TextBox? _processSearch;
    private DataGrid? _processGrid;
    private StackPanel? _processDetail;
    private readonly string _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ORACLE", "settings.json");
    private static readonly string[] Pages = ["DASHBOARD", "TRACE", "WHY", "LENS", "ATLAS", "LAB", "EVENT CONSOLE", "SETTINGS"];

    public MainWindow()
    {
        InitializeComponent(); LoadBrandAssets(); LoadSettings(); BuildNavigation(); OracleLog.Add("INFO", "ORACLE started. System data remains on this device.");
        OracleLog.Changed += RefreshEventView;
        _timer.Interval = TimeSpan.FromSeconds(_settings.RefreshSeconds); _timer.Tick += async (_, _) => { if (_active == "DASHBOARD") await RefreshDashboard(); }; _timer.Start();
        ShowPage("DASHBOARD"); Closed += (_, _) => { _timer.Stop(); OracleLog.Changed -= RefreshEventView; };
    }
    private void LoadBrandAssets()
    {
        try
        {
            var assetDirectory = Path.Combine(AppContext.BaseDirectory, "Assets");
            var logoPath = Path.Combine(assetDirectory, "oracle-mark.png"); var iconPath = Path.Combine(assetDirectory, "oracle.ico");
            if (File.Exists(logoPath)) { var logo = new System.Windows.Media.Imaging.BitmapImage(); logo.BeginInit(); logo.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; logo.UriSource = new Uri(logoPath, UriKind.Absolute); logo.EndInit(); logo.Freeze(); BrandMark.Source = logo; }
            if (File.Exists(iconPath)) { var icon = new System.Windows.Media.Imaging.BitmapImage(); icon.BeginInit(); icon.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; icon.UriSource = new Uri(iconPath, UriKind.Absolute); icon.EndInit(); icon.Freeze(); Icon = icon; }
        }
        catch (Exception ex) { OracleLog.Add("WARN", "One or more local brand assets could not be loaded: " + ex.Message); }
    }

    private void BuildNavigation()
    {
        var labels = new Dictionary<string, string> { ["DASHBOARD"] = "Dashboard", ["TRACE"] = "Processes", ["WHY"] = "Process details", ["LENS"] = "File inspection", ["ATLAS"] = "System information", ["LAB"] = "Snippet editor", ["EVENT CONSOLE"] = "Activity", ["SETTINGS"] = "Settings" };
        foreach (var page in Pages)
        {
            var glyph = new System.Windows.Shapes.Path { Data = Geometry.Parse(NavGeometry(page)), Stroke = (Brush)FindResource("Muted"), StrokeThickness = 1.45, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Fill = Brushes.Transparent, Width = 19, Height = 19, Stretch = Stretch.Uniform, Margin = new Thickness(1, 0, 13, 0), VerticalAlignment = VerticalAlignment.Center };
            var label = new TextBlock { Text = labels[page], VerticalAlignment = VerticalAlignment.Center, FontSize = 12.5 };
            var content = new StackPanel { Orientation = Orientation.Horizontal }; content.Children.Add(glyph); content.Children.Add(label);
            var b = new Button { Content = content, Tag = page, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 11, 8, 11), Margin = new Thickness(0, 1, 0, 3), BorderThickness = new Thickness(1), BorderBrush = Brushes.Transparent, Background = Brushes.Transparent, Foreground = (Brush)FindResource("Muted"), FontSize = 12, ToolTip = labels[page] }; AutomationProperties.SetName(b, labels[page]);
            b.Click += (_, _) => ShowPage(page); Navigation.Children.Add(b);
        }
    }
    private static string NavGeometry(string page) => page switch
    {
        "DASHBOARD" => "M3,3 H10 V10 H3 Z M14,3 H21 V10 H14 Z M3,14 H10 V21 H3 Z M14,14 H21 V21 H14 Z",
        "TRACE" => "M3,5 H21 M3,12 H21 M3,19 H21 M6,5 V5 M12,12 V12 M18,19 V19",
        "WHY" => "M12,21 A9,9 0 1 1 12,3 A9,9 0 1 1 12,21 M9.5,9 A2.6,2.6 0 1 1 14,11 C12,12 12,13 12,14 M12,17 V17.2",
        "LENS" => "M10.5,3 A7.5,7.5 0 1 0 10.5,18 A7.5,7.5 0 1 0 10.5,3 M16,16 L21,21",
        "ATLAS" => "M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 M3.5,12 H20.5 M12,3 C8,7 8,17 12,21 M12,3 C16,7 16,17 12,21",
        "LAB" => "M8,5 L3,12 L8,19 M16,5 L21,12 L16,19 M14,4 L10,20",
        "EVENT CONSOLE" => "M6,3 H15 L19,7 V21 H6 Z M15,3 V7 H19 M9,11 H16 M9,15 H16 M9,18 H14",
        _ => "M4,6 H20 M4,12 H20 M4,18 H20 M9,4 V8 M15,10 V14 M11,16 V20"
    };
    private void ShowPage(string page)
    {
        _active = page; var labels = new Dictionary<string, string> { ["DASHBOARD"] = "Dashboard", ["TRACE"] = "Processes", ["WHY"] = "Process details", ["LENS"] = "File inspection", ["ATLAS"] = "System information", ["LAB"] = "Snippet editor", ["EVENT CONSOLE"] = "Activity", ["SETTINGS"] = "Settings" };
        var subs = new Dictionary<string, string> { ["DASHBOARD"] = $"A clear view of what’s happening on {Environment.MachineName}.", ["TRACE"] = "See what’s running, how much memory it uses, and open a process for more detail.", ["WHY"] = "Review observed process details, with uncertainty clearly labelled.", ["LENS"] = "Preview supported files and inspect simple text findings. Files are never run.", ["ATLAS"] = "Processor, memory, operating system, and available storage.", ["LAB"] = "Write, open, and save small snippets as text. Execution is unavailable.", ["EVENT CONSOLE"] = "A local record of messages from ORACLE during this session.", ["SETTINGS"] = "Choose how often the dashboard refreshes and learn about this release." };
        PageTitle.Text = page == "DASHBOARD" ? LocalGreeting() : labels[page]; PageSubtitle.Text = subs[page]; FooterStatus.Text = "Local session · No administrator privileges needed";
        foreach (Button b in Navigation.Children) { var selected = b.Tag?.ToString() == page; b.Background = selected ? (Brush)FindResource("AccentWash") : Brushes.Transparent; b.BorderBrush = selected ? new SolidColorBrush(Color.FromRgb(75, 68, 91)) : Brushes.Transparent; b.Foreground = selected ? (Brush)FindResource("Accent") : (Brush)FindResource("Muted"); if (b.Content is StackPanel row && row.Children.Count > 0 && row.Children[0] is System.Windows.Shapes.Path p) p.Stroke = selected ? (Brush)FindResource("Accent") : (Brush)FindResource("Muted"); }
        PageContent.Content = page switch { "DASHBOARD" => DashboardPage(), "TRACE" => TracePage(), "WHY" => WhyPage(), "LENS" => LensPage(), "ATLAS" => AtlasPage(), "LAB" => LabPage(), "EVENT CONSOLE" => EventsPage(), _ => SettingsPage() };
        if (page == "DASHBOARD") _ = RefreshDashboard(); if (page == "TRACE") _ = RefreshProcesses(); if (page == "ATLAS") _ = RefreshAtlas(); if (page == "EVENT CONSOLE") RefreshEventView();
    }
    private static string LocalGreeting() => DateTime.Now.Hour switch { < 12 => "Good morning", < 17 => "Good afternoon", _ => "Good evening" };
    private Border Card(UIElement child, double? width = null) => new() { Child = child, Width = width ?? double.NaN, Padding = new Thickness(18), Margin = new Thickness(0, 0, 12, 12), CornerRadius = new CornerRadius(9), Background = (Brush)FindResource("Panel"), BorderBrush = (Brush)FindResource("Line"), BorderThickness = new Thickness(1) };
    private TextBlock Txt(string text, double size = 13, bool bold = false, string? color = null) => new() { Text = text, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Foreground = color is null ? (Brush)FindResource("Text") : new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)) };
    private StackPanel Stack(params UIElement[] children) { var s = new StackPanel(); foreach (var c in children) { if (c is FrameworkElement f && f.Margin == default) f.Margin = new Thickness(0, 0, 0, 7); s.Children.Add(c); } return s; }
    private Button Btn(string text, Action action) { var b = new Button { Content = text }; b.Click += (_, _) => action(); return b; }
    private UIElement Heading(string title, string subtitle) => Stack(Txt(title, 17, true), Txt(subtitle, 12, false, "#A8A6B3"));
    private UIElement DashboardPage()
    {
        var root = new StackPanel(); var top = new WrapPanel(); var cpuVal = Txt("Measuring…", 23, true); var ramVal = Txt("Loading…", 19, true); var uptime = Txt("Loading…", 19, true); var osVal = Txt("Loading…", 15, true);
        var cpuBar = new ProgressBar { Height = 5, Maximum = 100, IsIndeterminate = true, Foreground = (Brush)FindResource("Accent"), Background = (Brush)FindResource("Panel2") }; var ramBar = new ProgressBar { Height = 5, Maximum = 100, IsIndeterminate = true, Foreground = (Brush)FindResource("Good"), Background = (Brush)FindResource("Panel2") };
        top.Children.Add(Card(Stack(Txt("Processor", 11, true, "#A8A6B3"), cpuVal, cpuBar, Txt("System-wide use · sampled from Windows", 10, false, "#A8A6B3")), 250)); top.Children.Add(Card(Stack(Txt("Memory", 11, true, "#A8A6B3"), ramVal, ramBar, Txt("Used of physical memory", 10, false, "#A8A6B3")), 250)); top.Children.Add(Card(Stack(Txt("Uptime", 11, true, "#A8A6B3"), uptime), 205)); top.Children.Add(Card(Stack(Txt("Operating system", 11, true, "#A8A6B3"), osVal), 370)); root.Children.Add(top);
        var machine = Txt("", 13); var refreshed = Txt("Waiting for the first sample…", 11, false, "#A8A6B3"); var refreshButton = Btn("Refresh now", () => _ = RefreshDashboard()); var refreshRow = new DockPanel { LastChildFill = false }; DockPanel.SetDock(refreshButton, Dock.Right); refreshRow.Children.Add(refreshButton); refreshRow.Children.Add(refreshed);
        root.Children.Add(Card(Stack(Txt("This device", 11, true, "#A8A6B3"), machine, refreshRow)));
        root.Tag = new Action<SystemSnapshot>(s => { cpuVal.Text = s.Cpu; ramVal.Text = s.Memory; uptime.Text = s.Uptime; osVal.Text = s.Os; machine.Text = s.Machine; refreshed.Text = "Updated " + s.Refreshed; cpuBar.IsIndeterminate = !double.TryParse(s.CpuValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var cpu); if (!cpuBar.IsIndeterminate) cpuBar.Value = cpu; ramBar.IsIndeterminate = !double.TryParse(s.MemoryValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var memory); if (!ramBar.IsIndeterminate) ramBar.Value = memory; }); return root;
    }
    private async Task RefreshDashboard()
    {
        if (_refreshing || _active != "DASHBOARD") return; _refreshing = true;
        try { await Task.Delay(350); var snap = await Task.Run(_system.Snapshot); if (_active == "DASHBOARD" && PageContent.Content is StackPanel root && root.Tag is Action<SystemSnapshot> update) update(snap); }
        catch (Exception ex) { OracleLog.Add("ERROR", "Dashboard refresh failed: " + ex.Message); FooterStatus.Text = "Dashboard refresh failed. See Event Console."; }
        finally { _refreshing = false; }
    }
    private UIElement TracePage()
    {
        _processSearch = new TextBox { Width = 310, ToolTip = "Search process name or PID" }; _processSearch.TextChanged += (_, _) => ApplyProcessFilter();
        _processSearch = new TextBox { Width = 310, ToolTip = "Filter by process name or process ID" }; AutomationProperties.SetName(_processSearch, "Filter processes by name or ID"); _processSearch.TextChanged += (_, _) => ApplyProcessFilter();
        var refresh = Btn("Refresh list", () => _ = RefreshProcesses()); var top = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 10) }; DockPanel.SetDock(_processSearch, Dock.Left); DockPanel.SetDock(refresh, Dock.Right); top.Children.Add(_processSearch); top.Children.Add(refresh);
        _processGrid = new DataGrid { ItemsSource = _processRows, AutoGenerateColumns = false, IsReadOnly = true, CanUserSortColumns = true, SelectionMode = DataGridSelectionMode.Single, HeadersVisibility = DataGridHeadersVisibility.Column, Background = (Brush)FindResource("Panel"), Foreground = (Brush)FindResource("Text"), RowBackground = (Brush)FindResource("Panel"), AlternatingRowBackground = (Brush)FindResource("Panel2"), BorderBrush = (Brush)FindResource("Line"), GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, Height = 390 }; AutomationProperties.SetName(_processGrid, "Running processes");
        foreach (var (name, bind, width) in new[] { ("ID", "Pid", 90d), ("Process", "Name", 220d), ("CPU", "Cpu", 95d), ("Memory", "Memory", 130d), ("Status", "Status", 150d) }) _processGrid.Columns.Add(new DataGridTextColumn { Header = name, Binding = new System.Windows.Data.Binding(bind), Width = width });
        _processGrid.SelectionChanged += (_, _) => { if (_processGrid.SelectedItem is ProcessRow p) { _selectedProcess = p; ShowProcessDetails(); } };
        _processDetail = new StackPanel { Margin = new Thickness(0, 12, 0, 0) }; var root = new DockPanel(); DockPanel.SetDock(top, Dock.Top); DockPanel.SetDock(_processDetail, Dock.Bottom); root.Children.Add(top); root.Children.Add(_processDetail); root.Children.Add(_processGrid); return root;
    }
    private async Task RefreshProcesses()
    {
        FooterStatus.Text = "Loading running processes…"; try { var rows = await Task.Run(_system.Processes); _processRows.Clear(); foreach (var r in rows) _processRows.Add(r); ApplyProcessFilter(); FooterStatus.Text = $"{_processRows.Count} processes · CPU is estimated between refreshes"; OracleLog.Add("INFO", $"Process list refreshed ({rows.Count} entries)."); }
        catch (Exception ex) { OracleLog.Add("ERROR", "Process refresh failed: " + ex.Message); FooterStatus.Text = "Process refresh failed. See Event Console."; }
    }
    private void ApplyProcessFilter() { if (_processGrid is null) return; var view = System.Windows.Data.CollectionViewSource.GetDefaultView(_processRows); var q = _processSearch?.Text?.Trim() ?? ""; view.Filter = o => o is ProcessRow p && (q.Length == 0 || p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || p.Pid.ToString().Contains(q)); }
    private void ShowProcessDetails()
    {
        if (_selectedProcess is null || _processDetail is null) return; _processDetail.Children.Clear(); _processDetail.Children.Add(Heading($"{_selectedProcess.Name} · PID {_selectedProcess.Pid}", "Select WHY for additional factual metadata")); _processDetail.Children.Add(Txt($"Memory {_selectedProcess.Memory}   ·   CPU {_selectedProcess.Cpu}   ·   {_selectedProcess.Status}", 12, false, "#A8A6B3"));
        _processDetail.Children.Add(Btn("Open WHY details", () => ShowPage("WHY")));
    }
    private UIElement WhyPage()
    {
        var root = new StackPanel(); if (_selectedProcess is null) { root.Children.Add(Card(Stack(Txt("No process selected", 16, true), Txt("Go to TRACE, refresh the process list, then select a row to see its details.", 13, false, "#A8A6B3")))); return root; }
        root.Children.Add(Card(Stack(Txt("Loading process details…", 14)))); var pid = _selectedProcess.Pid;
        _ = Task.Run(() => _system.ProcessInfo(pid)).ContinueWith(t => Dispatcher.Invoke(() => { root.Children.Clear(); if (t.IsFaulted) { OracleLog.Add("ERROR", "Could not inspect selected process: " + (t.Exception?.GetBaseException().Message ?? "unknown error")); root.Children.Add(Card(Stack(Txt("Process is no longer available", 16, true), Txt("It may have exited or Windows may have denied access. Return to Processes and select a current entry.", 13, false, "#A8A6B3")))); return; } var d = t.Result; root.Children.Add(Card(Stack(Txt(d.Name, 19, true), Txt("Observed details", 11, true, "#B7A9EE"), Txt($"Process ID: {d.Pid}\nMemory: {d.Memory}\nExecutable path: {d.Path}\nPublisher: {d.Publisher}\nStatus: {d.Status}", 13), Txt("A little context", 11, true, "#B7A9EE"), Txt(d.Explanation, 13), Txt("ORACLE cannot verify Authenticode signatures in this release, so publisher identity is not asserted.", 11, false, "#A8A6B3")))); })); return root;
    }
    private UIElement LensPage()
    {
        var root = new StackPanel(); var actions = new WrapPanel(); actions.Children.Add(Btn("Choose file…", () => { var dlg = new OpenFileDialog { Title = "Select a file for static inspection", CheckFileExists = true }; if (dlg.ShowDialog(this) == true) InspectPath(dlg.FileName); })); actions.Children.Add(Btn("Choose folder…", () => { var dlg = new OpenFolderDialog { Title = "Choose a folder to list files for static inspection" }; if (dlg.ShowDialog(this) == true) { try { var files = FileInspector.FolderFiles(dlg.FolderName); var listing = new TextBlock { Text = string.Join("\n", files.Select(Path.GetFileName)), Foreground = (Brush)FindResource("Text"), FontFamily = new FontFamily("Consolas") }; root.Children.Clear(); root.Children.Add(Card(Stack(Txt(dlg.FolderName, 14, true), Txt($"Top-level files (up to 500): {files.Count}", 12, false, "#A8A6B3"), new ScrollViewer { Content = listing, MaxHeight = 170 }))); root.Children.Add(Btn("Choose a file to inspect…", () => { var f = new OpenFileDialog { InitialDirectory = dlg.FolderName }; if (f.ShowDialog(this) == true) InspectPath(f.FileName); })); } catch (Exception ex) { ShowLensError(ex); } } }));
        root.Children.Add(actions); root.Children.Add(Card(Stack(Txt("Static inspection only", 15, true), Txt("Choose a file to inspect bounded text content or executable metadata. Folder selection lists at most 500 top-level filenames. Selected files are never executed.", 13), Txt("This is not a full malware scanner.", 12, true, "#B7A9EE")))); return root;
    }
    private void InspectPath(string path)
    {
        if (PageContent.Content is not StackPanel root) return; try { var report = FileInspector.Inspect(path); root.Children.Clear(); root.Children.Add(Card(Stack(Txt(Path.GetFileName(report.Target), 16, true), Txt(report.Target, 11, false, "#A8A6B3"), Txt($"Type: {report.Kind}     Size: {report.Size}     Encoding: {report.Encoding}", 12), Txt("Findings", 12, true, "#B7A9EE"), Txt(string.Join("\n", report.Findings.Select(x => $"{x.Label}: {x.Value}")), 12), Txt(report.Note, 11, false, "#B7A9EE")))); root.Children.Add(Card(Stack(Txt("Preview", 12, true), new TextBox { Text = report.Preview, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, FontFamily = new FontFamily("Consolas"), FontSize = 12, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 280 }))); OracleLog.Add("INFO", "Static inspection opened: " + path); } catch (Exception ex) { ShowLensError(ex); }
    }
    private void ShowLensError(Exception ex) { OracleLog.Add("ERROR", "File inspection failed: " + ex.Message); if (PageContent.Content is StackPanel root) root.Children.Add(Card(Stack(Txt("Could not inspect file", 15, true), Txt("The file may have moved or access may be denied. " + ex.Message, 12, false, "#A8A6B3")))); }
    private UIElement AtlasPage()
    {
        var root = new StackPanel(); var basic = Stack(Txt("Loading system information…", 14)); root.Children.Add(Card(basic)); var volumes = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true, Height = 260, Background = (Brush)FindResource("Panel"), Foreground = (Brush)FindResource("Text"), RowBackground = (Brush)FindResource("Panel"), AlternatingRowBackground = (Brush)FindResource("Panel2"), BorderBrush = (Brush)FindResource("Line") }; root.Children.Add(Card(Stack(Txt("Storage volumes", 15, true), volumes))); root.Tag = new Action<(string Os, string Cpu, int Cores, string Architecture, string Memory, IReadOnlyList<VolumeInfo> Volumes)>(a => { basic.Children.Clear(); basic.Children.Add(Txt("System details", 11, true, "#A8A6B3")); basic.Children.Add(Txt($"OS: {a.Os}\nProcessor: {a.Cpu}\nLogical cores: {a.Cores}\nArchitecture: {a.Architecture}\nPhysical memory: {a.Memory}", 14)); volumes.ItemsSource = a.Volumes; }); return root;
    }
    private async Task RefreshAtlas() { try { var a = await Task.Run(_system.Atlas); if (_active == "ATLAS" && PageContent.Content is StackPanel root && root.Tag is Action<(string Os, string Cpu, int Cores, string Architecture, string Memory, IReadOnlyList<VolumeInfo> Volumes)> update) update(a); } catch (Exception ex) { OracleLog.Add("ERROR", "System information refresh failed: " + ex.Message); FooterStatus.Text = "Some system information is unavailable. See Event Console."; } }
    private UIElement LabPage()
    {
        _labPath = Txt("Unsaved snippet", 11, false, "#A8A6B3"); _labText = new TextBox { AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.NoWrap, FontFamily = new FontFamily("Consolas"), FontSize = 13, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 330, Text = "// ORACLE LAB\n// Notes and snippets only. Execution and sandboxing are unavailable.\n" };
        var tools = new WrapPanel(); tools.Children.Add(Btn("Open snippet…", OpenSnippet)); tools.Children.Add(Btn("Save snippet…", SaveSnippet)); tools.Children.Add(Btn("How this works", () => MessageBox.Show(this, "The editor stores plain text snippets on this device. ORACLE does not run or compile code, and does not provide an OS-isolated sandbox.", "About the snippet editor", MessageBoxButton.OK, MessageBoxImage.Information)));
        return Stack(tools, _labPath, Card(_labText), Card(Stack(Txt("Text only", 13, true, "#B7A9EE"), Txt("Your code is treated as text. Running snippets and OS-isolated sandboxing are not available.", 12, false, "#A8A6B3"))));
    }
    private void OpenSnippet() { var dlg = new OpenFileDialog { Filter = "Text and source files|*.txt;*.cs;*.py;*.js;*.json;*.md|All files|*.*", Title = "Open a snippet as text" }; if (dlg.ShowDialog(this) != true) return; try { var fi = new FileInfo(dlg.FileName); if (fi.Length > 1_000_000) throw new IOException("LAB limits snippets to 1 MB."); _labText!.Text = File.ReadAllText(dlg.FileName); _labPath!.Text = dlg.FileName; OracleLog.Add("INFO", "Snippet opened: " + dlg.FileName); } catch (Exception ex) { OracleLog.Add("ERROR", "Could not open snippet: " + ex.Message); MessageBox.Show(this, ex.Message, "Open failed", MessageBoxButton.OK, MessageBoxImage.Warning); } }
    private void SaveSnippet() { var dlg = new SaveFileDialog { Filter = "Text file|*.txt|All files|*.*", DefaultExt = ".txt", FileName = "oracle-snippet.txt", Title = "Save snippet as text" }; if (dlg.ShowDialog(this) != true) return; try { if (_labText!.Text.Length > 1_000_000) throw new IOException("LAB limits snippets to 1 MB."); File.WriteAllText(dlg.FileName, _labText.Text); _labPath!.Text = dlg.FileName; OracleLog.Add("INFO", "Snippet saved: " + dlg.FileName); } catch (Exception ex) { OracleLog.Add("ERROR", "Could not save snippet: " + ex.Message); MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButton.OK, MessageBoxImage.Warning); } }
    private UIElement EventsPage()
    {
        var root = new DockPanel(); var top = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 12) }; var note = Txt("Messages generated by ORACLE · This is not the Windows event log", 12, false, "#A8A6B3"); var clear = Btn("Clear activity", () => OracleLog.Clear()); DockPanel.SetDock(clear, Dock.Right); top.Children.Add(note); top.Children.Add(clear); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var grid = new DataGrid { ItemsSource = _eventRows, AutoGenerateColumns = false, IsReadOnly = true, CanUserSortColumns = true, Background = (Brush)FindResource("Panel"), Foreground = (Brush)FindResource("Text"), RowBackground = (Brush)FindResource("Panel"), AlternatingRowBackground = (Brush)FindResource("Panel2"), BorderBrush = (Brush)FindResource("Line") };
        grid.Columns.Add(new DataGridTextColumn { Header = "TIMESTAMP", Binding = new System.Windows.Data.Binding("Timestamp"), Width = 190 }); grid.Columns.Add(new DataGridTextColumn { Header = "LEVEL", Binding = new System.Windows.Data.Binding("Level"), Width = 90 }); grid.Columns.Add(new DataGridTextColumn { Header = "MESSAGE", Binding = new System.Windows.Data.Binding("Message"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) }); root.Children.Add(grid); return root;
    }
    private void RefreshEventView() { if (_active == "EVENT CONSOLE") Dispatcher.Invoke(() => { if (PageContent.Content is DockPanel root && root.Children.Count > 1 && root.Children[1] is DataGrid g) { g.Items.Refresh(); if (g.Items.Count > 0) g.ScrollIntoView(g.Items[0]); } }); }
    private UIElement SettingsPage()
    {
        var root = new StackPanel(); var interval = new ComboBox { Width = 180, ItemsSource = new[] { 2, 5, 10, 30 }, SelectedItem = _settings.RefreshSeconds }; var save = Btn("Save refresh interval", () => { if (interval.SelectedItem is not int n || n is not (2 or 5 or 10 or 30)) return; try { _settings.RefreshSeconds = n; Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!); File.WriteAllText(_settingsPath, JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true })); _timer.Interval = TimeSpan.FromSeconds(n); OracleLog.Add("INFO", $"Refresh interval saved: {n} seconds."); FooterStatus.Text = "Settings saved"; } catch (Exception ex) { OracleLog.Add("ERROR", "Settings could not be saved: " + ex.Message); FooterStatus.Text = "Could not save settings. See Event Console."; } });
        root.Children.Add(Card(Stack(Txt("Refresh interval", 16, true), Txt("Automatic refresh applies to DASHBOARD only.", 12, false, "#A8A6B3"), interval, save)));
        root.Children.Add(Card(Stack(Txt("About ORACLE", 16, true), Txt("ORACLE · Understand your system", 13), Txt("Version 2.0.0 · Windows desktop utility", 12, false, "#A8A6B3"), Txt("Built with .NET 10 and WPF. System and file data are read locally by the app. No account or administrator access is required.", 12))));
        root.Children.Add(Card(Stack(Txt("About the information shown", 14, true), Txt("System CPU is sampled from Windows system time. Per-process CPU is estimated between samples. Signature verification is unavailable. LENS inspects files statically and is not a malware scanner. The snippet editor does not execute or sandbox code.", 12, false, "#A8A6B3")))); return root;
    }
    private void LoadSettings() { try { if (File.Exists(_settingsPath)) { var s = JsonSerializer.Deserialize<OracleSettings>(File.ReadAllText(_settingsPath)); if (s is not null && s.RefreshSeconds is 2 or 5 or 10 or 30) _settings = s; else OracleLog.Add("ERROR", "Invalid settings file; using safe defaults."); } } catch (Exception ex) { OracleLog.Add("ERROR", "Settings could not be loaded; using defaults: " + ex.Message); } }
}
