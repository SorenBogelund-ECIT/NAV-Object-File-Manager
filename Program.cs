using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);

var staThread = new Thread(() =>
{
    try { Run(args); }
    catch (Exception ex)
    {
        MessageBox.Show(ex.ToString(), "NAV Object File Manager – fejl",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
});
staThread.SetApartmentState(ApartmentState.STA);
staThread.Start();
staThread.Join();

// ─────────────────────────────────────────────────────────────────────────────

static void Run(string[] args)
{
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    var encoding1252 = Encoding.GetEncoding(1252);

    // Afgør tilstand: split (standard) eller merge
    bool mergeMode;
    if (args.Length > 0 && args[0].Equals("--merge", StringComparison.OrdinalIgnoreCase))
    {
        mergeMode = true;
    }
    else if (args.Length > 0)
    {
        mergeMode = false;
    }
    else
    {
        using var modeForm = new ModeForm();
        if (modeForm.ShowDialog() != DialogResult.OK)
            return;
        mergeMode = modeForm.IsMerge;
    }

    if (mergeMode)
        RunMerge(args, encoding1252);
    else
        RunSplit(args, encoding1252);
}

// ─── Split-tilstand ───────────────────────────────────────────────────────────

static void RunSplit(string[] args, Encoding encoding1252)
{
    // Vælg input-filer
    List<string> inputFiles;
    if (args.Length > 0)
    {
        inputFiles = args.SkipLast(1).ToList();
    }
    else
    {
        using var dlg = new OpenFileDialog
        {
            Title       = "Vælg NAV-objektfiler",
            Filter      = "Tekstfiler (*.txt)|*.txt|Alle filer (*.*)|*.*",
            Multiselect = true,
        };
        if (dlg.ShowDialog() != DialogResult.OK)
            return;
        inputFiles = [.. dlg.FileNames.Order()];
    }

    if (inputFiles.Count == 0) return;

    // Vælg outputmappe
    string outputFolder;
    if (args.Length > 1)
    {
        outputFolder = args[^1];
    }
    else
    {
        using var dlg = new FolderBrowserDialog
        {
            Description            = "Vælg outputmappe",
            UseDescriptionForTitle = true,
            SelectedPath           = Path.Combine(Path.GetDirectoryName(inputFiles[0])!, "output"),
            ShowNewFolderButton    = true,
        };
        if (dlg.ShowDialog() != DialogResult.OK)
            return;
        outputFolder = dlg.SelectedPath;
    }

    Directory.CreateDirectory(outputFolder);

    // Vis progressvindue og behandl filer i baggrunden
    using var form = new ProgressForm(inputFiles, outputFolder, encoding1252);
    Application.Run(form);
}

// ─── Merge-tilstand ───────────────────────────────────────────────────────────

static void RunMerge(string[] args, Encoding encoding1252)
{
    // Vælg inputmappe med NAV-objektfiler
    string inputFolder;
    if (args.Length > 1)
    {
        inputFolder = args[1];
    }
    else
    {
        using var dlg = new FolderBrowserDialog
        {
            Description            = "Vælg mappe med NAV-objektfiler",
            UseDescriptionForTitle = true,
            ShowNewFolderButton    = false,
        };
        if (dlg.ShowDialog() != DialogResult.OK)
            return;
        inputFolder = dlg.SelectedPath;
    }

    // Vælg outputmappe
    string outputFolder;
    if (args.Length > 2)
    {
        outputFolder = args[2];
    }
    else
    {
        using var dlg = new FolderBrowserDialog
        {
            Description            = "Vælg outputmappe",
            UseDescriptionForTitle = true,
            SelectedPath           = Path.GetDirectoryName(inputFolder)!,
            ShowNewFolderButton    = true,
        };
        if (dlg.ShowDialog() != DialogResult.OK)
            return;
        outputFolder = dlg.SelectedPath;
    }

    Directory.CreateDirectory(outputFolder);

    using var form = new MergeProgressForm(inputFolder, outputFolder, encoding1252);
    Application.Run(form);
}

// ─────────────────────────────────────────────────────────────────────────────

sealed class ModeForm : Form
{
    public bool IsMerge { get; private set; }

    public ModeForm()
    {
        Text            = "NAV Object File Manager";
        Width           = 360;
        Height          = 150;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;

        var panel = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            Padding     = new Padding(16),
            RowCount    = 2,
            ColumnCount = 2,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(panel);

        var lbl = new Label
        {
            Text      = "Hvad vil du gøre?",
            AutoSize  = true,
            Dock      = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
        };
        panel.Controls.Add(lbl, 0, 0);
        panel.SetColumnSpan(lbl, 2);

        var btnSplit = new Button { Text = "Opdel (split)", Dock = DockStyle.Fill, Height = 36 };
        var btnMerge = new Button { Text = "Saml (merge)", Dock = DockStyle.Fill, Height = 36 };

        btnSplit.Click += (_, _) => { IsMerge = false; DialogResult = DialogResult.OK; Close(); };
        btnMerge.Click += (_, _) => { IsMerge = true;  DialogResult = DialogResult.OK; Close(); };

        panel.Controls.Add(btnSplit, 0, 1);
        panel.Controls.Add(btnMerge, 1, 1);
    }
}

// ─────────────────────────────────────────────────────────────────────────────

sealed class MergeProgressForm : Form
{
    private readonly string _inputFolder;
    private readonly string _outputFolder;
    private readonly Encoding _encoding;

    private readonly Label _lblFile;
    private readonly Label _lblCount;
    private readonly ProgressBar _bar;

    public MergeProgressForm(string inputFolder, string outputFolder, Encoding encoding)
    {
        _inputFolder  = inputFolder;
        _outputFolder = outputFolder;
        _encoding     = encoding;

        Text            = "NAV Object Merger";
        Width           = 520;
        Height          = 140;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;

        var panel = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            Padding     = new Padding(12),
            RowCount    = 3,
            ColumnCount = 1,
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        Controls.Add(panel);

        _lblFile  = new Label { Text = "Forbereder…", AutoSize = true, Dock = DockStyle.Fill };
        _lblCount = new Label { Text = "", AutoSize = true, Dock = DockStyle.Fill };
        _bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Value   = 0,
            Dock    = DockStyle.Fill,
            Style   = ProgressBarStyle.Continuous,
        };

        panel.Controls.Add(_lblFile);
        panel.Controls.Add(_lblCount);
        panel.Controls.Add(_bar);
    }

    private record ProgressInfo(int Done, int Total, string Label);

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        var worker = new System.ComponentModel.BackgroundWorker { WorkerReportsProgress = true };
        int totalObjects = 0;

        worker.DoWork += (_, _) =>
        {
            worker.ReportProgress(0, new ProgressInfo(0, 0, "Finder filer…"));

            var inputFiles = Directory
                .EnumerateFiles(_inputFolder, "*.txt", SearchOption.AllDirectories)
                .Order()
                .ToList();

            var headerRx = new Regex(@"^OBJECT\s+\w+\s+\d+", RegexOptions.Compiled);
            worker.ReportProgress(0, new ProgressInfo(0, 0, "Tæller objekter…"));
            int totalCount = inputFiles.Sum(f =>
                File.ReadLines(f, _encoding).Count(l => headerRx.IsMatch(l)));

            string outputFile = Path.Combine(_outputFolder, "merged-objects.txt");
            int done = 0;

            using var writer = new StreamWriter(outputFile, append: false, _encoding);
            var headerPattern = new Regex(@"^OBJECT\s+(\w+)\s+(\d+)\s+(.+?)\s*$", RegexOptions.Compiled);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in inputFiles)
            {
                var lines  = File.ReadAllLines(file, _encoding);
                var buffer = new StringBuilder();
                string? objKey = null;

                void FlushObj()
                {
                    if (seen.Add(objKey!))
                    {
                        writer.Write(buffer.ToString());
                        totalObjects++;
                    }
                    objKey = null;
                    buffer.Clear();
                }

                foreach (var line in lines)
                {
                    var m = headerPattern.Match(line);
                    if (m.Success)
                    {
                        if (objKey != null && buffer.Length > 0) FlushObj();
                        objKey = $"{m.Groups[1].Value}|{m.Groups[2].Value}";
                        done++;
                        worker.ReportProgress(0,
                            new ProgressInfo(done, totalCount,
                                $"{m.Groups[1].Value} {m.Groups[2].Value} {m.Groups[3].Value.Trim()}"));
                        buffer.Clear();
                        buffer.AppendLine(line);
                    }
                    else if (objKey != null)
                    {
                        buffer.AppendLine(line);
                        if (line == "}") FlushObj();
                    }
                }

                if (objKey != null && buffer.Length > 0) FlushObj();
            }
        };

        worker.ProgressChanged += (_, args) =>
        {
            if (args.UserState is not ProgressInfo p) return;
            if (p.Total > 0) _bar.Maximum = p.Total;
            _bar.Value     = Math.Min(p.Done, _bar.Maximum);
            _lblFile.Text  = p.Label;
            _lblCount.Text = p.Total > 0 ? $"Objekt {p.Done} af {p.Total}" : "";
        };

        worker.RunWorkerCompleted += (_, args) =>
        {
            if (args.Error != null)
            {
                MessageBox.Show(args.Error.ToString(), "NAV Object Merger – fejl",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                string outFile = Path.Combine(_outputFolder, "merged-objects.txt");
                MessageBox.Show(
                    $"{totalObjects} objekter samlet i:\n{outFile}",
                    "NAV Object Merger",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            Close();
        };

        worker.RunWorkerAsync();
    }
}

// ─────────────────────────────────────────────────────────────────────────────

sealed class ProgressForm : Form
{
    private readonly List<string> _files;
    private readonly string _outputFolder;
    private readonly Encoding _encoding;

    private readonly Label _lblFile;
    private readonly Label _lblCount;
    private readonly ProgressBar _bar;

    public ProgressForm(List<string> files, string outputFolder, Encoding encoding)
    {
        _files        = files;
        _outputFolder = outputFolder;
        _encoding     = encoding;

        Text            = "NAV Object Splitter";
        Width           = 520;
        Height          = 140;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;

        var panel = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            Padding     = new Padding(12),
            RowCount    = 3,
            ColumnCount = 1,
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        Controls.Add(panel);

        _lblFile = new Label { Text = "Forbereder…", AutoSize = true, Dock = DockStyle.Fill };
        _lblCount = new Label { Text = "", AutoSize = true, Dock = DockStyle.Fill };
        _bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,   // sættes efter optælling
            Value   = 0,
            Dock    = DockStyle.Fill,
            Style   = ProgressBarStyle.Continuous,
        };

        panel.Controls.Add(_lblFile);
        panel.Controls.Add(_lblCount);
        panel.Controls.Add(_bar);
    }

    // Bruges til at sende fremskridt fra baggrundstråd til UI-tråd
    private record ProgressInfo(int Done, int Total, string Label);

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        var worker = new System.ComponentModel.BackgroundWorker { WorkerReportsProgress = true };

        int totalObjects = 0;
        int skipped      = 0;

        worker.DoWork += (_, _) =>
        {
            var headerRx = new Regex(@"^OBJECT\s+\w+\s+\d+", RegexOptions.Compiled);
            var seen      = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Pas 1 – tæl objekter
            worker.ReportProgress(0, new ProgressInfo(0, 0, "Tæller objekter…"));
            int totalCount = _files.Sum(f =>
                File.ReadLines(f, _encoding).Count(l => headerRx.IsMatch(l)));

            // Pas 2 – behandl og rapportér per objekt
            int done = 0;
            foreach (var file in _files)
            {
                int count = ProcessFile(file, _outputFolder, _encoding, seen, ref skipped,
                    label =>
                    {
                        done++;
                        worker.ReportProgress(0,
                            new ProgressInfo(done, totalCount, label));
                    });
                totalObjects += count;
            }
        };

        worker.ProgressChanged += (_, args) =>
        {
            if (args.UserState is not ProgressInfo p) return;
            if (p.Total > 0) _bar.Maximum = p.Total;
            _bar.Value     = Math.Min(p.Done, _bar.Maximum);
            _lblFile.Text  = p.Label;
            _lblCount.Text = p.Total > 0 ? $"Objekt {p.Done} af {p.Total}" : "";
        };

        worker.RunWorkerCompleted += (_, args) =>
        {
            if (args.Error != null)
            {
                MessageBox.Show(args.Error.ToString(), "NAV Object Splitter – fejl",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                string msg = $"{totalObjects} objekter skrevet til:\n{_outputFolder}";
                if (skipped > 0) msg += $"\n\n({skipped} dubletter sprunget over)";
                MessageBox.Show(msg, "NAV Object Splitter",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            Close();
        };

        worker.RunWorkerAsync();
    }

    // ── NAV object parsing ────────────────────────────────────────────────────

    static int ProcessFile(string filePath, string outputFolder, Encoding encoding,
                           HashSet<string> seen, ref int skipped,
                           Action<string>? onObject = null)
    {
        var headerPattern = new Regex(@"^OBJECT\s+(\w+)\s+(\d+)\s+(.+?)\s*$", RegexOptions.Compiled);
        var lines         = File.ReadAllLines(filePath, encoding);
        var buffer        = new StringBuilder();
        string? objType   = null;
        int objNumber     = 0;
        string? objName   = null;
        int count         = 0;
        int localSkipped  = 0;

        void Flush()
        {
            string key = $"{objType}|{objNumber}";
            if (seen.Add(key))
            {
                SaveObject(objType!, objNumber, objName!, buffer.ToString(), outputFolder, encoding);
                count++;
            }
            else localSkipped++;
        }

        foreach (var line in lines)
        {
            var m = headerPattern.Match(line);
            if (m.Success)
            {
                if (objType != null && buffer.Length > 0) Flush();
                objType   = m.Groups[1].Value;
                objNumber = int.Parse(m.Groups[2].Value);
                objName   = m.Groups[3].Value.Trim();
                onObject?.Invoke($"{objType} {objNumber} {objName}");
                buffer.Clear();
                buffer.AppendLine(line);
            }
            else if (objType != null)
            {
                buffer.AppendLine(line);
                if (line == "}")
                {
                    Flush();
                    objType = null;
                    buffer.Clear();
                }
            }
        }

        if (objType != null && buffer.Length > 0) Flush();
        skipped += localSkipped;
        return count;
    }

    static void SaveObject(string type, int number, string name, string content,
                           string outputFolder, Encoding encoding)
    {
        string abbrev = type switch
        {
            "Table"     => "TAB",
            "Page"      => "PAG",
            "Codeunit"  => "COD",
            "Report"    => "REP",
            "XMLport"   => "XPO",
            "MenuSuite" => "MEN",
            _           => type.Length >= 3 ? type[..3].ToUpper() : type.ToUpper()
        };

        string typeFolder = Path.Combine(outputFolder, type + "s");
        Directory.CreateDirectory(typeFolder);

        string safeName = SanitizeFileName(name);
        string fileName = $"{abbrev}{number:D5} {safeName}.txt";
        File.WriteAllText(Path.Combine(typeFolder, fileName), content, encoding);
    }

    static string SanitizeFileName(string name)
    {
        var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
        var sb = new StringBuilder(name.Length);
        foreach (char c in name)
            sb.Append(invalid.Contains(c) ? '_' : c);
        return sb.ToString().Trim('_', ' ');
    }
}
