@echo off
chcp 65001 >nul
title 安装 IISMonitor Windows 服务

echo ============================================
echo   IISMonitor 服务安装脚本
echo ============================================
echo.

:: 检查是否以管理员身份运行
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [错误] 请以管理员身份运行此脚本！
    echo 右键点击本文件，选择"以管理员身份运行"。
    pause
    exit /b 1
)

set "SERVICE_NAME=IISMonitorService"
:: 优先使用 Release 产物，回退到 Debug
set "RELEASE_EXE=%~dp0IISMonitor\bin\Release\IISMonitor.exe"
set "DEBUG_EXE=%~dp0IISMonitor\bin\Debug\IISMonitor.exe"
if exist "%RELEASE_EXE%" (
    set "BIN_PATH=%RELEASE_EXE% --service"
    echo [信息] 使用 Release 产物
) else (
    set "BIN_PATH=%DEBUG_EXE% --service"
    echo [警告] 未找到 Release 产物，回退到 Debug
)

echo 正在创建服务 "%SERVICE_NAME%"...
sc create "%SERVICE_NAME%" binPath="%BIN_PATH%" start=auto

if %errorLevel% equ 0 (
    echo 服务创建成功！
) else (
    echo 服务创建失败（错误码: %errorLevel%）
    echo 可能服务已存在，尝试更新配置...
    sc config "%SERVICE_NAME%" binPath="%BIN_PATH%" start=auto
)

echo.
echo 正在设置服务描述...
sc description "%SERVICE_NAME%" "IIS 健康监控与自动重启服务 - 定时检查 IIS 应用程序池和站点状态，并在故障时自动恢复"

echo.
echo ============================================
echo   安装完成！
echo ============================================
echo.
echo 启动服务: sc start %SERVICE_NAME%
echo 停止服务: sc stop %SERVICE_NAME%
echo 卸载服务: sc delete %SERVICE_NAME%
echo.
echo 查看日志: logs\IISMonitor_*.log
echo.

pause
