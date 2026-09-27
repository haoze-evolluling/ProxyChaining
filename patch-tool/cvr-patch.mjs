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

import { PATCH_RULES } from './patch-rules.mjs';


function isClashVergeDir(dir) {
  if (!dir || !fs.existsSync(dir)) return false;
  const proxyGroups = path.join(dir, 'src', 'components', 'proxy', 'proxy-groups-chain.tsx');
  const tauri = path.join(dir, 'src-tauri');
  return fs.existsSync(proxyGroups) && fs.existsSync(tauri);
}

function checkPackageDir(dir) {
  if (!dir || !fs.existsSync(dir)) return null;
  try {
    const full = path.resolve(dir);
    if (isClashVergeDir(full)) return full;

    const subDirs = fs.readdirSync(full, { withFileTypes: true })
      .filter((d) => d.isDirectory())
      .map((d) => path.join(full, d.name));

    for (const sub of subDirs) {
      if (isClashVergeDir(sub)) return path.resolve(sub);
    }
  } catch {
    // ignore
  }
  return null;
}

/**
 * 校验与解析目标 Clash Verge Rev 仓库
 */
function resolveTargetDir(customDir) {
  if (customDir) {
    if (!fs.existsSync(customDir)) {
      throw new Error(`指定的目录不存在: ${customDir}`);
    }
    const pkgMatch = checkPackageDir(customDir);
    if (pkgMatch) return pkgMatch;
    if (!isClashVergeDir(customDir)) {
      throw new Error(`指定目录不是 Clash Verge Rev 仓库 (缺少 package.json/src-tauri): ${customDir}`);
    }
    return path.resolve(customDir);
  }

  if (process.env.CVR_DIR) {
    const pkgMatch = checkPackageDir(process.env.CVR_DIR);
    if (pkgMatch) return pkgMatch;
    if (isClashVergeDir(process.env.CVR_DIR)) return path.resolve(process.env.CVR_DIR);
  }

  const current = process.cwd();
  const candidateTargetDirs = [
    path.join(current, 'target-package'),
    path.join(current, '..', 'target-package'),
    path.join(__dirname, 'target-package'),
    path.join(__dirname, '..', 'target-package'),
  ];

  for (const cand of candidateTargetDirs) {
    const found = checkPackageDir(cand);
    if (found) return found;
  }

  if (isClashVergeDir(current)) return path.resolve(current);

  const parent = path.resolve(current, '..');
  if (isClashVergeDir(parent)) return parent;

  throw new Error('未能在 target-package 目录或默认路径自动定位到 Clash Verge Rev 项目！请将待注入的程序包放入 target-package 目录下。');
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
  ${colors.green}status${colors.reset}        检查目标仓库的补丁注入状态 (默认目标: 自动探测或 CVR_DIR)
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
