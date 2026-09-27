using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CvrProxyChainPatcher
{
    class Program
    {
        static void PrintHelp()
        {
            Console.WriteLine(@"
==============================================================
  Clash Verge Rev 跨订阅链式代理独立补丁与构建程序 (ProxyChaining)
==============================================================

用法:
  cvr-patch.exe <command> [target_dir] [options]

命令:
  status        检查目标仓库的补丁注入状态 (默认目标: 自动检测或 CVR_DIR)
  inject        对目标仓库进行非侵入式修改，解除跨订阅限制并自动备份 (别名: apply)
  restore       从备份或反向替换完全撤销补丁，恢复原版代码 (别名: rollback, revert)
  diff          在终端中以 Unified Diff 格式展示所有的局部修改内容
  export-patch  导出标准 .patch 文件，供 git apply 使用
  build         自动编译 Clash Verge 产物 (可选参数: --fast, --portable)
  check-env     检测系统编译环境 (Node.js, pnpm, Rust, MSVC, Git, WebView2)
  install-env   一键自动安装缺失的编译依赖环境并配置 PATH
  help          显示此帮助信息

示例:
  cvr-patch.exe status
  cvr-patch.exe inject
  cvr-patch.exe build
  cvr-patch.exe build --fast
  cvr-patch.exe check-env
  cvr-patch.exe install-env
");
        }

        static void CmdEnvInteractive()
        {
            while (true)
            {
                var items = EnvironmentManager.CheckAll();
                EnvironmentManager.PrintReport(items);

                bool hasMissing = false;
                foreach (var it in items)
                {
                    if (it.Status != EnvStatus.Ready) { hasMissing = true; break; }
                }

                Console.WriteLine("环境操作选项:");
                if (hasMissing)
                {
                    ConsoleHelper.WriteColor("  [1] 一键自动安装缺失依赖环境 (通过 winget / npm)\n", ConsoleColor.Green);
                    ConsoleHelper.WriteColor("  [2] 导出安装脚本 (install-build-env.bat / .ps1)\n", ConsoleColor.Yellow);
                }
                ConsoleHelper.WriteColor("  [3] 刷新并重新检测环境\n", ConsoleColor.Cyan);
                ConsoleHelper.WriteColor("  [0] 返回上级菜单\n", ConsoleColor.DarkGray);
                Console.WriteLine();

                string choice = ConsoleHelper.PromptInput("请输入选项", "0");
                if (choice == "0") break;

                if (choice == "1" && hasMissing)
                {
                    foreach (var it in items)
                    {
                        if (it.Status != EnvStatus.Ready)
                        {
                            EnvironmentManager.InstallItem(it);
                        }
                    }
                    EnvironmentManager.RefreshPath();
                    Console.WriteLine("\n按回车键刷新检测...");
                    Console.ReadLine();
                }
                else if (choice == "2" && hasMissing)
                {
                    string scriptDir = AppDomain.CurrentDomain.BaseDirectory;
                    EnvironmentManager.ExportInstallScripts(scriptDir, items);
                    Console.WriteLine("\n按回车键继续...");
                    Console.ReadLine();
                }
                else if (choice == "3")
                {
                    EnvironmentManager.RefreshPath();
                }
            }
        }

        static void CmdBuildInteractive(string targetDir)
        {
            ConsoleHelper.LogHeader("Clash Verge Rev 自动编译向导");

            // 1. 补丁检查
            var rules = PatchRules.GetRules();
            int appliedCount = 0;
            foreach (var r in rules)
            {
                string reason;
                if (PatchEngine.GetRuleStatus(targetDir, r, out reason) == RuleStatus.APPLIED) appliedCount++;
            }

            if (appliedCount < rules.Count)
            {
                ConsoleHelper.LogWarn(string.Format("检测到跨订阅补丁未完全注入 ({0}/{1} 模块生效)。", appliedCount, rules.Count));
                if (ConsoleHelper.PromptYesNo("是否在编译前自动注入跨订阅补丁？", true))
                {
                    PatchEngine.CmdInject(targetDir);
                }
            }
            else
            {
                ConsoleHelper.LogSuccess("跨订阅链式代理补丁已就绪 (4/4 模块生效)！");
            }

            // 2. 环境检查
            var envItems = EnvironmentManager.CheckAll();
            bool envReady = true;
            foreach (var it in envItems)
            {
                if (it.IsRequired && it.Status != EnvStatus.Ready) { envReady = false; break; }
            }

            if (!envReady)
            {
                EnvironmentManager.PrintReport(envItems);
                if (ConsoleHelper.PromptYesNo("检测到必要依赖环境缺失，是否先由程序自动尝试安装？", true))
                {
                    foreach (var it in envItems)
                    {
                        if (it.Status != EnvStatus.Ready && it.IsRequired)
                        {
                            EnvironmentManager.InstallItem(it);
                        }
                    }
                    EnvironmentManager.RefreshPath();
                }
            }

            // 3. 选择构建模式
            Console.WriteLine("\n请选择编译模式:");
            ConsoleHelper.WriteColor("  [1] 极速测试构建 (Fast Release: pnpm build:fast) [首选，编译时间短]\n", ConsoleColor.Green);
            ConsoleHelper.WriteColor("  [2] 标准完整发布构建 (Production: pnpm build) [包含安装包全量打包]\n", ConsoleColor.Cyan);
            ConsoleHelper.WriteColor("  [3] 便携免安装版构建 (Portable: pnpm portable)\n", ConsoleColor.Yellow);
            ConsoleHelper.WriteColor("  [0] 取消编译\n", ConsoleColor.DarkGray);
            Console.WriteLine();

            string choice = ConsoleHelper.PromptInput("请选择 [1-3]", "1");
            if (choice == "0") return;

            BuildProfile profile = BuildProfile.FastRelease;
            if (choice == "2") profile = BuildProfile.StandardRelease;
            else if (choice == "3") profile = BuildProfile.Portable;

            // 4. 依赖准备与 prebuild
            if (!BuildEngine.CheckAndInstallNpmDeps(targetDir))
            {
                ConsoleHelper.LogError("依赖准备未完成，已中止编译。");
                return;
            }

            BuildEngine.RunPrebuild(targetDir, false);

            // 5. 执行编译
            bool ok = BuildEngine.ExecuteBuild(targetDir, profile);
            if (ok)
            {
                // 6. 产物扫描与展示
                var artifacts = BuildEngine.ScanArtifacts(targetDir);
                BuildEngine.DisplayArtifacts(artifacts);

                if (artifacts.Count > 0)
                {
                    if (ConsoleHelper.PromptYesNo("是否在资源管理器中打开产物所在目录？", true))
                    {
                        BuildEngine.OpenExplorer(artifacts[0].FullName);
                    }
                }
            }
        }

        static void InteractiveMenu(string targetDir)
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("==============================================================");
                ConsoleHelper.WriteColor("  Clash Verge Rev 跨订阅链式代理独立补丁与构建程序\n", ConsoleColor.Cyan);
                Console.WriteLine("==============================================================");
                Console.WriteLine("当前目标仓库: " + targetDir);

                // Check patch status
                var rules = PatchRules.GetRules();
                int applied = 0;
                foreach (var rule in rules)
                {
                    string reason;
                    if (PatchEngine.GetRuleStatus(targetDir, rule, out reason) == RuleStatus.APPLIED) applied++;
                }

                Console.Write("当前补丁状态: ");
                if (applied == rules.Count)
                {
                    ConsoleHelper.WriteLineColor(string.Format("全部已注入 ({0}/{1} 模块生效)", applied, rules.Count), ConsoleColor.Green);
                }
                else if (applied == 0)
                {
                    ConsoleHelper.WriteLineColor(string.Format("原版状态未注入 (0/{0} 模块生效)", rules.Count), ConsoleColor.Gray);
                }
                else
                {
                    ConsoleHelper.WriteLineColor(string.Format("部分注入 ({0}/{1} 模块生效)", applied, rules.Count), ConsoleColor.Yellow);
                }

                Console.WriteLine("\n请选择操作:");
                ConsoleHelper.WriteColor("  [1] 一键注入补丁 (Inject / Apply)\n", ConsoleColor.Green);
                ConsoleHelper.WriteColor("  [2] 一键撤销还原 (Restore / Rollback)\n", ConsoleColor.Yellow);
                ConsoleHelper.WriteColor("  [3] 检查补丁状态 (Status)\n", ConsoleColor.Cyan);
                ConsoleHelper.WriteColor("  [4] 查看改动差异 (Unified Diff)\n", ConsoleColor.White);
                ConsoleHelper.WriteColor("  [5] 导出标准补丁 (Export .patch)\n", ConsoleColor.Magenta);
                ConsoleHelper.WriteColor("  [6] 自动编译 Clash Verge 产物 (Auto Build Artifacts)\n", ConsoleColor.Green);
                ConsoleHelper.WriteColor("  [7] 检查与安装编译环境 (Check & Install Environment)\n", ConsoleColor.Yellow);
                ConsoleHelper.WriteColor("  [8] 更改目标目录 (Change Target Directory)\n", ConsoleColor.DarkCyan);
                ConsoleHelper.WriteColor("  [0] 退出程序 (Exit)\n", ConsoleColor.DarkGray);
                Console.WriteLine();

                string choice = ConsoleHelper.PromptInput("请输入选项 [0-8]", "1");
                if (choice == "0") break;

                switch (choice)
                {
                    case "1":
                        PatchEngine.CmdInject(targetDir);
                        break;
                    case "2":
                        PatchEngine.CmdRestore(targetDir);
                        break;
                    case "3":
                        PatchEngine.CmdStatus(targetDir);
                        break;
                    case "4":
                        PatchEngine.CmdDiff(targetDir);
                        break;
                    case "5":
                        PatchEngine.CmdExportPatch(targetDir, null);
                        break;
                    case "6":
                        CmdBuildInteractive(targetDir);
                        break;
                    case "7":
                        CmdEnvInteractive();
                        break;
                    case "8":
                        Console.Write("\n请输入新的 Clash Verge Rev 根目录路径: ");
                        string lineRaw = Console.ReadLine();
                        string newDir = lineRaw != null ? lineRaw.Trim('"', ' ', '\'') : null;
                        if (!string.IsNullOrEmpty(newDir))
                        {
                            try
                            {
                                targetDir = PatchEngine.ResolveTargetDir(newDir);
                                ConsoleHelper.LogSuccess("目标目录已切换至: " + targetDir);
                            }
                            catch (Exception ex)
                            {
                                ConsoleHelper.LogError("切换失败: " + ex.Message);
                            }
                        }
                        break;
                    default:
                        ConsoleHelper.LogWarn("无效的选项，请重新输入。");
                        break;
                }

                Console.WriteLine();
                Console.Write("按回车键返回主菜单...");
                Console.ReadLine();
            }
        }

        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            if (args.Length > 0 && (args[0] == "help" || args[0] == "-h" || args[0] == "--help" || args[0] == "/?"))
            {
                PrintHelp();
                return 0;
            }

            if (args.Length > 0 && (args[0].ToLowerInvariant() == "check-env" || args[0].ToLowerInvariant() == "env"))
            {
                var items = EnvironmentManager.CheckAll();
                EnvironmentManager.PrintReport(items);
                return 0;
            }

            if (args.Length > 0 && args[0].ToLowerInvariant() == "install-env")
            {
                var items = EnvironmentManager.CheckAll();
                foreach (var it in items)
                {
                    if (it.Status != EnvStatus.Ready)
                    {
                        EnvironmentManager.InstallItem(it);
                    }
                }
                EnvironmentManager.RefreshPath();
                return 0;
            }

            string customTarget = (args.Length > 1 && !args[1].StartsWith("-")) ? args[1] : null;
            string targetDir = null;

            try
            {
                targetDir = PatchEngine.ResolveTargetDir(customTarget);
            }
            catch (Exception ex)
            {
                ConsoleHelper.LogError(ex.Message);
                if (args.Length > 0) return 1;
            }

            if (args.Length == 0)
            {
                while (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
                {
                    Console.Clear();
                    Console.WriteLine("==============================================================");
                    ConsoleHelper.WriteColor("  Clash Verge Rev 跨订阅链式代理独立补丁与构建程序\n", ConsoleColor.Cyan);
                    Console.WriteLine("==============================================================\n");
                    ConsoleHelper.LogWarn("未能在 target-package 目录或默认路径自动定位到 Clash Verge Rev 项目！");
                    Console.WriteLine("提示: 请将待注入的 Clash Verge Rev 源码或程序包解压放置在 target-package 目录下。");
                    Console.Write("或者手动输入 Clash Verge Rev 代码根目录路径 (输入 0 退出): ");
                    string inRaw = Console.ReadLine();
                    string input = inRaw != null ? inRaw.Trim('"', ' ', '\'') : null;
                    if (input == "0" || string.IsNullOrEmpty(input)) return 0;
                    try
                    {
                        targetDir = PatchEngine.ResolveTargetDir(input);
                    }
                    catch (Exception ex)
                    {
                        ConsoleHelper.LogError(ex.Message);
                        Console.WriteLine("按回车键重试...");
                        Console.ReadLine();
                    }
                }

                InteractiveMenu(targetDir);
                return 0;
            }

            if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
            {
                ConsoleHelper.LogError("未能在 target-package 目录或默认路径自动定位到 Clash Verge Rev 项目！");
                Console.WriteLine("提示: 请将待注入的源码放置在 target-package 目录下，或显式指定路径，例如:");
                Console.WriteLine("      cvr-patch.exe " + args[0] + " \"D:\\clash-verge-rev\"");
                return 1;
            }

            string command = args[0].ToLowerInvariant();
            switch (command)
            {
                case "status":
                case "check":
                    PatchEngine.CmdStatus(targetDir);
                    break;
                case "inject":
                case "apply":
                    PatchEngine.CmdInject(targetDir);
                    break;
                case "restore":
                case "rollback":
                case "revert":
                    PatchEngine.CmdRestore(targetDir);
                    break;
                case "diff":
                    PatchEngine.CmdDiff(targetDir);
                    break;
                case "export-patch":
                    PatchEngine.CmdExportPatch(targetDir, args.Length > 2 ? args[2] : null);
                    break;
                case "build":
                    bool isFast = false;
                    bool isPortable = false;
                    for (int i = 1; i < args.Length; i++)
                    {
                        if (args[i] == "--fast") isFast = true;
                        if (args[i] == "--portable") isPortable = true;
                    }
                    BuildProfile p = isPortable ? BuildProfile.Portable : (isFast ? BuildProfile.FastRelease : BuildProfile.StandardRelease);
                    BuildEngine.CheckAndInstallNpmDeps(targetDir);
                    BuildEngine.RunPrebuild(targetDir, false);
                    if (BuildEngine.ExecuteBuild(targetDir, p))
                    {
                        var artifacts = BuildEngine.ScanArtifacts(targetDir);
                        BuildEngine.DisplayArtifacts(artifacts);
                    }
                    break;
                default:
                    ConsoleHelper.LogError("未知命令: " + command);
                    PrintHelp();
                    return 1;
            }

            return 0;
        }
    }
}
