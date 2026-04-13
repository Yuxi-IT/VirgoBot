using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using VirgoBot.Configuration;
using VirgoBot.Contracts;
using VirgoBot.Functions;
using VirgoBot.Integrations.ILink;
using VirgoBot.Services;
using VirgoBot.Utilities;

namespace VirgoBot.Channels;

public class HttpServerHost
{
    private readonly Config _config;
    private readonly LLMService _llmService;
    private readonly WebSocketClientManager _wsManager;
    private readonly ActivityMonitor _activityMonitor;
    private readonly ILinkBridgeService _iLinkBridge;
    private readonly Func<ILinkIncomingMessage, Task> _handleILinkMessage;

    public HttpServerHost(
        Config config,
        LLMService llmService,
        WebSocketClientManager wsManager,
        ActivityMonitor activityMonitor,
        ILinkBridgeService iLinkBridge,
        Func<ILinkIncomingMessage, Task> handleILinkMessage)
    {
        _config = config;
        _llmService = llmService;
        _wsManager = wsManager;
        _activityMonitor = activityMonitor;
        _iLinkBridge = iLinkBridge;
        _handleILinkMessage = handleILinkMessage;
    }

    public async Task StartAsync()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add(_config.Server.ListenUrl);
        listener.Start();

        while (true)
        {
            var ctx = await listener.GetContextAsync();
            _ = Task.Run(async () =>
            {
                try
                {
                    if (ctx.Request.IsWebSocketRequest)
                    {
                        await HandleWebSocketConnection(ctx);
                        return;
                    }

                    ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    ctx.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                    ctx.Response.Headers.Add("Access-Control-Allow-Headers", "*");

                    if (ctx.Request.HttpMethod == "OPTIONS")
                    {
                        ctx.Response.StatusCode = 200;
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/chat" && ctx.Request.HttpMethod == "POST")
                    {
                        await HandleChatRequest(ctx);
                    }
                    else if (_config.ILink.Enabled &&
                             ctx.Request.Url?.AbsolutePath == _config.ILink.WebhookPath &&
                             ctx.Request.HttpMethod == "POST")
                    {
                        await HandleILinkWebhook(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/sticker/") == true && ctx.Request.HttpMethod == "GET")
                    {
                        await HandleStickerRequest(ctx);
                    }
                    else
                    {
                        ctx.Response.StatusCode = 404;
                    }
                }
                catch (Exception ex)
                {
                    ColorLog.Error("HTTP", $"请求处理失败: {ex.Message}");
                    ctx.Response.StatusCode = 500;
                }
                finally { ctx.Response.Close(); }
            });
        }
    }

    private async Task HandleWebSocketConnection(HttpListenerContext ctx)
    {
        var wsCtx = await ctx.AcceptWebSocketAsync(null);
        var ws = wsCtx.WebSocket;
        _wsManager.Add(ws);
        ColorLog.Success("WS", "客户端已连接");

        var buffer = new byte[AppConstants.WebSocketBufferSize];
        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                _wsManager.Remove(ws);
                ColorLog.Info("WS", "客户端已断开");
            }
            else if (result.MessageType == WebSocketMessageType.Text)
            {
                var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var req = JsonSerializer.Deserialize<JsonElement>(msg);

                if (req.TryGetProperty("type", out var typeEl))
                {
                    var type = typeEl.GetString();

                    if (type == "newMessage")
                    {
                        var username = req.GetProperty("username").GetString();
                        var effectiveUsername = username ?? "unknown";
                        var queueLength = req.GetProperty("queueLength").GetInt32();
                        ColorLog.Info("NEW-MSG", $"来自 {username}, 队列: {queueLength}");

                        var prompt = $"系统提示：用户 {username} 发来了新消息，请使用 switch_douyin_chat 函数回复";
                        ColorLog.Info("→AI", prompt);
                        var reply = await _llmService.AskAsync(effectiveUsername.GetHashCode(), prompt, null, null, async (targetUser) =>
                        {
                            var response = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "switchChat", username = targetUser }));
                            await ws.SendAsync(new ArraySegment<byte>(response), WebSocketMessageType.Text, true, CancellationToken.None);
                            ColorLog.Success("SWITCH", $"切换到 {targetUser}");
                        });
                        ColorLog.Success("AI→", reply);
                    }
                    else if (type == "message")
                    {
                        var message = req.GetProperty("message").GetString();
                        var userId = req.GetProperty("userId").GetString();
                        var effectiveUserId = userId ?? "unknown";
                        ColorLog.Info("→AI", $"[@{userId}] {message}");

                        _activityMonitor.UpdateActivity();
                        var reply = await _llmService.AskAsync(effectiveUserId.GetHashCode(), message ?? "");
                        ColorLog.Success("AI→", reply);

                        var response = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "sendMessage", content = reply }));
                        await ws.SendAsync(new ArraySegment<byte>(response), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    else if (type == "aiSwitchChat")
                    {
                        var username = req.GetProperty("username").GetString();
                        var response = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "switchChat", username }));
                        await ws.SendAsync(new ArraySegment<byte>(response), WebSocketMessageType.Text, true, CancellationToken.None);
                        ColorLog.Success("SWITCH", $"切换到 {username}");
                    }
                }
                else if (req.TryGetProperty("message", out var msgEl))
                {
                    var chatReq = JsonSerializer.Deserialize<ChatRequest>(msg);
                    ColorLog.Info("MSG-WS", $"[@{chatReq?.userId ?? ""}] '{chatReq?.message ?? ""}'");

                    _activityMonitor.UpdateActivity();
                    var reply = await _llmService.AskAsync(_config.AllowedUsers[0], chatReq?.message ?? "");

                    var response = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "reply", content = reply }));
                    await ws.SendAsync(new ArraySegment<byte>(response), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }
    }

    private async Task HandleChatRequest(HttpListenerContext ctx)
    {
        using var reader = new StreamReader(ctx.Request.InputStream);
        var body = await reader.ReadToEndAsync();
        var req = JsonSerializer.Deserialize<ChatRequest>(body);

        var reply = await _llmService.AskAsync(_config.AllowedUsers[0], req?.message ?? "");
        var response = Encoding.UTF8.GetBytes(reply);

        ColorLog.Info("MSG-HTTP", $"[@{req?.userId ?? ""}] '{req?.message ?? ""}'");

        ctx.Response.ContentType = "text/plain; charset=utf-8";
        ctx.Response.ContentLength64 = response.Length;
        await ctx.Response.OutputStream.WriteAsync(response);
    }

    private async Task HandleILinkWebhook(HttpListenerContext ctx)
    {
        using var reader = new StreamReader(ctx.Request.InputStream);
        var body = await reader.ReadToEndAsync();

        if (_iLinkBridge.TryParseIncoming(body, out var incoming) && incoming is not null)
        {
            await _handleILinkMessage(incoming);
        }

        ctx.Response.StatusCode = 200;
        var ok = Encoding.UTF8.GetBytes("ok");
        await ctx.Response.OutputStream.WriteAsync(ok);
    }

    private async Task HandleStickerRequest(HttpListenerContext ctx)
    {
        var filename = ctx.Request.Url!.AbsolutePath.Replace("/sticker/", "");
        var path = Path.Combine("stickers", filename);

        if (File.Exists(path))
        {
            var ext = Path.GetExtension(filename).ToLower();
            ctx.Response.ContentType = ext == ".gif" ? "image/gif" : "image/png";
            var data = await File.ReadAllBytesAsync(path);
            await ctx.Response.OutputStream.WriteAsync(data);
        }
        else ctx.Response.StatusCode = 404;
    }
}
