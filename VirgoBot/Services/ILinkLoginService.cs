using OpenILink.SDK;

namespace VirgoBot.Services;

public class ILinkLoginService : IDisposable
{
    private OpenILinkClient? _client;
    private string? _qrUrl;
    private string _status = "";
    private string? _token;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<LoginStartResponse> StartLoginAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _qrUrl = null;
            _status = "waiting";
            _token = null;

            _client = OpenILinkClient.Create("");

            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await _client.LoginWithQrAsync(
                        onQrCode: url =>
                        {
                            _qrUrl = url;
                            _status = "waiting";
                        },
                        onScanned: () =>
                        {
                            _status = "scanned";
                        });

                    _token = result.BotToken;
                    _status = "confirmed";
                }
                catch (TimeoutException)
                {
                    _status = "expired";
                }
                catch (Exception)
                {
                    _status = "expired";
                }
            });

            var timeout = DateTime.UtcNow.AddSeconds(10);
            while (_qrUrl == null && DateTime.UtcNow < timeout)
            {
                await Task.Delay(100);
            }

            return new LoginStartResponse
            {
                QrCodeUrl = _qrUrl ?? "",
                Status = _status
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    public LoginStatusResponse GetStatus()
    {
        return new LoginStatusResponse
        {
            Status = _status,
            Token = _status == "confirmed" ? _token : null
        };
    }

    public void Dispose()
    {
        _client?.Dispose();
        _lock.Dispose();
    }
}

public class LoginStartResponse
{
    public string QrCodeUrl { get; set; } = "";
    public string Status { get; set; } = "";
}

public class LoginStatusResponse
{
    public string Status { get; set; } = "";
    public string? Token { get; set; }
}
