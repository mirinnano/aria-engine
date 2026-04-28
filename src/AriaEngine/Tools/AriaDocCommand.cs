using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using AriaEngine.Core;

namespace AriaEngine.Tools;

public static class AriaDocCommand
{
    public static int Run(string[] args)
    {
        string scriptPath = "";
        string outputDir = "";

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out":
                    outputDir = args[++i];
                    break;
                default:
                    if (!args[i].StartsWith("--") && string.IsNullOrEmpty(scriptPath))
                        scriptPath = args[i];
                    break;
            }
        }

        if (string.IsNullOrEmpty(scriptPath))
        {
            Console.WriteLine("Usage: aria-doc <script.aria> --out <output_dir/>");
            return 1;
        }

        if (string.IsNullOrEmpty(outputDir))
        {
            Console.WriteLine("Error: --out <output_dir/> is required");
            return 1;
        }

        // Ensure output directory exists
        Directory.CreateDirectory(outputDir);

        // Parse the script
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);

        string[] lines;
        try
        {
            lines = File.ReadAllLines(scriptPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: Could not read script file: {ex.Message}");
            return 1;
        }

        var result = parser.Parse(lines, scriptPath);

        // Build doc output
        var docOutput = new DocOutput
        {
            File = Path.GetFileName(scriptPath),
            Functions = result.Functions.Select(f => new FunctionDoc
            {
                Name = f.QualifiedName,
                ShortName = f.ShortName,
                Doc = f.DocComment,
                Parameters = f.Parameters.Select(p => new ParameterDoc
                {
                    Name = p.Name,
                    Type = p.Type
                }).ToList(),
                ReturnType = f.ReturnType
            }).ToList(),
            Structs = result.Structs.Select(s => new StructDoc
            {
                Name = s.QualifiedName,
                ShortName = s.ShortName,
                Doc = s.DocComment,
                Fields = s.Fields.Select(f => new FieldDoc
                {
                    Name = f.Name,
                    Type = f.Type
                }).ToList()
            }).ToList()
        };

        // Output JSON
        string jsonPath = Path.Combine(outputDir, "doc.json");
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        string json = JsonSerializer.Serialize(docOutput, jsonOptions);
        File.WriteAllText(jsonPath, json);
        Console.WriteLine($"JSON: {jsonPath}");

        // Output Markdown
        string mdPath = Path.Combine(outputDir, "doc.md");
        string md = GenerateMarkdown(docOutput);
        File.WriteAllText(mdPath, md);
        Console.WriteLine($"Markdown: {mdPath}");

        return 0;
    }

    private static string GenerateMarkdown(DocOutput doc)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {doc.File} Documentation");
        sb.AppendLine();

        if (doc.Functions.Count > 0)
        {
            sb.AppendLine("## Functions");
            sb.AppendLine();
            foreach (var func in doc.Functions)
            {
                sb.AppendLine($"### {func.Name}");
                if (!string.IsNullOrEmpty(func.Doc))
                {
                    sb.AppendLine();
                    sb.AppendLine(func.Doc);
                }
                if (func.Parameters.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("**Parameters:**");
                    foreach (var p in func.Parameters)
                    {
                        sb.AppendLine($"- `{p.Name}` ({p.Type})");
                    }
                }
                if (!string.IsNullOrEmpty(func.ReturnType) && func.ReturnType != "void")
                {
                    sb.AppendLine();
                    sb.AppendLine($"**Return type:** `{func.ReturnType}`");
                }
                sb.AppendLine();
            }
        }

        if (doc.Structs.Count > 0)
        {
            sb.AppendLine("## Structs");
            sb.AppendLine();
            foreach (var s in doc.Structs)
            {
                sb.AppendLine($"### {s.Name}");
                if (!string.IsNullOrEmpty(s.Doc))
                {
                    sb.AppendLine();
                    sb.AppendLine(s.Doc);
                }
                if (s.Fields.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("**Fields:**");
                    foreach (var f in s.Fields)
                    {
                        sb.AppendLine($"- `{f.Name}` ({f.Type})");
                    }
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}

// Doc output model classes
internal class DocOutput
{
    public string File { get; set; } = "";
    public List<FunctionDoc> Functions { get; set; } = new();
    public List<StructDoc> Structs { get; set; } = new();
}

internal class FunctionDoc
{
    public string Name { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string? Doc { get; set; }
    public List<ParameterDoc> Parameters { get; set; } = new();
    public string ReturnType { get; set; } = "void";
}

internal class ParameterDoc
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
}

internal class StructDoc
{
    public string Name { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string? Doc { get; set; }
    public List<FieldDoc> Fields { get; set; } = new();
}

internal class FieldDoc
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
}
