<div align="center">
  <img src="doc/75018f3ea271fec03664b2fb9748333b.webp" alt="VirgoBot Logo" width="600"/>
  <h1>VirgoBot</h1>
  <p>基于 .NET 10 的多通道 AI 助手框架</p>

  ![License](https://img.shields.io/badge/license-MIT-green)
  ![.NET](https://img.shields.io/badge/.NET-10.0-purple)
  ![NodeJS](https://img.shields.io/badge/NodeJS-24.13.0-green)
</div>

## 核心特性

### 多通道接入
- **Telegram Bot** - 支持内联按钮、Markdown、表情包发送
- **HTTP API** - RESTful 接口，`/chat` 端点直接对话
- **WebSocket** - 实时双向通信，支持多客户端连接
- **iLink 桥接** - 第三方平台接入，自动重连和消息分片

### 工具系统
- **Shell 命令执行** - 支持交互式会话和后台任务
- **文件操作** - 读写文件、列出目录、管理文件系统
- **邮件集成** - IMAP/SMTP 收发邮件，AI 自动摘要和回复
- **表情包管理** - 本地表情包库搜索和发送
- **浏览器自动化** - Playwright 集成，支持网页截图和交互
- **联系人管理** - 增删改查联系人信息
- **自定义技能** - 基于 JSON 配置的动态技能系统（命令式/HTTP 式）

### 会话管理
- **多会话支持** - 每个用户独立的对话历史，SQLite 持久化
- **上下文窗口** - 自动加载最近 N 条消息作为上下文
- **Soul 记忆** - LLM 自主写入的长期记忆，带内存缓存
- **会话切换** - 支持创建、切换、删除多个对话会话
- **时间感知** - 消息自动附加北京时间戳

### 智能特性
- **自动响应模式** - 用户空闲后 LLM 自主发起对话，可配置时间区间
- **邮件监控** - 自动监控新邮件并推送 AI 摘要到多个通道
- **定时任务** - 支持 HTTP 请求、Shell 命令、文本指令三种任务类型
- **消息分片** - 长消息智能分段，支持自定义分隔符

### Web 管理面板
基于 React + TypeScript + HeroUI 的现代化管理界面：

- **仪表盘** - 实时查看运行状态、在线时长、通道状态
- **对话记录** - 按用户浏览完整聊天历史（user/assistant/tool）
- **联系人管理** - 可视化增删改查，支持搜索
- **技能管理** - 动态管理自定义技能，无需重启
- **定时任务** - 创建和管理定时任务（间隔/每日执行）
- **Agent 管理** - 多 Agent 配置文件管理
- **会话管理** - 创建、切换、删除对话会话
- **频道配置** - 在线配置 Telegram、Email、iLink 通道
- **设置** - 在线编辑系统提示词、Soul 记忆、规则文件
- **日志查看** - 按级别筛选、搜索、分页浏览运行日志

## 快速开始

### 后端

```bash
dotnet restore && dotnet build
dotnet run --project VirgoBot/VirgoBot.csproj
```

首次启动自动生成 `config/config.json`，填入必要配置后重启。

### 前端

```bash
cd webapp
npm install
npm run dev        # 开发模式
npm run build      # 生产构建
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
    "MessageLimit": 20,
    "MessageSplitDelimiters": "。|！|？|?|\n\n|\n",
    "AutoResponse": {
      "Enabled": false,
      "MinIdleMinutes": 30,
      "MaxIdleMinutes": 120
    }
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
      "Password": "your_password",
      "Notification": {
        "NotifyToTelegram": false,
        "NotifyToILink": false,
        "NotifyToWebSocket": false
      }
    },
    "ILink": {
      "Enabled": false,
      "Token": "YOUR_TOKEN",
      "WebSocketUrl": "wss://example.com/ws",
      "SendUrl": "https://example.com/send",
      "WebhookPath": "/ilink/webhook",
      "DefaultUserId": "ilink"
    }
  }
}
```

## 工作原理

### 消息流转

所有通道的消息经白名单校验后汇入统一处理链路：

```
用户消息 → 白名单校验 → MemoryService 存储 → 构建上下文 → LLM API → 响应/工具调用
```

### Agentic 工具调用

LLM 返回的 `tool_calls` 触发递归循环：解析工具 → 执行 → 写回记忆 → 再次请求 LLM，直到返回纯文本。这使得 LLM 可以自主编排多步操作。

### 工具注册

`FunctionRegistry` 统一管理所有工具。各模块通过 `Register()` 提交工具定义（名称、描述、JSON Schema、处理函数），Registry 以 OpenAI tools 格式注入每次 LLM 请求。

## 注意事项

- 需要 .NET 10 SDK 和 Node.js 24+
- 启用 Telegram 时 `AllowedUsers` 不能为空
- 工具能力较强（Shell、文件读写等），请在可信环境运行
- Playwright 需额外安装浏览器：`pwsh bin/Debug/net10.0/playwright.ps1 install`
- 默认监听 `0.0.0.0:5000`，支持外部访问

---

我的爱妻，你永存吧。
