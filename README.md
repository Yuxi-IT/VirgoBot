<div align="center">
  <img src="doc/75018f3ea271fec03664b2fb9748333b.webp" alt="VirgoBot Logo" width="200"/>
  <h1>VirgoBot</h1>
  <p>基于 .NET 10 的个人 AI 助手</p>

  ![License](https://img.shields.io/badge/license-MIT-green)
  ![.NET](https://img.shields.io/badge/.NET-10.0-purple)
  ![NodeJS](https://img.shields.io/badge/NodeJS-24.13.0-green)
</div>

## 工作原理

### 消息流转

用户消息从多个入口进入系统：Telegram Bot、HTTP `/chat` 接口、WebSocket、iLink 桥接。所有入口经过白名单校验后汇入同一条处理链路：

```
用户消息 → 白名单校验 → MemoryService 存储 → 构建上下文 → LLM API → 响应/工具调用
```

### Agentic 工具调用循环

LLM 返回的响应可能包含 `tool_calls`。此时进入递归循环：

1. 解析工具名称和参数
2. 通过 `FunctionRegistry` 分发到对应处理器执行
3. 将执行结果以 `tool` 角色写回会话记忆
4. 携带工具结果再次请求 LLM
5. 重复直到 LLM 返回纯文本（无工具调用）

这使得 LLM 可以自主编排多步操作——比如先查时间、再读文件、最后发邮件——而无需用户逐步指令。

### 会话记忆

每个用户拥有独立的对话历史，持久化在 SQLite（WAL 模式）中。每次请求加载最近 N 条消息（默认 20）作为上下文窗口，响应完成后裁剪旧记录。用户消息会自动附加当前北京时间，让 LLM 具备时间感知。

系统提示词（`system_memory.md`）定义人格，Soul 记忆（`soul.md`）存储 LLM 自主写入的长期记忆，带 5 分钟内存缓存避免频繁磁盘读取。

### 工具注册

`FunctionRegistry` 是工具系统的核心。各模块（Shell、文件、邮件、表情包等）通过 `Register()` 方法提交工具定义（名称、描述、JSON Schema 参数、异步处理函数），Registry 统一汇总后以 OpenAI tools 格式注入每次 LLM 请求。部分工具（Telegram 发图、邮件）在对应服务就绪后动态注册。

### 邮件集成

`EmailService` 每分钟 IMAP 轮询新邮件。发现新邮件后交给 `EmailManager` 生成 AI 摘要，再由 `EmailNotificationDispatcher` 同时推送到 Telegram（带"忽略"按钮）、WebSocket 和 iLink 三个通道。用户在 Telegram 中回复邮件提醒消息即可直接发送回信。

### 主动消息

`ActivityMonitor` 追踪用户最后活跃时间。空闲超过 30 分钟后，在随机 30–120 分钟内触发一次主动消息——构造一条包含空闲时长的提示发给 LLM，由 LLM 自行决定说什么。

### iLink 桥接

通过 WebSocket 长连接 + HTTP Webhook 双模式接入外部平台。自动重连（3 秒间隔），长消息按段落和标点分片发送并加入 300ms 间隔以应对平台速率限制。

### 多通道输出

LLM 的响应经 `MessageHelper` 按段落和标点智能分段，去除 `<think>` 标签后分别发往各通道。Telegram 支持 Markdown 渲染，WebSocket 发送结构化 JSON，iLink 走分片纯文本。分段之间插入 300ms 延迟以规避速率限制。

### Web 管理面板

后端在 `HttpServerHost` 中暴露一组 RESTful API（47 个端点），前端是一个 React + TypeScript + HeroUI 的单页应用，通过这些接口实现对机器人的可视化管理：

- **仪表盘** — 查看运行状态、在线时长、已连接客户端数、各通道（Telegram / HTTP / WebSocket / 邮件 / iLink）运行情况
- **对话记录** — 按用户浏览完整聊天历史，包括 user / assistant / tool 各角色的消息
- **联系人** — 增删改查，支持搜索
- **技能管理** — 可视化管理自定义技能（命令式/HTTP 式），支持动态增删改查
- **Agent 管理** — 创建、切换、编辑多个 Agent 配置文件
- **会话管理** — 创建、切换、删除不同的对话会话
- **设置** — 在线编辑系统提示词（System Memory）、Soul 记忆、规则文件，修改后即时生效无需重启；查看模型、服务端、邮件、iLink 等配置
- **日志** — 按级别筛选、搜索、分页浏览运行日志，支持一键清空

## 核心功能

### 智能对话
- 支持多轮对话，自动维护上下文
- 时间感知，消息自动附加时间戳
- 支持 Markdown 格式渲染（Telegram）

### 工具调用
- **Shell 命令** - 执行系统命令
- **文件操作** - 读写文件、列出目录
- **邮件收发** - IMAP/SMTP 集成，AI 摘要
- **表情包** - 搜索和发送表情包
- **浏览器自动化** - Playwright 集成
- **联系人管理** - 增删改查联系人
- **自定义技能** - JSON 配置的动态技能系统

### 多通道接入
- **Telegram Bot** - 支持内联按钮、Markdown、表情包
- **HTTP API** - RESTful 接口，`/chat` 端点直接对话
- **WebSocket** - 实时双向通信
- **iLink** - 第三方平台桥接

### 数据管理
- **多会话支持** - 每个用户独立的对话历史
- **Soul 记忆** - LLM 自主写入的长期记忆
- **会话切换** - 支持创建和切换多个对话会话

### 智能特性
- **主动消息** - 空闲后 LLM 自主发起对话
- **邮件监控** - 自动监控新邮件并推送 AI 摘要
- **活跃度追踪** - 记录用户最后活跃时间

## 快速开始

### 后端

```bash
dotnet restore && dotnet build
dotnet run --project VirgoBot/VirgoBot.csproj
```

首次启动自动生成 `config/config.json`，填入必要配置后重启即可。

### 前端

```bash
cd webapp
npm install
npm run dev
```

## 配置示例

```json
{
  "ApiKey": "YOUR_LLM_API_KEY",
  "BaseUrl": "https://api.openai.com/v1",
  "Model": "gpt-4",
  "Server": {
    "ListenUrl": "http://0.0.0.0:5000/",
    "MaxTokens": 8192,
    "MessageLimit": 20
  },
  "Channel": {
    "Telegram": {
      "Enabled": false,
      "BotToken": "YOUR_BOT_TOKEN",
      "AllowedUsers": [123456789]
    },
    "Email": {
      "Enabled": false,
      "ImapHost": "imap.example.com",
      "ImapPort": 993,
      "SmtpHost": "smtp.example.com",
      "SmtpPort": 587,
      "Address": "your@email.com",
      "Password": "your_password"
    }
  }
}
```

## 注意事项

- 需要 .NET 10 SDK
- 启用 `Telegram` 频道时 `AllowedUsers` 不能为空
- 工具能力较强（Shell、文件读写等），请在可信环境运行
- Playwright 需额外安装浏览器：`pwsh bin/Debug/net10.0/playwright.ps1 install`
- 默认监听 `0.0.0.0:5000`，支持外部访问

---

我的爱妻，你永存吧。
