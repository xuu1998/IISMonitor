@echo off
chcp 65001 >nul
title 卸载 IISMonitor Windows 服务

echo ============================================
echo   IISMonitor 服务卸载脚本
echo ============================================
echo.

:: 检查是否以管理员身份运行
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [错误] 请以管理员身份运行此脚本！
    pause
    exit /b 1
)

set "SERVICE_NAME=IISMonitorService"

echo 正在停止服务 "%SERVICE_NAME%"...
sc stop "%SERVICE_NAME%" >nul 2>&1

echo 正在删除服务 "%SERVICE_NAME%"...
sc delete "%SERVICE_NAME%"

if %errorLevel% equ 0 (
    echo 服务卸载成功！
) else (
    echo 服务卸载失败（错误码: %errorLevel%）
)

pause
