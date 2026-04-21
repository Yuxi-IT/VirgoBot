using System.Net.Http.Headers;
using ILink4NET.Client;
using ILink4NET.Media;
using ILink4NET.Models;
using VirgoBot.Configuration;
using VirgoBot.Utilities;

namespace VirgoBot.Integrations.ILink;

public sealed class ILinkBridgeService : IDisposable
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

    public async Task SendImageAsync(string userId, byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        if (_botClient == null)
        {
            throw new InvalidOperationException("iLink 客户端未初始化");
        }

        var contextToken = await _botClient.ContextTokens.GetAsync(userId, cancellationToken);
        if (string.IsNullOrWhiteSpace(contextToken))
        {
            throw new InvalidOperationException($"用户 {userId} 的 context_token 不存在，请先让用户发送一条消息");
        }

        await _botClient.SendImageAsync(userId, contextToken, imageBytes, cancellationToken);
        ColorLog.Success("ILINK", $"已发送图片给 {userId}");
    }

    public async Task SendVoiceAsync(string userId, byte[] voiceBytes, CancellationToken cancellationToken = default)
    {
        if (_botClient == null)
        {
            throw new InvalidOperationException("iLink 客户端未初始化");
        }

        var contextToken = await _botClient.ContextTokens.GetAsync(userId, cancellationToken);
        if (string.IsNullOrWhiteSpace(contextToken))
        {
            throw new InvalidOperationException($"用户 {userId} 的 context_token 不存在，请先让用户发送一条消息");
        }

        await _botClient.SendVoiceAsync(userId, contextToken, voiceBytes, cancellationToken);
        ColorLog.Success("ILINK", $"已发送语音给 {userId}");
    }

    public async Task SendVideoAsync(string userId, byte[] videoBytes, CancellationToken cancellationToken = default)
    {
        if (_botClient == null)
        {
            throw new InvalidOperationException("iLink 客户端未初始化");
        }

        var contextToken = await _botClient.ContextTokens.GetAsync(userId, cancellationToken);
        if (string.IsNullOrWhiteSpace(contextToken))
        {
            throw new InvalidOperationException($"用户 {userId} 的 context_token 不存在，请先让用户发送一条消息");
        }

        await _botClient.SendVideoAsync(userId, contextToken, videoBytes, cancellationToken);
        ColorLog.Success("ILINK", $"已发送视频给 {userId}");
    }

    public async Task SendFileAsync(string userId, byte[] fileBytes, string fileName, CancellationToken cancellationToken = default)
    {
        if (_botClient == null)
        {
            throw new InvalidOperationException("iLink 客户端未初始化");
        }

        var contextToken = await _botClient.ContextTokens.GetAsync(userId, cancellationToken);
        if (string.IsNullOrWhiteSpace(contextToken))
        {
            throw new InvalidOperationException($"用户 {userId} 的 context_token 不存在，请先让用户发送一条消息");
        }

        var fileKey = Guid.NewGuid().ToString("N");
        var mediaRef = await _botClient.UploadMediaAsync(userId, fileKey, MediaType.File, fileBytes, cancellationToken);

        // 为文件类型添加文件名和大小信息
        var fileRefWithMeta = new UploadedMediaReference(
            mediaRef.EncryptQueryParam,
            mediaRef.AesKey,
            fileName,
            fileBytes.Length);

        await _botClient.SendMediaMessageAsync(userId, contextToken, fileRefWithMeta, MediaType.File, cancellationToken);
        ColorLog.Success("ILINK", $"已发送文件 {fileName} 给 {userId}");
    }

    private SessionCredentials? ParseTokenToCredentials(string token)
    {
        var parts = token.Split(':');
        if (parts.Length != 2)
        {
            return null;
        }

        var botId = parts[0];
        var sessionToken = parts[1];

        var botIdParts = botId.Split('@');
        if (botIdParts.Length < 1)
        {
            return null;
        }

        var iLinkBotId = botIdParts[0];
        var iLinkUserId = botId;

        var apiBaseUriString = !string.IsNullOrWhiteSpace(_config.SendUrl)
            ? new Uri(_config.SendUrl).GetLeftPart(UriPartial.Authority)
            : "https://ilinkai.weixin.qq.com";

        var apiBaseUri = new Uri(apiBaseUriString);

        var botToken = token;

        return new SessionCredentials(botToken, iLinkBotId, iLinkUserId, apiBaseUri);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
