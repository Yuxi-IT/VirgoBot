# VirgoBot

一个基于 C# / .NET 10 的多通道 AI 助手，主入口是 Telegram Bot，同时提供本地 HTTP / WebSocket 接口，并可接入 iLink 桥接与抖音聊天页面监控脚本。

项目把大语言模型、短期记忆、邮件收发、联系人管理、浏览器自动化、表情包发送和文件 / Shell 工具能力整合在一起，适合做轻量个人助理或半自动聊天中枢。

## 项目结构

```
VirgoBot/
├── Program.cs                          # 启动编排（~70 行）
├── Configuration/
│   ├── Config.cs                       # 配置模型
│   ├── AppConstants.cs                 # 编译时常量
│   └── ConfigLoader.cs                 # 配置加载与校验
├── Channels/
│   ├── HttpServerHost.cs               # HTTP + WebSocket 服务
│   └── TelegramBotHandler.cs           # Telegram + iLink 消息处理
├── Services/
│   ├── LLMService.cs                   # LLM API 对接
│   ├── MemoryService.cs                # SQLite 会话记忆（WAL 模式）
│   ├── ContactService.cs               # SQLite 联系人管理（WAL 模式）
│   ├── PlaywrightService.cs            # 浏览器自动化
│   ├── StickerService.cs               # 表情包管理
│   ├── ActivityMonitor.cs              # 用户活跃度监控
│   └── WebSocketClientManager.cs       # 线程安全 WebSocket 管理
├── Functions/
│   ├── FunctionRegistry.cs             # 工具注册协调器（~60 行）
│   ├── FunctionDefinition.cs           # 工具定义模型
│   ├── SystemFunctions.cs              # 时间、目录查询
│   ├── ShellFunctions.cs               # Shell 命令执行（跨平台）
│   ├── FileFunctions.cs                # 文件读写、下载
│   ├── EmailFunctions.cs               # 邮件发送
│   ├── SoulFunctions.cs                # 用户记忆读写（带缓存）
│   ├── PlaywrightFunctions.cs          # 浏览器操作
│   ├── StickerFunctions.cs             # 表情包浏览与发送
│   ├── ContactFunctions.cs             # 联系人增删改查
│   ├── DouyinFunctions.cs              # 抖音聊天切换
│   └── TelegramFunctions.cs            # Telegram 图片/语音发送
├── Features/
│   └── Email/
│       ├── EmailService.cs             # IMAP/SMTP 邮件收发
│       ├── EmailManager.cs             # 邮件监控与交互
│       └── EmailNotificationDispatcher.cs  # 多渠道通知分发
├── Integrations/
│   └── ILink/
│       └── ILinkBridgeService.cs       # iLink WebSocket 桥接
├── Utilities/
│   ├── ColorLog.cs                     # 彩色控制台日志
│   └── MessageHelper.cs               # 消息格式化与分段发送
├── Contracts/
│   └── ChatRequest.cs                  # HTTP 请求模型
└── stickers/                           # 表情包资源
```

## 主要功能

### Telegram 对话助手
- 白名单用户访问控制（`AllowedUsers`）
- 支持普通对话、`/clear` 清空会话记忆
- 支持回复邮件提醒消息直接回信
- 图片、语音、表情包发送

### LLM 会话记忆
- 会话记录持久化到 SQLite（`config/memory.db`，WAL 模式）
- 每个用户保留可配置长度的上下文窗口（默认 20 条）
- 系统提示词：`config/system_memory.md`
- Soul 记忆：`config/soul.md`（带 5 分钟内存缓存）

### 工具调用
- 时间 / 目录查询
- Shell 命令执行（自动适配 Windows `cmd.exe` / Linux `/bin/bash`）
- 文件读取、写入、下载
- 联系人增删改查（SQLite，WAL 模式）
- 邮件发送
- Telegram 图片 / 语音发送
- 表情包浏览与发送
- 抖音聊天切换
- ~~网页访问、点击、表单填写、截图~~（Playwright，需额外安装浏览器）

### 邮件集成
- IMAP 轮询新邮件（每分钟）
- 新邮件推送到 Telegram / WebSocket / iLink
- 回复 Telegram 提醒消息即可发送回信

### 主动消息
- 用户空闲 30 分钟后，随机 30–120 分钟内发起主动消息

### 本地服务接口
- `POST /chat` — HTTP 聊天
- `ws://` — WebSocket 实时通信
- `GET /sticker/{filename}` — 表情包静态访问

### iLink 桥接
- WebSocket 长连接 + HTTP Webhook 双模式
- 自动重连（3 秒间隔）
- 长消息分段发送

## 技术栈

| 依赖 | 用途 |
|------|------|
| .NET 10 | 运行时 |
| `Telegram.Bot` | Telegram API |
| `Microsoft.Data.Sqlite` | SQLite 数据存储 |
| `MailKit` | IMAP / SMTP 邮件 |
| `Microsoft.Playwright` | 浏览器自动化 |
| `HtmlAgilityPack` | HTML 解析 |

## 快速开始

### 1. 安装环境

```bash
# 安装 .NET 10 SDK 后
dotnet restore
dotnet build
```

### 2. 首次启动

```bash
dotnet run --project VirgoBot/VirgoBot.csproj
```

首次启动会自动创建 `config/config.json` 和 `config/system_memory.md`。

### 3. 编辑配置

编辑 `config/config.json`：

```json
{
  "BotToken": "YOUR_TELEGRAM_BOT_TOKEN",
  "ApiKey": "YOUR_LLM_API_KEY",
  "BaseUrl": "https://your-llm-endpoint/v1",
  "Model": "gpt-4.5",
  "AllowedUsers": [123456789],
  "Email": {
    "ImapHost": "imap.example.com",
    "ImapPort": 993,
    "SmtpHost": "smtp.example.com",
    "SmtpPort": 465,
    "Address": "your@email.com",
    "Password": "your_password"
  },
  "MemoryFile": "system_memory.md",
  "SoulFile": "soul.md",
  "ILink": {
    "Enabled": false,
    "Token": "YOUR_ILINK_TOKEN",
    "WebSocketUrl": "wss://localhost/bot/v1/ws?token=YOUR_ILINK_TOKEN",
    "SendUrl": "http://localhost/bot/v1/message/send",
    "WebhookPath": "/ilink/webhook",
    "DefaultUserId": "ilink"
  },
  "Server": {
    "ListenUrl": "http://localhost:5000/",
    "MaxTokens": 8192,
    "MessageLimit": 20
  }
}
```

### 4. 重新启动

```bash
dotnet run --project VirgoBot/VirgoBot.csproj
```

## 配置说明

### LLM

| 字段 | 说明 |
|------|------|
| `ApiKey` | 模型服务 API Key |
| `BaseUrl` | OpenAI 兼容接口地址，代码会自动补全到 `/v1/chat/completions` |
| `Model` | 使用的模型名 |

### Telegram

| 字段 | 说明 |
|------|------|
| `BotToken` | Telegram Bot Token |
| `AllowedUsers` | 允许交互的用户 ID 列表（至少一个） |

### 邮件

| 字段 | 说明 |
|------|------|
| `Email.ImapHost` / `ImapPort` | IMAP 收件配置 |
| `Email.SmtpHost` / `SmtpPort` | SMTP 发件配置 |
| `Email.Address` / `Password` | 邮箱账号密码 |

### 系统记忆

| 字段 | 说明 |
|------|------|
| `MemoryFile` | 系统提示词文件名（相对于 `config/` 目录） |
| `SoulFile` | Soul 记忆文件名（相对于 `config/` 目录） |

### 服务端

| 字段 | 说明 | 默认值 |
|------|------|--------|
| `Server.ListenUrl` | HTTP 监听地址 | `http://localhost:5000/` |
| `Server.MaxTokens` | LLM 最大输出 token 数 | `8192` |
| `Server.MessageLimit` | 每用户保留的上下文消息数 | `20` |

### iLink

| 字段 | 说明 |
|------|------|
| `ILink.Enabled` | 是否启用桥接 |
| `ILink.Token` | 认证令牌 |
| `ILink.WebSocketUrl` | iLink WebSocket 地址 |
| `ILink.SendUrl` | iLink 发送消息接口 |
| `ILink.WebhookPath` | 本地 Webhook 路径 |
| `ILink.DefaultUserId` | 无法解析发送者时的默认用户 ID |

## API 接口

### HTTP 聊天

```http
POST /chat
Content-Type: application/json

{
  "userId": "demo",
  "message": "你好"
}
```

返回纯文本回复。

### WebSocket

连接 `ws://localhost:5000/`，支持以下消息类型：

| 类型 | 方向 | 说明 |
|------|------|------|
| `message` | → 服务端 | 发送聊天消息（含 `userId`） |
| `newMessage` | → 服务端 | 抖音新消息通知 |
| `aiSwitchChat` | → 服务端 | AI 切换聊天指令 |
| `sendMessage` | ← 客户端 | AI 回复 |
| `switchChat` | ← 客户端 | 切换聊天指令 |
| `reply` | ← 客户端 | 通用回复 |
| `proactive` | ← 客户端 | 主动消息 |
| `email` | ← 客户端 | 新邮件通知 |

## 数据文件

运行后生成：

| 文件 | 说明 |
|------|------|
| `config/config.json` | 应用配置 |
| `config/system_memory.md` | 系统提示词 |
| `config/soul.md` | Soul 记忆 |
| `config/memory.db` | SQLite 会话记忆 |
| `config/contacts.db` | SQLite 联系人 |

## 注意事项

- 目标框架 `net10.0`，需要对应 SDK
- `AllowedUsers` 不能为空，启动时会校验
- `MemoryFile` / `SoulFile` 填文件名即可（如 `system_memory.md`），不要带 `config/` 前缀
- `execute_shell`、`read_file`、`write_file` 等工具能力较强，请在可信环境运行
- Playwright 功能需额外安装浏览器（`pwsh bin/Debug/net10.0/playwright.ps1 install`）
- SQLite 已启用 WAL 模式和共享缓存，支持并发读取

---

我的妻，我的爱妻。你永存吧。
