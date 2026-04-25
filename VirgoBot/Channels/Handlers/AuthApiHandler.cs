using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using VirgoBot.Configuration;
using VirgoBot.Utilities;

namespace VirgoBot.Channels.Handlers;

public class AuthApiHandler
{
    private readonly Config _config;

    public AuthApiHandler(Config config)
    {
        _config = config;
    }

    public async Task HandleLoginRequest(HttpListenerContext ctx)
    {
        var body = await HttpResponseHelper.ReadRequestBody<JsonElement>(ctx);
        var username = body.GetProperty("username").GetString() ?? "";
        var password = body.GetProperty("password").GetString() ?? "";

        var usernameHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(username)));
        var passwordHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(password)));

        if (usernameHash != _config.Auth.UsernameHash || passwordHash != _config.Auth.PasswordHash)
        {
            await HttpResponseHelper.SendErrorResponse(ctx, 401, "用户名或密码错误");
            return;
        }

        var token = GenerateJwt(_config.Auth.JwtSecret);
        await HttpResponseHelper.SendJsonResponse(ctx, new { success = true, data = new { token } });
        ColorLog.Success("AUTH", $"用户 {username} 登录成功");
    }

    public async Task HandleGetAccessKeysRequest(HttpListenerContext ctx)
    {
        var keys = _config.Auth.AccessKeys.Select(k => new
        {
            k.Id,
            k.Name,
            key = k.Key.Length > 8 ? k.Key[..4] + "****" + k.Key[^4..] : "****",
            k.Note,
            k.Enabled,
            k.CreatedAt
        });
        await HttpResponseHelper.SendJsonResponse(ctx, new { success = true, data = keys });
    }

    public async Task HandleCreateAccessKeyRequest(HttpListenerContext ctx)
    {
        var body = await HttpResponseHelper.ReadRequestBody<JsonElement>(ctx);
        var name = body.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        var note = body.TryGetProperty("note", out var nt) ? nt.GetString() ?? "" : "";

        if (string.IsNullOrWhiteSpace(name))
        {
            await HttpResponseHelper.SendErrorResponse(ctx, 400, "名称不能为空");
            return;
        }

        var key = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        var accessKey = new AccessKeyConfig
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = name,
            Key = key,
            Note = note,
            Enabled = true,
            CreatedAt = DateTime.UtcNow
        };

        _config.Auth.AccessKeys.Add(accessKey);
        ConfigLoader.Save(_config);

        await HttpResponseHelper.SendJsonResponse(ctx, new { success = true, data = new { accessKey.Id, accessKey.Name, key, accessKey.Note, accessKey.Enabled, accessKey.CreatedAt } });
        ColorLog.Success("AUTH", $"AccessKey 已创建: {name}");
    }

    public async Task HandleDeleteAccessKeyRequest(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url?.AbsolutePath ?? "";
        var id = path.Split('/').Last();

        var removed = _config.Auth.AccessKeys.RemoveAll(k => k.Id == id);
        if (removed == 0)
        {
            await HttpResponseHelper.SendErrorResponse(ctx, 404, "AccessKey 不存在");
            return;
        }

        ConfigLoader.Save(_config);
        await HttpResponseHelper.SendJsonResponse(ctx, new { success = true });
        ColorLog.Success("AUTH", $"AccessKey 已删除: {id}");
    }

    public async Task HandleToggleAccessKeyRequest(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url?.AbsolutePath ?? "";
        var segments = path.Split('/');
        var id = segments[^2]; // /api/access-keys/{id}/toggle

        var key = _config.Auth.AccessKeys.FirstOrDefault(k => k.Id == id);
        if (key == null)
        {
            await HttpResponseHelper.SendErrorResponse(ctx, 404, "AccessKey 不存在");
            return;
        }

        key.Enabled = !key.Enabled;
        ConfigLoader.Save(_config);
        await HttpResponseHelper.SendJsonResponse(ctx, new { success = true, data = new { key.Enabled } });
    }

    private static string GenerateJwt(string secret)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "VirgoBot",
            audience: "VirgoBot",
            claims: [new Claim("role", "admin")],
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static bool ValidateJwt(string token, string secret)
    {
        if (string.IsNullOrWhiteSpace(secret)) return true; // no auth configured

        try
        {
            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidIssuer = "VirgoBot",
                ValidAudience = "VirgoBot",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            }, out _);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
