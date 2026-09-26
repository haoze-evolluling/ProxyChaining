<#
.SYNOPSIS
    Clash Verge Rev 跨订阅链式代理独立注入 / 补丁程序 (PowerShell 封装)
.DESCRIPTION
    用于一键注入、撤销、检查或导出针对 Clash Verge Rev 的跨订阅链式代理补丁。
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

$scriptPath = Join-Path $PSScriptRoot "cvr-patch.mjs"

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    Write-Error "未检测到 Node.js 环境，请先安装 Node.js (已测试兼容 Node.js 18+)。"
    exit 1
}

if ($TargetDir) {
    node $scriptPath $Command $TargetDir
} else {
    node $scriptPath $Command
}
