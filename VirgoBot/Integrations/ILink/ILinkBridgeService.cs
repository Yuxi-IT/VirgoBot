using OpenILink.SDK;
using VirgoBot.Utilities;

namespace VirgoBot.Integrations.ILink;

public sealed class ILinkBridgeService : IDisposable
{
    private readonly string _messageSplitDelimiters;
    private readonly OpenILinkClient _client;
    private CancellationTokenSource? _monitorCts;

    private const string BufferFilePath = "config/ilink_buffer.txt";

    public ILinkBridgeService(string token, string messageSplitDelimiters)
    {
        _messageSplitDelimiters = messageSplitDelimiters;
        _client = OpenILinkClient.Create(token);
    }

    public async Task StartAsync(Func<WeixinMessage, Task> onMessage, CancellationToken cancellationToken)
    {
        _monitorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        ColorLog.Info("ILINK", "正在启动 iLink 消息监听...");

        string? initialBuffer = null;
        if (File.Exists(BufferFilePath))
        {
            initialBuffer = File.ReadAllText(BufferFilePath);
            if (string.IsNullOrWhiteSpace(initialBuffer))
                initialBuffer = null;
        }

        var options = new MonitorOptions
        {
            InitialBuffer = initialBuffer,
            OnError = ex =>
            {
                ColorLog.Error("ILINK", $"轮询错误: {ex.Message}");
            },
            OnSessionExpired = () =>
            {
                ColorLog.Error("ILINK", "会话已过期，请重新登录");
            },
            OnBufferUpdated = buffer =>
            {
                try
                {
                    File.WriteAllText(BufferFilePath, buffer);
                }
                catch (Exception ex)
                {
                    ColorLog.Error("ILINK", $"保存 buffer 失败: {ex.Message}");
                }
            }
        };

        _ = Task.Run(async () =>
        {
            try
            {
                await _client.MonitorAsync(async message =>
                {
                    var text = message.ExtractText();
                    if (string.IsNullOrWhiteSpace(text))
                        return;

                    ColorLog.Info("ILINK", $"收到消息: UserId={message.FromUserId}, Text={text}");
                    await onMessage(message);
                }, options, _monitorCts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ColorLog.Error("ILINK", $"监听异常退出: {ex.Message}");
            }

            ColorLog.Info("ILINK", "消息监听已停止");
        }, _monitorCts.Token);

        ColorLog.Success("ILINK", "iLink 消息监听已启动");
    }

    public async Task ReplyTextAsync(WeixinMessage message, string text, CancellationToken cancellationToken = default)
    {
        await _client.ReplyTextAsync(message, text, cancellationToken);
        ColorLog.Success("ILINK", $"已回复: {text}");
    }

    public async Task ReplyLongTextAsync(WeixinMessage message, string text, CancellationToken cancellationToken = default)
    {
        var paragraphs = MessageSplitter.SplitMessage(text, _messageSplitDelimiters);

        foreach (var paragraph in paragraphs)
        {
            await _client.ReplyTextAsync(message, paragraph, cancellationToken);
            ColorLog.Success("ILINK", paragraph);
            await Task.Delay(300, cancellationToken);
        }
    }

    public async Task SendImageAsync(string userId, byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        var contextToken = _client.GetContextToken(userId)
            ?? throw new InvalidOperationException($"用户 {userId} 的 context_token 不存在");

        await _client.SendMediaFileAsync(userId, contextToken, imageBytes, "image.jpg", null, cancellationToken);
        ColorLog.Success("ILINK", $"已发送图片给 {userId}");
    }

    public async Task SendVoiceAsync(string userId, byte[] voiceBytes, CancellationToken cancellationToken = default)
    {
        var contextToken = _client.GetContextToken(userId)
            ?? throw new InvalidOperationException($"用户 {userId} 的 context_token 不存在");

        await _client.SendMediaFileAsync(userId, contextToken, voiceBytes, "voice.amr", null, cancellationToken);
        ColorLog.Success("ILINK", $"已发送语音给 {userId}");
    }

    public async Task SendVideoAsync(string userId, byte[] videoBytes, CancellationToken cancellationToken = default)
    {
        var contextToken = _client.GetContextToken(userId)
            ?? throw new InvalidOperationException($"用户 {userId} 的 context_token 不存在");

        await _client.SendMediaFileAsync(userId, contextToken, videoBytes, "video.mp4", null, cancellationToken);
        ColorLog.Success("ILINK", $"已发送视频给 {userId}");
    }

    public async Task SendFileAsync(string userId, byte[] fileBytes, string fileName, CancellationToken cancellationToken = default)
    {
        var contextToken = _client.GetContextToken(userId)
            ?? throw new InvalidOperationException($"用户 {userId} 的 context_token 不存在");

        await _client.SendMediaFileAsync(userId, contextToken, fileBytes, fileName, null, cancellationToken);
        ColorLog.Success("ILINK", $"已发送文件 {fileName} 给 {userId}");
    }

    public bool CanPushTo(string userId) => _client.CanPushTo(userId);

    public void Dispose()
    {
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _client.Dispose();
    }
}
