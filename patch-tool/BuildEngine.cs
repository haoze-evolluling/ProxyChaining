using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CvrProxyChainPatcher
{
    public enum TargetArch
    {
        WinX64,     // x86_64-pc-windows-msvc (Windows 64位, 绝大多数电脑推荐)
        WinArm64,   // aarch64-pc-windows-msvc (Windows ARM64, 骁龙本/Surface Pro X)
        WinX86      // i686-pc-windows-msvc (Windows 32位, 老旧系统兼容)
    }

    public enum PackageMode
    {
        NoBundle,       // 仅独立运行程序 (免安装纯 EXE, 跳过打包流程, 耗时最短, 调试首选)
        NsisInstaller,  // 标准 Windows 安装向导包 (NSIS Setup .exe 安装包)
        PortableZip,    // 绿色免安装便携版 (包含核心与资源的完整 ZIP 压缩包, 解压即用)
        FixedWebView2   // 内置 WebView2 离线安装包 (无需系统 WebView2 运行时)
    }

    public enum BuildProfile
    {
        FastRelease,     // 极速测试构建 (fast-release profile, 跳过重度优化以节省构建时间)
        StandardRelease, // 标准优化发布构建 (production release, 开启全量优化)
        Debug            // 调试构建 (debug, 附带调试符号与详细日志)
    }

    public class BuildOptions
    {
        public TargetArch Arch = TargetArch.WinX64;
        public PackageMode Mode = PackageMode.NoBundle;
        public BuildProfile Profile = BuildProfile.FastRelease;
        public bool ForcePrebuild = false;
        public bool SkipPrebuild = false;
    }

    public static class BuildEngine
    {
        public static string GetTriple(TargetArch arch)
        {
            switch (arch)
            {
                case TargetArch.WinArm64: return "aarch64-pc-windows-msvc";
                case TargetArch.WinX86: return "i686-pc-windows-msvc";
                case TargetArch.WinX64:
                default:
                    return "x86_64-pc-windows-msvc";
            }
        }

        public static string GetArchName(TargetArch arch)
        {
            switch (arch)
            {
                case TargetArch.WinArm64: return "Windows ARM64";
                case TargetArch.WinX86: return "Windows 32位 (x86)";
                case TargetArch.WinX64:
                default:
                    return "Windows 64位 (x64)";
            }
        }

        public static string GetModeName(PackageMode mode)
        {
            switch (mode)
            {
                case PackageMode.NoBundle: return "独立可执行程序 (纯 EXE, 跳过打包, 速度最快)";
                case PackageMode.NsisInstaller: return "标准 Windows 安装包 (NSIS Setup.exe)";
                case PackageMode.PortableZip: return "绿色免安装便携版 (Portable ZIP 压缩包)";
                case PackageMode.FixedWebView2: return "内置 WebView2 离线安装包 (Fixed WebView2)";
                default: return "未指定";
            }
        }

        public static string GetProfileName(BuildProfile profile)
        {
            switch (profile)
            {
                case BuildProfile.FastRelease: return "极速测试构建 (fast-release)";
                case BuildProfile.Debug: return "调试构建 (debug)";
                case BuildProfile.StandardRelease:
                default:
                    return "标准优化发布 (production release)";
            }
        }

        public static int RunProcessLive(string file, string args, string cwd, Dictionary<string, string> envVars = null)
        {
            try
            {
                EnvironmentManager.RefreshPath();
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    WorkingDirectory = cwd,
                    UseShellExecute = false
                };

                if (envVars != null)
                {
                    foreach (var kvp in envVars)
                    {
                        psi.EnvironmentVariables[kvp.Key] = kvp.Value;
                    }
                }

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
            if (!Directory.Exists(nodeModules))
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

        public static bool ValidateRequiredResources(string targetDir, string triple, out List<string> missing)
        {
            missing = new List<string>();

            string sidecarDir = Path.Combine(targetDir, "src-tauri", "sidecar");
            string resourcesDir = Path.Combine(targetDir, "src-tauri", "resources");

            string[] requiredSidecars = new string[]
            {
                Path.Combine(sidecarDir, "verge-mihomo-" + triple + ".exe"),
                Path.Combine(sidecarDir, "verge-mihomo-alpha-" + triple + ".exe")
            };

            foreach (var sc in requiredSidecars)
            {
                if (!File.Exists(sc))
                {
                    missing.Add(Path.GetFileName(sc));
                }
            }

            string[] requiredResources = new string[]
            {
                Path.Combine(resourcesDir, "clash-verge-service.exe"),
                Path.Combine(resourcesDir, "Country.mmdb")
            };

            foreach (var res in requiredResources)
            {
                if (!File.Exists(res))
                {
                    missing.Add("resources/" + Path.GetFileName(res));
                }
            }

            return missing.Count == 0;
        }

        public static bool RunPrebuild(string targetDir, string triple, bool force = false)
        {
            ConsoleHelper.LogInfo(string.Format("检查并准备侧载核心与平台资源 (pnpm run prebuild {0}) ...", triple));
            string forceArg = force ? "--force" : "";
            string cmd = string.Format("pnpm run prebuild {0} {1}", forceArg, triple).Trim();

            int code = RunProcessLive("cmd.exe", "/c " + cmd, targetDir);

            List<string> missing;
            bool ok = ValidateRequiredResources(targetDir, triple, out missing);
            if (code == 0 && ok)
            {
                ConsoleHelper.LogSuccess("核心与平台资源准备就绪！");
                return true;
            }
            else if (!ok)
            {
                ConsoleHelper.LogWarn("prebuild 执行后仍缺少关键核心文件: " + string.Join(", ", missing.ToArray()));
                return false;
            }
            else
            {
                ConsoleHelper.LogWarn("prebuild 流程返回退出码: " + code + " (但必需文件已检测存在)");
                return true;
            }
        }

        public static bool EnsureResourcesReady(string targetDir, string triple, bool force = false)
        {
            List<string> missing;
            if (!force && ValidateRequiredResources(targetDir, triple, out missing))
            {
                ConsoleHelper.LogSuccess("侧载核心与运行时依赖已完整存在，无需重复下载。");
                return true;
            }

            ConsoleHelper.LogInfo("检测到侧载组件缺失，自动启动资源下载/校验流程...");
            bool prebuildOk = RunPrebuild(targetDir, triple, force);
            if (!prebuildOk)
            {
                ConsoleHelper.LogError("必需的侧载核心准备失败！已阻止后续编译，避免耗费大量编译时间后报缺失错误。");
                Console.WriteLine("提示: 请确认网络能够访问 GitHub Releases，或手动将对应架构的核心文件放入:");
                Console.WriteLine("      src-tauri\\sidecar\\verge-mihomo-" + triple + ".exe");
                Console.WriteLine("      src-tauri\\sidecar\\verge-mihomo-alpha-" + triple + ".exe");
                return false;
            }
            return true;
        }

        public static void EnsureLocalBuildSigningSafe(string targetDir)
        {
            try
            {
                string confPath = Path.Combine(targetDir, "src-tauri", "tauri.conf.json");
                if (!File.Exists(confPath)) return;

                string privKey = Environment.GetEnvironmentVariable("TAURI_SIGNING_PRIVATE_KEY");
                if (string.IsNullOrEmpty(privKey))
                {
                    string content = File.ReadAllText(confPath, Encoding.UTF8);
                    if (content.Contains("\"createUpdaterArtifacts\": true") || content.Contains("\"createUpdaterArtifacts\":true"))
                    {
                        content = content.Replace("\"createUpdaterArtifacts\": true", "\"createUpdaterArtifacts\": false")
                                         .Replace("\"createUpdaterArtifacts\":true", "\"createUpdaterArtifacts\": false");
                        File.WriteAllText(confPath, content, Encoding.UTF8);
                        ConsoleHelper.LogInfo("检测到未配置 TAURI_SIGNING_PRIVATE_KEY，已自动将 createUpdaterArtifacts 设为 false (规避本地签名报错)。");
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.LogWarn("自动规避签名检查时异常: " + ex.Message);
            }
        }

        public static bool ExecuteBuild(string targetDir, BuildOptions options)
        {
            string triple = GetTriple(options.Arch);

            if (!options.SkipPrebuild)
            {
                if (!EnsureResourcesReady(targetDir, triple, options.ForcePrebuild))
                {
                    return false;
                }
            }

            EnsureLocalBuildSigningSafe(targetDir);

            StringBuilder cmdBuilder = new StringBuilder();
            cmdBuilder.Append("pnpm tauri build --target ").Append(triple);

            switch (options.Mode)
            {
                case PackageMode.NoBundle:
                case PackageMode.PortableZip:
                    cmdBuilder.Append(" --no-bundle");
                    break;
                case PackageMode.NsisInstaller:
                case PackageMode.FixedWebView2:
                    cmdBuilder.Append(" --bundles nsis");
                    break;
            }

            switch (options.Profile)
            {
                case BuildProfile.FastRelease:
                    cmdBuilder.Append(" -- --profile fast-release");
                    break;
                case BuildProfile.Debug:
                    cmdBuilder.Append(" --debug");
                    break;
                case BuildProfile.StandardRelease:
                default:
                    break;
            }

            string buildCmd = cmdBuilder.ToString();
            string title = string.Format("开始编译 Clash Verge Rev [{0} | {1} | {2}]",
                GetArchName(options.Arch), GetModeName(options.Mode), GetProfileName(options.Profile));

            ConsoleHelper.LogHeader(title);
            ConsoleHelper.LogInfo("工作目录: " + targetDir);
            ConsoleHelper.LogInfo("执行命令: " + buildCmd);
            Console.WriteLine("--------------------------------------------------------------------------------");

            var envVars = new Dictionary<string, string>
            {
                { "NODE_OPTIONS", "--max-old-space-size=4096" }
            };

            Stopwatch sw = Stopwatch.StartNew();
            int exitCode = RunProcessLive("cmd.exe", "/c " + buildCmd, targetDir, envVars);
            sw.Stop();

            Console.WriteLine("--------------------------------------------------------------------------------");
            if (exitCode == 0)
            {
                ConsoleHelper.LogSuccess(string.Format("主程序编译成功！总耗时: {0:mm\\:ss}", sw.Elapsed));

                if (options.Mode == PackageMode.PortableZip)
                {
                    ConsoleHelper.LogInfo("正在打包绿色便携免安装版 ZIP 压缩包...");
                    CreatePortablePackage(targetDir, triple, options.Profile);
                }
                return true;
            }
            else
            {
                ConsoleHelper.LogError(string.Format("编译失败！退出码: {0}，耗时: {1:mm\\:ss}", exitCode, sw.Elapsed));
                return false;
            }
        }

        public static void CreatePortablePackage(string targetDir, string triple, BuildProfile profile)
        {
            try
            {
                string profileDir = profile == BuildProfile.FastRelease ? "fast-release" : (profile == BuildProfile.Debug ? "debug" : "release");
                string[] candidateExes = new string[]
                {
                    Path.Combine(targetDir, "target", triple, profileDir, "clash-verge.exe"),
                    Path.Combine(targetDir, "src-tauri", "target", triple, profileDir, "clash-verge.exe"),
                    Path.Combine(targetDir, "target", profileDir, "clash-verge.exe"),
                    Path.Combine(targetDir, "src-tauri", "target", profileDir, "clash-verge.exe")
                };

                string sourceExe = null;
                foreach (var c in candidateExes)
                {
                    if (File.Exists(c)) { sourceExe = c; break; }
                }

                if (string.IsNullOrEmpty(sourceExe))
                {
                    ConsoleHelper.LogError("便携包打包失败: 未能定位到编译完成的 clash-verge.exe！");
                    return;
                }

                string pkgVersion = "2.5.6";
                string pkgJsonPath = Path.Combine(targetDir, "package.json");
                if (File.Exists(pkgJsonPath))
                {
                    try
                    {
                        string content = File.ReadAllText(pkgJsonPath, Encoding.UTF8);
                        int idx = content.IndexOf("\"version\":");
                        if (idx != -1)
                        {
                            int start = content.IndexOf('"', idx + 10);
                            int end = content.IndexOf('"', start + 1);
                            if (start != -1 && end != -1) pkgVersion = content.Substring(start + 1, end - start - 1);
                        }
                    }
                    catch { }
                }

                string portableRootDir = Path.Combine(targetDir, "target", "portable");
                Directory.CreateDirectory(portableRootDir);

                string folderName = string.Format("Clash.Verge_{0}_{1}_portable", pkgVersion, triple);
                string portableDir = Path.Combine(portableRootDir, folderName);
                if (Directory.Exists(portableDir)) Directory.Delete(portableDir, true);
                Directory.CreateDirectory(portableDir);

                // 1. Copy main executable
                File.Copy(sourceExe, Path.Combine(portableDir, "clash-verge.exe"), true);

                // 2. Touch .portable marker file
                File.WriteAllText(Path.Combine(portableDir, ".portable"), "", Encoding.UTF8);

                // 3. Copy sidecars
                string sidecarSrc = Path.Combine(targetDir, "src-tauri", "sidecar");
                string sidecarDest = Path.Combine(portableDir, "sidecar");
                Directory.CreateDirectory(sidecarDest);
                if (Directory.Exists(sidecarSrc))
                {
                    foreach (var f in Directory.GetFiles(sidecarSrc, "*.*"))
                    {
                        string fname = Path.GetFileName(f);
                        File.Copy(f, Path.Combine(sidecarDest, fname), true);
                        if (fname.StartsWith("verge-mihomo-") && fname.EndsWith(".exe") && !fname.Contains("alpha"))
                        {
                            File.Copy(f, Path.Combine(sidecarDest, "verge-mihomo.exe"), true);
                        }
                        else if (fname.StartsWith("verge-mihomo-alpha-") && fname.EndsWith(".exe"))
                        {
                            File.Copy(f, Path.Combine(sidecarDest, "verge-mihomo-alpha.exe"), true);
                        }
                    }
                }

                // 4. Copy resources
                string resSrc = Path.Combine(targetDir, "src-tauri", "resources");
                string resDest = Path.Combine(portableDir, "resources");
                Directory.CreateDirectory(resDest);
                if (Directory.Exists(resSrc))
                {
                    foreach (var f in Directory.GetFiles(resSrc, "*.*"))
                    {
                        File.Copy(f, Path.Combine(resDest, Path.GetFileName(f)), true);
                    }
                }

                // 5. Compress to ZIP using PowerShell Compress-Archive
                string zipPath = Path.Combine(portableRootDir, folderName + ".zip");
                if (File.Exists(zipPath)) File.Delete(zipPath);

                string psCmd = string.Format("Compress-Archive -Path '{0}\\*' -DestinationPath '{1}' -Force", portableDir, zipPath);
                int zipCode = RunProcessLive("powershell.exe", "-NoProfile -Command \"" + psCmd + "\"", targetDir);

                if (zipCode == 0 && File.Exists(zipPath))
                {
                    FileInfo fi = new FileInfo(zipPath);
                    ConsoleHelper.LogSuccess(string.Format("绿色便携版 ZIP 压缩包创建成功: {0} ({1})", fi.Name, ConsoleHelper.FormatFileSize(fi.Length)));
                }
                else
                {
                    ConsoleHelper.LogSuccess("便携免安装目录准备完成: " + portableDir);
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.LogError("便携包创建异常: " + ex.Message);
            }
        }

        public static List<FileInfo> ScanArtifacts(string targetDir)
        {
            List<FileInfo> artifacts = new List<FileInfo>();
            string[] searchDirs = new string[]
            {
                Path.Combine(targetDir, "target", "portable"),
                Path.Combine(targetDir, "src-tauri", "target"),
                Path.Combine(targetDir, "target")
            };

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var baseDir in searchDirs)
            {
                if (!Directory.Exists(baseDir)) continue;
                try
                {
                    DirectoryInfo di = new DirectoryInfo(baseDir);
                    var files = di.GetFiles("*.*", SearchOption.AllDirectories);
                    foreach (var f in files)
                    {
                        string ext = f.Extension.ToLowerInvariant();
                        if (ext == ".exe" || ext == ".msi" || ext == ".zip" || ext == ".7z")
                        {
                            string name = f.Name.ToLowerInvariant();
                            // Filter out intermediate cargo build script outputs and compiler temp files
                            if (name.Contains("build_script_build") || name.StartsWith("test-") || name.EndsWith(".d") ||
                                name.Contains("fingerprint") || name.Contains("deps\\") || f.FullName.Contains("\\deps\\") ||
                                f.FullName.Contains("\\build\\") || f.FullName.Contains("\\incremental\\"))
                                continue;

                            // Only include clash-verge executables, setup installers, and portable packages
                            if (!name.Contains("clash") && !name.Contains("verge") && !ext.Contains("zip"))
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

            ConsoleHelper.LogHeader("生成产物列表");
            ConsoleHelper.WriteLineColor("--------------------------------------------------------------------------------", ConsoleColor.DarkGray);
            Console.WriteLine(string.Format("{0,-35} | {1,-10} | {2,-19} | {3}", "文件名", "体积", "生成时间", "完整路径"));
            ConsoleHelper.WriteLineColor("--------------------------------------------------------------------------------", ConsoleColor.DarkGray);

            foreach (var f in artifacts)
            {
                string sizeStr = ConsoleHelper.FormatFileSize(f.Length);
                string timeStr = f.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                Console.WriteLine(string.Format("{0,-35} | {1,-10} | {2,-19} | {3}", f.Name, sizeStr, timeStr, f.FullName));
            }
            ConsoleHelper.WriteLineColor("--------------------------------------------------------------------------------\n", ConsoleColor.DarkGray);
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
