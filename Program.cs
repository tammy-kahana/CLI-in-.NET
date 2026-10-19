using System.CommandLine;
using System.Text;

var languageOption = new Option<string[]>("--language")
{
    Description = "languages to include or 'all'",
    Required = true,
    AllowMultipleArgumentsPerToken = true
};
languageOption.Aliases.Add("-l");

var outputOption = new Option<string>("--output")
{
    Description = "file path and name",
    DefaultValueFactory = _ => "bundle.txt"
};
outputOption.Aliases.Add("-o");

var noteOption = new Option<bool>("--note")
{
    Description = "write in bundle the source of the code"
};
noteOption.Aliases.Add("-n");

var sortOption = new Option<string>("--sort")
{
    Description = "copy first by name or by type",
    DefaultValueFactory = _ => "name"
};
sortOption.Aliases.Add("-s");

var removeOption = new Option<bool>("--remove-empty-lines")
{
    Description = "remove empty lines from file before copying"
};
removeOption.Aliases.Add("-r");

var authorOption = new Option<string>("--author")
{
    Description = "write in file the maker's name of this file"
};
authorOption.Aliases.Add("-a");

var supportedLang = new Dictionary<string, string[]>(
    StringComparer.OrdinalIgnoreCase)
{
    ["csharp"] = [".cs"],
    ["python"] = [".py"],
    ["javascript"] = [".js", ".jsx"],
    ["typescript"] = [".ts", ".tsx"],
    ["java"] = [".java"],
    ["c"] = [".c", ".h"],
    ["cpp"] = [".cpp", ".hpp", ".h"],
    ["html"] = [".html"],
    ["css"] = [".css"],
    ["json"] = [".json"],
    ["xml"] = [".xml"],
    ["sql"] = [".sql"],
    ["go"] = [".go"],
    ["rust"] = [".rs"]
};
var excludedDir = new HashSet<string>(
    StringComparer.OrdinalIgnoreCase)
{
    "bin", "obj", "debug", "release",
    ".git", ".vs", ".idea", "node_modules"
};
var bundleCommand = new Command("bundle", "Bundle code files to a single file");
bundleCommand.Add(languageOption);
bundleCommand.Add(outputOption);
bundleCommand.Add(noteOption);
bundleCommand.Add(sortOption);
bundleCommand.Add(removeOption);
bundleCommand.Add(authorOption);
bundleCommand.SetAction(parse =>
{
    var languages = parse.GetValue(languageOption) ?? [];
    var output = parse.GetValue(outputOption) ?? "bundle.txt";
    var notes = parse.GetValue(noteOption);
    var sort = parse.GetValue(sortOption) ?? "name";
    var removeEmpty = parse.GetValue(removeOption);
    var author = parse.GetValue(authorOption);

    if (languages.Length == 0)
    {
        Console.WriteLine("Error: language is required.");
        return;
    }
    bool all = languages.Any(x => x.Equals("all", StringComparison.OrdinalIgnoreCase));

    if (all && languages.Length > 1)
    {
        Console.Error.WriteLine("Error: 'all' cannot be combined with other languages.");
        return;
    }
    var invalid = languages.Where(x => !all && !supportedLang.ContainsKey(x)).ToArray();
    if (invalid.Length > 0)
    {
        Console.WriteLine($"Unsupported language: {string.Join(", ", invalid)}");
        Console.WriteLine($"supported: {string.Join(", ", supportedLang.Keys)}, all");
        return;
    }
    if (sort != "name" && sort != "type")
    {
        Console.WriteLine("Error: sort must be 'name' or 'type'");
        return;
    }
    string root = Directory.GetCurrentDirectory();
    try
    {
        string path = Path.GetFullPath(output);

        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(file =>
            {
                string relative = Path.GetRelativePath(root, file);
                string[] parts = relative.Split(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

                return !parts.Take(parts.Length - 1)
                    .Any(excludedDir.Contains);
            })
            .Where(file =>
            {
                if (all)
                    return supportedLang.Values.SelectMany(x => x).Contains(Path.GetExtension(file),StringComparer.OrdinalIgnoreCase);
                return languages.SelectMany(language => supportedLang[language]).Contains(Path.GetExtension(file),StringComparer.OrdinalIgnoreCase);
            })
            .Where(file =>
                !string.Equals(Path.GetFullPath(file),path,StringComparison.OrdinalIgnoreCase)).ToList();
        
        files = sort == "name"? files.OrderBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase).ToList()
            : files.OrderBy( file => Path.GetExtension(file),StringComparer.OrdinalIgnoreCase).ThenBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase).ToList();
        
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));

        if (!string.IsNullOrWhiteSpace(author))
        {
            writer.WriteLine($"# Author: {author}");
            writer.WriteLine();
        }

        foreach (string file in files)
        {
            string relative = Path.GetRelativePath(root, file);

            if (notes)
            {
                writer.WriteLine($"# Source: {relative}");
            }

            string content = File.ReadAllText(file);

            if (removeEmpty)
            {
                content = string.Join(Environment.NewLine,content.Split(["\r\n", "\n", "\r"],StringSplitOptions.None)
                    .Where(line => !string.IsNullOrWhiteSpace(line)));
            }

            writer.WriteLine(content);
            writer.WriteLine();
        }

        Console.WriteLine($"Bundle created: {path} ({files.Count} files)");
    }
    catch (UnauthorizedAccessException) 
    {
        Console.WriteLine("Error: Access denied. You do not have permission to write to the output file.");
    }
    catch (DirectoryNotFoundException)
    {
        Console.WriteLine("Error: The specified output directory does not exist.");
    }
    catch (IOException ex)
    {
        Console.WriteLine($"Error: A file operation failed: {ex.Message}");
    }
    catch (ArgumentException ex)
    {
        Console.WriteLine($"Error: Invalid file path: {ex.Message}");
    }
    catch (NotSupportedException ex)
    {
        Console.WriteLine($"Error: Unsupported file path: {ex.Message}");
    }
});


var rspCommand = new Command("create-rsp", "create rsp to ease the command");
rspCommand.SetAction(_ =>
{
    Console.Write("Languages (e.g. csharp python, or all): ");
    string[] languages = (Console.ReadLine() ?? "").Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);

    Console.Write("Output file [bundle.txt]: ");
    string output = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(output))
        output = "bundle.txt";

    Console.Write("Include source comments? (y/n): ");
    bool note = Console.ReadLine()?.ToLower() == "y";

    Console.Write("Sort by name or type [name]: ");
    string sort = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(sort))
        sort = "name";

    Console.Write("Remove empty lines? (y/n): ");
    bool removeEmpty = Console.ReadLine()?.ToLower() == "y";

    Console.Write("Author (optional): ");
    string author = Console.ReadLine() ?? "";

    if (languages.Length == 0 ||
        (languages.Contains("all") && languages.Length > 1) || languages.Any(x => x != "all" && !supportedLang.ContainsKey(x)))
    {
        Console.WriteLine("Error: Invalid languages.");
        return;
    }

    if (sort != "name" && sort != "type")
    {
        Console.WriteLine("Error: invalid sort");
        return;
    }

    var args = new List<string> { "bundle", "--language" };

    args.AddRange(languages);
    args.AddRange(["--output", output, "--sort", sort]);

    if (note)
        args.Add("--note");
    if (removeEmpty)
        args.Add("--remove-empty-lines");
    if (!string.IsNullOrWhiteSpace(author))
        args.AddRange(["--author", author]);

    File.WriteAllText("bundle.rsp", string.Join(" ", args));

    Console.WriteLine("Response file created: bundle.rsp");
    Console.WriteLine("Run with: p1 @bundle.rsp");
});
var rootCommand = new RootCommand("Root command for file Bundler CLI");
rootCommand.Subcommands.Add(bundleCommand);
rootCommand.Subcommands.Add(rspCommand);

return rootCommand.Parse(args).Invoke();