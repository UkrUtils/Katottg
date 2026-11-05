using UkrUtils.Katottg.Import;

namespace UkrUtils.Katottg.Tool;

/// <summary>Команди CLI <c>katottg</c>. Повертає код виходу: 0 — успіх, 1 — помилка, 2 — неправильні аргументи.</summary>
internal static class KatottgCli
{
    private const string Usage = """
        Usage:
          katottg import <file.xlsx|dir> [--verbose]
              Parse an official KATOTTG xlsx and print the import report.
          katottg build-snapshot <file.xlsx|dir> [-o|--output <path>]
              Build a binary snapshot (default: katottg.bin.gz). A directory means
              the latest katottg-*.xlsx in it (by file name).
          katottg search <text> [--snapshot <path>] [--limit <n>]
              Search the embedded (or given) snapshot by name or code.
        """;

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            (args.Length == 0 ? stderr : stdout).WriteLine(Usage);
            return args.Length == 0 ? 2 : 0;
        }

        try
        {
            return args[0] switch
            {
                "import" => Import(args[1..], stdout, stderr),
                "build-snapshot" => BuildSnapshot(args[1..], stdout, stderr),
                "search" => Search(args[1..], stdout, stderr),
                _ => UsageError(stderr, $"Unknown command '{args[0]}'."),
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static int UsageError(TextWriter stderr, string message)
    {
        stderr.WriteLine($"error: {message}");
        stderr.WriteLine(Usage);
        return 2;
    }

    private static int Import(string[] args, TextWriter stdout, TextWriter stderr)
    {
        var options = Options.Parse(args, flags: ["--verbose"], values: []);
        if (options.Error is not null || options.Positional.Count != 1)
            return UsageError(stderr, options.Error ?? "import expects one input file.");

        if (!TryResolveInput(options.Positional[0], stderr, out var path))
            return 1;

        var result = KatottgSheetParser.Parse(path);
        PrintReport(result, stdout, options.Has("--verbose"));
        return result.Report.HasErrors ? 1 : 0;
    }

    private static int BuildSnapshot(string[] args, TextWriter stdout, TextWriter stderr)
    {
        var options = Options.Parse(args, flags: [], values: ["-o", "--output"]);
        if (options.Error is not null || options.Positional.Count != 1)
            return UsageError(stderr, options.Error ?? "build-snapshot expects one input file.");

        if (!TryResolveInput(options.Positional[0], stderr, out var path))
            return 1;

        var result = KatottgSheetParser.Parse(path);
        PrintReport(result, stdout, verbose: false);
        if (result.Report.HasErrors)
        {
            stderr.WriteLine("error: import has errors; snapshot not written.");
            return 1;
        }

        var output = Path.GetFullPath(options.Value("-o") ?? options.Value("--output") ?? "katottg.bin.gz");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var temp = output + ".tmp";
        using (var stream = File.Create(temp))
            result.CreateDataset().Save(stream);
        File.Move(temp, output, overwrite: true);

        stdout.WriteLine($"Snapshot written: {output} ({new FileInfo(output).Length:N0} bytes)");
        return 0;
    }

    private static int Search(string[] args, TextWriter stdout, TextWriter stderr)
    {
        var options = Options.Parse(args, flags: [], values: ["--snapshot", "--limit"]);
        if (options.Error is not null || options.Positional.Count == 0)
            return UsageError(stderr, options.Error ?? "search expects a query.");

        var limit = 20;
        if (options.Value("--limit") is { } limitText && (!int.TryParse(limitText, out limit) || limit <= 0))
            return UsageError(stderr, $"Invalid --limit '{limitText}'.");

        KatottgDataset dataset;
        if (options.Value("--snapshot") is { } snapshotPath)
        {
            using var stream = File.OpenRead(snapshotPath);
            dataset = KatottgDataset.Load(stream);
        }
        else
        {
            dataset = KatottgDataset.Embedded;
        }

        var query = string.Join(' ', options.Positional);
        if (dataset.TryFind(query, out var exact) || (KatottgCode.TryParse(query, out var code) && (exact = dataset.FindBySignificantPart(code)) is not null))
        {
            stdout.WriteLine($"{exact.Code}  {exact.Category.ToLetter()}  {exact.FullName}");
            return 0;
        }

        stderr.WriteLine("Nothing found.");
        return 1;
    }

    private static bool TryResolveInput(string input, TextWriter stderr, out string path)
    {
        path = input;
        if (Directory.Exists(input))
        {
            var latest = Directory.GetFiles(input, "katottg-*.xlsx").Order(StringComparer.Ordinal).LastOrDefault();
            if (latest is null)
            {
                stderr.WriteLine($"error: no katottg-*.xlsx files in '{input}'.");
                return false;
            }
            path = latest;
            return true;
        }
        if (!File.Exists(input))
        {
            stderr.WriteLine($"error: file '{input}' not found.");
            return false;
        }
        return true;
    }

    private static void PrintReport(KatottgImportResult result, TextWriter stdout, bool verbose)
    {
        var report = result.Report;
        stdout.WriteLine($"Edition:  {result.Edition.Date:yyyy-MM-dd}  source: {result.Edition.Source}  sha256: {result.Edition.Sha256}");
        stdout.WriteLine($"Records:  {report.DataRows}  (header at row {report.HeaderRow}, skipped {report.SkippedRows}, empty {report.EmptyRows})");
        foreach (var group in result.Records.GroupBy(r => r.Category).OrderBy(g => g.Key))
            stdout.WriteLine($"  {group.Key.ToLetter()} {group.Key,-20} {group.Count(),6}");
        stdout.WriteLine($"Issues:   {report.Errors.Count()} error(s), {report.Warnings.Count()} warning(s), {report.Issues.Count(i => i.Severity == ImportSeverity.Info)} info");
        foreach (var issue in report.Issues.Where(i => verbose || i.Severity != ImportSeverity.Info))
            stdout.WriteLine($"  {issue}");
    }

    private sealed class Options
    {
        private readonly Dictionary<string, string> _values = [];
        private readonly HashSet<string> _flags = [];

        public List<string> Positional { get; } = [];
        public string? Error { get; private set; }

        public bool Has(string flag) => _flags.Contains(flag);
        public string? Value(string name) => _values.GetValueOrDefault(name);

        public static Options Parse(string[] args, string[] flags, string[] values)
        {
            var options = new Options();
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (flags.Contains(arg))
                    options._flags.Add(arg);
                else if (values.Contains(arg))
                {
                    if (i + 1 >= args.Length)
                    {
                        options.Error = $"Option '{arg}' needs a value.";
                        break;
                    }
                    options._values[arg] = args[++i];
                }
                else if (arg.StartsWith('-') && arg.Length > 1)
                {
                    options.Error = $"Unknown option '{arg}'.";
                    break;
                }
                else
                    options.Positional.Add(arg);
            }
            return options;
        }
    }
}
