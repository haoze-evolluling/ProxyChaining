# Clash Verge Rev 跨订阅链式代理独立注入/补丁程序 (ProxyChaining)

这是一个专为 **Clash Verge Rev** 设计的独立、非侵入式链式代理注入与补丁工具。

在不改变 Clash Verge Rev 原项目架构的前提下，本工具通过精准的局部补丁，解除链式代理对单组 / 单订阅来源的限制，使得来自不同订阅或分组的节点能够自由组合成代理链（例如：**订阅 A 节点 → 订阅 B 节点**）。

---

## 目录
- [一、核心问题与技术背景分析](#一核心问题与技术背景分析)
  - [1. 跨订阅节点被置灰（Opacity 0.55）的原因](#1-跨订阅节点被置灰opacity-055的原因)
  - [2. 无法跨订阅选择节点的原因](#2-无法跨订阅选择节点的原因)
  - [3. 跨订阅无法建立连接的原因](#3-跨订阅无法建立连接的原因)
  - [4. 全局模式下订阅节点不可见的原因](#4-全局模式下订阅节点不可见的原因)
- [二、非侵入式补丁方案设计](#二非侵入式补丁方案设计)
- [三、修改文件清单与改动详情](#三修改文件清单与改动详情)
- [四、快速上手与使用方法](#四快速上手与使用方法)
  - [1. 检查补丁状态](#1-检查补丁状态-status)
  - [2. 一键注入补丁](#2-一键注入补丁-inject)
  - [3. 一键撤销还原](#3-一键撤销还原-restore)
  - [4. 查看变更差异](#4-查看变更差异-diff)
  - [5. 导出标准补丁文件](#5-导出标准补丁文件-export-patch)
- [五、安全与备份机制](#五安全与备份机制)
- [六、Clash Verge Rev 后续升级与补丁同步指南](#六clash-verge-rev-后续升级与补丁同步指南)

---

## 一、核心问题与技术背景分析

Clash Verge Rev 原生的“链式代理”功能（基于 Mihomo 核心的 `dialer-proxy` 机制）在实现时做出了单组限制的假设，导致跨订阅或跨分组节点在建立代理链时出现以下三个核心问题：

### 1. 跨订阅节点被置灰（Opacity 0.55）的原因
- **问题定位**：`src/components/proxy/proxy-groups-chain.tsx` 与 `src/components/proxy/proxy-chain.tsx`。
- **机制**：前端对当前代理链内的节点调用 `rebindProxyChainItems(proxyChain, candidates, proxyView)` 进行重新绑定校验。原逻辑中 `candidates` **仅仅来自于当前选中的单一代理组（或当前渲染列表）**。
- **结果**：当用户在分组 A 选中节点 1 后，切换到分组 B，此时候选集 `candidates` 只包含分组 B 的节点；节点 1 无法在 `candidates` 中匹配，导致其 `recordId` 变为 `undefined`。
- **表象**：UI 判定 `proxy.recordId === undefined`，赋予卡片 `opacity: 0.55`（即节点被置灰），同时“连接”按钮因为 `currentProxyChain.some(({ recordId }) => recordId === undefined)` 而被禁用。

### 2. 无法跨订阅选择节点的原因
- **问题定位**：`src/components/proxy/proxy-groups-chain.tsx` 中的 `handleGroupSelect`。
- **机制**：在原实现中，用户点击规则模式的分组下拉菜单切换分组时，函数内硬编码了 `setProxyChain([])` 以及清除 localStorage 的逻辑。
- **结果**：用户每次切换分组浏览另一个订阅的节点，上一次选中的节点链就会被立刻清空，导致根本无法先后选中不同订阅的节点。

### 3. 跨订阅无法建立连接的原因
- **问题定位**：
  1. **前端代理组匹配**：`handleConnect` 默认把 `targetGroup` 设为界面当前选中的 `selectedGroup`，并调用 `selectNodeForGroup(targetGroup, lastNode.name)`。如果出口节点 `lastNode` 来自订阅 B，但当前选中的界面分组是订阅 A，Mihomo API 会直接报错拒绝（因为订阅 A 的代理组不包含订阅 B 的节点）。
  2. **后端运行时更新**：`src-tauri/src/config/runtime.rs` 中的 `update_proxy_chain_config` 仅在 `config["proxies"]` 中遍历寻找代理节点并插入 `dialer-proxy`。如果节点来自外部订阅文件（`proxy-providers`），该节点并不存在于主配置的 `proxies` 序列中，导致 `dialer-proxy` 根本无法写入生效。

### 4. 全局模式下订阅节点不可见的原因
- **问题定位**：`src/types/proxy-view.ts` 中的 `selectGlobalChainNodes`。
- **机制**：原逻辑通过 `node.source.kind === 'core'` 进行硬过滤，导致所有来自订阅提供者（`kind === 'provider'`）的节点在全局链式代理列表中被完全剔除。

---

## 二、非侵入式补丁方案设计

为了避免破坏 Clash Verge Rev 原项目架构，便于未来版本更新时无缝移植，本补丁程序遵循**最小修改原则（Minimal Invasive Patching）**：

```
                Clash Verge Rev 源码结构保持不变
               ┌─────────────────────────────────┐
               │                                 │
[前台 UI] ─────┼─► proxy-groups-chain.tsx        │ ◄── 取消换组清空 + 全局候选重绑
               │   proxy-chain.tsx               │ ◄── 全局候选重绑 + 自动出口组匹配
               │   proxy-view.ts                 │ ◄── 解锁全局模式订阅节点
               │                                 │
[后台 Rust] ───┼─► runtime.rs                    │ ◄── 增强运行时 dialer-proxy 注入
               │                                 │     (支持 provider 节点动态注入)
               └─────────────────────────────────┘
```

1. **零外部构建依赖**：注入程序使用纯 Node.js 标准库编写，无需额外 `npm install`。
2. **纯局部替换**：不增加、删除任何源文件，不改变构建系统（Vite / Cargo / Tauri 配置全保留）。
3. **安全双备份**：注入前自动在同级生成 `.cvr-patch.bak`，并在工具目录下建立带时间戳的历史镜像。
4. **一键回滚**：提供无损 restore 功能，可随时将源码 100% 还原为官方初始状态。

---

## 三、修改文件清单与改动详情

| 文件路径 | 模块性质 | 修改目标 | 核心效果 |
| :--- | :--- | :--- | :--- |
| `src/components/proxy/proxy-groups-chain.tsx` | 前端组件 | 候选集扩展 & 移除分组重置 | 切换订阅分组不丢链；跨订阅节点保留 `recordId` 不置灰 |
| `src/components/proxy/proxy-chain.tsx` | 前端组件 | 候选集扩展 & 出口组动态寻找 | 代理链卡片高亮显示；连接时自动匹配出口节点所在代理组 |
| `src/types/proxy-view.ts` | 前端类型 | 解除全局链式代理对 core 的过滤 | 全局模式下也能看到并选择订阅节点建立代理链 |
| `src-tauri/src/config/runtime.rs` | 后端 Rust | 运行时代理链注入逻辑增强 | 自动从 `proxy-providers` 文件中检索节点并注入 `dialer-proxy` |

---

## 四、快速上手与使用方法

所有操作均可在当前目录通过 PowerShell 脚本 `.\patch.ps1` 或 `node cvr-patch.mjs` 完成。默认目标目录为 `C:\Users\leehaoze\clash-verge-rev-dev`（可通过参数指定其他目录）。

### 1. 检查补丁状态 (`status`)
```powershell
.\patch.ps1 status
# 或者
node cvr-patch.mjs status
```
*输出示例：*
```
=== Checking Patch Status in: C:\Users\leehaoze\clash-verge-rev-dev ===

  [NOT_APPLIED] src\components\proxy\proxy-groups-chain.tsx
  [NOT_APPLIED] src\components\proxy\proxy-chain.tsx
  [NOT_APPLIED] src\types\proxy-view.ts
  [NOT_APPLIED] src-tauri\src\config\runtime.rs

[INFO] No patch modules applied yet (codebase is in original state). Run 'inject' to apply.
```

### 2. 一键注入补丁 (`inject` 或 `apply`)
```powershell
.\patch.ps1 inject
# 或者
node cvr-patch.mjs inject
```
*执行效果：*
1. 自动备份 4 个目标文件（同时生成 `.cvr-patch.bak` 和时间戳备份目录）。
2. 将跨订阅支持注入到指定位置。
3. 注入完成后，节点跨订阅加入代理链不再置灰，能够正常保存与建立连接。

### 3. 一键撤销还原 (`restore` 或 `rollback`)
```powershell
.\patch.ps1 restore
# 或者
node cvr-patch.mjs restore
```
*执行效果：*
- 优先从 `.cvr-patch.bak` 恢复；若不存在则自动执行 AST 逆向替换。
- 恢复后代码完全等同于官方原始代码，不留残余标记。

### 4. 查看变更差异 (`diff`)
```powershell
.\patch.ps1 diff
# 或者
node cvr-patch.mjs diff
```
*输出彩色 Unified Diff 补丁详情，清晰审阅所有被修改的行。*

### 5. 导出标准补丁文件 (`export-patch`)
```powershell
.\patch.ps1 export-patch
# 或者
node cvr-patch.mjs export-patch
```
*将补丁导出至 `patches/cross-subscription-chain.patch`，支持在 CI/CD 或 Git 环境下使用 `git apply`。*

---

## 五、安全与备份机制

1. **同级备份（`.cvr-patch.bak`）**：
   - 每次首次注入前，自动在原文件所在目录下生成 `.cvr-patch.bak`。
   - `restore` 命令将优先以此备份为准恢复。
2. **时间戳历史仓库（`backups/YYYY-MM-DD.../`）**：
   - 每次执行注入时，修改前的内容会被完整保存在 `backups/` 目录下，附带精确的执行时间。
3. **行尾符兼容（CRLF / LF）**：
   - 程序自动探测目标文件的换行风格（Windows CRLF 或 Linux/macOS LF），替换时严格维持原格式，避免引起 Git 全文件行尾变动的污染。

---

## 六、Clash Verge Rev 后续升级与补丁同步指南

当后续拉取或合并上游 Clash Verge Rev 新版本时，推荐的工作流如下：

```
[上游更新]
   │
   ▼
1. .\patch.ps1 restore         (将本地源码还原为纯净原版状态)
   │
   ▼
2. git pull / git merge        (拉取上游官方最新代码更新)
   │
   ▼
3. .\patch.ps1 status          (检查新版本代码中补丁点的匹配情况)
   │
   ▼
4. .\patch.ps1 inject          (重新一键注入补丁)
```

- 若官方对涉及的文件做出了小幅格式调整，`cvr-patch.mjs` 中的 `PATCH_RULES` 采用的是局部代码块特征匹配，只需在 `cvr-patch.mjs` 中微调对应的 `find` 块文本即可再次精准对齐。
- 由于没有增加任何额外的依赖或破坏性的工程改动，编译流程与官方原版完全一致（支持直接执行原项目的 `pnpm tauri dev` 或 `pnpm tauri build`）。
