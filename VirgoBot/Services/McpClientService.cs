using System.Diagnostics;
using System.Text;
using System.Text.Json;
using VirgoBot.Configuration;
using VirgoBot.Functions;
using VirgoBot.Utilities;

namespace VirgoBot.Services;

public enum McpConnectionStatus { Disconnected, Connecting, Connected, Error }

public interface IMcpTransport : IDisposable
{
    /// <summary>Send a JSON-RPC request and return the response body.</summary>
    Task<string> SendRequestAsync(string json);
    /// <summary>Send a JSON-RPC notification (no response expected).</summary>
    Task SendNotificationAsync(string json);
}

public class StdioMcpTransport : IMcpTransport
{
    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private readonly McpServerConfig _config;

    public StdioMcpTransport(McpServerConfig config) => _config = config;

    public async Task StartAsync()
    {
        var command = _config.Command;
        var args = _config.Args.ToList();

        // On Windows, .cmd/.bat files (like npm, npx, pnpm) can't be started directly
        // with UseShellExecute=false. Route through cmd.exe.
        if (OperatingSystem.IsWindows() && !command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            args.InsertRange(0, ["/c", command]);
            command = "cmd.exe";
        }

        var psi = new ProcessStartInfo
        {
            FileName = command,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        foreach (var (key, value) in _config.Env) psi.Environment[key] = value;

        _process = Process.Start(psi) ?? throw new Exception("Failed to start process");
        _stdin = _process.StandardInput;
        _stdin.AutoFlush = true;
        _stdout = _process.StandardOutput;

        // Drain stderr asynchronously to prevent buffer deadlock.
        // npx -y writes download progress to stderr; if the 4KB buffer fills up,
        // the child process blocks and stdout stops responding.
        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                ColorLog.Info("MCP", $"[{_config.Name}] {e.Data}");
        };
        _process.BeginErrorReadLine();
    }

    public async Task<string> SendRequestAsync(string json)
    {
        await _stdin!.WriteLineAsync(json);

        // Parse the expected id from the request so we can match the response
        int expectedId;
        using (var doc = JsonDocument.Parse(json))
            expectedId = doc.RootElement.GetProperty("id").GetInt32();

        var timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
        while (true)
        {
            var line = await ReadLineWithTimeoutAsync(_stdout!, timeout);
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Check if this line is the response we're looking for
            try
            {
                using var respDoc = JsonDocument.Parse(line);
                var root = respDoc.RootElement;
                if (!root.TryGetProperty("id", out var idEl)) continue; // notification, skip
                var respId = idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt32() : -1;
                if (respId != expectedId) continue;
                return line; // Found matching response
            }
            catch (JsonException) { continue; }
        }
    }

    public async Task SendNotificationAsync(string json)
    {
        await _stdin!.WriteLineAsync(json);
    }

    private static async Task<string?> ReadLineWithTimeoutAsync(StreamReader reader, TimeSpan timeout)
    {
        var readTask = reader.ReadLineAsync();
        var delayTask = Task.Delay(timeout);
        var completed = await Task.WhenAny(readTask, delayTask);
        if (completed == readTask) return await readTask;
        throw new TimeoutException("MCP server response timeout");
    }

    public void Dispose()
    {
        if (_process != null && !_process.HasExited)
        {
            try
            {
                _stdin?.Close();
                if (!_process.WaitForExit(3000)) _process.Kill();
            }
            catch { }
            _process.Dispose();
        }
        _stdin = null;
        _stdout = null;
    }
}

public class HttpMcpTransport : IMcpTransport
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;

    public HttpMcpTransport(McpServerConfig config)
    {
        _endpoint = config.Url.TrimEnd('/');
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds) };
    }

    public async Task<string> SendRequestAsync(string json)
    {
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(_endpoint, content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task SendNotificationAsync(string json)
    {
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await _httpClient.PostAsync(_endpoint, content);
    }

    public void Dispose() => _httpClient.Dispose();
}

public class McpConnection
{
    public McpServerConfig Config { get; }
    public McpConnectionStatus Status { get; set; } = McpConnectionStatus.Disconnected;
    public string? ErrorMessage { get; set; }
    public List<McpToolInfo> Tools { get; } = new();

    private IMcpTransport? _transport;
    private int _requestId;

    public McpConnection(McpServerConfig config) => Config = config;

    public async Task ConnectAsync()
    {
        Status = McpConnectionStatus.Connecting;
        try
        {
            _transport = Config.Transport == "stdio"
                ? await CreateStdioTransport()
                : new HttpMcpTransport(Config);

            await InitializeAsync();
            await DiscoverToolsAsync();
            Status = McpConnectionStatus.Connected;
        }
        catch
        {
            Status = McpConnectionStatus.Error;
            throw;
        }
    }

    private async Task<StdioMcpTransport> CreateStdioTransport()
    {
        var t = new StdioMcpTransport(Config);
        await t.StartAsync();
        return t;
    }

    private async Task InitializeAsync()
    {
        await SendRequestAsync("initialize", new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { },
            clientInfo = new { name = "VirgoBot", version = "1.0" }
        });
        await SendNotificationAsync("notifications/initialized");
    }

    private async Task DiscoverToolsAsync()
    {
        var result = await SendRequestAsync("tools/list", new { });
        if (result.TryGetProperty("tools", out var toolsEl) && toolsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var toolEl in toolsEl.EnumerateArray())
            {
                var tool = new McpToolInfo
                {
                    Name = toolEl.GetProperty("name").GetString() ?? "",
                    Description = toolEl.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                };
                if (toolEl.TryGetProperty("inputSchema", out var schema))
                    tool.InputSchema = JsonSerializer.Deserialize<object>(schema.GetRawText());
                Tools.Add(tool);
            }
        }
    }

    public async Task<string> CallToolAsync(string toolName, JsonElement arguments)
    {
        ColorLog.Info("MCP", $"调用工具: {Config.Name}/{toolName}");
        var result = await SendRequestAsync("tools/call", new { name = toolName, arguments });

        if (result.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var item in contentEl.EnumerateArray())
            {
                var type = item.TryGetProperty("type", out var t) ? t.GetString() : "text";
                if (type == "text" && item.TryGetProperty("text", out var textEl))
                    sb.AppendLine(textEl.GetString());
            }
            return sb.ToString().TrimEnd();
        }
        return result.GetRawText();
    }

    private async Task<JsonElement> SendRequestAsync(string method, object @params)
    {
        var id = Interlocked.Increment(ref _requestId);
        var json = JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params });
        var responseText = await _transport!.SendRequestAsync(json);
        return ParseJsonRpcResult(responseText);
    }

    private static JsonElement ParseJsonRpcResult(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;
        if (root.TryGetProperty("error", out var errorEl))
        {
            var errMsg = errorEl.TryGetProperty("message", out var m) ? m.GetString() : "Unknown error";
            throw new Exception($"MCP error: {errMsg}");
        }
        return root.TryGetProperty("result", out var resultEl) ? resultEl.Clone() : root.Clone();
    }

    private async Task SendNotificationAsync(string method)
    {
        var json = JsonSerializer.Serialize(new { jsonrpc = "2.0", method });
        await _transport!.SendNotificationAsync(json);
    }

    public async Task DisconnectAsync()
    {
        Status = McpConnectionStatus.Disconnected;
        Tools.Clear();
        _transport?.Dispose();
        _transport = null;
    }
}

public class McpServerStatus
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = "disconnected";
    public string Transport { get; set; } = "";
    public int ToolCount { get; set; }
    public string? Error { get; set; }
}

public class McpToolInfo
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public object? InputSchema { get; set; }
}

public class McpClientService : IDisposable
{
    private readonly Dictionary<string, McpConnection> _connections = new();

    public async Task ConnectAllAsync(List<McpServerConfig> configs)
    {
        var tasks = configs.Where(c => c.Enabled).Select(ConnectWithRetryAsync);
        await Task.WhenAll(tasks);
    }

    private async Task ConnectWithRetryAsync(McpServerConfig config)
    {
        try
        {
            var conn = new McpConnection(config);
            await conn.ConnectAsync();
            _connections[config.Name] = conn;
            ColorLog.Success("MCP", $"已连接: {config.Name} ({conn.Tools.Count} 个工具)");
        }
        catch (Exception ex)
        {
            ColorLog.Error("MCP", $"连接失败 [{config.Name}]: {ex.Message}，尝试重连...");
            // Retry once
            try
            {
                var conn = new McpConnection(config);
                await conn.ConnectAsync();
                _connections[config.Name] = conn;
                ColorLog.Success("MCP", $"重连成功: {config.Name} ({conn.Tools.Count} 个工具)");
            }
            catch (Exception retryEx)
            {
                ColorLog.Error("MCP", $"重连失败 [{config.Name}]: {retryEx.Message}");
                _connections[config.Name] = new McpConnection(config)
                {
                    Status = McpConnectionStatus.Error,
                    ErrorMessage = retryEx.Message
                };
            }
        }
    }

    public async Task DisconnectAllAsync()
    {
        foreach (var conn in _connections.Values)
        {
            try { await conn.DisconnectAsync(); } catch { }
        }
        _connections.Clear();
    }

    public List<FunctionDefinition> GetAllToolDefinitions()
    {
        var definitions = new List<FunctionDefinition>();
        foreach (var (serverName, conn) in _connections)
        {
            if (conn.Status != McpConnectionStatus.Connected) continue;
            foreach (var tool in conn.Tools)
            {
                var sName = serverName;
                var tName = tool.Name;
                var funcName = $"mcp_{sName}_{tName}";
                definitions.Add(new FunctionDefinition(
                    funcName,
                    tool.Description ?? "",
                    tool.InputSchema ?? new { type = "object", properties = new { } },
                    async args => await CallToolAsync(sName, tName, args)
                ));
            }
        }
        return definitions;
    }

    public async Task<string> CallToolAsync(string serverName, string toolName, JsonElement args)
    {
        if (!_connections.TryGetValue(serverName, out var conn) || conn.Status != McpConnectionStatus.Connected)
            return $"MCP server '{serverName}' is not connected";
        return await conn.CallToolAsync(toolName, args);
    }

    public List<McpServerStatus> GetStatuses()
    {
        return _connections.Select(kv => new McpServerStatus
        {
            Name = kv.Key,
            Status = StatusToString(kv.Value.Status),
            Transport = kv.Value.Config.Transport,
            ToolCount = kv.Value.Tools.Count,
            Error = kv.Value.ErrorMessage,
        }).ToList();
    }

    public McpServerStatus? GetStatus(string name)
    {
        if (!_connections.TryGetValue(name, out var conn)) return null;
        return new McpServerStatus
        {
            Name = name,
            Status = StatusToString(conn.Status),
            Transport = conn.Config.Transport,
            ToolCount = conn.Tools.Count,
            Error = conn.ErrorMessage,
        };
    }

    public List<McpToolInfo> GetTools(string serverName)
    {
        return _connections.TryGetValue(serverName, out var conn) ? conn.Tools : new List<McpToolInfo>();
    }

    public async Task ReconnectServerAsync(string name, List<McpServerConfig> configs)
    {
        if (_connections.TryGetValue(name, out var existing))
        {
            try { await existing.DisconnectAsync(); } catch { }
            _connections.Remove(name);
        }

        var config = configs.FirstOrDefault(c => c.Name == name);
        if (config == null || !config.Enabled) return;

        await ConnectWithRetryAsync(config);
    }

    private static string StatusToString(McpConnectionStatus status) => status switch
    {
        McpConnectionStatus.Connected => "connected",
        McpConnectionStatus.Connecting => "connecting",
        McpConnectionStatus.Error => "error",
        _ => "disconnected",
    };

    public void Dispose()
    {
        foreach (var conn in _connections.Values)
        {
            try { conn.DisconnectAsync().GetAwaiter().GetResult(); } catch { }
        }
        _connections.Clear();
    }
}
