using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CvrProxyChainPatcher
{
    public enum BuildProfile
    {
        FastRelease,
        StandardRelease,
        Portable
    }

    public static class BuildEngine
    {
        public static int RunProcessLive(string file, string args, string cwd)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    WorkingDirectory = cwd,
                    UseShellExecute = false
                };
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit();
                    return p.ExitCode;
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.LogError("进程执行失败: " + ex.Message);
                return -1;
            }
        }

        public static bool CheckAndInstallNpmDeps(string targetDir)
        {
            string nodeModules = Path.Combine(targetDir, "node_modules");
            bool exists = Directory.Exists(nodeModules);

            if (!exists)
            {
                ConsoleHelper.LogWarn("未检测到 node_modules 依赖目录！");
                if (ConsoleHelper.PromptYesNo("是否自动运行 'pnpm install' 安装前端与构建依赖？", true))
                {
                    ConsoleHelper.LogInfo("正在运行: pnpm install ...");
                    int code = RunProcessLive("cmd.exe", "/c pnpm install", targetDir);
                    if (code != 0)
                    {
                        ConsoleHelper.LogError("pnpm install 执行失败，退出码: " + code);
                        return false;
                    }
                    ConsoleHelper.LogSuccess("依赖安装完成！");
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        public static bool RunPrebuild(string targetDir, bool force = false)
        {
            ConsoleHelper.LogInfo("检查并准备侧载核心与平台资源 (pnpm run prebuild) ...");
            string cmd = force ? "pnpm run prebuild --force" : "pnpm run prebuild";
            int code = RunProcessLive("cmd.exe", "/c " + cmd, targetDir);
            if (code == 0)
            {
                ConsoleHelper.LogSuccess("核心与平台资源准备就绪！");
                return true;
            }
            else
            {
                ConsoleHelper.LogWarn("prebuild 流程返回退出码: " + code + " (若已本地放置核心可忽略)");
                return false;
            }
        }

        public static bool ExecuteBuild(string targetDir, BuildProfile profile)
        {
            string buildCmd;
            string profileName;

            switch (profile)
            {
                case BuildProfile.FastRelease:
                    buildCmd = "pnpm run build:fast";
                    profileName = "极速测试构建 (fast-release)";
                    break;
                case BuildProfile.Portable:
                    buildCmd = "pnpm run portable";
                    profileName = "便携版构建 (portable)";
                    break;
                case BuildProfile.StandardRelease:
                default:
                    buildCmd = "pnpm run build";
                    profileName = "标准完整发布构建 (production release)";
                    break;
            }

            ConsoleHelper.LogHeader("开始编译 Clash Verge Rev: " + profileName);
            ConsoleHelper.LogInfo("工作目录: " + targetDir);
            ConsoleHelper.LogInfo("执行命令: " + buildCmd);
            Console.WriteLine("--------------------------------------------------------------------------------");

            Stopwatch sw = Stopwatch.StartNew();
            int exitCode = RunProcessLive("cmd.exe", "/c " + buildCmd, targetDir);
            sw.Stop();

            Console.WriteLine("--------------------------------------------------------------------------------");
            if (exitCode == 0)
            {
                ConsoleHelper.LogSuccess(string.Format("编译成功！总耗时: {0:mm\\:ss}", sw.Elapsed));
                return true;
            }
            else
            {
                ConsoleHelper.LogError(string.Format("编译失败！退出码: {0}，耗时: {1:mm\\:ss}", exitCode, sw.Elapsed));
                return false;
            }
        }

        public static List<FileInfo> ScanArtifacts(string targetDir)
        {
            List<FileInfo> artifacts = new List<FileInfo>();

            string[] searchDirs = new string[]
            {
                Path.Combine(targetDir, "src-tauri", "target", "release", "bundle", "nsis"),
                Path.Combine(targetDir, "src-tauri", "target", "release", "bundle", "msi"),
                Path.Combine(targetDir, "src-tauri", "target", "fast-release", "bundle", "nsis"),
                Path.Combine(targetDir, "src-tauri", "target", "release"),
                Path.Combine(targetDir, "src-tauri", "target", "fast-release")
            };

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dir in searchDirs)
            {
                if (!Directory.Exists(dir)) continue;

                try
                {
                    DirectoryInfo di = new DirectoryInfo(dir);
                    var files = di.GetFiles("*.*", SearchOption.TopDirectoryOnly);
                    foreach (var f in files)
                    {
                        string ext = f.Extension.ToLowerInvariant();
                        if (ext == ".exe" || ext == ".msi" || ext == ".zip" || ext == ".7z")
                        {
                            string name = f.Name.ToLowerInvariant();
                            // Filter out intermediate cargo build script outputs
                            if (name.Contains("build_script_build") || name.StartsWith("test-") || name.EndsWith(".d"))
                                continue;

                            if (!seen.Contains(f.FullName))
                            {
                                seen.Add(f.FullName);
                                artifacts.Add(f);
                            }
                        }
                    }
                }
                catch { }
            }

            artifacts.Sort((a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));
            return artifacts;
        }

        public static void DisplayArtifacts(List<FileInfo> artifacts)
        {
            if (artifacts.Count == 0)
            {
                ConsoleHelper.LogWarn("未能在目标产物目录下检索到打包输出文件 (请确认编译是否有报错)。");
                return;
            }

            ConsoleHelper.LogHeader("Clash Verge Rev 生成产物列表");
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine(string.Format("{0,-35} | {1,-10} | {2,-19} | {3}", "文件名", "体积", "生成时间", "完整路径"));
            Console.WriteLine("--------------------------------------------------------------------------------");

            foreach (var f in artifacts)
            {
                string sizeStr = ConsoleHelper.FormatFileSize(f.Length);
                string timeStr = f.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                Console.WriteLine(string.Format("{0,-35} | {1,-10} | {2,-19} | {3}", f.Name, sizeStr, timeStr, f.FullName));
            }
            Console.WriteLine("--------------------------------------------------------------------------------\n");
        }

        public static void OpenExplorer(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    Process.Start("explorer.exe", "/select,\"" + path + "\"");
                }
                else if (Directory.Exists(path))
                {
                    Process.Start("explorer.exe", "\"" + path + "\"");
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.LogError("无法打开资源管理器: " + ex.Message);
            }
        }
    }
}
