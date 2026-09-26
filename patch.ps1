<#
.SYNOPSIS
    Clash Verge Rev 跨订阅链式代理独立注入 / 补丁程序 (PowerShell 封装)
.DESCRIPTION
    用于一键注入、撤销、检查或导出针对 Clash Verge Rev 的跨订阅链式代理补丁。
    若本机未安装 Node.js，将自动使用编译好的独立 cvr-patch.exe 执行。
.EXAMPLE
    .\patch.ps1 status
    .\patch.ps1 inject
    .\patch.ps1 restore
    .\patch.ps1 diff
    .\patch.ps1 export-patch
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('status', 'check', 'inject', 'apply', 'restore', 'rollback', 'revert', 'diff', 'export-patch', 'help')]
    [string]$Command = 'status',

    [Parameter(Position = 1)]
    [string]$TargetDir = 'C:\Users\leehaoze\clash-verge-rev-dev'
)

$exePath = Join-Path $PSScriptRoot "cvr-patch.exe"
$scriptPath = Join-Path $PSScriptRoot "cvr-patch.mjs"

if (Test-Path $exePath) {
    if ($TargetDir) {
        & $exePath $Command $TargetDir
    } else {
        & $exePath $Command
    }
} elseif (Get-Command node -ErrorAction SilentlyContinue) {
    if ($TargetDir) {
        node $scriptPath $Command $TargetDir
    } else {
        node $scriptPath $Command
    }
} else {
    Write-Host "[INFO] 未检测到 Node.js 环境，正在调用 Windows 内置编译器自动生成 cvr-patch.exe ..." -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot "build.ps1") -NoPause
    if (Test-Path $exePath) {
        if ($TargetDir) {
            & $exePath $Command $TargetDir
        } else {
            & $exePath $Command
        }
    } else {
        Write-Error "未能执行补丁程序，请先运行 build.bat 编译 EXE 或安装 Node.js 环境。"
        exit 1
    }
}
