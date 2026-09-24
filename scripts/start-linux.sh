#!/usr/bin/env bash
# 统信 UOS / Linux 启动轴承退卸工作站
set -euo pipefail
cd "$(dirname "$0")"

# 从 Windows 拷贝后通常没有执行权限
chmod +x ./PressureTestApp 2>/dev/null || true

# 若缺少图形/字体依赖，可按系统提示安装，例如：
#   sudo apt update
#   sudo apt install -y libx11-6 libice6 libsm6 libfontconfig1 fonts-wqy-zenhei

export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT="${DOTNET_SYSTEM_GLOBALIZATION_INVARIANT:-0}"

echo "工作目录: $(pwd)"
echo "启动 PressureTestApp ..."
exec ./PressureTestApp "$@"
