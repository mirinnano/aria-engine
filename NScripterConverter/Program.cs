using System.Text;

if (args.Length < 2)
{
    Console.WriteLine("Usage: NScripterConverter <input-00.txt> <output.aria>");
    return 1;
}

string inputPath = args[0];
string outputPath = args[1];

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Input file not found: {inputPath}");
    return 2;
}

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
Encoding sourceEncoding = Encoding.GetEncoding(932);
string[] lines = File.ReadAllLines(inputPath, sourceEncoding);

var outLines = new List<string>
{
    "; converted by NScripterConverter",
    "compat_mode on",
    "ui_theme \"clean\"",
    ""
};

foreach (string raw in lines)
{
    string line = raw.TrimEnd();
    if (string.IsNullOrWhiteSpace(line))
    {
        outLines.Add(string.Empty);
        continue;
    }

    string command = line.Split(' ', '\t', StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
    switch (command)
    {
        case "versionstr":
        case "rmenu":
        case "humanz":
        case "windowback":
        case "usewheel":
        case "textgosub":
        case "globalon":
        case "kidokumode":
        case "effectskip":
        case "nsa":
        case "mode_ext":
        case "mode_wave_demo":
        case "defvoicevol":
        case "defsevol":
        case "defmp3vol":
        case "killmenu":
        case "menusetwindow":
        case "game":
        case "texec":
        case "getcursorpos":
            outLines.Add($"; [ns-unsupported] {line}");
            break;
        case "defaultspeed":
            {
                string[] parts = line.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length >= 2 && int.TryParse(parts[1], out int speed))
                {
                    outLines.Add($"defaultspeed {speed}");
                }
                else
                {
                    outLines.Add("; [ns-unsupported] defaultspeed parse failed");
                }
                break;
            }
        default:
            outLines.Add(line);
            break;
    }
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
File.WriteAllLines(outputPath, outLines, new UTF8Encoding(false));
Console.WriteLine($"Wrote: {outputPath}");
Console.WriteLine($"Lines: {lines.Length} -> {outLines.Count}");
return 0;
