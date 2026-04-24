using System.Text.Json;
using Telegram.Bot;
using VirgoBot.Configuration;
using VirgoBot.Features.Email;
using VirgoBot.Integrations.ILink;
using VirgoBot.Services;
using VirgoBot.Utilities;

namespace VirgoBot.Functions;

public class FunctionRegistry
{
    private readonly Dictionary<string, Func<JsonElement, Task<string>>> _functions = new();
    private readonly List<object> _toolSchemas = new();
    private readonly Dictionary<string, string> _categoryMap = new(); // name -> category
    private readonly Config _config;

    public FunctionRegistry(Config config, MemoryService memoryService, ScheduledTaskService scheduledTaskService)
    {
        _config = config;
        RegisterAll(SystemFunctions.Register());
        RegisterAll(ShellFunctions.Register());
        RegisterAll(FileFunctions.Register());
        RegisterAll(SoulFunctions.Register(memoryService));
        RegisterAll(SkillManagementFunctions.Register());
        RegisterAll(ScheduledTaskFunctions.Register(scheduledTaskService));
        RegisterAll(SkillLoader.LoadAll(), "skill");
    }

    public void SetEmailService(EmailService emailService)
        => RegisterAll(EmailFunctions.Register(emailService));

    public void SetContactService(ContactService contactService)
        => RegisterAll(ContactFunctions.Register(contactService));

    public void SetILinkBridgeService(ILinkBridgeService iLinkBridge)
        => RegisterAll(ILinkFunctions.Register(iLinkBridge));

    public void SetShellSessionService(ShellSessionService shellSessionService)
        => RegisterAll(InteractiveShellFunctions.Register(shellSessionService));

    public void SetMcpService(McpClientService mcpService)
    {
        // Unregister old MCP tools before registering new ones
        UnregisterByCategory("mcp");
        RegisterAll(mcpService.GetAllToolDefinitions(), "mcp");
    }

    public void SetTelegramBot(TelegramBotClient bot, long chatId)
    {
        if (!_functions.ContainsKey("send_photo"))
        {
            RegisterAll(TelegramFunctions.Register(bot, chatId));
        }
    }

    public async Task<string> ExecuteAsync(string name, JsonElement input)
    {
        return _functions.TryGetValue(name, out var handler) ? await handler(input) : "unknown tool";
    }

    public object[] GetToolSchemas() => _toolSchemas.ToArray();

    public void UnregisterByCategory(string category)
    {
        var toRemove = _categoryMap.Where(kv => kv.Value == category).Select(kv => kv.Key).ToList();
        foreach (var name in toRemove)
        {
            _functions.Remove(name);
            _categoryMap.Remove(name);
            _toolSchemas.RemoveAll(s =>
            {
                var json = JsonSerializer.Serialize(s);
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("name", out var n) && n.GetString() == name;
            });
        }
        if (toRemove.Count > 0)
            ColorLog.Info("FUNC", $"已卸载 {toRemove.Count} 个 [{category}] 工具");
    }

    public Dictionary<string, int> GetToolCountByCategory()
    {
        var counts = new Dictionary<string, int>();
        foreach (var cat in _categoryMap.Values)
        {
            counts.TryGetValue(cat, out var count);
            counts[cat] = count + 1;
        }
        return counts;
    }

    private void RegisterAll(IEnumerable<FunctionDefinition> definitions, string? categoryOverride = null)
    {
        foreach (var def in definitions)
        {
            var category = categoryOverride ?? def.Category;
            if (_functions.ContainsKey(def.Name))
            {
                ColorLog.Warning("FUNC", $"工具名称冲突: {def.Name}，将覆盖旧定义");
                // Remove old schema
                _toolSchemas.RemoveAll(s =>
                {
                    var json = JsonSerializer.Serialize(s);
                    using var doc = JsonDocument.Parse(json);
                    return doc.RootElement.TryGetProperty("name", out var n) && n.GetString() == def.Name;
                });
            }
            _functions[def.Name] = def.Handler;
            _categoryMap[def.Name] = category;
            _toolSchemas.Add(new { name = def.Name, description = def.Description, input_schema = def.InputSchema });
        }
    }
}
