# VirgoBot

一个基于 C# / .NET 的多通道 AI 助手项目，主入口是 Telegram Bot，同时提供本地 HTTP / WebSocket 接口，并可选接入 iLink 桥接与抖音聊天页面监控脚本。

项目把大语言模型、短期记忆、邮件收发、联系人管理、浏览器自动化、表情包发送和简单文件 / Shell 工具能力整合在一起，适合做个人助理或半自动聊天中枢。

## 项目定位

VirgoBot 的核心思路是：

- 用 Telegram 作为主要控制台
- 用 SQLite 保存会话记忆和联系人
- 用工具调用扩展模型能力
- 用本地 HTTP / WebSocket 对接外部页面或脚本
- 用 iLink 作为额外消息桥接通道

## 主要功能

- Telegram 对话助手
  - 只允许 `config.json` 中白名单用户访问
  - 支持普通对话
  - 支持 `/clear` 清空当前用户的会话记忆
- LLM 会话记忆
  - 会话记录保存到 `config/memory.db`
  - 每个用户保留最近一段上下文，避免无限增长
  - 系统提示词来自 `config/system_memory.md`
- 工具调用能力
  - 时间查询
  - 工作目录查询
  - 特殊目录查询
  - Shell 命令执行
  - 文件读取 / 写入
  - 文件下载
  - 网页访问、点击、表单填写、截图
  - 联系人增删改查
  - 邮件发送
  - Telegram 图片 / 语音发送
  - 表情包浏览与发送
  - 抖音聊天切换指令
- 邮件集成
  - 通过 IMAP 轮询新邮件
  - 通过 SMTP 发送邮件
  - 新邮件会推送到 Telegram，也可推送给 WebSocket 客户端
  - 可直接回复邮件提醒消息完成回信
- 主动消息提醒
  - 用户长时间无活动时，机器人可主动发起提醒
- 本地服务接口
  - `POST http://localhost:5000/chat`
  - `ws://localhost:5000/`
  - `GET /sticker/{filename}` 提供表情包静态访问
- iLink 桥接
  - 支持 webhook 接收
  - 支持 WebSocket 接收
  - 支持向 iLink 端分段发送长消息
- 抖音聊天监控脚本
  - 根目录 `chat-monitor.js`
  - 通过浏览器页面脚本把消息转发到本地 WebSocket

## 技术栈

- .NET 10 控制台应用
- `Telegram.Bot`
- `Microsoft.Data.Sqlite`
- `MailKit`
- `Microsoft.Playwright`
- `HtmlAgilityPack`
- `Newtonsoft.Json`

## 项目结构

```text
VirgoBot/
├─ VirgoBot.slnx
├─ chat-monitor.js                 # 抖音聊天页面监控脚本
├─ README.md
└─ VirgoBot/
   ├─ Program.cs                  # 程序入口，负责组装所有服务
   ├─ VirgoBot.csproj
   ├─ Helpers/
   │  ├─ Config.cs                # 配置模型
   │  ├─ LLMService.cs            # LLM 请求、消息拼装、工具调用循环
   │  ├─ MemoryService.cs         # SQLite 会话记忆
   │  ├─ FunctionRegistry.cs      # 工具注册中心
   │  ├─ EmailService.cs          # 邮件收发
   │  ├─ EmailManager.cs          # 邮件提醒与回复处理
   │  ├─ PlaywrightService.cs     # 浏览器自动化
   │  ├─ StickerService.cs        # 表情包索引与匹配
   │  ├─ ContactService.cs        # SQLite 联系人管理
   │  ├─ ActivityMonitor.cs       # 空闲后主动消息
   │  ├─ MessageHelper.cs         # 长消息分段发送、think 标签处理
   │  └─ ILinkBridgeService.cs    # iLink 消息桥接
   ├─ InlineKeyboards/
   └─ stickers/                   # 表情包素材与 stickers.json 索引
```

## 运行前准备

### 1. 安装环境

- 安装 .NET 10 SDK
- 安装 Playwright 依赖浏览器

```powershell
dotnet build
pwsh bin/Debug/net10.0/playwright.ps1 install
```

如果你是首次还原依赖，也可以直接：

```powershell
dotnet restore
dotnet build
```

### 2. 首次启动生成配置

程序首次启动会自动创建：

- `config/config.json`
- `config/system_memory.md`

启动命令：

```powershell
dotnet run --project .\VirgoBot\VirgoBot.csproj
```

### 3. 编辑配置文件

把生成后的 `config/config.json` 按实际环境填写，例如：

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
  "ILink": {
    "Enabled": false,
    "Token": "YOUR_ILINK_TOKEN",
    "WebSocketUrl": "wss://localhost/bot/v1/ws?token=YOUR_ILINK_TOKEN",
    "SendUrl": "http://localhost/bot/v1/message/send",
    "WebhookPath": "/ilink/webhook",
    "DefaultUserId": "ilink"
  }
}
```

## 配置说明

### LLM

- `ApiKey`: 模型服务 API Key
- `BaseUrl`: OpenAI 兼容接口地址，代码会自动补全到 `/v1/chat/completions`
- `Model`: 使用的模型名

### Telegram

- `BotToken`: Telegram Bot Token
- `AllowedUsers`: 允许与机器人交互的 Telegram 用户 ID 列表

注意：当前入口代码中 `EmailManager` 和 `ActivityMonitor` 默认直接读取 `AllowedUsers[0]`，因此这个数组至少要有一个用户 ID。

### 邮件

- `Email.ImapHost` / `Email.ImapPort`: 收件配置
- `Email.SmtpHost` / `Email.SmtpPort`: 发件配置
- `Email.Address` / `Email.Password`: 邮箱账号

### 系统记忆

- `MemoryFile`: 系统提示词文件名
- 推荐填相对路径 `system_memory.md`

说明：当前 `Program.cs` 会从 `config` 目录再拼一次路径，所以如果这里写成 `config/system_memory.md`，可能导致路径重复。

### iLink

- `Enabled`: 是否启用桥接
- `Token`: 认证令牌
- `WebSocketUrl`: iLink WebSocket 地址
- `SendUrl`: iLink 发送消息接口
- `WebhookPath`: 本地接收 webhook 的路径
- `DefaultUserId`: 无法解析发送者时使用的默认用户标识

## 本地接口

### HTTP 聊天接口

```http
POST /chat
Content-Type: application/json

{
  "userId": "demo",
  "message": "你好"
}
```

返回值为纯文本回复。

### WebSocket

地址：

```text
ws://localhost:5000/
```

当前代码里主要处理这些消息类型：

- `message`
- `newMessage`
- `aiSwitchChat`
- 简化版 `message/userId`

这部分主要被 `chat-monitor.js` 使用，用来桥接网页聊天界面。

## 数据文件

运行后通常会生成这些数据：

- `config/config.json`
- `config/system_memory.md`
- `config/memory.db`
- `config/contacts.db`

## 典型工作流

### Telegram 助手

1. 用户向 Telegram Bot 发消息
2. `Program.cs` 调用 `LLMService`
3. `LLMService` 读取记忆、拼接 system prompt、发起模型请求
4. 若模型返回工具调用，则由 `FunctionRegistry` 执行工具
5. 工具结果回灌给模型，得到最终答案
6. 结果分段发送回 Telegram

### 邮件提醒

1. `EmailManager` 每分钟轮询新邮件
2. 新邮件摘要交给 LLM 生成提醒文案
3. 通知发送到 Telegram / WebSocket 客户端
4. 用户回复提醒消息即可发送回信

### 抖音聊天桥接

1. 浏览器注入 `chat-monitor.js`
2. 脚本通过 `ws://localhost:5000/` 与本地服务通信
3. 页面新消息转给 VirgoBot
4. VirgoBot 调用模型生成回复
5. 脚本把回复自动填回聊天输入框

## 已知注意事项

- 项目目标框架是 `net10.0`，需要本机有对应 SDK。
- Playwright 只引用包还不够，首次运行前通常还要安装浏览器。
- `AllowedUsers` 不能为空，否则部分功能会在启动时出错。
- `MemoryFile` 建议填 `system_memory.md`，不要重复带 `config/` 前缀。
- 本地 HTTP 服务固定监听 `http://localhost:5000/`。
- `execute_shell`、`read_file`、`write_file` 等工具能力较强，实际使用时建议只在可信环境运行。

## 当前状态总结

这是一个已经具备可运行主干的个人 AI 助手项目，特点不是单一聊天，而是把：

- Telegram 对话
- 本地网页 / WebSocket 桥接
- 邮件提醒与回信
- 联系人管理
- 浏览器自动化
- 表情包能力
- 可扩展工具调用

整合到了一个进程里。

如果后面继续演进，这个项目比较适合往两个方向发展：

- 作为个人多渠道消息助理继续增强稳定性和权限控制
- 拆分为更清晰的“聊天入口层 + 工具层 + 集成层”
