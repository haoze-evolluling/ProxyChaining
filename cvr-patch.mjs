#!/usr/bin/env node
/**
 * cvr-patch.mjs
 * 
 * Clash Verge Rev 跨订阅链式代理独立注入 / 补丁程序
 * Independent injection / patch tool for cross-subscription proxy chaining in Clash Verge Rev.
 * 
 * Features:
 * - Non-invasive patching: only targets specific localized spots.
 * - Auto-backup & clean rollback: preserves original files (.bak & timestamped).
 * - Multi-subscription support:
 *   1. Solves cross-subscription nodes being grayed out (recordId loss).
 *   2. Solves chain being wiped when switching groups/subscriptions.
 *   3. Solves connection failure by resolving exit node's target proxy group.
 *   4. Solves runtime backend dialer-proxy injection for provider proxies.
 *   5. Unlocks subscription nodes in Global chain mode.
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// 默认目标 Clash Verge Rev 开发目录
const DEFAULT_CVR_DIR = 'C:\\Users\\leehaoze\\clash-verge-rev-dev';

// ANSI 颜色辅助
const colors = {
  reset: '\x1b[0m',
  bold: '\x1b[1m',
  green: '\x1b[32m',
  yellow: '\x1b[33m',
  blue: '\x1b[34m',
  cyan: '\x1b[36m',
  red: '\x1b[31m',
  gray: '\x1b[90m',
};

const log = {
  info: (msg) => console.log(`${colors.cyan}[INFO]${colors.reset} ${msg}`),
  success: (msg) => console.log(`${colors.green}[SUCCESS]${colors.reset} ${msg}`),
  warn: (msg) => console.log(`${colors.yellow}[WARN]${colors.reset} ${msg}`),
  error: (msg) => console.error(`${colors.red}[ERROR]${colors.reset} ${msg}`),
  header: (msg) => console.log(`\n${colors.bold}${colors.blue}=== ${msg} ===${colors.reset}\n`),
};

/**
 * 规范化换行符为 LF 进行比对，替换时保持原文件原有换行风格
 */
function normalizeLineEndings(str) {
  return str.replace(/\r\n/g, '\n');
}

function detectLineEnding(str) {
  const crlfIndex = str.indexOf('\r\n');
  return crlfIndex !== -1 ? '\r\n' : '\n';
}

function applyLineEnding(str, eol) {
  return str.replace(/\r\n|\n/g, eol);
}

/**
 * 补丁规则定义
 */
const PATCH_RULES = [
  {
    id: 'proxy-groups-chain',
    relPath: path.join('src', 'components', 'proxy', 'proxy-groups-chain.tsx'),
    description: '解除切换分组时清空代理链的限制，并使用全量候选节点避免跨订阅节点置灰',
    chunks: [
      {
        name: '使用全量节点作为候选节点，防止切换分组时已选节点失去 recordId',
        find: `  const candidateNodes = useMemo(
    () =>
      renderList.flatMap((item) => {
        const occurrences = item.memberCol ?? (item.member ? [item.member] : [])
        return occurrences.flatMap(({ member }) =>
          member.kind === 'node' ? [member.node] : [],
        )
      }),
    [renderList],
  )

  const currentProxyChain = useMemo(
    () =>
      proxyView
        ? rebindProxyChainItems(proxyChain, candidateNodes, proxyView)
        : proxyChain.map((item) => ({
            ...item,
            recordId: undefined,
            delay: undefined,
          })),
    [candidateNodes, proxyChain, proxyView],
  )`,
        replace: `  const candidateNodes = useMemo(
    () =>
      renderList.flatMap((item) => {
        const occurrences = item.memberCol ?? (item.member ? [item.member] : [])
        return occurrences.flatMap(({ member }) =>
          member.kind === 'node' ? [member.node] : [],
        )
      }),
    [renderList],
  )

  // [CVR-PATCH] 全量候选节点：解除跨组/跨订阅时节点因不在当前视图列表而被置灰的问题
  const allCandidateNodes = useMemo(
    () => (proxyView ? Object.values(proxyView.records) : candidateNodes),
    [proxyView, candidateNodes],
  )

  const currentProxyChain = useMemo(
    () =>
      proxyView
        ? rebindProxyChainItems(proxyChain, allCandidateNodes, proxyView)
        : proxyChain.map((item) => ({
            ...item,
            recordId: undefined,
            delay: undefined,
          })),
    [allCandidateNodes, proxyChain, proxyView],
  )`,
      },
      {
        name: '移除切换代理组时强制清空代理链的代码',
        find: `  const handleGroupSelect = (groupName: string) => {
    onGroupSelect(groupName)
    handleGroupMenuClose()

    if (mode === 'rule') {
      updateProxyChainConfigInRuntime(null)
      localStorage.removeItem('proxy-chain-group')
      localStorage.removeItem('proxy-chain-exit-node')
      localStorage.removeItem('proxy-chain-items')
      setProxyChain([])
    }
  }`,
        replace: `  const handleGroupSelect = (groupName: string) => {
    onGroupSelect(groupName)
    handleGroupMenuClose()

    // [CVR-PATCH] 跨订阅支持：切换分组时不自动清空代理链，允许将不同订阅/分组的节点加入同一条链
  }`,
      },
      {
        name: 'handleChangeProxy 添加节点时使用 allCandidateNodes 重新绑定',
        find: `  const handleChangeProxy = useCallback(
    (_group: ProxyGroupView, member: ResolvedProxyMember) => {
      if (!isInteractableMember(member) || member.kind !== 'node') return
      const { node } = member
      setProxyChain((prev) => {
        const current = proxyView
          ? rebindProxyChainItems(prev, candidateNodes, proxyView)
          : prev`,
        replace: `  const handleChangeProxy = useCallback(
    (_group: ProxyGroupView, member: ResolvedProxyMember) => {
      if (!isInteractableMember(member) || member.kind !== 'node') return
      const { node } = member
      setProxyChain((prev) => {
        // [CVR-PATCH] 跨订阅支持：使用全局节点列表重新绑定，防止上一个分组的节点丢失 recordId
        const current = proxyView
          ? rebindProxyChainItems(prev, allCandidateNodes, proxyView)
          : prev`,
      },
      {
        name: 'handleChangeProxy 依赖项补充 allCandidateNodes',
        find: `    [candidateNodes, proxyView, t],
  )`,
        replace: `    [allCandidateNodes, candidateNodes, proxyView, t],
  )`,
      },
    ],
  },
  {
    id: 'proxy-chain',
    relPath: path.join('src', 'components', 'proxy', 'proxy-chain.tsx'),
    description: '在代理链面板中全局寻找候选节点，避免置灰；并在连接时智能匹配出口节点所属代理组',
    chunks: [
      {
        name: '候选节点范围扩大为全部 ProxyNodeView，彻底解除置灰与禁用',
        find: `  const candidates = useMemo(() => {
    if (!proxyView) return []
    if (mode === 'rule' && selectedGroup) {
      return selectRuleChainMembers(proxyView, selectedGroup).flatMap(
        ({ member }) => (member.kind === 'node' ? [member.node] : []),
      )
    }
    if (!runtimeConfig) return []
    const runtimeProxies = (
      runtimeConfig as RuntimeConfigWithProxySequence | null
    )?.proxies
    return selectGlobalChainNodes(proxyView, runtimeProxies)
  }, [mode, proxyView, runtimeConfig, selectedGroup])`,
        replace: `  // [CVR-PATCH] 跨订阅支持：候选节点包含 proxyView 中的全部节点，避免跨组/跨订阅节点 recordId 丢失而置灰
  const candidates = useMemo(() => {
    if (!proxyView) return []
    return Object.values(proxyView.records)
  }, [proxyView])`,
      },
      {
        name: 'isConnected 判断支持跨组记录的 proxy-chain-group',
        find: `    if (!selectedGroup) {
      return false
    }

    const proxyChainGroup = proxyView.groups.find(
      (group) => group.name === selectedGroup,
    )

    return proxyChainGroup?.now === lastNode.name
  }, [proxyView, currentProxyChain, mode, selectedGroup])`,
        replace: `    if (!selectedGroup) {
      return false
    }

    // [CVR-PATCH] 优先使用存储的代理链连接组，防止切换浏览其他分组时连接状态判断错误
    const chainGroupName =
      localStorage.getItem('proxy-chain-group') || selectedGroup
    const proxyChainGroup = proxyView.groups.find(
      (group) => group.name === chainGroupName,
    )

    return proxyChainGroup?.now === lastNode.name
  }, [proxyView, currentProxyChain, mode, selectedGroup])`,
      },
      {
        name: 'handleConnect 动态解析出口节点所属代理组，避免跨订阅连接报错',
        find: `      // 根据模式确定使用的代理组名称
      if (mode !== 'global' && !selectedGroup) {
        throw new Error('规则模式下必须选择代理组')
      }

      const targetGroup = mode === 'global' ? 'GLOBAL' : selectedGroup

      await selectNodeForGroup(targetGroup || 'GLOBAL', lastNode.name)
      // The chain moves the group like any other selection, so the profile has to learn about
      // it: what the profile holds is what gets re-applied the next time the core starts.
      recordSelection(targetGroup || 'GLOBAL', lastNode.name)
      localStorage.setItem('proxy-chain-group', targetGroup || 'GLOBAL')
      localStorage.setItem('proxy-chain-exit-node', lastNode.name)`,
        replace: `      // 根据模式确定使用的代理组名称
      if (mode !== 'global' && !selectedGroup) {
        throw new Error('规则模式下必须选择代理组')
      }

      let targetGroup = mode === 'global' ? 'GLOBAL' : selectedGroup

      // [CVR-PATCH] 跨订阅支持：如果当前 selectedGroup 不包含出口节点，自动寻找包含出口节点的代理组
      if (mode !== 'global' && proxyView) {
        const currentGroupObj = proxyView.groups.find(
          (g) => g.name === targetGroup,
        )
        const hasNode = currentGroupObj?.members.some(
          (m) => m.name === lastNode.name,
        )
        if (!hasNode) {
          const matchingGroup = proxyView.groups.find((g) =>
            g.members.some((m) => m.name === lastNode.name),
          )
          if (matchingGroup) {
            targetGroup = matchingGroup.name
          }
        }
      }

      await selectNodeForGroup(targetGroup || 'GLOBAL', lastNode.name)
      // The chain moves the group like any other selection, so the profile has to learn about
      // it: what the profile holds is what gets re-applied the next time the core starts.
      recordSelection(targetGroup || 'GLOBAL', lastNode.name)
      localStorage.setItem('proxy-chain-group', targetGroup || 'GLOBAL')
      localStorage.setItem('proxy-chain-exit-node', lastNode.name)`,
      },
    ],
  },
  {
    id: 'proxy-view-types',
    relPath: path.join('src', 'types', 'proxy-view.ts'),
    description: '解除全局链式代理节点仅允许 core 来源的限制，使订阅提供者节点在全局模式下可用',
    chunks: [
      {
        name: 'selectGlobalChainNodes 包含全部节点',
        find: `export const selectGlobalChainNodes = (
  view: ProxyViewV1,
  runtimeProxies: unknown,
) =>
  view.global === null ? [] : selectRuntimeStandaloneNodes(view, runtimeProxies)`,
        replace: `export const selectGlobalChainNodes = (
  view: ProxyViewV1,
  runtimeProxies: unknown,
) => {
  if (view.global === null) return []
  // [CVR-PATCH] 跨订阅支持：全局模式下展示全部可用节点，允许订阅节点参与全局链式代理
  const allNodes = Object.values(view.records)
  return allNodes.length > 0
    ? allNodes
    : selectRuntimeStandaloneNodes(view, runtimeProxies)
}`,
      },
    ],
  },
  {
    id: 'runtime-backend',
    relPath: path.join('src-tauri', 'src', 'config', 'runtime.rs'),
    description: '增强后端运行时链式代理更新逻辑，支持从 proxy-providers 订阅文件中自动查找节点并注入 dialer-proxy',
    chunks: [
      {
        name: 'update_proxy_chain_config 支持跨订阅/provider 节点识别与注入',
        find: `    /// Rebuilds \`dialer-proxy\` links from an ordered proxy chain, or removes them for \`None\`.
    #[inline]
    pub fn update_proxy_chain_config(&mut self, proxy_chain_config: Option<Value>) {
        let config = if let Some(config) = self.config.as_mut() {
            config
        } else {
            return;
        };

        if let Some(Value::Sequence(proxies)) = config.get_mut("proxies") {
            proxies.iter_mut().for_each(|proxy| {
                if let Some(proxy) = proxy.as_mapping_mut()
                    && proxy.get("dialer-proxy").is_some()
                {
                    proxy.remove("dialer-proxy");
                }
            });
        }

        if let Some(Value::Sequence(dialer_proxies)) = proxy_chain_config
            && let Some(Value::Sequence(proxies)) = config.get_mut("proxies")
        {
            for (i, dialer_proxy) in dialer_proxies.iter().enumerate() {
                if let Some(Value::Mapping(proxy)) =
                    proxies.iter_mut().find(|proxy| proxy.get("name") == Some(dialer_proxy))
                    && i != 0
                    && let Some(dialer_proxy) = dialer_proxies.get(i - 1)
                {
                    proxy.insert("dialer-proxy".into(), dialer_proxy.to_owned());
                }
            }
        }
    }`,
        replace: `    /// Rebuilds \`dialer-proxy\` links from an ordered proxy chain, or removes them for \`None\`.
    #[inline]
    pub fn update_proxy_chain_config(&mut self, proxy_chain_config: Option<Value>) {
        let config = if let Some(config) = self.config.as_mut() {
            config
        } else {
            return;
        };

        // [CVR-PATCH] 清理之前注入的临时跨订阅代理节点与 dialer-proxy
        if let Some(Value::Sequence(proxies)) = config.get_mut("proxies") {
            proxies.retain(|proxy| {
                proxy.get("_injected_chain_proxy").and_then(Value::as_bool) != Some(true)
            });
            proxies.iter_mut().for_each(|proxy| {
                if let Some(proxy) = proxy.as_mapping_mut()
                    && proxy.get("dialer-proxy").is_some()
                {
                    proxy.remove("dialer-proxy");
                }
            });
        }

        if let Some(Value::Sequence(dialer_proxies)) = proxy_chain_config {
            if !config.contains_key("proxies") {
                config.insert("proxies".into(), Value::Sequence(Vec::new()));
            }

            // [CVR-PATCH] 如果链中有节点不在主配置的 proxies 中（例如来自 proxy-providers），从 provider 文件中查找并注入
            let app_home = crate::utils::dirs::app_home_dir().ok();
            if let Some(Value::Mapping(providers)) = config.get("proxy-providers") {
                let provider_paths: Vec<std::path::PathBuf> = providers
                    .values()
                    .filter_map(|p| p.get("path").and_then(Value::as_str))
                    .filter_map(|p| {
                        let path = std::path::Path::new(p);
                        if path.is_absolute() {
                            Some(path.to_path_buf())
                        } else {
                            app_home.as_ref().map(|home| home.join(path))
                        }
                    })
                    .collect();

                if let Some(Value::Sequence(proxies)) = config.get_mut("proxies") {
                    for (i, dialer_proxy) in dialer_proxies.iter().enumerate() {
                        if i == 0 {
                            continue;
                        }
                        let exists = proxies.iter().any(|p| p.get("name") == Some(dialer_proxy));
                        if !exists {
                            for path in &provider_paths {
                                if let Ok(content) = std::fs::read_to_string(path)
                                    && let Ok(provider_yaml) = serde_yaml_ng::from_str::<Mapping>(&content)
                                    && let Some(Value::Sequence(p_nodes)) = provider_yaml.get("proxies")
                                {
                                    if let Some(found_node) = p_nodes.iter().find(|n| n.get("name") == Some(dialer_proxy)) {
                                        if let Some(mut cloned_map) = found_node.as_mapping().cloned() {
                                            if let Some(prev_proxy) = dialer_proxies.get(i - 1) {
                                                cloned_map.insert("dialer-proxy".into(), prev_proxy.to_owned());
                                                cloned_map.insert("_injected_chain_proxy".into(), Value::Bool(true));
                                                proxies.push(Value::Mapping(cloned_map));
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if let Some(Value::Sequence(proxies)) = config.get_mut("proxies") {
                for (i, dialer_proxy) in dialer_proxies.iter().enumerate() {
                    if let Some(Value::Mapping(proxy)) =
                        proxies.iter_mut().find(|proxy| proxy.get("name") == Some(dialer_proxy))
                        && i != 0
                        && let Some(dialer_proxy) = dialer_proxies.get(i - 1)
                    {
                        proxy.insert("dialer-proxy".into(), dialer_proxy.to_owned());
                    }
                }
            }
        }
    }`,
      },
    ],
  },
];

/**
 * 校验目标 Clash Verge Rev 仓库
 */
function resolveTargetDir(customDir) {
  const target = customDir || process.env.CVR_DIR || DEFAULT_CVR_DIR;
  if (!fs.existsSync(target)) {
    throw new Error(`Target Clash Verge Rev directory does not exist: ${target}`);
  }
  const pkgPath = path.join(target, 'package.json');
  if (!fs.existsSync(pkgPath)) {
    throw new Error(`Target directory is not a Clash Verge Rev repository (missing package.json): ${target}`);
  }
  return path.resolve(target);
}

/**
 * 检查当前补丁状态
 */
function getRuleStatus(targetDir, rule) {
  const fullPath = path.join(targetDir, rule.relPath);
  if (!fs.existsSync(fullPath)) {
    return { status: 'MISSING', reason: 'File does not exist' };
  }

  const content = normalizeLineEndings(fs.readFileSync(fullPath, 'utf8'));
  let allPatched = true;
  let allOriginal = true;

  for (const chunk of rule.chunks) {
    const normFind = normalizeLineEndings(chunk.find);
    const normReplace = normalizeLineEndings(chunk.replace);

    const hasReplace = content.includes(normReplace);
    const hasFind = content.includes(normFind);

    if (hasReplace) {
      allOriginal = false;
    } else if (hasFind) {
      allPatched = false;
    } else {
      allPatched = false;
      allOriginal = false;
    }
  }

  if (allPatched) return { status: 'APPLIED' };
  if (allOriginal) return { status: 'NOT_APPLIED' };
  return { status: 'MODIFIED_OR_PARTIAL' };
}

/**
 * 命令: status
 */
function cmdStatus(targetDir) {
  log.header(`Checking Patch Status in: ${targetDir}`);

  let appliedCount = 0;
  let totalRules = PATCH_RULES.length;

  for (const rule of PATCH_RULES) {
    const { status, reason } = getRuleStatus(targetDir, rule);
    let color = colors.yellow;
    if (status === 'APPLIED') {
      color = colors.green;
      appliedCount++;
    } else if (status === 'NOT_APPLIED') {
      color = colors.gray;
    } else if (status === 'MISSING') {
      color = colors.red;
    }

    console.log(`  [${color}${status}${colors.reset}] ${rule.relPath}`);
    console.log(`          ${colors.gray}${rule.description}${colors.reset}`);
    if (reason) {
      console.log(`          ${colors.red}${reason}${colors.reset}`);
    }
  }

  console.log();
  if (appliedCount === totalRules) {
    log.success(`All ${totalRules} patch modules are currently APPLIED (cross-subscription proxy chaining enabled).`);
  } else if (appliedCount === 0) {
    log.info(`No patch modules applied yet (codebase is in original state). Run 'inject' to apply.`);
  } else {
    log.warn(`Partially applied: ${appliedCount}/${totalRules} modules applied.`);
  }
}

/**
 * 命令: inject / apply
 */
function cmdInject(targetDir) {
  log.header(`Injecting Patches into: ${targetDir}`);

  const backupDir = path.join(__dirname, 'backups', new Date().toISOString().replace(/[:.]/g, '-'));
  fs.mkdirSync(backupDir, { recursive: true });

  let successCount = 0;

  for (const rule of PATCH_RULES) {
    const fullPath = path.join(targetDir, rule.relPath);
    if (!fs.existsSync(fullPath)) {
      log.error(`File not found: ${rule.relPath}`);
      continue;
    }

    const originalContent = fs.readFileSync(fullPath, 'utf8');
    const eol = detectLineEnding(originalContent);
    let content = normalizeLineEndings(originalContent);

    const { status } = getRuleStatus(targetDir, rule);
    if (status === 'APPLIED') {
      log.info(`Already applied: ${rule.relPath}`);
      successCount++;
      continue;
    }

    // 保存备份:
    // 1. 同目录下的 .cvr-patch.bak
    const localBak = `${fullPath}.cvr-patch.bak`;
    if (!fs.existsSync(localBak)) {
      fs.writeFileSync(localBak, originalContent, 'utf8');
    }
    // 2. 独立版本库备份
    const snapshotPath = path.join(backupDir, rule.relPath);
    fs.mkdirSync(path.dirname(snapshotPath), { recursive: true });
    fs.writeFileSync(snapshotPath, originalContent, 'utf8');

    let allChunksApplied = true;
    for (const chunk of rule.chunks) {
      const normFind = normalizeLineEndings(chunk.find);
      const normReplace = normalizeLineEndings(chunk.replace);

      if (content.includes(normReplace)) {
        // 该块已应用
        continue;
      }

      if (!content.includes(normFind)) {
        log.error(`Chunk pattern mismatch in ${rule.relPath}: "${chunk.name}"`);
        allChunksApplied = false;
        break;
      }

      content = content.replace(normFind, normReplace);
    }

    if (!allChunksApplied) {
      log.error(`Failed to apply all chunks for ${rule.relPath}. Aborting this file.`);
      continue;
    }

    // 写入修改后的文件，恢复原有行尾
    const finalContent = applyLineEnding(content, eol);
    fs.writeFileSync(fullPath, finalContent, 'utf8');
    log.success(`Injected patch: ${rule.relPath}`);
    successCount++;
  }

  console.log();
  if (successCount === PATCH_RULES.length) {
    log.success(`All patches applied successfully! Backups saved in ${backupDir}`);
  } else {
    log.warn(`Applied ${successCount}/${PATCH_RULES.length} patches with some warnings.`);
  }
}

/**
 * 命令: restore / rollback
 */
function cmdRestore(targetDir) {
  log.header(`Restoring Original Files in: ${targetDir}`);

  let restoredCount = 0;

  for (const rule of PATCH_RULES) {
    const fullPath = path.join(targetDir, rule.relPath);
    const localBak = `${fullPath}.cvr-patch.bak`;

    if (!fs.existsSync(fullPath)) {
      log.warn(`File does not exist: ${rule.relPath}`);
      continue;
    }

    // 优先从 localBak 恢复
    if (fs.existsSync(localBak)) {
      const bakContent = fs.readFileSync(localBak, 'utf8');
      fs.writeFileSync(fullPath, bakContent, 'utf8');
      fs.unlinkSync(localBak);
      log.success(`Restored from backup: ${rule.relPath}`);
      restoredCount++;
      continue;
    }

    // 如果没有 .bak，尝试反向替换
    const currentContent = fs.readFileSync(fullPath, 'utf8');
    const eol = detectLineEnding(currentContent);
    let content = normalizeLineEndings(currentContent);
    let modified = false;

    for (const chunk of rule.chunks) {
      const normFind = normalizeLineEndings(chunk.find);
      const normReplace = normalizeLineEndings(chunk.replace);

      if (content.includes(normReplace)) {
        content = content.replace(normReplace, normFind);
        modified = true;
      }
    }

    if (modified) {
      fs.writeFileSync(fullPath, applyLineEnding(content, eol), 'utf8');
      log.success(`Reverted patch modifications: ${rule.relPath}`);
      restoredCount++;
    } else {
      log.info(`File is already in original state: ${rule.relPath}`);
    }
  }

  console.log();
  log.success(`Restore process complete. ${restoredCount} files restored/verified.`);
}

/**
 * 命令: diff
 */
function cmdDiff(targetDir) {
  log.header(`Generating Patch Preview (Unified Diff)`);

  for (const rule of PATCH_RULES) {
    console.log(`${colors.bold}${colors.cyan}--- a/${rule.relPath}${colors.reset}`);
    console.log(`${colors.bold}${colors.cyan}+++ b/${rule.relPath}${colors.reset}`);

    for (const chunk of rule.chunks) {
      console.log(`${colors.yellow}@@ ${chunk.name} @@${colors.reset}`);
      const findLines = chunk.find.split('\n');
      const replaceLines = chunk.replace.split('\n');

      for (const line of findLines) {
        console.log(`${colors.red}-${line}${colors.reset}`);
      }
      for (const line of replaceLines) {
        console.log(`${colors.green}+${line}${colors.reset}`);
      }
    }
    console.log();
  }
}

/**
 * 命令: export-patch
 */
function cmdExportPatch(targetDir, outputPath) {
  const outPath = outputPath || path.join(__dirname, 'patches', 'cross-subscription-chain.patch');
  fs.mkdirSync(path.dirname(outPath), { recursive: true });

  let diffText = '';

  for (const rule of PATCH_RULES) {
    const unixPath = rule.relPath.replace(/\\/g, '/');
    diffText += `diff --git a/${unixPath} b/${unixPath}\n`;
    diffText += `--- a/${unixPath}\n`;
    diffText += `+++ b/${unixPath}\n`;

    for (const chunk of rule.chunks) {
      diffText += `@@ -0,0 +0,0 @@ /* ${chunk.name} */\n`;
      const findLines = chunk.find.split('\n');
      const replaceLines = chunk.replace.split('\n');

      for (const line of findLines) {
        diffText += `-${line}\n`;
      }
      for (const line of replaceLines) {
        diffText += `+${line}\n`;
      }
    }
    diffText += '\n';
  }

  fs.writeFileSync(outPath, diffText, 'utf8');
  log.success(`Exported unified patch to: ${outPath}`);
}

/**
 * CLI 入口
 */
function printHelp() {
  console.log(`
${colors.bold}Clash Verge Rev 跨订阅链式代理独立注入/补丁程序${colors.reset}

${colors.bold}用法:${colors.reset}
  node cvr-patch.mjs <command> [target_dir] [options]

${colors.bold}命令:${colors.reset}
  ${colors.green}status${colors.reset}        检查目标仓库的补丁注入状态 (默认目标: ${DEFAULT_CVR_DIR})
  ${colors.green}inject${colors.reset}        对目标仓库进行非侵入式修改，解除跨订阅限制并自动备份 (别名: apply)
  ${colors.green}restore${colors.reset}       从备份或反向替换完全撤销补丁，恢复原版代码 (别名: rollback, revert)
  ${colors.green}diff${colors.reset}          在终端中以 Unified Diff 格式展示所有的局部修改内容
  ${colors.green}export-patch${colors.reset}  导出标准 .patch 文件，供 git apply 使用

${colors.bold}示例:${colors.reset}
  node cvr-patch.mjs status
  node cvr-patch.mjs inject
  node cvr-patch.mjs restore
  node cvr-patch.mjs inject "D:\\Projects\\clash-verge-rev"
`);
}

function main() {
  const args = process.argv.slice(2);
  const command = args[0] || 'status';
  const customTarget = args[1] && !args[1].startsWith('--') ? args[1] : null;

  if (['help', '-h', '--help'].includes(command)) {
    printHelp();
    return;
  }

  let targetDir;
  try {
    targetDir = resolveTargetDir(customTarget);
  } catch (err) {
    log.error(err.message);
    process.exit(1);
  }

  switch (command) {
    case 'status':
    case 'check':
      cmdStatus(targetDir);
      break;
    case 'inject':
    case 'apply':
      cmdInject(targetDir);
      break;
    case 'restore':
    case 'rollback':
    case 'revert':
      cmdRestore(targetDir);
      break;
    case 'diff':
      cmdDiff(targetDir);
      break;
    case 'export-patch':
      cmdExportPatch(targetDir, args[2]);
      break;
    default:
      log.error(`Unknown command: ${command}`);
      printHelp();
      process.exit(1);
  }
}

main();
