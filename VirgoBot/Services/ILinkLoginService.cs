using ILink4NET.Client;
using ILink4NET.Models;

namespace VirgoBot.Services;

public class ILinkLoginService
{
    private readonly HttpClient _httpClient;
    private ILinkBotClient? _botClient;

    public ILinkLoginService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<LoginQrCodeResponse> CreateQrCodeAsync()
    {
        _botClient = ILinkBotClient.CreateDefault(_httpClient);
        var qrCode = await _botClient.Login.CreateQrCodeAsync();
        return new LoginQrCodeResponse
        {
            QrCode = qrCode.QrCode,
            QrCodeImageUri = qrCode.QrCodeImageUri.ToString()
        };
    }

    public async Task<QrCodeStatusResponse> QueryStatusAsync(string qrCode)
    {
        if (_botClient == null)
        {
            throw new InvalidOperationException("请先调用 CreateQrCodeAsync 创建二维码");
        }

        var result = await _botClient.Login.QueryQrCodeStatusAsync(qrCode);
        return new QrCodeStatusResponse
        {
            Status = result.Status.ToString(),
            Credentials = result.Credentials != null ? new CredentialsDto
            {
                BotToken = result.Credentials.BotToken,
                ILinkBotId = result.Credentials.ILinkBotId,
                ILinkUserId = result.Credentials.ILinkUserId,
                ApiBaseUri = result.Credentials.ApiBaseUri.ToString()
            } : null
        };
    }
}

public class LoginQrCodeResponse
{
    public string QrCode { get; set; } = "";
    public string QrCodeImageUri { get; set; } = "";
}

public class QrCodeStatusResponse
{
    public string Status { get; set; } = "";
    public CredentialsDto? Credentials { get; set; }
}

public class CredentialsDto
{
    public string BotToken { get; set; } = "";
    public string ILinkBotId { get; set; } = "";
    public string ILinkUserId { get; set; } = "";
    public string ApiBaseUri { get; set; } = "";
}
