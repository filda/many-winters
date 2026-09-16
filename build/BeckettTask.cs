using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace ManyWinters.Build;

/// <summary>
/// Makes sure one Godot editor is open on the project with Beckett listening, then points the
/// MCP client configs at it. Never starts a second editor: Beckett would walk to the next free
/// port and every client config would go stale.
/// </summary>
[TaskName("Beckett")]
public sealed class BeckettTask : FrostingTask<BuildContext>
{
    private const string EditorWindowTitle = "ManyWinters Godot - Godot Engine";

    public override void Run(BuildContext context)
    {
        var port = ReadPort(context);
        if (IsListening(port))
        {
            context.Log.Information(Verbosity.Normal, "Beckett is already listening on port {0}.", port);
        }
        else if (EditorIsOpen())
        {
            context.Log.Information(Verbosity.Normal, "An editor is open but nothing listens on port {0} yet; waiting.", port);
        }
        else
        {
            context.Log.Information(Verbosity.Normal, "No editor on this project; starting one.");
            var startInfo = new ProcessStartInfo("godot")
            {
                WorkingDirectory = context.RootDirectory,
                UseShellExecute = true,
            };
            startInfo.ArgumentList.Add("-e");
            startInfo.ArgumentList.Add("--path");
            startInfo.ArgumentList.Add(context.GodotProjectPath);

            _ = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start Godot.");
        }

        var deadline = DateTime.UtcNow.AddSeconds(180);
        while (!IsListening(port))
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException($"Beckett did not start listening on port {port}.");
            }

            Thread.Sleep(TimeSpan.FromSeconds(2));
            port = ReadPort(context);
        }

        var token = File.ReadAllText(Path.Combine(context.GodotProjectPath, ".beckett", "token")).Trim();
        var url = $"http://127.0.0.1:{port}/mcp/{token}";
        WriteClaudeCodeConfig(context, url);
        WriteOpenCodeConfig(context, url);
        context.Log.Information(Verbosity.Normal, "Beckett is ready at {0}", url);
    }

    private static int ReadPort(BuildContext context)
    {
        var path = Path.Combine(context.GodotProjectPath, ".beckett", "port");
        return File.Exists(path) && int.TryParse(File.ReadAllText(path).Trim(), out var port) ? port : 8770;
    }

    private static bool IsListening(int port)
    {
        try
        {
            using var client = new TcpClient();
            return client.ConnectAsync("127.0.0.1", port).Wait(TimeSpan.FromMilliseconds(250));
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static bool EditorIsOpen()
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.ProcessName.StartsWith("Godot_v", StringComparison.Ordinal)
                        && process.MainWindowTitle.Contains(EditorWindowTitle, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                catch (InvalidOperationException)
                {
                    // The process exited between enumeration and inspection.
                }
            }
        }

        return false;
    }

    // Beckett writes its own client config next to project.godot, where no MCP client running
    // from the repo root looks. Claude Code reads .mcp.json, OpenCode reads opencode.json; both
    // are gitignored because the URL carries this machine's token. Other servers listed in
    // either file are kept.
    private static void WriteClaudeCodeConfig(BuildContext context, string url)
    {
        var path = Path.Combine(context.RootDirectory, ".mcp.json");
        var root = LoadJsonObject(path);
        var beckett = GetOrAddObject(GetOrAddObject(root, "mcpServers"), "beckett");
        beckett["type"] = "http";
        beckett["url"] = url;
        SaveIfChanged(context, path, root);
    }

    private static void WriteOpenCodeConfig(BuildContext context, string url)
    {
        var path = Path.Combine(context.RootDirectory, "opencode.json");
        var root = LoadJsonObject(path);
        root["$schema"] ??= "https://opencode.ai/config.json";
        var beckett = GetOrAddObject(GetOrAddObject(root, "mcp"), "beckett");
        beckett["type"] = "remote";
        beckett["url"] = url;
        beckett["enabled"] = true;
        SaveIfChanged(context, path, root);
    }

    private static JsonObject LoadJsonObject(string path)
    {
        if (!File.Exists(path))
        {
            return new JsonObject();
        }

        return JsonNode.Parse(File.ReadAllText(path)) as JsonObject
            ?? throw new InvalidOperationException($"'{path}' does not hold a JSON object.");
    }

    private static JsonObject GetOrAddObject(JsonObject parent, string name)
    {
        if (parent[name] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        parent[name] = created;
        return created;
    }

    private static void SaveIfChanged(BuildContext context, string path, JsonObject root)
    {
        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
            .Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        if (File.Exists(path) && File.ReadAllText(path) == json)
        {
            return;
        }

        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        context.Log.Information(Verbosity.Normal, "Updated {0}", Path.GetFileName(path));
    }
}
