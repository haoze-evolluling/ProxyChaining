# Clash Verge Rev 跨订阅链式代理独立注入/补丁程序 (ProxyChaining)

这是一个专为 **Clash Verge Rev** 设计的独立、非侵入式链式代理注入与补丁工具。

在不改变 Clash Verge Rev 原项目架构的前提下，本工具通过精准的局部补丁，解除链式代理对单组 / 单订阅来源的限制，使得来自不同订阅或分组的节点能够自由组合成代理链（例如：**订阅 A 节点 → 订阅 B 节点**）。

---

## 项目结构与文件夹布局（首页三大核心目录）

为了方便用户直接分发、放置程序与一键注入，本仓库根目录（首页）规划为清晰直观的 3 个功能文件夹：

```text
ProxyChaining/
├── patch-tool/       # [1. 项目程序] 存放独立补丁注入工具 (cvr-patch.exe / 脚本 / 编译工具)
├── target-package/   # [2. 待注入程序包] 供用户解压或放置目标 Clash Verge Rev 源码/程序
└── instructions/     # [3. 说明及指引] 包含图文与操作步骤提示文档
```

- **`patch-tool/`**：内部包含编译好的原生独立 EXE（`cvr-patch.exe`）、Node.js 脚本与自动化构建脚本。程序已内置**智能探测机制**，双击运行时会自动穿透检测 `../target-package/` 下的目标程序，免去配置路径。
- **`target-package/`**：用户将下载好的 Clash Verge Rev 源码或解压包直接丢进此文件夹即可，工具自动识别并匹配。
- **`instructions/`**：内含 `【提示】请将待注入的程序包放在“target-package”目录下.txt`，为使用者提供指引。

---

## 目录
- [项目结构与文件夹布局（首页三大核心目录）](#项目结构与文件夹布局首页三大核心目录)
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

本工具提供 **纯原生独立 EXE 程序**（适合无开发环境电脑）、**PowerShell 封装** 及 **Node.js 脚本** 三种使用方式。

无论采用哪种方式，推荐的标准使用流程为：
1. **解压目标包**：将待打补丁的 Clash Verge Rev 源码或程序包放入 `target-package/` 目录。
2. **进入工具目录**：打开 `patch-tool/` 目录。
3. **运行注入**：双击 `cvr-patch.exe` 按提示操作，工具会自动穿透检测 `target-package/` 并在备份后完成注入。

### 方式 A：无环境电脑直接使用独立 EXE（推荐）
在没有安装 Node.js、Python 或任何开发环境的电脑上：
- **双击运行 `patch-tool/cvr-patch.exe`**：直接启动控制台交互式菜单，输入数字 `1` 即可一键注入补丁，输入 `2` 即可撤销还原，界面友好，执行完毕后按回车返回或退出。
- **命令行方式**（在 `patch-tool` 目录下或指定路径执行）：
  ```cmd
  cd patch-tool

  # 检查补丁状态 (自动扫描 ../target-package)
  cvr-patch.exe status

  # 一键注入补丁
  cvr-patch.exe inject

  # 一键撤销还原
  cvr-patch.exe restore

  # 查看变更差异
  cvr-patch.exe diff

  # 导出 patch 补丁文件
  cvr-patch.exe export-patch

  # 亦可显式指定自定义项目路径
  cvr-patch.exe inject "D:\Projects\clash-verge-rev"
  ```

### 方式 B：一键编译 EXE 脚本 (`build.bat` / `build.ps1`)
如果需要重新编译或在其他电脑上从源码生成 `cvr-patch.exe`：
- **进入 `patch-tool/`，双击 `build.bat`** 或在终端执行：
  ```cmd
  cd patch-tool
  build.bat
  ```
  *脚本利用 Windows 10/11 系统自带的 .NET Framework 编译器 `csc.exe`，0 外部依赖，秒级生成体积仅 46KB 的原生可执行文件。*
- **PowerShell 用户**：
  ```powershell
  cd patch-tool
  .\build.ps1
  # 或者通过 npm / pnpm
  npm run build:exe
  ```

### 方式 C：通过脚本运行 (`patch.ps1` 或 `node cvr-patch.mjs`)
如果系统已安装 Node.js 或已编译 EXE，也可直接使用脚本：
```powershell
cd patch-tool

# 自动选择已编译 EXE 或 Node.js 执行 (优先探测 target-package)
.\patch.ps1 status
.\patch.ps1 inject
.\patch.ps1 restore
.\patch.ps1 diff
.\patch.ps1 export-patch

# 或者直接用 Node.js 运行
node cvr-patch.mjs status
node cvr-patch.mjs inject
node cvr-patch.mjs restore
```

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
