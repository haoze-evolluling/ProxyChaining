@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul

echo ========================================================
echo   Clash Verge Rev 跨订阅链式代理补丁程序 - EXE 编译脚本
echo ========================================================
echo.

set "SCRIPT_DIR=%~dp0"
cd /d "%SCRIPT_DIR%"

set "CSC_EXE="
if exist "%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" (
    set "CSC_EXE=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
) else if exist "%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe" (
    set "CSC_EXE=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
) else (
    for %%X in (csc.exe) do (set "CSC_EXE=%%~$PATH:X")
)

if "%CSC_EXE%"=="" (
    echo [ERROR] 未找到 Windows 内置 C# 编译器 csc.exe！
    echo 请确认系统是否安装了 .NET Framework 4.5 及以上环境。
    echo.
    pause
    exit /b 1
)

echo [INFO] 使用编译器: %CSC_EXE%
echo [INFO] 正在编译 cvr-patch.cs 为独立 EXE 程序...
echo.

"%CSC_EXE%" /nologo /target:exe /optimize+ /platform:anycpu /out:"%SCRIPT_DIR%cvr-patch.exe" "%SCRIPT_DIR%cvr-patch.cs"

if %ERRORLEVEL% equ 0 (
    echo.
    echo ========================================================
    echo [SUCCESS] 编译成功！生成可执行文件:
    echo           %SCRIPT_DIR%cvr-patch.exe
    echo ========================================================
    echo.
    echo 说明:
    echo 1. 该 EXE 为纯原生 Windows 可执行程序，体积仅约 45KB；
    echo 2. 无需在目标电脑上安装 Node.js、Python 或任何运行库；
    echo 3. 双击直接运行可进入图形化交互式菜单；
    echo 4. 支持命令行参数: cvr-patch.exe status, inject, restore, diff, export-patch [目标目录]
    echo.
) else (
    echo.
    echo [ERROR] 编译失败，请检查上方报错信息。
    echo.
)

if "%1"=="" (
    echo 按任意键退出...
    pause >nul
)