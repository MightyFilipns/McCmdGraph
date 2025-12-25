using System.Text.Json;
using DotNetGraph.Compilation;
using DotNetGraph.Extensions;
using DotNetGraph.Core;

namespace MC_cmd_graph;

static class McCmdGraph
{
    static int Main(string[] args)
    {
        string file_path = null;
        string out_file = System.IO.Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "commands.dot";
        if (args.Length == 0)
        {
            Console.WriteLine("Minecraft command graph generator\n" +
                              "Generates a .dot graph file from a commands.json file from the minecraft data generator\n" +
                              "The file in which to write the output is an optional second argument, if not specified the output will be written to current working directory" +
                              "Usage mccmdgraph /path/to/commands.json /path/to/commands.dot");
            return 0;
        }

        if (args.Length > 2)
        {
            Console.WriteLine("Too many arguments");
            return 1;
        }

        file_path = args[0];
        if(args.Length > 1)
            out_file = args[1];

        if (file_path.ContainsAny(Path.GetInvalidPathChars()))
        {
            Console.WriteLine($"Input file path {file_path} contains invalid characters.");
            
            return 1;
        }
        
        if (out_file.ContainsAny(Path.GetInvalidPathChars()))
        {
            Console.WriteLine($"Output file path {out_file} contains invalid characters.");
            return 1;
        }

        if (Directory.Exists(out_file))
        {
            Console.WriteLine($"Output path {out_file} is a directory");
            return 1;
        }
        
        if (Directory.Exists(file_path))
        {
            Console.WriteLine($"Input path {file_path} is a directory");
            return 1;
        }

        if (File.Exists(out_file))
        {
            Console.WriteLine($"Output file {out_file} already exists.");
            return 1;
        }
        
        if (!File.Exists(file_path))
        {
            Console.WriteLine("File not found: " + file_path);
            return 1;
        }
        
        string? data = null;
        try
        {
            Console.WriteLine($"Reading file: {file_path}");
            data = File.ReadAllText(file_path);
        }
        catch (Exception exception)
        {
            Console.WriteLine("Failed to Read JSON file. Error: \n" + exception.Message);
            return 1;
        }

        CommandNode root;
        
        try
        {
            Console.WriteLine($"Parsing JSON");
            root = JsonSerializer.Deserialize<CommandNode>(data)!;
        }
        catch (Exception e)
        {
            Console.WriteLine("Failed to parse JSON file. Error: \n" + e.Message);
            Console.WriteLine(e);
            return 1;
        }
        
        Console.WriteLine($"Creating Graph");
        DotGraph g = new DotGraph().WithIdentifier("MC commands").Directed().Strict();
        
        root.AddToGraph(g, "root", null);
        root.AddRedirects(g, root);
        
        using var writer = new StringWriter();
        var context = new CompilationContext(writer, new CompilationOptions());
        
        g.CompileAsync(context).Wait();
        
        var result = writer.GetStringBuilder().ToString();
        
        try
        {
            Console.WriteLine($"Writing to file: {out_file}");
            File.WriteAllText(out_file, result);
        }
        catch (Exception e)
        {
            Console.WriteLine("Failed to write commands.dot file. Error: \n" + e.Message);
            Console.WriteLine(e);
            return 1;
        }
        Console.WriteLine("Done");
        return 0;
    }
}
public class CommandNode
{
    // Used internally represent nodes
    private static int IdCntr = 0;
    
    public required string type { get; set; }
    public Dictionary<string, CommandNode>? children { get; set; }
    public bool? executable { get; set; }
    public string? parser { get; set; }
    public List<string>? redirect { get; set; }

    private int id = -1;
    
    public void AddRedirects(DotGraph g, CommandNode root)
    {
        if (redirect != null)
        {
            foreach (var se in redirect)
            {
                int other_id = root.children[se].id;
                var edg = new DotEdge().From(id.ToString()).To(other_id.ToString());
                edg.WithStyle(DotEdgeStyle.Dashed);
                g.Add(edg);
            }
        }


        if (children != null)
        {
            foreach (var keyValuePair in children)
            {
                keyValuePair.Value.AddRedirects(g, root);
            }
        }
    }
    
    // This function recursively adds all vertexes and edges to the graph not including redirects
    public void AddToGraph(DotGraph g, string name, DotNode? parent)
    {
        executable ??= false;

        string label = "(root)";
        switch (type)
        {
            case "argument":
                label = $"<{name}>";
                label += $"\n({RemoveNameSpace(parser)})";
                break;
            case "literal":
                label = $"\"{name}\"";
                break;
        }
        
        var nd = new DotNode()
            .WithIdentifier(IdCntr.ToString())
            .WithShape(DotNodeShape.Box)
            .WithLabel(label)
            .WithFillColor((bool)executable ? DotColor.PaleGreen : DotColor.White)
            .WithFontColor(DotColor.Black)
            .WithStyle(DotNodeStyle.Filled)
            .WithColor(DotColor.Black);

        id = IdCntr;
        
        IdCntr++;
        g.Add(nd);

        if (parent != null)
        {
            var edg = new DotEdge().From(parent).To(nd);
            g.Add(edg);
        }

        if (children != null)
        {
            foreach (var kv in children)
            {
                kv.Value.AddToGraph(g, kv.Key, nd);
            }
        }
    }
    
    public static string RemoveNameSpace(string s)
    {
        int ind = s.IndexOf(':', StringComparison.InvariantCulture);
        if (ind == -1)
        {
            throw new InvalidDataException($"The parser id {s} does not contain a namespace.?");
        }

        ind++; // Exclude the ':'

        return s[ind..];
    }
}