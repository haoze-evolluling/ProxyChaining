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
==================================================
  Clash Verge Rev 跨订阅链式代理补丁与构建工具
==================================================

用法:
  cvr-patch.exe <command> [target_dir] [options]

常用命令:
  inject        注入跨订阅补丁并自动备份 (别名: apply)
  restore       撤销补丁恢复原版代码 (别名: rollback)
  build         编译 Windows 客户端产物
  status        检查补丁生效状态
  diff          查看代码修改差异
  export-patch  导出标准补丁文件 (.patch)
  check-env     检测编译依赖环境
  install-env   自动安装缺失的编译环境
  help          显示帮助信息

编译选项 (cvr-patch.exe build [options]):
  --win-x64, --x64       64位架构 (默认)
  --win-arm64, --arm64   ARM64 架构
  --win-x86, --x86       32位架构
  --no-bundle, --exe     仅编译独立程序 (纯 EXE，最快)
  --nsis, --setup        标准安装包 (Setup.exe)
  --portable, --zip      绿色便携版 (ZIP 压缩包)
  --fast                 快速构建 (--profile fast-release)
  --release              正式发布构建

示例:
  cvr-patch.exe                            # 启动交互式操作菜单
  cvr-patch.exe build                      # 启动客户端编译向导
  cvr-patch.exe build --no-bundle --fast   # 极速编译 64位纯 EXE
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

                Console.WriteLine("操作选项:");
                if (hasMissing)
                {
                    ConsoleHelper.WriteMenuItem("1", "自动安装缺失环境", "(通过 winget / npm)");
                    ConsoleHelper.WriteMenuItem("2", "导出离线安装脚本", "(install-build-env.bat / .ps1)");
                }
                ConsoleHelper.WriteMenuItem("3", "刷新重新检测", null);
                ConsoleHelper.WriteMenuItem("0", "返回上级", null, true);
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
                    Console.WriteLine();
                    ConsoleHelper.WriteColor("按回车键刷新检测...", ConsoleColor.DarkGray);
                    Console.ReadLine();
                }
                else if (choice == "2" && hasMissing)
                {
                    string scriptDir = AppDomain.CurrentDomain.BaseDirectory;
                    EnvironmentManager.ExportInstallScripts(scriptDir, items);
                    Console.WriteLine();
                    ConsoleHelper.WriteColor("按回车键继续...", ConsoleColor.DarkGray);
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
            Console.WriteLine("\n[1/3] 选择目标架构:");
            ConsoleHelper.WriteMenuItem("1", "64位 (x64)", "[绝大多数电脑推荐]");
            ConsoleHelper.WriteMenuItem("2", "ARM64", "[高通骁龙本 / Surface Pro]");
            ConsoleHelper.WriteMenuItem("3", "32位 (x86)", "[老旧系统兼容]");
            string cArch = ConsoleHelper.PromptInput("请选择 [1-3]", "1");
            if (cArch == "2") opt.Arch = TargetArch.WinArm64;
            else if (cArch == "3") opt.Arch = TargetArch.WinX86;
            else opt.Arch = TargetArch.WinX64;

            Console.WriteLine("\n[2/3] 选择打包形式:");
            ConsoleHelper.WriteMenuItem("1", "独立运行程序", "(纯 EXE，耗时最短)");
            ConsoleHelper.WriteMenuItem("2", "标准安装包  ", "(Setup.exe 安装向导)");
            ConsoleHelper.WriteMenuItem("3", "便携压缩包  ", "(ZIP 绿色版，解压即用)");
            ConsoleHelper.WriteMenuItem("4", "离线完整包  ", "(内置 WebView2 运行时)");
            string cMode = ConsoleHelper.PromptInput("请选择 [1-4]", "1");
            if (cMode == "2") opt.Mode = PackageMode.NsisInstaller;
            else if (cMode == "3") opt.Mode = PackageMode.PortableZip;
            else if (cMode == "4") opt.Mode = PackageMode.FixedWebView2;
            else opt.Mode = PackageMode.NoBundle;

            Console.WriteLine("\n[3/3] 选择优化级别:");
            ConsoleHelper.WriteMenuItem("1", "快速测试构建", "(Fast Release: 编译快) [推荐]");
            ConsoleHelper.WriteMenuItem("2", "正式优化构建", "(Standard Release: 体积最小)");
            ConsoleHelper.WriteMenuItem("3", "调试构建    ", "(Debug: 携带完整符号)");
            string cProf = ConsoleHelper.PromptInput("请选择 [1-3]", "1");
            if (cProf == "2") opt.Profile = BuildProfile.StandardRelease;
            else if (cProf == "3") opt.Profile = BuildProfile.Debug;
            else opt.Profile = BuildProfile.FastRelease;

            return opt;
        }

        static void CmdBuildInteractive(string targetDir)
        {
            ConsoleHelper.LogHeader("Clash Verge Rev 客户端编译向导");

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
                ConsoleHelper.LogWarn(string.Format("检测到补丁未完全注入 ({0}/{1} 模块生效)。", appliedCount, rules.Count));
                if (ConsoleHelper.PromptYesNo("是否在编译前自动注入跨订阅补丁？", true))
                {
                    PatchEngine.CmdInject(targetDir);
                }
            }
            else
            {
                ConsoleHelper.LogSuccess(string.Format("补丁状态: 全部就绪 ({0}/{0} 模块生效)", rules.Count));
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
                if (ConsoleHelper.PromptYesNo("检测到必要依赖环境缺失，是否自动尝试安装？", true))
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
            Console.WriteLine();
            ConsoleHelper.WriteDivider();
            Console.WriteLine("请选择构建产物类型:");
            ConsoleHelper.WriteMenuItem("1", "快速独立程序", "(64位 EXE，耗时最短，调试首选)");
            ConsoleHelper.WriteMenuItem("2", "标准安装包  ", "(64位 Setup.exe，推荐日常使用)");
            ConsoleHelper.WriteMenuItem("3", "便携免安装包", "(64位 ZIP 压缩包，解压即用)");
            ConsoleHelper.WriteMenuItem("4", "正式发布包  ", "(全量优化 Release 安装包)");
            ConsoleHelper.WriteMenuItem("5", "自定义构建  ", "(自选架构/打包格式/优化级别)");
            ConsoleHelper.WriteMenuItem("6", "预下载内核  ", "(Prebuild 侧载核心资源与规则库)");
            ConsoleHelper.WriteMenuItem("0", "取消返回    ", null, true);
            ConsoleHelper.WriteDivider();

            string choice = ConsoleHelper.PromptInput("请选择 [0-6]", "1");
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
                options.Mode = PackageMode.PortableZip;
                options.Profile = BuildProfile.FastRelease;
            }
            else if (choice == "4")
            {
                options.Arch = TargetArch.WinX64;
                options.Mode = PackageMode.NsisInstaller;
                options.Profile = BuildProfile.StandardRelease;
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
                ConsoleHelper.WriteHeader("Clash Verge Rev 跨订阅补丁工具");
                Console.Write("目标路径: ");
                ConsoleHelper.WriteLineColor(targetDir, ConsoleColor.White);

                // Check patch status
                var rules = PatchRules.GetRules();
                int applied = 0;
                foreach (var rule in rules)
                {
                    string reason;
                    if (PatchEngine.GetRuleStatus(targetDir, rule, out reason) == RuleStatus.APPLIED) applied++;
                }

                Console.Write("补丁状态: ");
                if (applied == rules.Count)
                {
                    ConsoleHelper.WriteLineColor(string.Format("全部已生效 ({0}/{1} 模块)", applied, rules.Count), ConsoleColor.Green);
                }
                else if (applied == 0)
                {
                    ConsoleHelper.WriteLineColor(string.Format("未注入 (0/{0} 模块)", rules.Count), ConsoleColor.Yellow);
                }
                else
                {
                    ConsoleHelper.WriteLineColor(string.Format("部分生效 ({0}/{1} 模块)", applied, rules.Count), ConsoleColor.Yellow);
                }

                Console.WriteLine("\n请选择操作:");
                ConsoleHelper.WriteMenuItem("1", "注入补丁", applied == rules.Count ? "(当前已全部生效)" : null);
                ConsoleHelper.WriteMenuItem("2", "还原原版", applied == 0 ? "(当前已是原版)" : null);
                ConsoleHelper.WriteMenuItem("3", "编译客户端", "一键生成 Windows 运行程序或安装包");
                ConsoleHelper.WriteMenuItem("4", "高级设置与工具", "差异对比 / 导出补丁 / 环境检测 / 路径切换");
                ConsoleHelper.WriteMenuItem("0", "退出", null, true);
                Console.WriteLine();

                string defaultChoice = (applied == rules.Count) ? "3" : "1";
                string choice = ConsoleHelper.PromptInput("请输入选项 [0-4]", defaultChoice);
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
                        CmdBuildInteractive(targetDir);
                        break;
                    case "4":
                        AdvancedMenu(ref targetDir);
                        continue;
                    default:
                        ConsoleHelper.LogWarn("无效的选项，请重新输入。");
                        break;
                }

                Console.WriteLine();
                ConsoleHelper.WriteColor("按回车键返回主菜单...", ConsoleColor.DarkGray);
                Console.ReadLine();
            }
        }

        static void AdvancedMenu(ref string targetDir)
        {
            while (true)
            {
                Console.Clear();
                ConsoleHelper.WriteHeader("高级设置与工具");
                Console.Write("目标路径: ");
                ConsoleHelper.WriteLineColor(targetDir, ConsoleColor.White);

                Console.WriteLine("\n请选择操作:");
                ConsoleHelper.WriteMenuItem("1", "补丁状态详情", "查看各模块生效状态");
                ConsoleHelper.WriteMenuItem("2", "查看改动差异", "预览 Unified Diff 代码改动");
                ConsoleHelper.WriteMenuItem("3", "导出补丁文件", "生成 .patch 格式补丁");
                ConsoleHelper.WriteMenuItem("4", "编译环境管理", "检测与安装 Node/Rust/MSVC 依赖");
                ConsoleHelper.WriteMenuItem("5", "更改目标路径", "切换其他 Clash Verge 仓库目录");
                ConsoleHelper.WriteMenuItem("0", "返回主菜单", null, true);
                Console.WriteLine();

                string choice = ConsoleHelper.PromptInput("请输入选项 [0-5]", "0");
                if (choice == "0") break;

                switch (choice)
                {
                    case "1":
                        PatchEngine.CmdStatus(targetDir);
                        break;
                    case "2":
                        PatchEngine.CmdDiff(targetDir);
                        break;
                    case "3":
                        PatchEngine.CmdExportPatch(targetDir, null);
                        break;
                    case "4":
                        CmdEnvInteractive();
                        continue;
                    case "5":
                        Console.Write("\n请输入新的 Clash Verge 根目录路径: ");
                        string lineRaw = Console.ReadLine();
                        string newDir = lineRaw != null ? lineRaw.Trim('"', ' ', '\'') : null;
                        if (!string.IsNullOrEmpty(newDir))
                        {
                            try
                            {
                                targetDir = PatchEngine.ResolveTargetDir(newDir);
                                ConsoleHelper.LogSuccess("目标路径已切换至: " + targetDir);
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
                ConsoleHelper.WriteColor("按回车键继续...", ConsoleColor.DarkGray);
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
                    ConsoleHelper.WriteHeader("Clash Verge Rev 跨订阅补丁工具");
                    ConsoleHelper.LogWarn("未能在 target-package 目录或默认路径定位到 Clash Verge Rev 项目。");
                    Console.WriteLine("提示: 请将源码目录放置在 target-package 文件夹下，或直接输入路径。");
                    Console.Write("请输入代码根目录路径 (输入 0 退出): ");
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
                        ConsoleHelper.WriteColor("按回车键重试...", ConsoleColor.DarkGray);
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
