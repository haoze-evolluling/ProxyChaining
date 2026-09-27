<#
.SYNOPSIS
    编译 Clash Verge Rev 跨订阅链式代理补丁程序为独立 EXE 可执行文件
.DESCRIPTION
    利用 Windows 系统自带的 .NET Framework C# 编译器 (csc.exe)，将补丁工具编译为体积仅约 45KB 的纯原生 EXE。
    无需安装 Node.js、Python 或任何额外开发工具，生成的 EXE 可直接拷贝到任意无环境的电脑上双击或在命令行中运行。
.EXAMPLE
    .\build.ps1
#>

[CmdletBinding()]
param(
    [switch]$NoPause
)

$scriptDir = $PSScriptRoot
Set-Location $scriptDir

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  Clash Verge Rev 跨订阅链式代理补丁程序 - EXE 编译脚本" -ForegroundColor Cyan
Write-Host "========================================================`n" -ForegroundColor Cyan

$cscPath = $null
$candidates = @(
    "$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:SystemRoot\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

foreach ($path in $candidates) {
    if (Test-Path $path) {
        $cscPath = $path
        break
    }
}

if (-not $cscPath) {
    $cmd = Get-Command csc.exe -ErrorAction SilentlyContinue
    if ($cmd) {
        $cscPath = $cmd.Source
    }
}

if (-not $cscPath) {
    Write-Host "[ERROR] 未检测到 Windows 内置 C# 编译器 csc.exe！" -ForegroundColor Red
    Write-Host "请确认系统是否具有 .NET Framework 4.5+ (Windows 10/11 默认自带)。" -ForegroundColor Yellow
    exit 1
}

Write-Host "[INFO] 编译器路径: $cscPath" -ForegroundColor DarkGray
Write-Host "[INFO] 正在编译 C# 源码 -> cvr-patch.exe ..." -ForegroundColor Green

$outFile = Join-Path $scriptDir "cvr-patch.exe"
$sourcePattern = Join-Path $scriptDir "*.cs"

$process = Start-Process -FilePath $cscPath -ArgumentList "/nologo /target:exe /optimize+ /platform:anycpu /out:`"$outFile`" `"$sourcePattern`"" -NoNewWindow -Wait -PassThru

if ($process.ExitCode -eq 0) {
    $exeItem = Get-Item $outFile
    $sizeKb = [Math]::Round($exeItem.Length / 1024, 2)
    Write-Host "`n========================================================" -ForegroundColor Green
    Write-Host "[SUCCESS] 独立 EXE 编译成功！" -ForegroundColor Green
    Write-Host "文件路径: $outFile" -ForegroundColor White
    Write-Host "程序体积: $sizeKb KB ($($exeItem.Length) 字节)" -ForegroundColor White
    Write-Host "========================================================`n" -ForegroundColor Green
    Write-Host "特性说明:" -ForegroundColor Cyan
    Write-Host "  1. 零环境依赖: 无论目标电脑有无 Node.js/环境，均可直接运行" -ForegroundColor Gray
    Write-Host "  2. 双击图形菜单: 无参数直接双击将启动交互式操作菜单" -ForegroundColor Gray
    Write-Host "  3. 命令行兼容: 支持与 cvr-patch.mjs 完全相同的指令 (status, inject, restore, diff, export-patch)" -ForegroundColor Gray
} else {
    Write-Host "`n[ERROR] 编译失败，进程退出码: $($process.ExitCode)" -ForegroundColor Red
    exit $process.ExitCode
}
