using System.Diagnostics;
using System.Text;
using System.Text.Json;
using VirgoBot.Configuration;
using VirgoBot.Functions;
using VirgoBot.Utilities;

namespace VirgoBot.Services;

public class McpClientService : IDisposable
{
    private readonly Dictionary<string, McpConnection> _connections = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public async Task ConnectAllAsync(List<McpServerConfig> configs)
    {
        foreach (var config in configs.Where(c => c.Enabled))
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
                ColorLog.Error("MCP", $"连接失败 [{config.Name}]: {ex.Message}");
                _connections[config.Name] = new McpConnection(config) { Status = "error", ErrorMessage = ex.Message };
            }
        }
    }

    public async Task DisconnectAllAsync()
    {
        foreach (var conn in _connections.Values)
        {
            try { await conn.DisconnectAsync(); }
            catch { /* ignore */ }
        }
        _connections.Clear();
    }

    public List<FunctionDefinition> GetAllToolDefinitions()
    {
        var definitions = new List<FunctionDefinition>();
        foreach (var (serverName, conn) in _connections)
        {
            if (conn.Status != "connected") continue;
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
        if (!_connections.TryGetValue(serverName, out var conn) || conn.Status != "connected")
            return $"MCP server '{serverName}' is not connected";

        return await conn.CallToolAsync(toolName, args);
    }

    public List<McpServerStatus> GetStatuses()
    {
        return _connections.Select(kv => new McpServerStatus
        {
            Name = kv.Key,
            Status = kv.Value.Status,
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
            Status = conn.Status,
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

        try
        {
            var conn = new McpConnection(config);
            await conn.ConnectAsync();
            _connections[name] = conn;
            ColorLog.Success("MCP", $"已重连: {name} ({conn.Tools.Count} 个工具)");
        }
        catch (Exception ex)
        {
            ColorLog.Error("MCP", $"重连失败 [{name}]: {ex.Message}");
            _connections[name] = new McpConnection(config) { Status = "error", ErrorMessage = ex.Message };
        }
    }

    public void Dispose()
    {
        foreach (var conn in _connections.Values)
        {
            try { conn.DisconnectAsync().GetAwaiter().GetResult(); } catch { }
        }
        _connections.Clear();
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

public class McpConnection
{
    public McpServerConfig Config { get; }
    public string Status { get; set; } = "disconnected";
    public string? ErrorMessage { get; set; }
    public List<McpToolInfo> Tools { get; } = new();

    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private readonly HttpClient? _httpClient;
    private int _requestId;
    private string? _sseEndpoint;

    public McpConnection(McpServerConfig config)
    {
        Config = config;
        if (config.Transport == "sse")
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }
    }

    public async Task ConnectAsync()
    {
        Status = "connecting";
        try
        {
            if (Config.Transport == "stdio")
                await ConnectStdioAsync();
            else if (Config.Transport == "sse")
                await ConnectSseAsync();
            else
                throw new Exception($"Unknown transport: {Config.Transport}");

            Status = "connected";
        }
        catch
        {
            Status = "error";
            throw;
        }
    }

    private async Task ConnectStdioAsync()
    {
        var psi = new ProcessStartInfo
        {
            FileName = Config.Command,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in Config.Args)
            psi.ArgumentList.Add(arg);

        foreach (var (key, value) in Config.Env)
            psi.Environment[key] = value;

        _process = Process.Start(psi) ?? throw new Exception("Failed to start process");
        _stdin = _process.StandardInput;
        _stdin.AutoFlush = true;
        _stdout = _process.StandardOutput;

        // Initialize handshake
        var initResult = await SendRequestAsync("initialize", new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { },
            clientInfo = new { name = "VirgoBot", version = "1.0" }
        });

        // Send initialized notification
        await SendNotificationAsync("notifications/initialized");

        // Discover tools
        await DiscoverToolsAsync();
    }

    private async Task ConnectSseAsync()
    {
        _sseEndpoint = Config.Url.TrimEnd('/');

        // Initialize handshake
        var initResult = await SendRequestAsync("initialize", new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { },
            clientInfo = new { name = "VirgoBot", version = "1.0" }
        });

        // Send initialized notification
        await SendNotificationAsync("notifications/initialized");

        // Discover tools
        await DiscoverToolsAsync();
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
                {
                    tool.InputSchema = JsonSerializer.Deserialize<object>(schema.GetRawText());
                }

                Tools.Add(tool);
            }
        }
    }

    public async Task<string> CallToolAsync(string toolName, JsonElement arguments)
    {
        var result = await SendRequestAsync("tools/call", new
        {
            name = toolName,
            arguments
        });

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
        var request = new
        {
            jsonrpc = "2.0",
            id,
            method,
            @params
        };

        var json = JsonSerializer.Serialize(request);

        if (Config.Transport == "stdio")
            return await SendStdioRequestAsync(json, id);
        else
            return await SendSseRequestAsync(json, id);
    }

    private async Task SendNotificationAsync(string method)
    {
        var notification = new
        {
            jsonrpc = "2.0",
            method
        };

        var json = JsonSerializer.Serialize(notification);

        if (Config.Transport == "stdio")
        {
            await _stdin!.WriteLineAsync(json);
        }
        else
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            await _httpClient!.PostAsync(_sseEndpoint, content);
        }
    }

    private async Task<JsonElement> SendStdioRequestAsync(string json, int expectedId)
    {
        await _stdin!.WriteLineAsync(json);

        // Read lines until we get a response with matching id
        while (true)
        {
            var line = await ReadLineWithTimeoutAsync(_stdout!, TimeSpan.FromSeconds(30));
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                // Skip notifications (no id)
                if (!root.TryGetProperty("id", out var idEl)) continue;

                var responseId = idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt32() : 0;
                if (responseId != expectedId) continue;

                if (root.TryGetProperty("error", out var errorEl))
                {
                    var errMsg = errorEl.TryGetProperty("message", out var m) ? m.GetString() : "Unknown error";
                    throw new Exception($"MCP error: {errMsg}");
                }

                if (root.TryGetProperty("result", out var resultEl))
                    return resultEl.Clone();

                return root.Clone();
            }
            catch (JsonException)
            {
                continue; // Skip non-JSON lines
            }
        }
    }

    private static async Task<string?> ReadLineWithTimeoutAsync(StreamReader reader, TimeSpan timeout)
    {
        var cts = new CancellationTokenSource(timeout);
        try
        {
            var readTask = reader.ReadLineAsync();
            var delayTask = Task.Delay(timeout, cts.Token);
            var completedTask = await Task.WhenAny(readTask, delayTask);
            if (completedTask == readTask)
            {
                cts.Cancel();
                return await readTask;
            }
            throw new TimeoutException("MCP server response timeout");
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("MCP server response timeout");
        }
    }

    private async Task<JsonElement> SendSseRequestAsync(string json, int expectedId)
    {
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient!.PostAsync(_sseEndpoint, content);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var errorEl))
        {
            var errMsg = errorEl.TryGetProperty("message", out var m) ? m.GetString() : "Unknown error";
            throw new Exception($"MCP error: {errMsg}");
        }

        if (root.TryGetProperty("result", out var resultEl))
            return resultEl.Clone();

        return root.Clone();
    }

    public async Task DisconnectAsync()
    {
        Status = "disconnected";
        Tools.Clear();

        if (_process != null && !_process.HasExited)
        {
            try
            {
                _stdin?.Close();
                if (!_process.WaitForExit(3000))
                    _process.Kill();
            }
            catch { }
            _process.Dispose();
            _process = null;
        }

        _stdin = null;
        _stdout = null;
        _httpClient?.Dispose();
    }
}
