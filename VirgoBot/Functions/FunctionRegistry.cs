using System.Text.Json;
using Telegram.Bot;
using VirgoBot.Configuration;
using VirgoBot.Features.Email;
using VirgoBot.Integrations.ILink;
using VirgoBot.Services;

namespace VirgoBot.Functions;

public class FunctionRegistry
{
    private readonly Dictionary<string, Func<JsonElement, Task<string>>> _functions = new();
    private readonly List<object> _toolSchemas = new();
    private readonly Config _config;

    public FunctionRegistry(Config config, MemoryService memoryService)
    {
        _config = config;
        RegisterAll(SystemFunctions.Register());
        RegisterAll(ShellFunctions.Register());
        RegisterAll(FileFunctions.Register());
        RegisterAll(SoulFunctions.Register(memoryService));
        RegisterAll(SkillManagementFunctions.Register());
        RegisterAll(SkillLoader.LoadAll());
    }

    public void SetEmailService(EmailService emailService)
        => RegisterAll(EmailFunctions.Register(emailService));

    public void SetPlaywrightService(PlaywrightService playwrightService)
        => RegisterAll(PlaywrightFunctions.Register(playwrightService));

    public void SetStickerService(StickerService stickerService)
        => RegisterAll(StickerFunctions.Register(stickerService));

    public void SetContactService(ContactService contactService)
        => RegisterAll(ContactFunctions.Register(contactService));

    public void SetILinkBridgeService(ILinkBridgeService iLinkBridge)
        => RegisterAll(ILinkFunctions.Register(iLinkBridge));

    public void SetShellSessionService(ShellSessionService shellSessionService)
        => RegisterAll(InteractiveShellFunctions.Register(shellSessionService));

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

    private void RegisterAll(IEnumerable<FunctionDefinition> definitions)
    {
        foreach (var def in definitions)
        {
            _functions[def.Name] = def.Handler;
            _toolSchemas.Add(new { name = def.Name, description = def.Description, input_schema = def.InputSchema });
        }
    }
}
