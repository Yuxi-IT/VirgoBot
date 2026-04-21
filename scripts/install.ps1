# VirgoBot 一键安装脚本 (Windows PowerShell)
# Usage: powershell -ExecutionPolicy Bypass -File scripts/install.ps1

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Banner {
    Write-Host ""
    Write-Host "  ╔══════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "  ║          VirgoBot Installer           ║" -ForegroundColor Cyan
    Write-Host "  ║     .NET 10 + Node.js + React         ║" -ForegroundColor Cyan
    Write-Host "  ╚══════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Step {
    param([string]$Message)
    Write-Host "[*] $Message" -ForegroundColor Yellow
}

function Write-Ok {
    param([string]$Message)
    Write-Host "[+] $Message" -ForegroundColor Green
}

function Write-Err {
    param([string]$Message)
    Write-Host "[-] $Message" -ForegroundColor Red
}

function Get-MajorVersion {
    param([string]$VersionString)
    if ($VersionString -match '(\d+)') {
        return [int]$Matches[1]
    }
    return 0
}

# ── Banner ──────────────────────────────────────────────
Write-Banner

$projectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $projectRoot
Write-Step "项目根目录: $projectRoot"

# ── .NET SDK ────────────────────────────────────────────
Write-Step "检测 .NET SDK..."
$dotnetOk = $false
try {
    $dotnetVersion = & dotnet --version 2>$null
    $major = Get-MajorVersion $dotnetVersion
    if ($major -ge 10) {
        Write-Ok ".NET SDK $dotnetVersion 已安装"
        $dotnetOk = $true
    } else {
        Write-Err ".NET SDK 版本 $dotnetVersion 过低 (需要 >= 10)"
    }
} catch {
    Write-Err "未检测到 .NET SDK"
}

if (-not $dotnetOk) {
    Write-Step "尝试通过 winget 安装 .NET SDK 10..."
    try {
        winget install Microsoft.DotNet.SDK.10 --accept-source-agreements --accept-package-agreements
        Write-Ok ".NET SDK 10 安装完成，请重新打开终端后再次运行此脚本"
        exit 0
    } catch {
        Write-Err "winget 安装失败，请手动安装: https://dotnet.microsoft.com/download/dotnet/10.0"
        exit 1
    }
}

# ── Node.js ─────────────────────────────────────────────
Write-Step "检测 Node.js..."
$nodeOk = $false
try {
    $nodeVersion = & node --version 2>$null
    $major = Get-MajorVersion $nodeVersion
    if ($major -ge 18) {
        Write-Ok "Node.js $nodeVersion 已安装"
        $nodeOk = $true
    } else {
        Write-Err "Node.js 版本 $nodeVersion 过低 (需要 >= 18)"
    }
} catch {
    Write-Err "未检测到 Node.js"
}

if (-not $nodeOk) {
    Write-Step "尝试通过 winget 安装 Node.js..."
    try {
        winget install OpenJS.NodeJS --accept-source-agreements --accept-package-agreements
        Write-Ok "Node.js 安装完成，请重新打开终端后再次运行此脚本"
        exit 0
    } catch {
        Write-Err "winget 安装失败，请手动安装: https://nodejs.org/"
        exit 1
    }
}

# ── 后端构建 ────────────────────────────────────────────
Write-Step "还原 NuGet 依赖..."
dotnet restore
Write-Ok "NuGet 依赖还原完成"

Write-Step "构建后端 (Release)..."
dotnet build -c Release
Write-Ok "后端构建完成"

# ── 前端构建 ────────────────────────────────────────────
Write-Step "安装前端依赖..."
Push-Location webapp
npm install
Write-Ok "前端依赖安装完成"

Write-Step "构建前端..."
npm run build
Write-Ok "前端构建完成"
Pop-Location

# ── 配置检查 ────────────────────────────────────────────
$configPath = Join-Path $projectRoot "config\config.json"
if (Test-Path $configPath) {
    Write-Ok "配置文件已存在: $configPath"
} else {
    Write-Host ""
    Write-Host "  配置文件尚未生成。首次运行时会自动创建 config/config.json，" -ForegroundColor Magenta
    Write-Host "  请填入 API Key 等必要配置后重启。" -ForegroundColor Magenta
}

# ── 完成 ────────────────────────────────────────────────
Write-Host ""
Write-Host "  ✅ 安装完成！使用以下命令启动：" -ForegroundColor Green
Write-Host ""
Write-Host "    dotnet run --project VirgoBot/VirgoBot.csproj" -ForegroundColor White
Write-Host ""
