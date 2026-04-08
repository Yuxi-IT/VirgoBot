using System.Diagnostics;
using System.Text;
using System.Text.Json;
using HtmlAgilityPack;
using Telegram.Bot;
using Telegram.Bot.Types;
using VirgoBot.Features.Email;

namespace VirgoBot.Helpers;

public class FunctionRegistry
{
    private readonly Dictionary<string, Func<JsonElement, Task<string>>> _functions = new();
    private readonly List<object> _toolSchemas = new();
    private EmailService? _emailService;
    private PlaywrightService? _playwrightService;
    private StickerService? _stickerService;
    private ContactService? _contactService;
    private TelegramBotClient? _bot;
    private long _chatId;

    public FunctionRegistry()
    {
        RegisterDefaultFunctions();
        RegisterDouyinFunctions();
    }

    public void SetEmailService(EmailService emailService)
    {
        _emailService = emailService;
        RegisterEmailFunctions();
    }

    public void SetPlaywrightService(PlaywrightService playwrightService)
    {
        _playwrightService = playwrightService;
        RegisterPlaywrightFunctions();
    }

    public void SetStickerService(StickerService stickerService)
    {
        _stickerService = stickerService;
        RegisterStickerFunctions();
    }

    public void SetContactService(ContactService contactService)
    {
        _contactService = contactService;
        RegisterContactFunctions();
    }

    public void SetTelegramBot(TelegramBotClient bot, long chatId)
    {
        _bot = bot;
        _chatId = chatId;
        if (_bot != null && !_functions.ContainsKey("send_photo"))
        {
            RegisterTelegramFunctions();
        }
    }

    private void RegisterDefaultFunctions()
    {
        var workspace = Path.Combine(Environment.CurrentDirectory, "workspace");
        if(!Directory.Exists(workspace)) Directory.CreateDirectory(workspace);

        Register("get_time", "获取当前服务器时间", new { type = "object", properties = new { } }, _ =>
            Task.FromResult(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));

        Register("get_workspace", "获取工作目录路径", new { type = "object", properties = new { } }, _ =>
            Task.FromResult(workspace));

        Register("get_specialfolder", "获取特殊目录路径(桌面,文档,音乐等)",
        new { type = "object", properties = new { } }, async _ =>
        {
            var folders = new[]
            {
                Environment.SpecialFolder.Desktop,
                Environment.SpecialFolder.MyDocuments,
                Environment.SpecialFolder.MyMusic,
                Environment.SpecialFolder.MyPictures,
                Environment.SpecialFolder.MyVideos,
                Environment.SpecialFolder.ApplicationData,
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolder.ProgramFiles,
            };

            var sb = new StringBuilder();

            foreach (var folder in folders)
            {
                string path = Environment.GetFolderPath(folder);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    sb.AppendLine(path);
                }
            }

            return sb.ToString();
        });

        Register("execute_shell", "执行Powershell命令并返回结果", new
        {
            type = "object",
            properties = new
            {
                command = new { type = "string", description = "要执行的命令" }
            },
            required = new[] { "command" }
        }, input =>
        {
            var cmd = input.GetProperty("command").GetString() ?? "";
            return Task.FromResult(ExecuteShell(cmd));
        });

        Register("read_file", "读取文件内容", new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "文件路径" }
            },
            required = new[] { "path" }
        }, input =>
        {
            var path = input.GetProperty("path").GetString() ?? "";
            return Task.FromResult(File.Exists(path) ? File.ReadAllText(path) : "文件不存在");
        });

        Register("write_file", "写入文件内容", new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "文件路径" },
                content = new { type = "string", description = "文件内容" }
            },
            required = new[] { "path", "content" }
        }, input =>
        {
            var path = input.GetProperty("path").GetString() ?? "";
            var content = input.GetProperty("content").GetString() ?? "";
            File.WriteAllText(path, content);
            return Task.FromResult("写入成功");
        });

        Register("download_file", "从URL下载文件到指定位置", new
        {
            type = "object",
            properties = new
            {
                url = new { type = "string", description = "文件URL" },
                save_path = new { type = "string", description = "保存路径" }
            },
            required = new[] { "url", "save_path" }
        }, async input =>
        {
            var url = input.GetProperty("url").GetString() ?? "";
            var savePath = input.GetProperty("save_path").GetString() ?? "";

            using var client = new HttpClient();
            var data = await client.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(savePath, data);
            return $"文件已下载到: {savePath} ({data.Length} 字节)";
        });

    }

    private void RegisterEmailFunctions()
    {
        Register("send_email", "发送邮件", new
        {
            type = "object",
            properties = new
            {
                to = new { type = "string", description = "收件人邮箱地址" },
                subject = new { type = "string", description = "邮件主题" },
                body = new { type = "string", description = "邮件正文内容" }
            },
            required = new[] { "to", "subject", "body" }
        }, async input =>
        {
            var to = input.GetProperty("to").GetString() ?? "";
            var subject = input.GetProperty("subject").GetString() ?? "";
            var body = input.GetProperty("body").GetString() ?? "";
            await _emailService!.SendEmailAsync(to, subject, body);
            return "邮件发送成功";
        });
    }

    private void RegisterPlaywrightFunctions()
    {
        Register("playwright_navigate", "使用浏览器访问网页并获取完整HTML内容", new
        {
            type = "object",
            properties = new
            {
                url = new { type = "string", description = "要访问的网址" }
            },
            required = new[] { "url" }
        }, async input =>
        {
            var url = input.GetProperty("url").GetString() ?? "";
            return await _playwrightService!.NavigateAndGetContentAsync(url);
        });

        Register("playwright_click", "点击网页元素并获取结果", new
        {
            type = "object",
            properties = new
            {
                url = new { type = "string", description = "网址" },
                selector = new { type = "string", description = "CSS选择器" }
            },
            required = new[] { "url", "selector" }
        }, async input =>
        {
            var url = input.GetProperty("url").GetString() ?? "";
            var selector = input.GetProperty("selector").GetString() ?? "";
            return await _playwrightService!.ClickAndGetContentAsync(url, selector);
        });

        Register("playwright_fill_form", "填写表单并提交", new
        {
            type = "object",
            properties = new
            {
                url = new { type = "string", description = "网址" },
                fields = new { type = "object", description = "字段映射(选择器:值)" },
                submit_selector = new { type = "string", description = "提交按钮选择器" }
            },
            required = new[] { "url", "fields", "submit_selector" }
        }, async input => 
        {
            var url = input.GetProperty("url").GetString() ?? "";
            var submitSelector = input.GetProperty("submit_selector").GetString() ?? "";
            var fields = new Dictionary<string, string>();
            foreach (var prop in input.GetProperty("fields").EnumerateObject())
                fields[prop.Name] = prop.Value.GetString() ?? "";
            return await _playwrightService!.FillFormAsync(url, fields, submitSelector);
        });

        Register("playwright_screenshot", "截取网页截图", new
        {
            type = "object",
            properties = new
            {
                url = new { type = "string", description = "网址" },
                save_path = new { type = "string", description = "保存路径" }
            },
            required = new[] { "url", "save_path" }
        }, async input =>
        {
            var url = input.GetProperty("url").GetString() ?? "";
            var savePath = input.GetProperty("save_path").GetString() ?? "";
            var screenshot = await _playwrightService!.ScreenshotAsync(url);
            await File.WriteAllBytesAsync(savePath, screenshot);
            return $"截图已保存到: {savePath}";
        });
    }

    private void RegisterStickerFunctions()
    {
        Register("list_stickers", "浏览表情包列表", new
        {
            type = "object",
            properties = new
            {
                page = new { type = "number", description = "页码(1-5)" }
            },
            required = new[] { "page" }
        }, async input =>
        {
            var page = input.GetProperty("page").GetInt32();
            return _stickerService!.GetStickerList(page);
        });

        Register("send_sticker", "发送选中的表情包", new
        {
            type = "object",
            properties = new
            {
                filename = new { type = "string", description = "表情包文件名" }
            },
            required = new[] { "filename" }
        }, async input =>
        {
            var filename = input.GetProperty("filename").GetString() ?? "";
            var sticker = _stickerService!.GetStickerByFilename(filename);
            return sticker ?? "no_match";
        });
    }

    private void RegisterContactFunctions()
    {
        Register("add_contact", "添加联系人", new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string", description = "姓名" },
                email = new { type = "string", description = "邮箱(可选)" },
                phone = new { type = "string", description = "电话(可选)" },
                notes = new { type = "string", description = "备注(可选)" }
            },
            required = new[] { "name" }
        }, async input =>
        {
            var name = input.GetProperty("name").GetString() ?? "";
            var email = input.TryGetProperty("email", out var e) ? e.GetString() : null;
            var phone = input.TryGetProperty("phone", out var p) ? p.GetString() : null;
            var notes = input.TryGetProperty("notes", out var n) ? n.GetString() : null;
            _contactService!.AddContact(name, email, phone, notes);
            return "联系人添加成功";
        });

        Register("list_contacts", "查看所有联系人", new
        {
            type = "object",
            properties = new { }
        }, async input =>
        {
            var contacts = _contactService!.GetAllContacts();
            if (contacts.Count == 0) return "通讯录为空";
            var sb = new StringBuilder();
            foreach (var c in contacts)
            {
                sb.AppendLine($"[{c.Id}] {c.Name}");
                if (!string.IsNullOrEmpty(c.Email)) sb.AppendLine($"  邮箱: {c.Email}");
                if (!string.IsNullOrEmpty(c.Phone)) sb.AppendLine($"  电话: {c.Phone}");
                if (!string.IsNullOrEmpty(c.Notes)) sb.AppendLine($"  备注: {c.Notes}");
            }
            return sb.ToString();
        });

        Register("find_contact", "搜索联系人", new
        {
            type = "object",
            properties = new
            {
                keyword = new { type = "string", description = "搜索关键词" }
            },
            required = new[] { "keyword" }
        }, async input =>
        {
            var keyword = input.GetProperty("keyword").GetString() ?? "";
            var contact = _contactService!.FindContact(keyword);
            if (contact == null) return "未找到联系人";
            var sb = new StringBuilder();
            sb.AppendLine($"[{contact.Id}] {contact.Name}");
            if (!string.IsNullOrEmpty(contact.Email)) sb.AppendLine($"邮箱: {contact.Email}");
            if (!string.IsNullOrEmpty(contact.Phone)) sb.AppendLine($"电话: {contact.Phone}");
            if (!string.IsNullOrEmpty(contact.Notes)) sb.AppendLine($"备注: {contact.Notes}");
            return sb.ToString();
        });

        Register("update_contact", "修改联系人信息", new
        {
            type = "object",
            properties = new
            {
                id = new { type = "number", description = "联系人ID" },
                name = new { type = "string", description = "新姓名(可选)" },
                email = new { type = "string", description = "新邮箱(可选)" },
                phone = new { type = "string", description = "新电话(可选)" },
                notes = new { type = "string", description = "新备注(可选)" }
            },
            required = new[] { "id" }
        }, async input =>
        {
            var id = input.GetProperty("id").GetInt32();
            var name = input.TryGetProperty("name", out var n) ? n.GetString() : null;
            var email = input.TryGetProperty("email", out var e) ? e.GetString() : null;
            var phone = input.TryGetProperty("phone", out var p) ? p.GetString() : null;
            var notes = input.TryGetProperty("notes", out var nt) ? nt.GetString() : null;
            _contactService!.UpdateContact(id, name, email, phone, notes);
            return "联系人更新成功";
        });

        Register("delete_contact", "删除联系人", new
        {
            type = "object",
            properties = new
            {
                id = new { type = "number", description = "联系人ID" }
            },
            required = new[] { "id" }
        }, async input =>
        {
            var id = input.GetProperty("id").GetInt32();
            _contactService!.DeleteContact(id);
            return "联系人删除成功";
        });
    }

    private void RegisterDouyinFunctions()
    {
        Register("switch_douyin_chat", "切换抖音聊天到指定用户", new
        {
            type = "object",
            properties = new
            {
                username = new { type = "string", description = "要切换到的用户名" }
            },
            required = new[] { "username" }
        }, async input =>
        {
            var username = input.GetProperty("username").GetString() ?? "";
            // 通过WebSocket通知客户端切换聊天
            return $"switch_chat:{username}";
        });
    }

    private void RegisterTelegramFunctions()
    {
        Register("send_photo", "发送图片到Telegram", new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "图片路径(本地路径或URL)" },
                caption = new { type = "string", description = "图片说明(可选)" }
            },
            required = new[] { "path" }
        }, async input =>
        {
            var path = input.GetProperty("path").GetString() ?? "";
            var caption = input.TryGetProperty("caption", out var c) ? c.GetString() : null;

            if (path.StartsWith("http://") || path.StartsWith("https://"))
            {
                await _bot!.SendPhoto(_chatId, InputFile.FromUri(path), caption: caption);
            }
            else
            {
                if (!System.IO.File.Exists(path)) return "文件不存在";
                using var stream = System.IO.File.OpenRead(path);
                await _bot!.SendPhoto(_chatId, InputFile.FromStream(stream, Path.GetFileName(path)), caption: caption);
            }
            return "图片已发送";
        });

        Register("send_voice", "发送语音到Telegram", new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "语音文件路径(本地路径或URL,支持.ogg/.mp3)" },
                caption = new { type = "string", description = "语音说明(可选)" }
            },
            required = new[] { "path" }
        }, async input =>
        {
            var path = input.GetProperty("path").GetString() ?? "";
            var caption = input.TryGetProperty("caption", out var c) ? c.GetString() : null;

            if (path.StartsWith("http://") || path.StartsWith("https://"))
            {
                await _bot!.SendVoice(_chatId, InputFile.FromUri(path), caption: caption);
            }
            else
            {
                if (!System.IO.File.Exists(path)) return "文件不存在";
                using var stream = System.IO.File.OpenRead(path);
                await _bot!.SendVoice(_chatId, InputFile.FromStream(stream, Path.GetFileName(path)), caption: caption);
            }
            return "语音已发送";
        });
    }

    public void Register(string name, string description, object inputSchema, Func<JsonElement, Task<string>> handler)
    {
        _functions[name] = handler;
        _toolSchemas.Add(new { name, description, input_schema = inputSchema });
    }

    public async Task<string> ExecuteAsync(string name, JsonElement input)
    {
        return _functions.ContainsKey(name) ? await _functions[name](input) : "unknown tool";
    }

    public object[] GetToolSchemas() => _toolSchemas.ToArray();

    private string ExecuteShell(string command)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return string.IsNullOrEmpty(error) ? output : $"{output}\n错误: {error}";
        }
        catch (Exception ex)
        {
            return $"执行失败: {ex.Message}";
        }
    }
}
