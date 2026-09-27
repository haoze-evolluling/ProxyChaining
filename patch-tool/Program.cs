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
  build         自动编译 Clash Verge Windows 产物 (支持多种架构与产物选项)
  check-env     检测系统编译环境 (Node.js, pnpm, Rust, MSVC, Git, WebView2)
  install-env   一键自动安装缺失的编译依赖环境并配置 PATH
  help          显示此帮助信息

编译选项 (cvr-patch.exe build [options]):
  --win-x64, --x64       目标架构: Windows 64位 (默认)
  --win-arm64, --arm64   目标架构: Windows ARM64
  --win-x86, --x86       目标架构: Windows 32位
  --no-bundle, --exe     产物模式: 仅编译独立可执行程序 (跳过打包，速度最快)
  --nsis, --setup        产物模式: 标准 Windows NSIS 安装包 (Setup.exe)
  --portable, --zip      产物模式: 绿色免安装便携版 (ZIP 压缩包)
  --fast                 构建配置: 极速构建 (--profile fast-release)
  --release              构建配置: 标准优化发布构建 (生产模式)
  --debug                构建配置: 调试构建
  --prebuild-only        仅下载与校验侧载核心资源，不执行后续编译
  --force-prebuild       强制重新下载最新侧载核心

示例:
  cvr-patch.exe build                      # 启动交互式 Windows 编译向导
  cvr-patch.exe build --no-bundle --fast   # 编译 Windows 64位纯 EXE (极速调试)
  cvr-patch.exe build --nsis               # 编译 Windows 64位标准 NSIS 安装包
  cvr-patch.exe build --portable           # 编译 Windows 64位绿色免安装 ZIP
  cvr-patch.exe build --win-arm64 --fast   # 编译 Windows ARM64 产物
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

        static BuildOptions PromptCustomBuildOptions()
        {
            BuildOptions opt = new BuildOptions();
            Console.WriteLine("\n[1/3] 请选择目标 Windows 架构:");
            ConsoleHelper.WriteColor("  [1] Windows 64位 (x86_64-pc-windows-msvc) [绝大多数电脑推荐]\n", ConsoleColor.Green);
            ConsoleHelper.WriteColor("  [2] Windows ARM64 (aarch64-pc-windows-msvc) [高通骁龙本/Surface Pro X]\n", ConsoleColor.Cyan);
            ConsoleHelper.WriteColor("  [3] Windows 32位 (i686-pc-windows-msvc) [老旧系统兼容]\n", ConsoleColor.Yellow);
            string cArch = ConsoleHelper.PromptInput("请选择 [1-3]", "1");
            if (cArch == "2") opt.Arch = TargetArch.WinArm64;
            else if (cArch == "3") opt.Arch = TargetArch.WinX86;
            else opt.Arch = TargetArch.WinX64;

            Console.WriteLine("\n[2/3] 请选择产物打包形式:");
            ConsoleHelper.WriteColor("  [1] 仅独立运行程序 (免安装纯 EXE, 跳过打包流程, 耗时最短, 调试首选)\n", ConsoleColor.Green);
            ConsoleHelper.WriteColor("  [2] 标准 Windows NSIS 安装包 (Setup.exe 安装向导)\n", ConsoleColor.Cyan);
            ConsoleHelper.WriteColor("  [3] 绿色免安装便携版 (包含核心与资源的 ZIP 压缩包, 解压即用)\n", ConsoleColor.Magenta);
            ConsoleHelper.WriteColor("  [4] 内置 WebView2 离线安装包 (无需系统 WebView2 运行时)\n", ConsoleColor.Yellow);
            string cMode = ConsoleHelper.PromptInput("请选择 [1-4]", "1");
            if (cMode == "2") opt.Mode = PackageMode.NsisInstaller;
            else if (cMode == "3") opt.Mode = PackageMode.PortableZip;
            else if (cMode == "4") opt.Mode = PackageMode.FixedWebView2;
            else opt.Mode = PackageMode.NoBundle;

            Console.WriteLine("\n[3/3] 请选择构建优化级别:");
            ConsoleHelper.WriteColor("  [1] 极速测试构建 (Fast Release: --profile fast-release) [推荐, 编译快]\n", ConsoleColor.Green);
            ConsoleHelper.WriteColor("  [2] 标准优化发布 (Standard Release: 生产优化, 体积最小执行最快)\n", ConsoleColor.Cyan);
            ConsoleHelper.WriteColor("  [3] 调试构建 (Debug: 携带完整调试符号与日志)\n", ConsoleColor.Yellow);
            string cProf = ConsoleHelper.PromptInput("请选择 [1-3]", "1");
            if (cProf == "2") opt.Profile = BuildProfile.StandardRelease;
            else if (cProf == "3") opt.Profile = BuildProfile.Debug;
            else opt.Profile = BuildProfile.FastRelease;

            return opt;
        }

        static void CmdBuildInteractive(string targetDir)
        {
            ConsoleHelper.LogHeader("Clash Verge Rev 自动编译向导 (Windows 产物构建)");

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
                ConsoleHelper.LogSuccess(string.Format("全部补丁模块已就绪 ({0}/{0} 模块生效)！", rules.Count));
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

            // 3. 构建选项配置
            Console.WriteLine("\n--------------------------------------------------------------");
            ConsoleHelper.WriteColor("【Windows 目标产物快速选项】:\n", ConsoleColor.Cyan);
            ConsoleHelper.WriteColor("  [1] Windows x64 极速独立程序 (纯 EXE, 跳过打包流程, 耗时最短, 调试首选)\n", ConsoleColor.Green);
            ConsoleHelper.WriteColor("  [2] Windows x64 极速安装包 (NSIS Setup .exe, 兼顾构建速度与安装向导)\n", ConsoleColor.Cyan);
            ConsoleHelper.WriteColor("  [3] Windows x64 正式安装包 (全量优化 Release NSIS Setup)\n", ConsoleColor.Yellow);
            ConsoleHelper.WriteColor("  [4] Windows x64 绿色便携免安装包 (Portable .zip 压缩包, 解压即用)\n", ConsoleColor.Magenta);
            ConsoleHelper.WriteColor("\n【更多自定义与高级选项】:\n", ConsoleColor.Cyan);
            ConsoleHelper.WriteColor("  [5] 自定义 Windows 构建配置 (自由定制: 架构 x64/ARM64/x86 | 打包格式 | 优化级别)\n", ConsoleColor.White);
            ConsoleHelper.WriteColor("  [6] 预下载与校验侧载核心 (Prebuild: mihomo, service, mmdb 规则库)\n", ConsoleColor.DarkYellow);
            ConsoleHelper.WriteColor("  [0] 取消编译并返回\n", ConsoleColor.DarkGray);
            Console.WriteLine("--------------------------------------------------------------");

            string choice = ConsoleHelper.PromptInput("请选择 [1-6]", "1");
            if (choice == "0") return;

            BuildOptions options = new BuildOptions();
            if (choice == "1")
            {
                options.Arch = TargetArch.WinX64;
                options.Mode = PackageMode.NoBundle;
                options.Profile = BuildProfile.FastRelease;
            }
            else if (choice == "2")
            {
                options.Arch = TargetArch.WinX64;
                options.Mode = PackageMode.NsisInstaller;
                options.Profile = BuildProfile.FastRelease;
            }
            else if (choice == "3")
            {
                options.Arch = TargetArch.WinX64;
                options.Mode = PackageMode.NsisInstaller;
                options.Profile = BuildProfile.StandardRelease;
            }
            else if (choice == "4")
            {
                options.Arch = TargetArch.WinX64;
                options.Mode = PackageMode.PortableZip;
                options.Profile = BuildProfile.FastRelease;
            }
            else if (choice == "5")
            {
                options = PromptCustomBuildOptions();
                if (options == null) return;
            }
            else if (choice == "6")
            {
                string triple = BuildEngine.GetTriple(TargetArch.WinX64);
                BuildEngine.RunPrebuild(targetDir, triple, true);
                return;
            }

            // 4. 前端依赖安装检查
            if (!BuildEngine.CheckAndInstallNpmDeps(targetDir))
            {
                ConsoleHelper.LogError("依赖准备未完成，已中止编译。");
                return;
            }

            // 5. 执行编译
            bool ok = BuildEngine.ExecuteBuild(targetDir, options);
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
            EnvironmentManager.RefreshPath();

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
                    BuildOptions buildOpt = new BuildOptions();
                    bool hasSpecificMode = false;
                    bool hasSpecificProfile = false;

                    for (int i = 1; i < args.Length; i++)
                    {
                        string arg = args[i].ToLowerInvariant();
                        if (arg == "--win-x64" || arg == "--x64") { buildOpt.Arch = TargetArch.WinX64; }
                        else if (arg == "--win-arm64" || arg == "--arm64") { buildOpt.Arch = TargetArch.WinArm64; }
                        else if (arg == "--win-x86" || arg == "--x86" || arg == "--i686") { buildOpt.Arch = TargetArch.WinX86; }
                        else if (arg == "--no-bundle" || arg == "--raw" || arg == "--exe") { buildOpt.Mode = PackageMode.NoBundle; hasSpecificMode = true; }
                        else if (arg == "--nsis" || arg == "--setup" || arg == "--installer") { buildOpt.Mode = PackageMode.NsisInstaller; hasSpecificMode = true; }
                        else if (arg == "--portable" || arg == "--zip") { buildOpt.Mode = PackageMode.PortableZip; hasSpecificMode = true; }
                        else if (arg == "--fixed-webview2" || arg == "--webview2") { buildOpt.Mode = PackageMode.FixedWebView2; hasSpecificMode = true; }
                        else if (arg == "--fast") { buildOpt.Profile = BuildProfile.FastRelease; hasSpecificProfile = true; }
                        else if (arg == "--release" || arg == "--prod") { buildOpt.Profile = BuildProfile.StandardRelease; hasSpecificProfile = true; }
                        else if (arg == "--debug") { buildOpt.Profile = BuildProfile.Debug; hasSpecificProfile = true; }
                        else if (arg == "--force-prebuild") { buildOpt.ForcePrebuild = true; }
                        else if (arg == "--skip-prebuild") { buildOpt.SkipPrebuild = true; }
                        else if (arg == "--prebuild-only")
                        {
                            string t = BuildEngine.GetTriple(buildOpt.Arch);
                            BuildEngine.RunPrebuild(targetDir, t, true);
                            return 0;
                        }
                    }

                    if (!hasSpecificMode) buildOpt.Mode = PackageMode.NoBundle;
                    if (!hasSpecificProfile) buildOpt.Profile = BuildProfile.FastRelease;

                    BuildEngine.CheckAndInstallNpmDeps(targetDir);
                    if (BuildEngine.ExecuteBuild(targetDir, buildOpt))
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
