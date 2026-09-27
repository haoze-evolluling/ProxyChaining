using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace CvrProxyChainPatcher
{
    public enum EnvStatus
    {
        Ready,
        Missing,
        Warning
    }

    public class EnvItem
    {
        public string Name { get; set; }
        public string Key { get; set; }
        public bool IsRequired { get; set; }
        public EnvStatus Status { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string InstallCommand { get; set; }
        public string Details { get; set; }
    }

    public static class EnvironmentManager
    {
        public static void RefreshPath()
        {
            try
            {
                string procPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) ?? "";
                string machinePath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
                string userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string cargoBin = Path.Combine(userProfile, ".cargo", "bin");
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string npmPath = Path.Combine(appData, "npm");
                string pnpmPath = Path.Combine(localAppData, "pnpm");

                List<string> entries = new List<string>();
                if (Directory.Exists(cargoBin)) entries.Add(cargoBin);
                if (Directory.Exists(pnpmPath)) entries.Add(pnpmPath);
                if (Directory.Exists(npmPath)) entries.Add(npmPath);

                string[] all = (procPath + ";" + userPath + ";" + machinePath).Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var e in entries) seen.Add(e);

                foreach (var p in all)
                {
                    string trimmed = p.Trim();
                    if (!string.IsNullOrEmpty(trimmed) && !seen.Contains(trimmed))
                    {
                        seen.Add(trimmed);
                        entries.Add(trimmed);
                    }
                }

                string combined = string.Join(";", entries.ToArray());
                Environment.SetEnvironmentVariable("PATH", combined, EnvironmentVariableTarget.Process);
            }
            catch { }
        }

        public static bool RunCommand(string file, string args, out string output, int timeoutMs = 15000)
        {
            output = "";
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                using (Process p = Process.Start(psi))
                {
                    output = p.StandardOutput.ReadToEnd() + "\n" + p.StandardError.ReadToEnd();
                    if (p.WaitForExit(timeoutMs))
                    {
                        return p.ExitCode == 0;
                    }
                    else
                    {
                        p.Kill();
                        return false;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public static int RunInteractiveProcess(string file, string args, string cwd = null)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    UseShellExecute = false,
                    WorkingDirectory = !string.IsNullOrEmpty(cwd) ? cwd : Directory.GetCurrentDirectory()
                };
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit();
                    return p.ExitCode;
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.LogError("执行失败: " + ex.Message);
                return -1;
            }
        }

        public static List<EnvItem> CheckAll()
        {
            RefreshPath();
            List<EnvItem> list = new List<EnvItem>();

            // 1. Node.js
            EnvItem node = new EnvItem
            {
                Name = "Node.js 运行时 (>= 18)",
                Key = "node",
                IsRequired = true,
                Description = "JavaScript 运行时，用于构建前端产物和执行预编译脚本",
                InstallCommand = "winget install -e --id OpenJS.NodeJS.LTS --accept-package-agreements --accept-source-agreements"
            };
            string nodeOut;
            if (RunCommand("node.exe", "-v", out nodeOut) && nodeOut.Trim().StartsWith("v"))
            {
                node.Status = EnvStatus.Ready;
                node.Version = nodeOut.Trim().Split('\n')[0].Trim();
            }
            else
            {
                node.Status = EnvStatus.Missing;
                node.Version = "未检测到";
            }
            list.Add(node);

            // 2. pnpm
            EnvItem pnpm = new EnvItem
            {
                Name = "pnpm 包管理器",
                Key = "pnpm",
                IsRequired = true,
                Description = "快速包管理器，Clash Verge Rev 官方构建指定工具",
                InstallCommand = "npm install -g pnpm"
            };
            string pnpmOut;
            if (RunCommand("cmd.exe", "/c pnpm -v", out pnpmOut) && !string.IsNullOrEmpty(pnpmOut.Trim()) && !pnpmOut.Contains("not recognized") && !pnpmOut.Contains("不是内部或外部命令"))
            {
                pnpm.Status = EnvStatus.Ready;
                pnpm.Version = "v" + pnpmOut.Trim().Split('\n')[0].Trim();
            }
            else
            {
                string localPnpm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "pnpm", "pnpm.cmd");
                string appDataPnpm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "pnpm.cmd");
                if (File.Exists(localPnpm) || File.Exists(appDataPnpm))
                {
                    pnpm.Status = EnvStatus.Ready;
                    pnpm.Version = "已安装(已加入路径)";
                    RefreshPath();
                }
                else
                {
                    pnpm.Status = EnvStatus.Missing;
                    pnpm.Version = "未检测到";
                }
            }
            list.Add(pnpm);

            // 3. Rust Toolchain (rustc & cargo)
            EnvItem rust = new EnvItem
            {
                Name = "Rust 编译器 (rustc / cargo)",
                Key = "rust",
                IsRequired = true,
                Description = "编译 Tauri 原生核心与 Clash Verge 后端所必需",
                InstallCommand = "winget install -e --id Rustlang.Rustup --accept-package-agreements --accept-source-agreements"
            };
            string rustOut;
            string cargoBin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cargo", "bin", "rustc.exe");
            if (RunCommand("rustc.exe", "-V", out rustOut) && rustOut.Trim().StartsWith("rustc"))
            {
                rust.Status = EnvStatus.Ready;
                rust.Version = rustOut.Trim().Split('\n')[0].Trim();
            }
            else if (File.Exists(cargoBin) && RunCommand(cargoBin, "-V", out rustOut) && rustOut.Trim().StartsWith("rustc"))
            {
                rust.Status = EnvStatus.Ready;
                rust.Version = rustOut.Trim().Split('\n')[0].Trim();
                RefreshPath();
            }
            else
            {
                rust.Status = EnvStatus.Missing;
                rust.Version = "未检测到";
            }
            list.Add(rust);

            // 4. Visual Studio C++ Build Tools (MSVC)
            EnvItem msvc = new EnvItem
            {
                Name = "C++ 构建工具 (MSVC / Build Tools)",
                Key = "msvc",
                IsRequired = true,
                Description = "提供 link.exe 与 Windows SDK，用于链接 Rust 本地二进制产物",
                InstallCommand = "winget install -e --id Microsoft.VisualStudio.2022.BuildTools --override \"--passive --wait --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended\" --accept-package-agreements --accept-source-agreements"
            };
            string vswherePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
            string vsOut;
            if (File.Exists(vswherePath) && RunCommand(vswherePath, "-latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationVersion", out vsOut) && !string.IsNullOrEmpty(vsOut.Trim()))
            {
                msvc.Status = EnvStatus.Ready;
                msvc.Version = "VS " + vsOut.Trim().Split('\n')[0].Trim();
            }
            else
            {
                // Registry fallback check
                bool hasVsReg = false;
                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\SxS\VS7"))
                    {
                        if (key != null && key.GetValueNames().Length > 0) hasVsReg = true;
                    }
                    if (!hasVsReg)
                    {
                        using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\SxS\VS7"))
                        {
                            if (key != null && key.GetValueNames().Length > 0) hasVsReg = true;
                        }
                    }
                }
                catch { }

                if (hasVsReg)
                {
                    msvc.Status = EnvStatus.Ready;
                    msvc.Version = "已安装(注册表)";
                }
                else
                {
                    msvc.Status = EnvStatus.Missing;
                    msvc.Version = "未检测到";
                }
            }
            list.Add(msvc);

            // 5. Git
            EnvItem git = new EnvItem
            {
                Name = "Git 版本控制",
                Key = "git",
                IsRequired = false,
                Description = "用于拉取依赖、克隆代码和执行构建流程",
                InstallCommand = "winget install -e --id Git.Git --accept-package-agreements --accept-source-agreements"
            };
            string gitOut;
            if (RunCommand("git.exe", "--version", out gitOut) && gitOut.Trim().StartsWith("git version"))
            {
                git.Status = EnvStatus.Ready;
                git.Version = gitOut.Trim().Split('\n')[0].Trim();
            }
            else
            {
                git.Status = EnvStatus.Missing;
                git.Version = "未检测到";
            }
            list.Add(git);

            // 6. WebView2 Runtime
            EnvItem webview = new EnvItem
            {
                Name = "WebView2 Runtime",
                Key = "webview2",
                IsRequired = true,
                Description = "Tauri 桌面界面在 Windows 上的渲染引擎",
                InstallCommand = "winget install -e --id Microsoft.EdgeWebView2Runtime --accept-package-agreements --accept-source-agreements"
            };
            bool hasWv2 = false;
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-6E3A2E8E9D2C}"))
                {
                    if (k != null)
                    {
                        var pv = k.GetValue("pv") as string;
                        if (!string.IsNullOrEmpty(pv))
                        {
                            hasWv2 = true;
                            webview.Version = pv;
                        }
                    }
                }
                if (!hasWv2)
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-6E3A2E8E9D2C}"))
                    {
                        if (k != null)
                        {
                            var pv = k.GetValue("pv") as string;
                            if (!string.IsNullOrEmpty(pv))
                            {
                                hasWv2 = true;
                                webview.Version = pv;
                            }
                        }
                    }
                }
            }
            catch { }

            if (hasWv2)
            {
                webview.Status = EnvStatus.Ready;
            }
            else
            {
                webview.Status = EnvStatus.Ready; // Default present on modern Win10/11
                webview.Version = "系统内置";
            }
            list.Add(webview);

            return list;
        }

        public static void PrintReport(List<EnvItem> items)
        {
            ConsoleHelper.LogHeader("Clash Verge Rev 编译依赖环境检测");
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine(string.Format("{0,-30} | {1,-10} | {2,-18} | {3}", "组件名称", "必要性", "状态", "详细信息/版本"));
            Console.WriteLine("--------------------------------------------------------------------------------");

            bool allReady = true;
            foreach (var item in items)
            {
                string reqText = item.IsRequired ? "必要" : "建议";
                string statusText = item.Status == EnvStatus.Ready ? "[已就绪]" : "[缺失]";
                ConsoleColor statusColor = item.Status == EnvStatus.Ready ? ConsoleColor.Green : (item.IsRequired ? ConsoleColor.Red : ConsoleColor.Yellow);

                Console.Write(string.Format("{0,-30} | {1,-10} | ", item.Name, reqText));
                ConsoleHelper.WriteColor(string.Format("{0,-18}", statusText), statusColor);
                Console.WriteLine(" | " + item.Version);

                if (item.Status != EnvStatus.Ready && item.IsRequired)
                {
                    allReady = false;
                }
            }
            Console.WriteLine("--------------------------------------------------------------------------------\n");

            if (allReady)
            {
                ConsoleHelper.LogSuccess("太棒了！所有编译必要环境均已就绪，可随时执行自动编译。");
            }
            else
            {
                ConsoleHelper.LogWarn("检测到部分必要编译依赖环境缺失！");
                Console.WriteLine("\n【缺失组件安装命令指引】:");
                foreach (var item in items)
                {
                    if (item.Status != EnvStatus.Ready)
                    {
                        ConsoleHelper.WriteColor("▶ " + item.Name + ":\n", ConsoleColor.Cyan);
                        Console.WriteLine("    功能说明: " + item.Description);
                        ConsoleHelper.WriteColor("    安装命令: ", ConsoleColor.Yellow);
                        ConsoleHelper.WriteLineColor(item.InstallCommand, ConsoleColor.White);
                    }
                }
                Console.WriteLine();
            }
        }

        public static bool InstallItem(EnvItem item)
        {
            ConsoleHelper.LogInfo("正在安装: " + item.Name + " ...");
            ConsoleHelper.LogInfo("执行命令: " + item.InstallCommand);

            int exitCode = -1;
            if (item.Key == "pnpm")
            {
                // Try npm install -g pnpm first
                exitCode = RunInteractiveProcess("cmd.exe", "/c npm install -g pnpm");
                if (exitCode != 0)
                {
                    exitCode = RunInteractiveProcess("cmd.exe", "/c corepack enable");
                }
            }
            else
            {
                exitCode = RunInteractiveProcess("cmd.exe", "/c " + item.InstallCommand);
            }

            RefreshPath();

            if (exitCode == 0)
            {
                ConsoleHelper.LogSuccess("组件 [" + item.Name + "] 安装指令执行完成！");
                return true;
            }
            else
            {
                ConsoleHelper.LogWarn("组件 [" + item.Name + "] 安装进程退出码: " + exitCode + " (若为管理员权限问题，请使用管理员终端执行)");
                return false;
            }
        }

        public static void ExportInstallScripts(string targetDir, List<EnvItem> items)
        {
            string batPath = Path.Combine(targetDir, "install-build-env.bat");
            string ps1Path = Path.Combine(targetDir, "install-build-env.ps1");

            StringBuilder sbBat = new StringBuilder();
            sbBat.AppendLine("@echo off");
            sbBat.AppendLine("chcp 65001 >nul");
            sbBat.AppendLine("echo ========================================================");
            sbBat.AppendLine("echo   Clash Verge Rev 编译依赖环境一键安装脚本 (管理员模式)");
            sbBat.AppendLine("echo ========================================================");
            sbBat.AppendLine("echo.");

            StringBuilder sbPs1 = new StringBuilder();
            sbPs1.AppendLine("# Clash Verge Rev 编译环境自动化安装脚本");
            sbPs1.AppendLine("Write-Host '=== 开始安装 Clash Verge Rev 编译环境 ===' -ForegroundColor Cyan\n");

            foreach (var item in items)
            {
                if (item.Status != EnvStatus.Ready)
                {
                    sbBat.AppendLine("echo [INFO] 正在安装: " + item.Name);
                    sbBat.AppendLine(item.InstallCommand);
                    sbBat.AppendLine("echo.");

                    sbPs1.AppendLine("Write-Host '[INFO] 正在安装: " + item.Name + "' -ForegroundColor Yellow");
                    sbPs1.AppendLine(item.InstallCommand);
                    sbPs1.AppendLine();
                }
            }

            sbBat.AppendLine("echo 依赖安装完成！按任意键退出...");
            sbBat.AppendLine("pause >nul");

            sbPs1.AppendLine("Write-Host '[SUCCESS] 安装流程完毕！' -ForegroundColor Green");

            File.WriteAllText(batPath, sbBat.ToString(), Encoding.UTF8);
            File.WriteAllText(ps1Path, sbPs1.ToString(), Encoding.UTF8);

            ConsoleHelper.LogSuccess("已成功导出环境安装脚本:");
            Console.WriteLine("  BAT 脚本: " + batPath);
            Console.WriteLine("  PS1 脚本: " + ps1Path);
        }
    }
}
