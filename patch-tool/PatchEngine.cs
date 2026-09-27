using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CvrProxyChainPatcher
{
    public enum RuleStatus
    {
        APPLIED,
        NOT_APPLIED,
        MODIFIED_OR_PARTIAL,
        MISSING
    }

    public static class PatchEngine
    {
        public static bool IsClashVergeDir(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
            string proxyGroups = Path.Combine(dir, "src", "components", "proxy", "proxy-groups-chain.tsx");
            string tauri = Path.Combine(dir, "src-tauri");
            return File.Exists(proxyGroups) && Directory.Exists(tauri);
        }

        public static string CheckPackageDir(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;
            try
            {
                string full = Path.GetFullPath(dir);
                if (IsClashVergeDir(full)) return full;

                string[] subDirs = Directory.GetDirectories(full);
                foreach (var sub in subDirs)
                {
                    if (IsClashVergeDir(sub)) return Path.GetFullPath(sub);
                }
            }
            catch { }
            return null;
        }

        public static string ResolveTargetDir(string customDir)
        {
            if (!string.IsNullOrEmpty(customDir))
            {
                if (!Directory.Exists(customDir))
                    throw new DirectoryNotFoundException("指定的目录不存在: " + customDir);
                string pkgMatch = CheckPackageDir(customDir);
                if (pkgMatch != null) return pkgMatch;
                if (!IsClashVergeDir(customDir))
                    throw new InvalidOperationException("指定目录不是 Clash Verge Rev 仓库 (缺少 package.json/src-tauri): " + customDir);
                return Path.GetFullPath(customDir);
            }

            string envDir = Environment.GetEnvironmentVariable("CVR_DIR");
            if (!string.IsNullOrEmpty(envDir))
            {
                string pkgMatch = CheckPackageDir(envDir);
                if (pkgMatch != null) return pkgMatch;
                if (IsClashVergeDir(envDir)) return Path.GetFullPath(envDir);
            }

            string current = Directory.GetCurrentDirectory();
            string appBase = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            string[] candidateTargetDirs = new string[]
            {
                Path.Combine(current, "target-package"),
                Path.Combine(current, "..", "target-package"),
                Path.Combine(appBase, "target-package"),
                Path.Combine(appBase, "..", "target-package")
            };

            foreach (var cand in candidateTargetDirs)
            {
                string found = CheckPackageDir(cand);
                if (found != null) return found;
            }

            if (IsClashVergeDir(current))
            {
                return Path.GetFullPath(current);
            }

            string parent = Path.GetFullPath(Path.Combine(current, ".."));
            if (IsClashVergeDir(parent))
            {
                return parent;
            }

            return null;
        }

        public static RuleStatus GetRuleStatus(string targetDir, PatchRule rule, out string reason)
        {
            reason = null;
            string fullPath = Path.Combine(targetDir, rule.RelPath);
            if (!File.Exists(fullPath))
            {
                reason = "文件不存在";
                return RuleStatus.MISSING;
            }

            string content = ConsoleHelper.NormalizeLineEndings(File.ReadAllText(fullPath, Encoding.UTF8));
            bool allPatched = true;
            bool allOriginal = true;

            foreach (var chunk in rule.Chunks)
            {
                string normFind = ConsoleHelper.NormalizeLineEndings(chunk.Find);
                string normReplace = ConsoleHelper.NormalizeLineEndings(chunk.Replace);

                bool hasReplace = content.Contains(normReplace);
                bool hasFind = content.Contains(normFind);

                if (hasReplace)
                {
                    allOriginal = false;
                }
                else if (hasFind)
                {
                    allPatched = false;
                }
                else
                {
                    allPatched = false;
                    allOriginal = false;
                }
            }

            if (allPatched) return RuleStatus.APPLIED;
            if (allOriginal) return RuleStatus.NOT_APPLIED;
            return RuleStatus.MODIFIED_OR_PARTIAL;
        }

        public static void CmdStatus(string targetDir)
        {
            ConsoleHelper.LogHeader("补丁生效状态: " + targetDir);

            int appliedCount = 0;
            var rules = PatchRules.GetRules();
            int total = rules.Count;

            foreach (var rule in rules)
            {
                string reason;
                RuleStatus status = GetRuleStatus(targetDir, rule, out reason);
                ConsoleColor color = ConsoleColor.Yellow;
                string statusText = "异常";
                if (status == RuleStatus.APPLIED)
                {
                    color = ConsoleColor.Green;
                    statusText = "已注入";
                    appliedCount++;
                }
                else if (status == RuleStatus.NOT_APPLIED)
                {
                    color = ConsoleColor.DarkGray;
                    statusText = "未注入";
                }
                else if (status == RuleStatus.MISSING)
                {
                    color = ConsoleColor.Red;
                    statusText = "缺失";
                }
                else if (status == RuleStatus.MODIFIED_OR_PARTIAL)
                {
                    color = ConsoleColor.Yellow;
                    statusText = "部分修改";
                }

                Console.Write("  [");
                ConsoleHelper.WriteColor(statusText, color);
                Console.WriteLine("] " + rule.RelPath);
                Console.Write("          ");
                ConsoleHelper.WriteLineColor(rule.Description, ConsoleColor.DarkGray);
                if (!string.IsNullOrEmpty(reason))
                {
                    Console.Write("          ");
                    ConsoleHelper.WriteLineColor(reason, ConsoleColor.Red);
                }
            }

            Console.WriteLine();
            if (appliedCount == total)
            {
                ConsoleHelper.LogSuccess(string.Format("全部 {0} 个补丁模块均已生效 (跨订阅代理已启用)！", total));
            }
            else if (appliedCount == 0)
            {
                ConsoleHelper.LogInfo("尚未注入任何补丁模块 (代码处于原版状态)。");
            }
            else
            {
                ConsoleHelper.LogWarn(string.Format("部分生效: 已注入 {0}/{1} 个模块。", appliedCount, total));
            }
        }

        public static void CmdInject(string targetDir)
        {
            ConsoleHelper.LogHeader("正在注入补丁至: " + targetDir);

            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss-fffZ");
            string backupDir = Path.Combine(exeDir, "backups", timestamp);
            Directory.CreateDirectory(backupDir);

            int successCount = 0;
            var rules = PatchRules.GetRules();

            foreach (var rule in rules)
            {
                string fullPath = Path.Combine(targetDir, rule.RelPath);
                if (!File.Exists(fullPath))
                {
                    ConsoleHelper.LogError("未找到文件: " + rule.RelPath);
                    continue;
                }

                string originalContent = File.ReadAllText(fullPath, Encoding.UTF8);
                string eol = ConsoleHelper.DetectLineEnding(originalContent);
                string content = ConsoleHelper.NormalizeLineEndings(originalContent);

                string reason;
                RuleStatus status = GetRuleStatus(targetDir, rule, out reason);
                if (status == RuleStatus.APPLIED)
                {
                    ConsoleHelper.LogInfo("已处于注入状态，无需重复打补丁: " + rule.RelPath);
                    successCount++;
                    continue;
                }

                string localBak = fullPath + ".cvr-patch.bak";
                if (!File.Exists(localBak))
                {
                    File.WriteAllText(localBak, originalContent, Encoding.UTF8);
                }

                string snapshotPath = Path.Combine(backupDir, rule.RelPath);
                string snapshotDir = Path.GetDirectoryName(snapshotPath);
                if (!Directory.Exists(snapshotDir)) Directory.CreateDirectory(snapshotDir);
                File.WriteAllText(snapshotPath, originalContent, Encoding.UTF8);

                bool allChunksApplied = true;
                foreach (var chunk in rule.Chunks)
                {
                    string normFind = ConsoleHelper.NormalizeLineEndings(chunk.Find);
                    string normReplace = ConsoleHelper.NormalizeLineEndings(chunk.Replace);

                    if (content.Contains(normReplace))
                    {
                        continue;
                    }

                    if (!content.Contains(normFind))
                    {
                        ConsoleHelper.LogError("匹配块失配 (" + rule.RelPath + "): \"" + chunk.Name + "\"");
                        allChunksApplied = false;
                        break;
                    }

                    content = content.Replace(normFind, normReplace);
                }

                if (!allChunksApplied)
                {
                    ConsoleHelper.LogError("文件补丁未能全部匹配，已放弃对该文件的修改: " + rule.RelPath);
                    continue;
                }

                string finalContent = ConsoleHelper.ApplyLineEnding(content, eol);
                File.WriteAllText(fullPath, finalContent, Encoding.UTF8);
                ConsoleHelper.LogSuccess("成功注入补丁: " + rule.RelPath);
                successCount++;
            }

            Console.WriteLine();
            if (successCount == rules.Count)
            {
                ConsoleHelper.LogSuccess("全部补丁成功注入！原始文件已备份至: " + backupDir);
            }
            else
            {
                ConsoleHelper.LogWarn(string.Format("完成注入: {0}/{1} 个文件处理成功。", successCount, rules.Count));
            }
        }

        public static void CmdRestore(string targetDir)
        {
            ConsoleHelper.LogHeader("正在恢复原版文件: " + targetDir);

            int restoredCount = 0;
            var rules = PatchRules.GetRules();

            foreach (var rule in rules)
            {
                string fullPath = Path.Combine(targetDir, rule.RelPath);
                string localBak = fullPath + ".cvr-patch.bak";

                if (!File.Exists(fullPath))
                {
                    ConsoleHelper.LogWarn("文件不存在: " + rule.RelPath);
                    continue;
                }

                if (File.Exists(localBak))
                {
                    string bakContent = File.ReadAllText(localBak, Encoding.UTF8);
                    File.WriteAllText(fullPath, bakContent, Encoding.UTF8);
                    File.Delete(localBak);
                    ConsoleHelper.LogSuccess("已从备份副本还原: " + rule.RelPath);
                    restoredCount++;
                    continue;
                }

                string currentContent = File.ReadAllText(fullPath, Encoding.UTF8);
                string eol = ConsoleHelper.DetectLineEnding(currentContent);
                string content = ConsoleHelper.NormalizeLineEndings(currentContent);
                bool modified = false;

                foreach (var chunk in rule.Chunks)
                {
                    string normFind = ConsoleHelper.NormalizeLineEndings(chunk.Find);
                    string normReplace = ConsoleHelper.NormalizeLineEndings(chunk.Replace);

                    if (content.Contains(normReplace))
                    {
                        content = content.Replace(normReplace, normFind);
                        modified = true;
                    }
                }

                if (modified)
                {
                    File.WriteAllText(fullPath, ConsoleHelper.ApplyLineEnding(content, eol), Encoding.UTF8);
                    ConsoleHelper.LogSuccess("已逆向撤销补丁改动: " + rule.RelPath);
                    restoredCount++;
                }
                else
                {
                    ConsoleHelper.LogInfo("文件已处于原始未修改状态: " + rule.RelPath);
                }
            }

            Console.WriteLine();
            ConsoleHelper.LogSuccess(string.Format("还原流程结束，共还原/确认 {0} 个文件。", restoredCount));
        }

        public static void CmdDiff(string targetDir)
        {
            ConsoleHelper.LogHeader("生成补丁变更差异预览 (Unified Diff)");

            var rules = PatchRules.GetRules();
            foreach (var rule in rules)
            {
                ConsoleHelper.WriteLineColor("--- a/" + rule.RelPath.Replace('\\', '/'), ConsoleColor.Cyan);
                ConsoleHelper.WriteLineColor("+++ b/" + rule.RelPath.Replace('\\', '/'), ConsoleColor.Cyan);

                foreach (var chunk in rule.Chunks)
                {
                    ConsoleHelper.WriteLineColor("@@ " + chunk.Name + " @@", ConsoleColor.Yellow);
                    string[] findLines = ConsoleHelper.NormalizeLineEndings(chunk.Find).Split('\n');
                    string[] replaceLines = ConsoleHelper.NormalizeLineEndings(chunk.Replace).Split('\n');

                    foreach (var line in findLines)
                    {
                        ConsoleHelper.WriteLineColor("-" + line, ConsoleColor.Red);
                    }
                    foreach (var line in replaceLines)
                    {
                        ConsoleHelper.WriteLineColor("+" + line, ConsoleColor.Green);
                    }
                }
                Console.WriteLine();
            }
        }

        public static void CmdExportPatch(string targetDir, string outputPath)
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string outPath = !string.IsNullOrEmpty(outputPath)
                ? outputPath
                : Path.Combine(exeDir, "patches", "cross-subscription-chain.patch");

            string dir = Path.GetDirectoryName(outPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            StringBuilder sb = new StringBuilder();
            var rules = PatchRules.GetRules();

            foreach (var rule in rules)
            {
                string unixPath = rule.RelPath.Replace('\\', '/');
                sb.AppendLine("diff --git a/" + unixPath + " b/" + unixPath);
                sb.AppendLine("--- a/" + unixPath);
                sb.AppendLine("+++ b/" + unixPath);

                foreach (var chunk in rule.Chunks)
                {
                    sb.AppendLine("@@ -0,0 +0,0 @@ /* " + chunk.Name + " */");
                    string[] findLines = ConsoleHelper.NormalizeLineEndings(chunk.Find).Split('\n');
                    string[] replaceLines = ConsoleHelper.NormalizeLineEndings(chunk.Replace).Split('\n');

                    foreach (var line in findLines)
                    {
                        sb.AppendLine("-" + line);
                    }
                    foreach (var line in replaceLines)
                    {
                        sb.AppendLine("+" + line);
                    }
                }
                sb.AppendLine();
            }

            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
            ConsoleHelper.LogSuccess("已导出 Unified Patch 补丁文件至: " + outPath);
        }
    }
}
