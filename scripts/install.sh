#!/usr/bin/env bash
# VirgoBot 一键安装脚本 (Linux / macOS)
# Usage: bash scripts/install.sh

set -euo pipefail

# ── 彩色输出 ────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
CYAN='\033[0;36m'; MAGENTA='\033[0;35m'; NC='\033[0m'

banner()  { printf "${CYAN}%s${NC}\n" "$1"; }
step()    { printf "${YELLOW}[*] %s${NC}\n" "$1"; }
ok()      { printf "${GREEN}[+] %s${NC}\n" "$1"; }
err()     { printf "${RED}[-] %s${NC}\n" "$1"; }

banner ""
banner "  ╔══════════════════════════════════════╗"
banner "  ║          VirgoBot Installer           ║"
banner "  ║     .NET 10 + Node.js + React         ║"
banner "  ╚══════════════════════════════════════╝"
echo ""

PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$PROJECT_ROOT"
step "项目根目录: $PROJECT_ROOT"

# ── 辅助：获取主版本号 ──────────────────────────────────
major_version() {
    echo "$1" | grep -oE '[0-9]+' | head -1
}

# ── .NET SDK ────────────────────────────────────────────
step "检测 .NET SDK..."
DOTNET_OK=false
if command -v dotnet &>/dev/null; then
    DOTNET_VER=$(dotnet --version 2>/dev/null || echo "0")
    DOTNET_MAJOR=$(major_version "$DOTNET_VER")
    if [ "$DOTNET_MAJOR" -ge 10 ] 2>/dev/null; then
        ok ".NET SDK $DOTNET_VER 已安装"
        DOTNET_OK=true
    else
        err ".NET SDK 版本 $DOTNET_VER 过低 (需要 >= 10)"
    fi
else
    err "未检测到 .NET SDK"
fi

if [ "$DOTNET_OK" = false ]; then
    step "通过 dotnet-install.sh 安装 .NET SDK 10..."
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
    chmod +x /tmp/dotnet-install.sh
    /tmp/dotnet-install.sh --channel 10.0
    export DOTNET_ROOT="$HOME/.dotnet"
    export PATH="$DOTNET_ROOT:$PATH"
    ok ".NET SDK 已安装到 $DOTNET_ROOT"
    echo ""
    printf "${MAGENTA}  请将以下内容添加到 ~/.bashrc 或 ~/.zshrc:${NC}\n"
    printf "${MAGENTA}    export DOTNET_ROOT=\"\$HOME/.dotnet\"${NC}\n"
    printf "${MAGENTA}    export PATH=\"\$DOTNET_ROOT:\$PATH\"${NC}\n"
    echo ""
fi

# ── Node.js ─────────────────────────────────────────────
step "检测 Node.js..."
NODE_OK=false
if command -v node &>/dev/null; then
    NODE_VER=$(node --version 2>/dev/null || echo "v0")
    NODE_MAJOR=$(major_version "$NODE_VER")
    if [ "$NODE_MAJOR" -ge 18 ] 2>/dev/null; then
        ok "Node.js $NODE_VER 已安装"
        NODE_OK=true
    else
        err "Node.js 版本 $NODE_VER 过低 (需要 >= 18)"
    fi
else
    err "未检测到 Node.js"
fi

if [ "$NODE_OK" = false ]; then
    step "尝试安装 Node.js..."
    if command -v apt-get &>/dev/null; then
        step "检测到 apt，通过 NodeSource 安装..."
        curl -fsSL https://deb.nodesource.com/setup_lts.x | sudo -E bash -
        sudo apt-get install -y nodejs
    elif command -v dnf &>/dev/null; then
        step "检测到 dnf，通过 NodeSource 安装..."
        curl -fsSL https://rpm.nodesource.com/setup_lts.x | sudo bash -
        sudo dnf install -y nodejs
    elif command -v pacman &>/dev/null; then
        step "检测到 pacman..."
        sudo pacman -Sy --noconfirm nodejs npm
    else
        err "未识别的包管理器，请手动安装 Node.js >= 18"
        printf "${MAGENTA}  推荐使用 nvm: https://github.com/nvm-sh/nvm${NC}\n"
        exit 1
    fi
    ok "Node.js 安装完成"
fi

# ── 后端构建 ────────────────────────────────────────────
step "还原 NuGet 依赖..."
dotnet restore
ok "NuGet 依赖还原完成"

step "构建后端 (Release)..."
dotnet build -c Release
ok "后端构建完成"

# ── 前端构建 ────────────────────────────────────────────
step "安装前端依赖..."
cd webapp
npm install
ok "前端依赖安装完成"

step "构建前端..."
npm run build
ok "前端构建完成"
cd "$PROJECT_ROOT"

# ── 配置检查 ────────────────────────────────────────────
CONFIG_PATH="$PROJECT_ROOT/config/config.json"
if [ -f "$CONFIG_PATH" ]; then
    ok "配置文件已存在: $CONFIG_PATH"
else
    echo ""
    printf "${MAGENTA}  配置文件尚未生成。首次运行时会自动创建 config/config.json，${NC}\n"
    printf "${MAGENTA}  请填入 API Key 等必要配置后重启。${NC}\n"
fi

# ── 完成 ────────────────────────────────────────────────
echo ""
printf "${GREEN}  ✅ 安装完成！使用以下命令启动：${NC}\n"
echo ""
echo "    dotnet run --project VirgoBot/VirgoBot.csproj"
echo ""

# 确保脚本自身可执行
chmod +x "$0"
