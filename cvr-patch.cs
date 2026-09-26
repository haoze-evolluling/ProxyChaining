using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CvrProxyChainPatcher
{
    class PatchChunk
    {
        public string Name { get; set; }
        public string Find { get; set; }
        public string Replace { get; set; }
    }

    class PatchRule
    {
        public string Id { get; set; }
        public string RelPath { get; set; }
        public string Description { get; set; }
        public List<PatchChunk> Chunks { get; set; }
    }

    class Program
    {
        const string DEFAULT_CVR_DIR = @"C:\Users\leehaoze\clash-verge-rev-dev";

        static readonly List<PatchRule> Rules = new List<PatchRule>
        {
            new PatchRule
            {
                Id = "proxy-groups-chain",
                RelPath = Path.Combine("src", "components", "proxy", "proxy-groups-chain.tsx"),
                Description = "解除切换分组时清空代理链的限制，并使用全量候选节点避免跨订阅节点置灰",
                Chunks = new List<PatchChunk>
                {
                    new PatchChunk
                    {
                        Name = "使用全量节点作为候选节点，防止切换分组时已选节点失去 recordId",
                        Find = "  const candidateNodes = useMemo(\n" +
                               "    () =>\n" +
                               "      renderList.flatMap((item) => {\n" +
                               "        const occurrences = item.memberCol ?? (item.member ? [item.member] : [])\n" +
                               "        return occurrences.flatMap(({ member }) =>\n" +
                               "          member.kind === 'node' ? [member.node] : [],\n" +
                               "        )\n" +
                               "      }),\n" +
                               "    [renderList],\n" +
                               "  )\n" +
                               "\n" +
                               "  const currentProxyChain = useMemo(\n" +
                               "    () =>\n" +
                               "      proxyView\n" +
                               "        ? rebindProxyChainItems(proxyChain, candidateNodes, proxyView)\n" +
                               "        : proxyChain.map((item) => ({\n" +
                               "            ...item,\n" +
                               "            recordId: undefined,\n" +
                               "            delay: undefined,\n" +
                               "          })),\n" +
                               "    [candidateNodes, proxyChain, proxyView],\n" +
                               "  )",
                        Replace = "  const candidateNodes = useMemo(\n" +
                                  "    () =>\n" +
                                  "      renderList.flatMap((item) => {\n" +
                                  "        const occurrences = item.memberCol ?? (item.member ? [item.member] : [])\n" +
                                  "        return occurrences.flatMap(({ member }) =>\n" +
                                  "          member.kind === 'node' ? [member.node] : [],\n" +
                                  "        )\n" +
                                  "      }),\n" +
                                  "    [renderList],\n" +
                                  "  )\n" +
                                  "\n" +
                                  "  // [CVR-PATCH] 全量候选节点：解除跨组/跨订阅时节点因不在当前视图列表而被置灰的问题\n" +
                                  "  const allCandidateNodes = useMemo(\n" +
                                  "    () => (proxyView ? Object.values(proxyView.records) : candidateNodes),\n" +
                                  "    [proxyView, candidateNodes],\n" +
                                  "  )\n" +
                                  "\n" +
                                  "  const currentProxyChain = useMemo(\n" +
                                  "    () =>\n" +
                                  "      proxyView\n" +
                                  "        ? rebindProxyChainItems(proxyChain, allCandidateNodes, proxyView)\n" +
                                  "        : proxyChain.map((item) => ({\n" +
                                  "            ...item,\n" +
                                  "            recordId: undefined,\n" +
                                  "            delay: undefined,\n" +
                                  "          })),\n" +
                                  "    [allCandidateNodes, proxyChain, proxyView],\n" +
                                  "  )"
                    },
                    new PatchChunk
                    {
                        Name = "移除切换代理组时强制清空代理链的代码",
                        Find = "  const handleGroupSelect = (groupName: string) => {\n" +
                               "    onGroupSelect(groupName)\n" +
                               "    handleGroupMenuClose()\n" +
                               "\n" +
                               "    if (mode === 'rule') {\n" +
                               "      updateProxyChainConfigInRuntime(null)\n" +
                               "      localStorage.removeItem('proxy-chain-group')\n" +
                               "      localStorage.removeItem('proxy-chain-exit-node')\n" +
                               "      localStorage.removeItem('proxy-chain-items')\n" +
                               "      setProxyChain([])\n" +
                               "    }\n" +
                               "  }",
                        Replace = "  const handleGroupSelect = (groupName: string) => {\n" +
                                  "    onGroupSelect(groupName)\n" +
                                  "    handleGroupMenuClose()\n" +
                                  "\n" +
                                  "    // [CVR-PATCH] 跨订阅支持：切换分组时不自动清空代理链，允许将不同订阅/分组的节点加入同一条链\n" +
                                  "  }"
                    },
                    new PatchChunk
                    {
                        Name = "handleChangeProxy 添加节点时使用 allCandidateNodes 重新绑定",
                        Find = "  const handleChangeProxy = useCallback(\n" +
                               "    (_group: ProxyGroupView, member: ResolvedProxyMember) => {\n" +
                               "      if (!isInteractableMember(member) || member.kind !== 'node') return\n" +
                               "      const { node } = member\n" +
                               "      setProxyChain((prev) => {\n" +
                               "        const current = proxyView\n" +
                               "          ? rebindProxyChainItems(prev, candidateNodes, proxyView)\n" +
                               "          : prev",
                        Replace = "  const handleChangeProxy = useCallback(\n" +
                                  "    (_group: ProxyGroupView, member: ResolvedProxyMember) => {\n" +
                                  "      if (!isInteractableMember(member) || member.kind !== 'node') return\n" +
                                  "      const { node } = member\n" +
                                  "      setProxyChain((prev) => {\n" +
                                  "        // [CVR-PATCH] 跨订阅支持：使用全局节点列表重新绑定，防止上一个分组的节点丢失 recordId\n" +
                                  "        const current = proxyView\n" +
                                  "          ? rebindProxyChainItems(prev, allCandidateNodes, proxyView)\n" +
                                  "          : prev"
                    },
                    new PatchChunk
                    {
                        Name = "handleChangeProxy 依赖项补充 allCandidateNodes",
                        Find = "    [candidateNodes, proxyView, t],\n" +
                               "  )",
                        Replace = "    [allCandidateNodes, candidateNodes, proxyView, t],\n" +
                                  "  )"
                    }
                }
            },
            new PatchRule
            {
                Id = "proxy-chain",
                RelPath = Path.Combine("src", "components", "proxy", "proxy-chain.tsx"),
                Description = "在代理链面板中全局寻找候选节点，避免置灰；并在连接时智能匹配出口节点所属代理组",
                Chunks = new List<PatchChunk>
                {
                    new PatchChunk
                    {
                        Name = "候选节点范围扩大为全部 ProxyNodeView，彻底解除置灰与禁用",
                        Find = "  const candidates = useMemo(() => {\n" +
                               "    if (!proxyView) return []\n" +
                               "    if (mode === 'rule' && selectedGroup) {\n" +
                               "      return selectRuleChainMembers(proxyView, selectedGroup).flatMap(\n" +
                               "        ({ member }) => (member.kind === 'node' ? [member.node] : []),\n" +
                               "      )\n" +
                               "    }\n" +
                               "    if (!runtimeConfig) return []\n" +
                               "    const runtimeProxies = (\n" +
                               "      runtimeConfig as RuntimeConfigWithProxySequence | null\n" +
                               "    )?.proxies\n" +
                               "    return selectGlobalChainNodes(proxyView, runtimeProxies)\n" +
                               "  }, [mode, proxyView, runtimeConfig, selectedGroup])",
                        Replace = "  // [CVR-PATCH] 跨订阅支持：候选节点包含 proxyView 中的全部节点，避免跨组/跨订阅节点 recordId 丢失而置灰\n" +
                                  "  const candidates = useMemo(() => {\n" +
                                  "    if (!proxyView) return []\n" +
                                  "    return Object.values(proxyView.records)\n" +
                                  "  }, [proxyView])"
                    },
                    new PatchChunk
                    {
                        Name = "isConnected 判断支持跨组记录的 proxy-chain-group",
                        Find = "    if (!selectedGroup) {\n" +
                               "      return false\n" +
                               "    }\n" +
                               "\n" +
                               "    const proxyChainGroup = proxyView.groups.find(\n" +
                               "      (group) => group.name === selectedGroup,\n" +
                               "    )\n" +
                               "\n" +
                               "    return proxyChainGroup?.now === lastNode.name\n" +
                               "  }, [proxyView, currentProxyChain, mode, selectedGroup])",
                        Replace = "    if (!selectedGroup) {\n" +
                                  "      return false\n" +
                                  "    }\n" +
                                  "\n" +
                                  "    // [CVR-PATCH] 优先使用存储的代理链连接组，防止切换浏览其他分组时连接状态判断错误\n" +
                                  "    const chainGroupName =\n" +
                                  "      localStorage.getItem('proxy-chain-group') || selectedGroup\n" +
                                  "    const proxyChainGroup = proxyView.groups.find(\n" +
                                  "      (group) => group.name === chainGroupName,\n" +
                                  "    )\n" +
                                  "\n" +
                                  "    return proxyChainGroup?.now === lastNode.name\n" +
                                  "  }, [proxyView, currentProxyChain, mode, selectedGroup])"
                    },
                    new PatchChunk
                    {
                        Name = "handleConnect 动态解析出口节点所属代理组，避免跨订阅连接报错",
                        Find = "      // 根据模式确定使用的代理组名称\n" +
                               "      if (mode !== 'global' && !selectedGroup) {\n" +
                               "        throw new Error('规则模式下必须选择代理组')\n" +
                               "      }\n" +
                               "\n" +
                               "      const targetGroup = mode === 'global' ? 'GLOBAL' : selectedGroup\n" +
                               "\n" +
                               "      await selectNodeForGroup(targetGroup || 'GLOBAL', lastNode.name)\n" +
                               "      // The chain moves the group like any other selection, so the profile has to learn about\n" +
                               "      // it: what the profile holds is what gets re-applied the next time the core starts.\n" +
                               "      recordSelection(targetGroup || 'GLOBAL', lastNode.name)\n" +
                               "      localStorage.setItem('proxy-chain-group', targetGroup || 'GLOBAL')\n" +
                               "      localStorage.setItem('proxy-chain-exit-node', lastNode.name)",
                        Replace = "      // 根据模式确定使用的代理组名称\n" +
                                  "      if (mode !== 'global' && !selectedGroup) {\n" +
                                  "        throw new Error('规则模式下必须选择代理组')\n" +
                                  "      }\n" +
                                  "\n" +
                                  "      let targetGroup = mode === 'global' ? 'GLOBAL' : selectedGroup\n" +
                                  "\n" +
                                  "      // [CVR-PATCH] 跨订阅支持：如果当前 selectedGroup 不包含出口节点，自动寻找包含出口节点的代理组\n" +
                                  "      if (mode !== 'global' && proxyView) {\n" +
                                  "        const currentGroupObj = proxyView.groups.find(\n" +
                                  "          (g) => g.name === targetGroup,\n" +
                                  "        )\n" +
                                  "        const hasNode = currentGroupObj?.members.some(\n" +
                                  "          (m) => m.name === lastNode.name,\n" +
                                  "        )\n" +
                                  "        if (!hasNode) {\n" +
                                  "          const matchingGroup = proxyView.groups.find((g) =>\n" +
                                  "            g.members.some((m) => m.name === lastNode.name),\n" +
                                  "          )\n" +
                                  "          if (matchingGroup) {\n" +
                                  "            targetGroup = matchingGroup.name\n" +
                                  "          }\n" +
                                  "        }\n" +
                                  "      }\n" +
                                  "\n" +
                                  "      await selectNodeForGroup(targetGroup || 'GLOBAL', lastNode.name)\n" +
                                  "      // The chain moves the group like any other selection, so the profile has to learn about\n" +
                                  "      // it: what the profile holds is what gets re-applied the next time the core starts.\n" +
                                  "      recordSelection(targetGroup || 'GLOBAL', lastNode.name)\n" +
                                  "      localStorage.setItem('proxy-chain-group', targetGroup || 'GLOBAL')\n" +
                                  "      localStorage.setItem('proxy-chain-exit-node', lastNode.name)"
                    }
                }
            },
            new PatchRule
            {
                Id = "proxy-view-types",
                RelPath = Path.Combine("src", "types", "proxy-view.ts"),
                Description = "解除全局链式代理节点仅允许 core 来源的限制，使订阅提供者节点在全局模式下可用",
                Chunks = new List<PatchChunk>
                {
                    new PatchChunk
                    {
                        Name = "selectGlobalChainNodes 包含全部节点",
                        Find = "export const selectGlobalChainNodes = (\n" +
                               "  view: ProxyViewV1,\n" +
                               "  runtimeProxies: unknown,\n" +
                               ") =>\n" +
                               "  view.global === null ? [] : selectRuntimeStandaloneNodes(view, runtimeProxies)",
                        Replace = "export const selectGlobalChainNodes = (\n" +
                                  "  view: ProxyViewV1,\n" +
                                  "  runtimeProxies: unknown,\n" +
                                  ") => {\n" +
                                  "  if (view.global === null) return []\n" +
                                  "  // [CVR-PATCH] 跨订阅支持：全局模式下展示全部可用节点，允许订阅节点参与全局链式代理\n" +
                                  "  const allNodes = Object.values(view.records)\n" +
                                  "  return allNodes.length > 0\n" +
                                  "    ? allNodes\n" +
                                  "    : selectRuntimeStandaloneNodes(view, runtimeProxies)\n" +
                                  "}"
                    }
                }
            },
            new PatchRule
            {
                Id = "runtime-backend",
                RelPath = Path.Combine("src-tauri", "src", "config", "runtime.rs"),
                Description = "增强后端运行时链式代理更新逻辑，支持从 proxy-providers 订阅文件中自动查找节点并注入 dialer-proxy",
                Chunks = new List<PatchChunk>
                {
                    new PatchChunk
                    {
                        Name = "update_proxy_chain_config 支持跨订阅/provider 节点识别与注入",
                        Find = "    /// Rebuilds `dialer-proxy` links from an ordered proxy chain, or removes them for `None`.\n" +
                               "    #[inline]\n" +
                               "    pub fn update_proxy_chain_config(&mut self, proxy_chain_config: Option<Value>) {\n" +
                               "        let config = if let Some(config) = self.config.as_mut() {\n" +
                               "            config\n" +
                               "        } else {\n" +
                               "            return;\n" +
                               "        };\n" +
                               "\n" +
                               "        if let Some(Value::Sequence(proxies)) = config.get_mut(\"proxies\") {\n" +
                               "            proxies.iter_mut().for_each(|proxy| {\n" +
                               "                if let Some(proxy) = proxy.as_mapping_mut()\n" +
                               "                    && proxy.get(\"dialer-proxy\").is_some()\n" +
                               "                {\n" +
                               "                    proxy.remove(\"dialer-proxy\");\n" +
                               "                }\n" +
                               "            });\n" +
                               "        }\n" +
                               "\n" +
                               "        if let Some(Value::Sequence(dialer_proxies)) = proxy_chain_config\n" +
                               "            && let Some(Value::Sequence(proxies)) = config.get_mut(\"proxies\")\n" +
                               "        {\n" +
                               "            for (i, dialer_proxy) in dialer_proxies.iter().enumerate() {\n" +
                               "                if let Some(Value::Mapping(proxy)) =\n" +
                               "                    proxies.iter_mut().find(|proxy| proxy.get(\"name\") == Some(dialer_proxy))\n" +
                               "                    && i != 0\n" +
                               "                    && let Some(dialer_proxy) = dialer_proxies.get(i - 1)\n" +
                               "                {\n" +
                               "                    proxy.insert(\"dialer-proxy\".into(), dialer_proxy.to_owned());\n" +
                               "                }\n" +
                               "            }\n" +
                               "        }\n" +
                               "    }",
                        Replace = "    /// Rebuilds `dialer-proxy` links from an ordered proxy chain, or removes them for `None`.\n" +
                                  "    #[inline]\n" +
                                  "    pub fn update_proxy_chain_config(&mut self, proxy_chain_config: Option<Value>) {\n" +
                                  "        let config = if let Some(config) = self.config.as_mut() {\n" +
                                  "            config\n" +
                                  "        } else {\n" +
                                  "            return;\n" +
                                  "        };\n" +
                                  "\n" +
                                  "        // [CVR-PATCH] 清理之前注入的临时跨订阅代理节点与 dialer-proxy\n" +
                                  "        if let Some(Value::Sequence(proxies)) = config.get_mut(\"proxies\") {\n" +
                                  "            proxies.retain(|proxy| {\n" +
                                  "                proxy.get(\"_injected_chain_proxy\").and_then(Value::as_bool) != Some(true)\n" +
                                  "            });\n" +
                                  "            proxies.iter_mut().for_each(|proxy| {\n" +
                                  "                if let Some(proxy) = proxy.as_mapping_mut()\n" +
                                  "                    && proxy.get(\"dialer-proxy\").is_some()\n" +
                                  "                {\n" +
                                  "                    proxy.remove(\"dialer-proxy\");\n" +
                                  "                }\n" +
                                  "            });\n" +
                                  "        }\n" +
                                  "\n" +
                                  "        if let Some(Value::Sequence(dialer_proxies)) = proxy_chain_config {\n" +
                                  "            if !config.contains_key(\"proxies\") {\n" +
                                  "                config.insert(\"proxies\".into(), Value::Sequence(Vec::new()));\n" +
                                  "            }\n" +
                                  "\n" +
                                  "            // [CVR-PATCH] 如果链中有节点不在主配置的 proxies 中（例如来自 proxy-providers），从 provider 文件中查找并注入\n" +
                                  "            let app_home = crate::utils::dirs::app_home_dir().ok();\n" +
                                  "            if let Some(Value::Mapping(providers)) = config.get(\"proxy-providers\") {\n" +
                                  "                let provider_paths: Vec<std::path::PathBuf> = providers\n" +
                                  "                    .values()\n" +
                                  "                    .filter_map(|p| p.get(\"path\").and_then(Value::as_str))\n" +
                                  "                    .filter_map(|p| {\n" +
                                  "                        let path = std::path::Path::new(p);\n" +
                                  "                        if path.is_absolute() {\n" +
                                  "                            Some(path.to_path_buf())\n" +
                                  "                        } else {\n" +
                                  "                            app_home.as_ref().map(|home| home.join(path))\n" +
                                  "                        }\n" +
                                  "                    })\n" +
                                  "                    .collect();\n" +
                                  "\n" +
                                  "                if let Some(Value::Sequence(proxies)) = config.get_mut(\"proxies\") {\n" +
                                  "                    for (i, dialer_proxy) in dialer_proxies.iter().enumerate() {\n" +
                                  "                        if i == 0 {\n" +
                                  "                            continue;\n" +
                                  "                        }\n" +
                                  "                        let exists = proxies.iter().any(|p| p.get(\"name\") == Some(dialer_proxy));\n" +
                                  "                        if !exists {\n" +
                                  "                            for path in &provider_paths {\n" +
                                  "                                if let Ok(content) = std::fs::read_to_string(path)\n" +
                                  "                                    && let Ok(provider_yaml) = serde_yaml_ng::from_str::<Mapping>(&content)\n" +
                                  "                                    && let Some(Value::Sequence(p_nodes)) = provider_yaml.get(\"proxies\")\n" +
                                  "                                {\n" +
                                  "                                    if let Some(found_node) = p_nodes.iter().find(|n| n.get(\"name\") == Some(dialer_proxy)) {\n" +
                                  "                                        if let Some(mut cloned_map) = found_node.as_mapping().cloned() {\n" +
                                  "                                            if let Some(prev_proxy) = dialer_proxies.get(i - 1) {\n" +
                                  "                                                cloned_map.insert(\"dialer-proxy\".into(), prev_proxy.to_owned());\n" +
                                  "                                                cloned_map.insert(\"_injected_chain_proxy\".into(), Value::Bool(true));\n" +
                                  "                                                proxies.push(Value::Mapping(cloned_map));\n" +
                                  "                                                break;\n" +
                                  "                                            }\n" +
                                  "                                        }\n" +
                                  "                                    }\n" +
                                  "                                }\n" +
                                  "                            }\n" +
                                  "                        }\n" +
                                  "                    }\n" +
                                  "                }\n" +
                                  "            }\n" +
                                  "\n" +
                                  "            if let Some(Value::Sequence(proxies)) = config.get_mut(\"proxies\") {\n" +
                                  "                for (i, dialer_proxy) in dialer_proxies.iter().enumerate() {\n" +
                                  "                    if let Some(Value::Mapping(proxy)) =\n" +
                                  "                        proxies.iter_mut().find(|proxy| proxy.get(\"name\") == Some(dialer_proxy))\n" +
                                  "                        && i != 0\n" +
                                  "                        && let Some(dialer_proxy) = dialer_proxies.get(i - 1)\n" +
                                  "                    {\n" +
                                  "                        proxy.insert(\"dialer-proxy\".into(), dialer_proxy.to_owned());\n" +
                                  "                    }\n" +
                                  "                }\n" +
                                  "            }\n" +
                                  "        }\n" +
                                  "    }"
                    }
                }
            }
        };

        static void WriteColor(string text, ConsoleColor color)
        {
            var old = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ForegroundColor = old;
        }

        static void WriteLineColor(string text, ConsoleColor color)
        {
            var old = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = old;
        }

        static void LogInfo(string msg)
        {
            WriteColor("[INFO] ", ConsoleColor.Cyan);
            Console.WriteLine(msg);
        }

        static void LogSuccess(string msg)
        {
            WriteColor("[SUCCESS] ", ConsoleColor.Green);
            Console.WriteLine(msg);
        }

        static void LogWarn(string msg)
        {
            WriteColor("[WARN] ", ConsoleColor.Yellow);
            Console.WriteLine(msg);
        }

        static void LogError(string msg)
        {
            WriteColor("[ERROR] ", ConsoleColor.Red);
            Console.WriteLine(msg);
        }

        static void LogHeader(string msg)
        {
            Console.WriteLine();
            WriteColor("=== " + msg + " ===", ConsoleColor.Cyan);
            Console.WriteLine("\n");
        }

        static string NormalizeLineEndings(string text)
        {
            return text.Replace("\r\n", "\n");
        }

        static string DetectLineEnding(string text)
        {
            return text.Contains("\r\n") ? "\r\n" : "\n";
        }

        static string ApplyLineEnding(string text, string eol)
        {
            return text.Replace("\r\n", "\n").Replace("\n", eol);
        }

        static bool IsClashVergeDir(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
            string proxyGroups = Path.Combine(dir, "src", "components", "proxy", "proxy-groups-chain.tsx");
            string tauri = Path.Combine(dir, "src-tauri");
            return File.Exists(proxyGroups) && Directory.Exists(tauri);
        }

        static string ResolveTargetDir(string customDir)
        {
            // 1. Explicit argument
            if (!string.IsNullOrEmpty(customDir))
            {
                if (!Directory.Exists(customDir))
                    throw new DirectoryNotFoundException("指定的目录不存在: " + customDir);
                if (!IsClashVergeDir(customDir))
                    throw new InvalidOperationException("指定目录不是 Clash Verge Rev 仓库 (缺少 package.json/src-tauri): " + customDir);
                return Path.GetFullPath(customDir);
            }

            // 2. Environment variable
            string envDir = Environment.GetEnvironmentVariable("CVR_DIR");
            if (!string.IsNullOrEmpty(envDir) && IsClashVergeDir(envDir))
            {
                return Path.GetFullPath(envDir);
            }

            // 3. Current Directory
            string current = Directory.GetCurrentDirectory();
            if (IsClashVergeDir(current))
            {
                return Path.GetFullPath(current);
            }

            // 4. Parent Directory
            string parent = Path.GetFullPath(Path.Combine(current, ".."));
            if (IsClashVergeDir(parent))
            {
                return parent;
            }

            // 5. Default directory
            if (IsClashVergeDir(DEFAULT_CVR_DIR))
            {
                return Path.GetFullPath(DEFAULT_CVR_DIR);
            }

            return null;
        }

        enum RuleStatus
        {
            APPLIED,
            NOT_APPLIED,
            MODIFIED_OR_PARTIAL,
            MISSING
        }

        static RuleStatus GetRuleStatus(string targetDir, PatchRule rule, out string reason)
        {
            reason = null;
            string fullPath = Path.Combine(targetDir, rule.RelPath);
            if (!File.Exists(fullPath))
            {
                reason = "文件不存在";
                return RuleStatus.MISSING;
            }

            string content = NormalizeLineEndings(File.ReadAllText(fullPath, Encoding.UTF8));
            bool allPatched = true;
            bool allOriginal = true;

            foreach (var chunk in rule.Chunks)
            {
                string normFind = NormalizeLineEndings(chunk.Find);
                string normReplace = NormalizeLineEndings(chunk.Replace);

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

        static void CmdStatus(string targetDir)
        {
            LogHeader("检查补丁状态: " + targetDir);

            int appliedCount = 0;
            int total = Rules.Count;

            foreach (var rule in Rules)
            {
                string reason;
                RuleStatus status = GetRuleStatus(targetDir, rule, out reason);
                ConsoleColor color = ConsoleColor.Yellow;
                if (status == RuleStatus.APPLIED)
                {
                    color = ConsoleColor.Green;
                    appliedCount++;
                }
                else if (status == RuleStatus.NOT_APPLIED)
                {
                    color = ConsoleColor.Gray;
                }
                else if (status == RuleStatus.MISSING)
                {
                    color = ConsoleColor.Red;
                }

                Console.Write("  [");
                WriteColor(status.ToString(), color);
                Console.WriteLine("] " + rule.RelPath);
                Console.Write("          ");
                WriteLineColor(rule.Description, ConsoleColor.DarkGray);
                if (!string.IsNullOrEmpty(reason))
                {
                    Console.Write("          ");
                    WriteLineColor(reason, ConsoleColor.Red);
                }
            }

            Console.WriteLine();
            if (appliedCount == total)
            {
                LogSuccess(string.Format("全部 {0} 个补丁模块均已注入 (跨订阅链式代理已启用)！", total));
            }
            else if (appliedCount == 0)
            {
                LogInfo("尚未注入任何补丁模块 (代码处于原版初始状态)。可运行 'inject' 执行注入。");
            }
            else
            {
                LogWarn(string.Format("部分应用: 已注入 {0}/{1} 个模块。", appliedCount, total));
            }
        }

        static void CmdInject(string targetDir)
        {
            LogHeader("正在注入补丁至: " + targetDir);

            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss-fffZ");
            string backupDir = Path.Combine(exeDir, "backups", timestamp);
            Directory.CreateDirectory(backupDir);

            int successCount = 0;

            foreach (var rule in Rules)
            {
                string fullPath = Path.Combine(targetDir, rule.RelPath);
                if (!File.Exists(fullPath))
                {
                    LogError("未找到文件: " + rule.RelPath);
                    continue;
                }

                string originalContent = File.ReadAllText(fullPath, Encoding.UTF8);
                string eol = DetectLineEnding(originalContent);
                string content = NormalizeLineEndings(originalContent);

                string reason;
                RuleStatus status = GetRuleStatus(targetDir, rule, out reason);
                if (status == RuleStatus.APPLIED)
                {
                    LogInfo("已处于注入状态，无需重复打补丁: " + rule.RelPath);
                    successCount++;
                    continue;
                }

                // 备份文件
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
                    string normFind = NormalizeLineEndings(chunk.Find);
                    string normReplace = NormalizeLineEndings(chunk.Replace);

                    if (content.Contains(normReplace))
                    {
                        continue;
                    }

                    if (!content.Contains(normFind))
                    {
                        LogError("匹配块失配 (" + rule.RelPath + "): \"" + chunk.Name + "\"");
                        allChunksApplied = false;
                        break;
                    }

                    content = content.Replace(normFind, normReplace);
                }

                if (!allChunksApplied)
                {
                    LogError("文件补丁未能全部匹配，已放弃对该文件的修改: " + rule.RelPath);
                    continue;
                }

                string finalContent = ApplyLineEnding(content, eol);
                File.WriteAllText(fullPath, finalContent, Encoding.UTF8);
                LogSuccess("成功注入补丁: " + rule.RelPath);
                successCount++;
            }

            Console.WriteLine();
            if (successCount == Rules.Count)
            {
                LogSuccess("全部补丁成功注入！原始文件已备份至: " + backupDir);
            }
            else
            {
                LogWarn(string.Format("完成注入: {0}/{1} 个文件处理成功。", successCount, Rules.Count));
            }
        }

        static void CmdRestore(string targetDir)
        {
            LogHeader("正在恢复原版文件: " + targetDir);

            int restoredCount = 0;

            foreach (var rule in Rules)
            {
                string fullPath = Path.Combine(targetDir, rule.RelPath);
                string localBak = fullPath + ".cvr-patch.bak";

                if (!File.Exists(fullPath))
                {
                    LogWarn("文件不存在: " + rule.RelPath);
                    continue;
                }

                // 优先从 .cvr-patch.bak 恢复
                if (File.Exists(localBak))
                {
                    string bakContent = File.ReadAllText(localBak, Encoding.UTF8);
                    File.WriteAllText(fullPath, bakContent, Encoding.UTF8);
                    File.Delete(localBak);
                    LogSuccess("已从备份副本还原: " + rule.RelPath);
                    restoredCount++;
                    continue;
                }

                // 反向替换
                string currentContent = File.ReadAllText(fullPath, Encoding.UTF8);
                string eol = DetectLineEnding(currentContent);
                string content = NormalizeLineEndings(currentContent);
                bool modified = false;

                foreach (var chunk in rule.Chunks)
                {
                    string normFind = NormalizeLineEndings(chunk.Find);
                    string normReplace = NormalizeLineEndings(chunk.Replace);

                    if (content.Contains(normReplace))
                    {
                        content = content.Replace(normReplace, normFind);
                        modified = true;
                    }
                }

                if (modified)
                {
                    File.WriteAllText(fullPath, ApplyLineEnding(content, eol), Encoding.UTF8);
                    LogSuccess("已逆向撤销补丁改动: " + rule.RelPath);
                    restoredCount++;
                }
                else
                {
                    LogInfo("文件已处于原始未修改状态: " + rule.RelPath);
                }
            }

            Console.WriteLine();
            LogSuccess(string.Format("还原流程结束，共还原/确认 {0} 个文件。", restoredCount));
        }

        static void CmdDiff(string targetDir)
        {
            LogHeader("生成补丁变更差异预览 (Unified Diff)");

            foreach (var rule in Rules)
            {
                WriteLineColor("--- a/" + rule.RelPath.Replace('\\', '/'), ConsoleColor.Cyan);
                WriteLineColor("+++ b/" + rule.RelPath.Replace('\\', '/'), ConsoleColor.Cyan);

                foreach (var chunk in rule.Chunks)
                {
                    WriteLineColor("@@ " + chunk.Name + " @@", ConsoleColor.Yellow);
                    string[] findLines = NormalizeLineEndings(chunk.Find).Split('\n');
                    string[] replaceLines = NormalizeLineEndings(chunk.Replace).Split('\n');

                    foreach (var line in findLines)
                    {
                        WriteLineColor("-" + line, ConsoleColor.Red);
                    }
                    foreach (var line in replaceLines)
                    {
                        WriteLineColor("+" + line, ConsoleColor.Green);
                    }
                }
                Console.WriteLine();
            }
        }

        static void CmdExportPatch(string targetDir, string outputPath)
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string outPath = !string.IsNullOrEmpty(outputPath)
                ? outputPath
                : Path.Combine(exeDir, "patches", "cross-subscription-chain.patch");

            string dir = Path.GetDirectoryName(outPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            StringBuilder sb = new StringBuilder();

            foreach (var rule in Rules)
            {
                string unixPath = rule.RelPath.Replace('\\', '/');
                sb.AppendLine("diff --git a/" + unixPath + " b/" + unixPath);
                sb.AppendLine("--- a/" + unixPath);
                sb.AppendLine("+++ b/" + unixPath);

                foreach (var chunk in rule.Chunks)
                {
                    sb.AppendLine("@@ -0,0 +0,0 @@ /* " + chunk.Name + " */");
                    string[] findLines = NormalizeLineEndings(chunk.Find).Split('\n');
                    string[] replaceLines = NormalizeLineEndings(chunk.Replace).Split('\n');

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
            LogSuccess("已导出 Unified Patch 补丁文件至: " + outPath);
        }

        static void PrintHelp()
        {
            Console.WriteLine(@"
==============================================================
  Clash Verge Rev 跨订阅链式代理独立补丁程序 (ProxyChaining)
==============================================================

用法:
  cvr-patch.exe <command> [target_dir] [options]

命令:
  status        检查目标仓库的补丁注入状态 (默认目标: 自动检测或 CVR_DIR)
  inject        对目标仓库进行非侵入式修改，解除跨订阅限制并自动备份 (别名: apply)
  restore       从备份或反向替换完全撤销补丁，恢复原版代码 (别名: rollback, revert)
  diff          在终端中以 Unified Diff 格式展示所有的局部修改内容
  export-patch  导出标准 .patch 文件，供 git apply 使用
  help          显示此帮助信息

示例:
  cvr-patch.exe status
  cvr-patch.exe inject
  cvr-patch.exe restore
  cvr-patch.exe inject ""D:\Projects\clash-verge-rev""
");
        }

        static void InteractiveMenu(string targetDir)
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("==============================================================");
                WriteColor("  Clash Verge Rev 跨订阅链式代理独立补丁程序 (ProxyChaining)\n", ConsoleColor.Cyan);
                Console.WriteLine("==============================================================");
                Console.WriteLine("当前目标仓库: " + targetDir);

                // Check status
                int applied = 0;
                foreach (var rule in Rules)
                {
                    string reason;
                    if (GetRuleStatus(targetDir, rule, out reason) == RuleStatus.APPLIED) applied++;
                }

                Console.Write("当前补丁状态: ");
                if (applied == Rules.Count)
                {
                    WriteLineColor(string.Format("全部已注入 ({0}/{1} 模块生效)", applied, Rules.Count), ConsoleColor.Green);
                }
                else if (applied == 0)
                {
                    WriteLineColor(string.Format("原版状态未注入 (0/{0} 模块生效)", Rules.Count), ConsoleColor.Gray);
                }
                else
                {
                    WriteLineColor(string.Format("部分注入 ({0}/{1} 模块生效)", applied, Rules.Count), ConsoleColor.Yellow);
                }

                Console.WriteLine("\n请选择操作:");
                WriteColor("  [1] 一键注入补丁 (Inject / Apply)\n", ConsoleColor.Green);
                WriteColor("  [2] 一键撤销还原 (Restore / Rollback)\n", ConsoleColor.Yellow);
                WriteColor("  [3] 检查补丁状态 (Status)\n", ConsoleColor.Cyan);
                WriteColor("  [4] 查看改动差异 (Unified Diff)\n", ConsoleColor.White);
                WriteColor("  [5] 导出标准补丁 (Export .patch)\n", ConsoleColor.Magenta);
                WriteColor("  [6] 更改目标目录 (Change Target Directory)\n", ConsoleColor.DarkCyan);
                WriteColor("  [0] 退出程序 (Exit)\n", ConsoleColor.DarkGray);
                Console.WriteLine();
                Console.Write("请输入选项 [0-6]: ");

                string choiceRaw = Console.ReadLine();
                string choice = choiceRaw != null ? choiceRaw.Trim() : null;
                if (choice == "0") break;

                switch (choice)
                {
                    case "1":
                        CmdInject(targetDir);
                        break;
                    case "2":
                        CmdRestore(targetDir);
                        break;
                    case "3":
                        CmdStatus(targetDir);
                        break;
                    case "4":
                        CmdDiff(targetDir);
                        break;
                    case "5":
                        CmdExportPatch(targetDir, null);
                        break;
                    case "6":
                        Console.Write("\n请输入新的 Clash Verge Rev 根目录路径: ");
                        string lineRaw = Console.ReadLine();
                        string newDir = lineRaw != null ? lineRaw.Trim('"', ' ', '\'') : null;
                        if (!string.IsNullOrEmpty(newDir))
                        {
                            try
                            {
                                targetDir = ResolveTargetDir(newDir);
                                LogSuccess("目标目录已切换至: " + targetDir);
                            }
                            catch (Exception ex)
                            {
                                LogError("切换失败: " + ex.Message);
                            }
                        }
                        break;
                    default:
                        LogWarn("无效的选项，请重新输入。");
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

            string customTarget = (args.Length > 1 && !args[1].StartsWith("-")) ? args[1] : null;
            string targetDir = null;

            try
            {
                targetDir = ResolveTargetDir(customTarget);
            }
            catch (Exception ex)
            {
                LogError(ex.Message);
                if (args.Length > 0) return 1;
            }

            // Interactive mode if no arguments provided
            if (args.Length == 0)
            {
                while (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
                {
                    Console.Clear();
                    Console.WriteLine("==============================================================");
                    WriteColor("  Clash Verge Rev 跨订阅链式代理独立补丁程序 (ProxyChaining)\n", ConsoleColor.Cyan);
                    Console.WriteLine("==============================================================\n");
                    LogWarn("未能在当前目录或默认路径自动定位到 Clash Verge Rev 项目！");
                    Console.Write("请输入 Clash Verge Rev 代码根目录路径 (输入 0 退出): ");
                    string inRaw = Console.ReadLine();
                    string input = inRaw != null ? inRaw.Trim('"', ' ', '\'') : null;
                    if (input == "0" || string.IsNullOrEmpty(input)) return 0;
                    try
                    {
                        targetDir = ResolveTargetDir(input);
                    }
                    catch (Exception ex)
                    {
                        LogError(ex.Message);
                        Console.WriteLine("按回车键重试...");
                        Console.ReadLine();
                    }
                }

                InteractiveMenu(targetDir);
                return 0;
            }

            string command = args[0].ToLowerInvariant();
            switch (command)
            {
                case "status":
                case "check":
                    CmdStatus(targetDir);
                    break;
                case "inject":
                case "apply":
                    CmdInject(targetDir);
                    break;
                case "restore":
                case "rollback":
                case "revert":
                    CmdRestore(targetDir);
                    break;
                case "diff":
                    CmdDiff(targetDir);
                    break;
                case "export-patch":
                    CmdExportPatch(targetDir, args.Length > 2 ? args[2] : null);
                    break;
                default:
                    LogError("未知命令: " + command);
                    PrintHelp();
                    return 1;
            }

            return 0;
        }
    }
}
