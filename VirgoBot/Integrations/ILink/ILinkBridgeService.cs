using System.Net.Http.Headers;
using ILink4NET.Client;
using ILink4NET.Models;
using VirgoBot.Configuration;
using VirgoBot.Utilities;

namespace VirgoBot.Integrations.ILink;

public sealed class ILinkBridgeService
{
    private readonly ILinkChannelConfig _config;
    private readonly HttpClient _httpClient;
    private readonly string _messageSplitDelimiters;
    private ILinkBotClient? _botClient;

    public ILinkBridgeService(ILinkChannelConfig config, string messageSplitDelimiters)
    {
        _config = config;
        _messageSplitDelimiters = messageSplitDelimiters;
        _httpClient = new HttpClient();

        if (!string.IsNullOrWhiteSpace(_config.Token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _config.Token);
        }
    }

    public bool IsEnabled => _config.Enabled;

    public async Task StartAsync(Func<IncomingMessage, Task> onMessage, CancellationToken cancellationToken)
    {
        if (!_config.Enabled)
        {
            return;
        }

        ColorLog.Info("ILINK", "正在初始化 iLink 客户端...");

        _botClient = ILinkBotClient.CreateDefault(_httpClient);

        // 如果有 Token，直接使用 Token 登录
        if (!string.IsNullOrWhiteSpace(_config.Token))
        {
            ColorLog.Info("ILINK", "使用 Token 登录...");
            var credentials = ParseTokenToCredentials(_config.Token);
            if (credentials != null)
            {
                _botClient.SetCredentials(credentials);
                ColorLog.Success("ILINK", "Token 登录成功");
            }
            else
            {
                ColorLog.Error("ILINK", "Token 格式无效");
                return;
            }
        }
        else
        {
            ColorLog.Error("ILINK", "未配置 Token");
            return;
        }

        // 启动消息轮询
        _ = Task.Run(() => RunPollingLoopAsync(onMessage, cancellationToken), cancellationToken);
    }

    private async Task RunPollingLoopAsync(Func<IncomingMessage, Task> onMessage, CancellationToken cancellationToken)
    {
        var cursor = string.Empty;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_botClient == null)
                {
                    break;
                }

                var batch = await _botClient.GetUpdatesAsync(cursor, cancellationToken);
                cursor = batch.NextCursor;

                foreach (var message in batch.Messages)
                {
                    if (string.IsNullOrWhiteSpace(message.Text))
                    {
                        continue;
                    }

                    ColorLog.Info("ILINK", $"收到消息: UserId={message.UserId}, Text={message.Text}");
                    await onMessage(message);
                }

                // 短暂延迟避免过于频繁的轮询
                if (batch.Messages.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                ColorLog.Error("ILINK", $"轮询错误: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }

        ColorLog.Info("ILINK", "消息轮询已停止");
    }

    public async Task ReplyTextAsync(IncomingMessage originalMessage, string text, CancellationToken cancellationToken = default)
    {
        if (_botClient == null)
        {
            throw new InvalidOperationException("iLink 客户端未初始化");
        }

        await _botClient.ReplyTextAsync(originalMessage, text, cancellationToken);
        ColorLog.Success("ILINK", $"已回复: {text}");
    }

    public async Task ReplyLongTextAsync(IncomingMessage originalMessage, string text, CancellationToken cancellationToken = default)
    {
        if (_botClient == null)
        {
            throw new InvalidOperationException("iLink 客户端未初始化");
        }

        var paragraphs = MessageSplitter.SplitMessage(text, _messageSplitDelimiters);

        foreach (var paragraph in paragraphs)
        {
            await _botClient.ReplyTextAsync(originalMessage, paragraph, cancellationToken);
            ColorLog.Success("ILINK", paragraph);
            await Task.Delay(300, cancellationToken);
        }
    }

    private SessionCredentials? ParseTokenToCredentials(string token)
    {
        // Token 格式: "botId@im.bot:sessionToken"
        // 例如: "4eaa53b3fc83@im.bot:060000d9886a065e232f93134a76ed112eb6bc"

        var parts = token.Split(':');
        if (parts.Length != 2)
        {
            return null;
        }

        var botId = parts[0]; // 例如: "4eaa53b3fc83@im.bot"
        var sessionToken = parts[1]; // 例如: "060000d9886a065e232f93134a76ed112eb6bc"

        // 从 botId 中提取实际的 ID
        var botIdParts = botId.Split('@');
        if (botIdParts.Length < 1)
        {
            return null;
        }

        var iLinkBotId = botIdParts[0]; // 例如: "4eaa53b3fc83"
        var iLinkUserId = botId; // 使用完整的 botId 作为 userId

        // 从配置中获取 API 基础 URL
        var apiBaseUriString = !string.IsNullOrWhiteSpace(_config.SendUrl)
            ? new Uri(_config.SendUrl).GetLeftPart(UriPartial.Authority)
            : "https://ilinkai.weixin.qq.com";

        var apiBaseUri = new Uri(apiBaseUriString);

        // BotToken 格式: "botId:sessionToken"
        var botToken = token;

        return new SessionCredentials(botToken, iLinkBotId, iLinkUserId, apiBaseUri);
    }
}
