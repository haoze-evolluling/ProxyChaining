using System;
using System.Collections.Generic;
using System.IO;

namespace CvrProxyChainPatcher
{
    public static partial class PatchRules
    {
        private static List<PatchRule> GetUpdateRules()
        {
            return new List<PatchRule>
            {
                new PatchRule
                {
                    Id = "disable-update-service",
                    RelPath = Path.Combine("src", "services", "update.ts"),
                    Description = "屏蔽在线检查更新服务与请求，直接返回最新状态，防止拉取官方更新包",
                    Chunks = new List<PatchChunk>
                    {
                        new PatchChunk
                        {
                            Name = "checkUpdateSafe 默认屏蔽检查更新请求并返回 null",
                            Find = "export const checkUpdateSafe = async (\n" +
                                   "  options?: CheckOptions,\n" +
                                   "): Promise<Update | null> => {\n" +
                                   "  const result = await check(options ?? {})\n" +
                                   "  if (!result) return null",
                            Replace = "export const checkUpdateSafe = async (\n" +
                                      "  _options?: CheckOptions,\n" +
                                      "): Promise<Update | null> => {\n" +
                                      "  // [CVR-PATCH] 默认屏蔽在线检查更新，防止官方更新覆盖自定义构建版本\n" +
                                      "  return null\n" +
                                      "}\n" +
                                      "\n" +
                                      "export const checkUpdateInternal = async (\n" +
                                      "  options?: CheckOptions,\n" +
                                      "): Promise<Update | null> => {\n" +
                                      "  const result = await check(options ?? {})\n" +
                                      "  if (!result) return null"
                        }
                    }
                },
                new PatchRule
                {
                    Id = "disable-update-hook",
                    RelPath = Path.Combine("src", "hooks", "use-update.ts"),
                    Description = "默认关闭前端自动检查更新逻辑与定时请求，避免覆盖自定义版本",
                    Chunks = new List<PatchChunk>
                    {
                        new PatchChunk
                        {
                            Name = "shouldCheck 默认仅在显式为 true 时触发更新",
                            Find = "  const { verge } = useVerge()\n" +
                                   "  const { auto_check_update } = verge || {}\n" +
                                   "\n" +
                                   "  const shouldCheck = enabled && auto_check_update !== false",
                            Replace = "  const { verge } = useVerge()\n" +
                                      "  const { auto_check_update } = verge || {}\n" +
                                      "\n" +
                                      "  // [CVR-PATCH] 默认关闭自动检查更新，避免官方升级覆盖自定义补丁版本\n" +
                                      "  const shouldCheck = enabled && auto_check_update === true"
                        }
                    }
                },
                new PatchRule
                {
                    Id = "disable-update-ui",
                    RelPath = Path.Combine("src", "components", "setting", "mods", "misc-viewer.tsx"),
                    Description = "将设置中心杂项中的自动检查更新开关默认值设为关闭 (false)",
                    Chunks = new List<PatchChunk>
                    {
                        new PatchChunk
                        {
                            Name = "初始状态中 autoCheckUpdate 默认为 false",
                            Find = "    appLogMaxCount: 12,\n" +
                                   "    autoCloseConnection: true,\n" +
                                   "    autoCheckUpdate: true,\n" +
                                   "    enableBuiltinEnhanced: true,",
                            Replace = "    appLogMaxCount: 12,\n" +
                                      "    autoCloseConnection: true,\n" +
                                      "    // [CVR-PATCH] 默认关闭自动检查更新\n" +
                                      "    autoCheckUpdate: false,\n" +
                                      "    enableBuiltinEnhanced: true,"
                        },
                        new PatchChunk
                        {
                            Name = "配置回退中 autoCheckUpdate 默认为 false",
                            Find = "        autoCloseConnection: verge?.auto_close_connection ?? true,\n" +
                                   "        autoCheckUpdate: verge?.auto_check_update ?? true,\n" +
                                   "        enableBuiltinEnhanced: verge?.enable_builtin_enhanced ?? true,",
                            Replace = "        autoCloseConnection: verge?.auto_close_connection ?? true,\n" +
                                      "        // [CVR-PATCH] 默认关闭自动检查更新\n" +
                                      "        autoCheckUpdate: verge?.auto_check_update ?? false,\n" +
                                      "        enableBuiltinEnhanced: verge?.enable_builtin_enhanced ?? true,"
                        }
                    }
                },
                new PatchRule
                {
                    Id = "disable-update-config",
                    RelPath = Path.Combine("src-tauri", "src", "config", "verge.rs"),
                    Description = "将后端配置默认 auto_check_update 设为 false",
                    Chunks = new List<PatchChunk>
                    {
                        new PatchChunk
                        {
                            Name = "后端 verge.rs 默认 auto_check_update 设为 false",
                            Find = "            auto_close_connection: Some(true),\n" +
                                   "            auto_check_update: Some(true),\n" +
                                   "            enable_builtin_enhanced: Some(true),",
                            Replace = "            auto_close_connection: Some(true),\n" +
                                      "            // [CVR-PATCH] 默认关闭自动检查更新\n" +
                                      "            auto_check_update: Some(false),\n" +
                                      "            enable_builtin_enhanced: Some(true),"
                        }
                    }
                },
                new PatchRule
                {
                    Id = "disable-update-silent",
                    RelPath = Path.Combine("src-tauri", "src", "core", "updater.rs"),
                    Description = "静默更新检测默认关闭，且在启动时清理残留缓存",
                    Chunks = new List<PatchChunk>
                    {
                        new PatchChunk
                        {
                            Name = "SilentUpdater check_and_download 默认不自动下载更新",
                            Find = "        let auto_check = Config::verge().await.latest_arc().auto_check_update.unwrap_or(true);\n" +
                                   "        if !auto_check {\n" +
                                   "            logging!(debug, Type::System, \"Silent update skipped: auto_check_update is false\");\n" +
                                   "            return Ok(());\n" +
                                   "        }",
                            Replace = "        // [CVR-PATCH] 默认不自动执行后台静默检查与下载更新\n" +
                                      "        let auto_check = Config::verge().await.latest_arc().auto_check_update.unwrap_or(false);\n" +
                                      "        if !auto_check {\n" +
                                      "            logging!(debug, Type::System, \"Silent update skipped: auto_check_update is false\");\n" +
                                      "            return Ok(());\n" +
                                      "        }"
                        }
                    }
                }
            };
        }
    }
}
