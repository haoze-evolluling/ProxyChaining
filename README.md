# Clash Verge Rev 跨订阅链式代理独立注入/补丁程序 (ProxyChaining)

这是一个专为 **Clash Verge Rev** 设计的独立、非侵入式链式代理注入与构建工具。

在不改变 Clash Verge Rev 原项目架构的前提下，本工具通过精准的局部补丁，解除链式代理对单组 / 单订阅来源的限制，使得来自不同订阅或不同分组的节点能够自由组合成代理链（例如：**订阅 A 入口节点 → 订阅 B 出口节点**）。同时内置客户端一键编译、环境自动安装、侧载内核预下载及官方更新屏蔽等全流程功能。

---

> [!WARNING]
> ### 免责声明 (Disclaimer)
> 1. **技术交流与学术研究**：本项目（ProxyChaining / cvr-patch）属于个人开源技术研究项目，仅用于网络协议研究、前端交互逻辑改进及开源代码技术交流，**严禁将本项目用于任何违反所在国家或地区法律法规的活动**。
> 2. **合规性要求**：使用者在下载、使用本项目及其补丁代码、或基于本项目编译衍生版本时，须自行确保符合当地法律法规及网络运营管理规定。因违规使用而产生的任何法律责任，均由使用者自行承担。
> 3. **第三方独立性**：本项目为非官方独立补丁工具，与 Clash Verge Rev 官方团队、Mihomo 内核团队及相关组织**均无任何商业关联或隶属关系**。相关软件商标、源码版权均归其各自原作者所有。
> 4. **风险自担与无担保**：本工具按“现状（AS-IS）”提供，不提供任何明示或暗示的担保。虽然补丁工具内置了完善的文件自动备份与恢复机制，但使用者须自行承担代码修改、环境依赖变更、客户端构建及程序运行过程中可能带来的数据变动、配置异常或系统风险。作者不对因使用或无法使用本工具而导致的任何直接或间接损失承担责任。
> 5. **知情同意**：凡以任何方式获取、下载、运行本工具（包括但不限于 Release 预编译程序、源码构建脚本等），即视为已完整阅读、充分理解并完全同意本声明的所有条款。

---

## 目录
- [核心特性概览](#核心特性概览)
- [快速上手与使用指南（重点）](#快速上手与使用指南重点)
  - [【推荐】场景一：从 Release 仅下载了单个 `cvr-patch.exe`](#推荐场景一从-release-仅下载了单个-cvr-patchexe)
  - [场景二：使用克隆或下载的完整项目仓库](#场景二使用克隆或下载的完整项目仓库)
  - [场景三：命令行 CLI 快捷指令汇总](#场景三命令行-cli-快捷指令汇总)
- [一、修改文件清单与补丁详情](#一修改文件清单与补丁详情)
- [二、核心问题与技术背景分析](#二核心问题与技术背景分析)
  - [1. 跨订阅节点被置灰（Opacity 0.55）的原因](#1-跨订阅节点被置灰opacity-055的原因)
  - [2. 无法跨订阅选择节点的原因](#2-无法跨订阅选择节点的原因)
  - [3. 跨订阅无法建立连接的原因](#3-跨订阅无法建立连接的原因)
  - [4. 全局模式下订阅节点不可见的原因](#4-全局模式下订阅节点不可见的原因)
  - [5. 官方在线更新覆盖自定义版本的问题](#5-官方在线更新覆盖自定义版本的问题)
- [三、非侵入式补丁架构设计](#三非侵入式补丁架构设计)
- [四、安全与备份机制](#四安全与备份机制)
- [五、Clash Verge Rev 后续升级与补丁同步指南](#五clash-verge-rev-后续升级与补丁同步指南)

---

## 核心特性概览

- 🔓 **跨订阅自由串联**：打破单订阅/单代理组壁垒，支持来自不同订阅提供者（Proxy Providers）及不同策略组的节点自由混搭成链。
- 🎨 **前端 UI 完美修复**：切换分组不丢链条；跨订阅节点不再置灰（解决 `opacity: 0.55` 及按钮禁用问题）；全局链式模式下正常展现订阅节点。
- ⚙️ **运行时内核动态注入**：自动解析并写入外部订阅节点至 Mihomo 核心的 `dialer-proxy` 链条，保证代理链路真实生效通畅。
- 🛡️ **屏蔽官方在线更新**：自动屏蔽在线检查更新与覆盖弹窗，防止官方自动更新覆盖带有跨订阅功能的自定义编译版本。
- 🚀 **开箱即用原生 EXE**：提供仅几十 KB 的绿色可执行文件 `cvr-patch.exe`，**零外部环境依赖**（无需提前安装 Node.js/Python），双击即用。
- 🔨 **全自动构建与环境管理**：内置一键编译 Windows 客户端（支持纯 EXE、标准安装包、绿色 ZIP）、环境依赖检测与自动安装、以及侧载核心（Mihomo/Geo 规则）预下载。

---

## 快速上手与使用指南（重点）

### 【推荐】场景一：从 Release 仅下载了单个 `cvr-patch.exe`

如果您直接在 GitHub Releases 中下载了编译好的 `cvr-patch.exe` 单文件，而没有克隆整个 ProxyChaining 仓库，请按照以下极简步骤使用：

#### 步骤 1：准备 Clash Verge Rev 源码
由于本工具是通过修改源码中的逻辑来生成具备跨订阅功能的客户端，请先准备一份 Clash Verge Rev 源码：
- **方式 A（下载 ZIP 包）**：前往 [Clash Verge Rev Releases](https://github.com/clash-verge-rev/clash-verge-rev/releases) 或代码主页，下载 `Source code (zip)` 并解压到本地任意目录（例如 `D:\clash-verge-rev`）。
- **方式 B（Git 克隆）**：
  ```bash
  git clone https://github.com/clash-verge-rev/clash-verge-rev.git
  ```

#### 步骤 2：放置与运行 `cvr-patch.exe`（支持以下任一姿势）

- **姿势 A（最推荐，零配置）：直接放进源码根目录**
  - 把下载好的 `cvr-patch.exe` 复制到解压后的 `clash-verge-rev` 源码根目录下（即与 `package.json`、`src-tauri` 同级）。
  - **直接双击运行 `cvr-patch.exe`**，程序会自动检测当前目录并直接进入操作主菜单！
- **姿势 B（免移动）：任意位置双击 + 拖入目录**
  - 无论 `cvr-patch.exe` 放在桌面还是下载文件夹，直接双击运行它。
  - 程序检测不到默认路径时，会提示：
    ```text
    请输入代码根目录路径 (输入 0 退出):
    ```
  - 此时直接将解压后的 `clash-verge-rev` 文件夹**直接拖入控制台窗口**（或粘贴完整路径）后按回车即可。
- **姿势 C（规范整理）：使用 target-package 目录**
  - 在 `cvr-patch.exe` 同级目录下新建一个名为 `target-package` 的文件夹。
  - 将 `clash-verge-rev` 文件夹放进 `target-package` 中。双击 `cvr-patch.exe`，程序会自动穿透识别。
- **姿势 D（命令行直接调用）**：
  ```cmd
  cvr-patch.exe inject "D:\clash-verge-rev"
  cvr-patch.exe build "D:\clash-verge-rev"
  ```

#### 步骤 3：交互菜单极简“两步走”（注入与编译）

启动后将看到直观的主菜单：

```text
==================================================
  Clash Verge Rev 跨订阅补丁工具
==================================================
目标路径: D:\clash-verge-rev
补丁状态: 未注入 (0/10 模块)

请选择操作:
  [1] 注入补丁
  [2] 还原原版
  [3] 编译客户端     一键生成 Windows 运行程序或安装包
  [4] 高级设置与工具 差异对比 / 导出补丁 / 环境检测 / 路径切换
  [0] 退出
```

1. **第一步：输入 `1` 并回车（注入补丁）**
   - 程序自动对原文件建立备份（生成 `.cvr-patch.bak` 与带时间戳的镜像）。
   - 毫秒级注入跨订阅代理链核心补丁并屏蔽官方更新，状态将变为 `全部已生效 (10/10 模块)`。
2. **第二步：输入 `3` 并回车（编译客户端）**
   - **环境自动检测与安装**：向导会自动检查系统是否安装了 Node.js、pnpm、Rust、MSVC 构建工具。若有缺失，会提示是否一键自动安装，无需手动到处寻找安装包。
   - **侧载资源自动准备**：自动下载匹配系统架构的 Mihomo 核心以及 GeoIP / GeoSite 规则文件。
   - **签名避让**：自动规避私有签名密钥缺失导致的 Tauri 编译报错。
   - **选择产物类型**：
     - `1` 快速独立程序（纯 EXE，耗时最短，免安装极速调试，首推）
     - `2` 标准安装包（Setup.exe，带安装向导）
     - `3` 便携免安装包（ZIP 绿色版）
   - **编译完成**：程序会自动弹出 Windows 资源管理器并高亮定位生成的客户端程序，双击即可直接使用！
3. **如需还原**：随时输入 `2` 并回车，即可 100% 撤销补丁，恢复纯净官方源码。

---

### 场景二：使用克隆或下载的完整项目仓库

如果您克隆或下载了本项目的完整仓库（包含 3 个核心文件夹结构）：

```text
ProxyChaining/
├── patch-tool/       # [1. 项目程序] 存放 cvr-patch.exe、Node 脚本及编译工具
├── target-package/   # [2. 待注入程序包] 供用户解压或放置目标 Clash Verge Rev 源码
└── instructions/     # [3. 说明及指引] 包含图文与操作步骤提示文档
```

1. **放置源码**：将解压后的 Clash Verge Rev 源码文件夹丢进 `target-package/` 目录下。
2. **启动工具**：进入 `patch-tool/` 目录，直接双击运行 `cvr-patch.exe`。
3. **自动穿透识别**：工具会自动穿透扫描 `../target-package/`，无需任何配置，按菜单提示输入数字即可完成注入与编译。

---

### 场景三：命令行 CLI 快捷指令汇总

对于熟悉命令行的开发者，`cvr-patch.exe` 支持完整的参数化调用：

| 命令 / 指令 | 作用说明 | 典型示例 |
| :--- | :--- | :--- |
| `cvr-patch.exe` | 启动交互式控制台菜单（自动探测项目） | `.\cvr-patch.exe` |
| `cvr-patch.exe inject [目录]` | 一键注入跨订阅与防更新补丁 | `.\cvr-patch.exe inject "D:\cvr"` |
| `cvr-patch.exe restore [目录]` | 一键撤销还原为官方源码 | `.\cvr-patch.exe restore "D:\cvr"` |
| `cvr-patch.exe status [目录]` | 检查当前各文件补丁注入状态 | `.\cvr-patch.exe status` |
| `cvr-patch.exe diff [目录]` | 查看代码 Unified Diff 变更差异 | `.\cvr-patch.exe diff` |
| `cvr-patch.exe export-patch` | 导出标准 `.patch` 补丁文件 | `.\cvr-patch.exe export-patch` |
| `cvr-patch.exe check-env` | 检测本机前端与 Rust 编译依赖环境 | `.\cvr-patch.exe check-env` |
| `cvr-patch.exe install-env` | 通过 winget / npm 自动安装缺失环境 | `.\cvr-patch.exe install-env` |
| `cvr-patch.exe build [目录] [选项]` | 一键编译 Windows 客户端 | 见下方详细选项 |

**编译常用选项：**
```cmd
# 极速编译 64 位纯 EXE 运行程序 (免打包, 耗时最短)
cvr-patch.exe build --no-bundle --fast

# 编译 64 位标准 NSIS 安装包 (Setup.exe)
cvr-patch.exe build --nsis

# 编译 64 位绿色免安装便携版 (ZIP)
cvr-patch.exe build --portable

# 仅预下载侧载核心资源 (Mihomo 内核、Service 与 Geo 规则)
cvr-patch.exe build --prebuild-only
```

---

## 一、修改文件清单与补丁详情

本工具对 Clash Verge Rev 代码进行精准微创修改，共涉及以下 10 个核心模块（跨订阅链式代理 5 个 + 屏蔽官方更新 5 个）：

| 文件路径 | 模块类型 | 修改目的 | 核心效果 |
| :--- | :--- | :--- | :--- |
| `src/components/proxy/proxy-chain-model.ts` | 前端模型 | 跨订阅节点保留已有 recordId | 节点不在当前活跃视图中时保留信息，防止连接按钮被禁用或置灰 |
| `src/components/proxy/proxy-groups-chain.tsx` | 前端组件 | 候选集扩展 & 移除换组清空 | 切换订阅分组不丢失已选链条；跨订阅节点保留 `recordId` 不置灰 |
| `src/components/proxy/proxy-chain.tsx` | 前端组件 | 全局候选集重绑 & 出口组动态匹配 | 链条卡片高亮；连接时自动匹配出口节点所在代理组与入口节点修正 |
| `src/types/proxy-view.ts` | 前端类型 | 解除全局模式对 core 的过滤 | 全局模式下也能看到并选择订阅提供者（provider）节点建立代理链 |
| `src-tauri/src/config/runtime.rs` | 后端 Rust | 运行时代理链注入逻辑增强 | 自动从外部 profiles 与 providers 中检索节点并注入 `dialer-proxy` |
| `src/services/update.ts` | 前端服务 | 屏蔽在线更新检查服务与请求 | 静默屏蔽更新请求，防止官方更新提示与自动下载覆盖自定义版本 |
| `src/hooks/use-update.ts` | 前端 Hook | 默认关闭自动检查更新定时器 | 避免客户端启动和运行时定期触发官方更新轮询 |
| `src/components/setting/mods/misc-viewer.tsx` | 前端组件 | 设置界面更新开关默认关闭 | 将杂项设置中的自动检查更新开关默认值设为关闭 (false) |
| `src-tauri/src/config/verge.rs` | 后端 Rust | 后端默认配置关闭更新检测 | 将后端全局配置中的默认 `auto_check_update` 设为 false |
| `src-tauri/src/core/updater.rs` | 后端 Rust | 后端静默更新检测与清理残留 | 静默更新检测默认关闭，且在启动时清理残留的更新缓存 |

---

## 二、核心问题与技术背景分析

Clash Verge Rev 原生链式代理基于 Mihomo 核心的 `dialer-proxy` 机制，但原版前端与配置写入逻辑假定了所有节点必须属于同一个策略组，从而导致以下问题：

### 1. 跨订阅节点被置灰（Opacity 0.55）的原因
- **机制**：前端对当前代理链内的节点调用 `rebindProxyChainItems(proxyChain, candidates, proxyView)` 进行校验。原逻辑中 `candidates` 仅仅来自于当前选中的单一代理组。
- **结果**：当在分组 A 选中节点 1 后切换到分组 B，此时 `candidates` 仅包含分组 B 的节点；节点 1 无法匹配，其 `recordId` 变为 `undefined`。
- **表象**：UI 判定 `proxy.recordId === undefined`，赋予卡片 `opacity: 0.55` 置灰样式，同时“连接”按钮因校验失败而被禁用。

### 2. 无法跨订阅选择节点的原因
- **机制**：在原实现 `src/components/proxy/proxy-groups-chain.tsx` 的 `handleGroupSelect` 中，用户切换分组时硬编码了 `setProxyChain([])` 以及清除本地存储的逻辑。
- **结果**：用户每次切换分组浏览另一个订阅的节点，上一次选中的节点链就会被立刻清空。

### 3. 跨订阅无法建立连接的原因
- **前端代理组匹配**：`handleConnect` 默认把当前界面的 `selectedGroup` 作为目标组。如果出口节点来自订阅 B，但当前选中的界面分组是订阅 A，Mihomo API 会因组内无此节点而报错拒绝。
- **后端运行时更新**：`runtime.rs` 的 `update_proxy_chain_config` 仅在 `config["proxies"]` 中遍历寻找代理节点并插入 `dialer-proxy`。外部订阅文件（`proxy-providers`）中的节点并不在此序列中，导致 `dialer-proxy` 根本无法写入生效。

### 4. 全局模式下订阅节点不可见的原因
- **机制**：`src/types/proxy-view.ts` 中的 `selectGlobalChainNodes` 原逻辑通过 `node.source.kind === 'core'` 进行硬过滤，导致所有来自订阅提供者的节点在全局链式代理列表中被完全剔除。

### 5. 官方在线更新覆盖自定义版本的问题
- 自定义编译的版本在运行一段时间后，Clash Verge Rev 的自动更新机制可能会检测到官方上游的新版本发布，并提示或下载官方安装包，覆盖掉带有跨订阅补丁的自定义构建。补丁工具通过中立屏蔽前端更新检测，保障自定义版本的持久稳定运行。

---

## 三、非侵入式补丁架构设计

本工具严格遵循**最小侵入性原则（Minimal Invasive Patching）**：

```
                Clash Verge Rev 源码结构保持不变
               ┌─────────────────────────────────┐
               │                                 │
[前台 UI] ─────┼─► proxy-groups-chain.tsx        │ ◄── 取消换组清空 + 全局候选重绑
               │   proxy-chain.tsx               │ ◄── 全局候选重绑 + 自动出口组匹配
               │   proxy-view.ts                 │ ◄── 解锁全局模式订阅节点
               │   update.ts / use-update.ts     │ ◄── 屏蔽在线更新覆盖
               │                                 │
[后台 Rust] ───┼─► runtime.rs                    │ ◄── 增强运行时 dialer-proxy 注入
               │                                 │     (支持 provider 节点动态注入)
               └─────────────────────────────────┘
```

1. **纯局部替换**：不增加、删除任何源文件，不改动 Vite / Cargo / Tauri 构建骨架。
2. **多语言同源实现**：同时提供轻量原生 C# 独立 EXE（零外部依赖）与标准 Node.js ESM 脚本。
3. **无损还原能力**：随时提供一键 restore，100% 还原为官方初始状态。

---

## 四、安全与备份机制

1. **同级备份（`.cvr-patch.bak`）**：
   - 每次首次注入前，自动在被修改文件同目录下生成 `.cvr-patch.bak`。
   - `restore` 命令优先以此备份为准恢复。
2. **时间戳历史仓库（`backups/YYYY-MM-DD.../`）**：
   - 每次执行注入时，修改前的内容会被完整保存在 `backups/` 目录下，附带精确的执行时间。
3. **行尾符智能匹配（CRLF / LF）**：
   - 程序自动探测目标文件的换行风格，修改时严格维持原格式，避免引起 Git 全文件换行符变动的脏提交。

---

## 五、Clash Verge Rev 后续升级与补丁同步指南

当需要跟随上游 Clash Verge Rev 升级时，推荐的工作流如下：

```
[上游更新]
   │
   ▼
1. cvr-patch.exe restore       (将本地源码还原为纯净原版状态)
   │
   ▼
2. git pull / git merge        (拉取上游官方最新代码更新)
   │
   ▼
3. cvr-patch.exe status        (检查新版本代码中补丁点的匹配情况)
   │
   ▼
4. cvr-patch.exe inject        (重新一键注入补丁)
   │
   ▼
5. cvr-patch.exe build         (重新一键编译新版客户端)
```

若官方对涉及的代码格式进行了细微调整，补丁规则基于局部特征块匹配，仅需微调对应规则中的 `Find` 锚点即可再次精准对齐。
