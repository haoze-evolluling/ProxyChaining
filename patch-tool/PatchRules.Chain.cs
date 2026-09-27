using System;
using System.Collections.Generic;
using System.IO;

namespace CvrProxyChainPatcher
{
    public static partial class PatchRules
    {
        private static List<PatchRule> GetChainRules()
        {
            return new List<PatchRule>
            {
                new PatchRule
                {
                    Id = "proxy-chain-model",
                    RelPath = Path.Combine("src", "components", "proxy", "proxy-chain-model.ts"),
                    Description = "跨订阅节点在当前视图中未匹配时保留原有 recordId 与信息，防止连接按钮被禁用或节点置灰",
                    Chunks = new List<PatchChunk>
                    {
                        new PatchChunk
                        {
                            Name = "rebindProxyChainItems 保留跨订阅节点的已有属性",
                            Find = "    const record =\n" +
                                   "      rebound === undefined ? undefined : getRecord(proxyView, rebound.recordId)\n" +
                                   "    return {\n" +
                                   "      ...item,\n" +
                                   "      recordId: rebound?.recordId,\n" +
                                   "      source: rebound?.source ?? item.source,\n" +
                                   "      type: rebound?.type ?? item.type,\n" +
                                   "      delay: record?.history.at(-1)?.delay,\n" +
                                   "    }",
                            Replace = "    const record =\n" +
                                      "      rebound === undefined ? undefined : getRecord(proxyView, rebound.recordId)\n" +
                                      "    return {\n" +
                                      "      ...item,\n" +
                                      "      // [CVR-PATCH] 跨订阅支持：节点不在当前活跃视图中时，保留既有 recordId 与信息，防止按钮被禁用或节点置灰\n" +
                                      "      recordId: rebound?.recordId ?? item.recordId ?? item.name,\n" +
                                      "      source: rebound?.source ?? item.source,\n" +
                                      "      type: rebound?.type ?? item.type,\n" +
                                      "      delay: record?.history.at(-1)?.delay ?? item.delay,\n" +
                                      "    }"
                        }
                    }
                },
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
                        },
                        new PatchChunk
                        {
                            Name = "持久化代理链到 localStorage 时保留 recordId 与 source 信息，避免刷新重绑失败",
                            Find = "      const persistedChain = currentProxyChain.map(\n" +
                                   "        ({ id, name, type, delay }) => ({\n" +
                                   "          id,\n" +
                                   "          name,\n" +
                                   "          type,\n" +
                                   "          delay,\n" +
                                   "        }),\n" +
                                   "      )",
                            Replace = "      const persistedChain = currentProxyChain.map(\n" +
                                      "        ({ id, name, type, delay, recordId, source }) => ({\n" +
                                      "          id,\n" +
                                      "          name,\n" +
                                      "          type,\n" +
                                      "          delay,\n" +
                                      "          recordId,\n" +
                                      "          source,\n" +
                                      "        }),\n" +
                                      "      )"
                        }
                    }
                },
                new PatchRule
                {
                    Id = "proxy-chain",
                    RelPath = Path.Combine("src", "components", "proxy", "proxy-chain.tsx"),
                    Description = "在代理链面板中全局寻找候选节点，避免置灰；放宽连接校验并智能匹配出口组",
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
                        },
                        new PatchChunk
                        {
                            Name = "解除连接节点校验必须带有 recordId 的硬限制，允许跨订阅节点连接",
                            Find = "    if (\n" +
                                   "      currentProxyChain.length < 2 ||\n" +
                                   "      currentProxyChain.some(({ recordId }) => !recordId)\n" +
                                   "    ) {\n" +
                                   "      alert(t('proxies.page.chain.minimumNodes'))\n" +
                                   "      return\n" +
                                   "    }",
                            Replace = "    // [CVR-PATCH] 跨订阅支持：节点校验放宽，只要节点存在合法名称即可发起代理链连接\n" +
                                      "    if (\n" +
                                      "      currentProxyChain.length < 2 ||\n" +
                                      "      currentProxyChain.some((item) => !item || !item.name)\n" +
                                      "    ) {\n" +
                                      "      alert(t('proxies.page.chain.minimumNodes'))\n" +
                                      "      return\n" +
                                      "    }"
                        },
                        new PatchChunk
                        {
                            Name = "解除连接按钮 disabled 判定中对 recordId 的依赖",
                            Find = "            disabled={\n" +
                                   "              isConnecting ||\n" +
                                   "              (!isConnected &&\n" +
                                   "                (currentProxyChain.length < 2 ||\n" +
                                   "                  currentProxyChain.some(\n" +
                                   "                    ({ recordId }) => recordId === undefined,\n" +
                                   "                  ) ||\n" +
                                   "                  (mode === 'global' && proxyView?.global === null) ||\n" +
                                   "                  (mode !== 'global' && !selectedGroup)))\n" +
                                   "            }",
                            Replace = "            // [CVR-PATCH] 跨订阅支持：只要节点有名字即可启用连接按钮，不再因 recordId 未在当前活跃视图中而变灰禁用\n" +
                                      "            disabled={\n" +
                                      "              isConnecting ||\n" +
                                      "              (!isConnected &&\n" +
                                      "                (currentProxyChain.length < 2 ||\n" +
                                      "                  currentProxyChain.some((item) => !item || !item.name) ||\n" +
                                      "                  (mode === 'global' && proxyView?.global === null) ||\n" +
                                      "                  (mode !== 'global' && !selectedGroup)))\n" +
                                      "            }"
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
                        },
                        new PatchChunk
                        {
                            Name = "rebindNode 优雅降级匹配与容错，避免跨订阅或同名节点 recordId 丢失置灰",
                            Find = "export function rebindNode(\n" +
                                   "  candidates: readonly ProxyNodeView[],\n" +
                                   "  binding: ProxyNodeBinding,\n" +
                                   ") {\n" +
                                   "  const matches = candidates.filter(\n" +
                                   "    (node) =>\n" +
                                   "      node.name === binding.name &&\n" +
                                   "      (binding.source === undefined || sameSource(node.source, binding.source)),\n" +
                                   "  )\n" +
                                   "  const unique = new Map(matches.map((node) => [node.recordId, node]))\n" +
                                   "  return unique.size === 1 ? unique.values().next().value : undefined\n" +
                                   "}",
                            Replace = "export function rebindNode(\n" +
                                      "  candidates: readonly ProxyNodeView[],\n" +
                                      "  binding: ProxyNodeBinding,\n" +
                                      ") {\n" +
                                      "  let matches = candidates.filter(\n" +
                                      "    (node) =>\n" +
                                      "      node.name === binding.name &&\n" +
                                      "      (binding.source === undefined || sameSource(node.source, binding.source)),\n" +
                                      "  )\n" +
                                      "  if (matches.length === 0 && binding.source !== undefined) {\n" +
                                      "    matches = candidates.filter((node) => node.name === binding.name)\n" +
                                      "  }\n" +
                                      "  const unique = new Map(matches.map((node) => [node.recordId, node]))\n" +
                                      "  return unique.size >= 1 ? unique.values().next().value : undefined\n" +
                                      "}"
                        }
                    }
                },
                new PatchRule
                {
                    Id = "runtime-backend",
                    RelPath = Path.Combine("src-tauri", "src", "config", "runtime.rs"),
                    Description = "增强后端运行时链式代理更新逻辑，全面扫描 profiles 订阅与 providers 注入 dialer-proxy",
                    Chunks = new List<PatchChunk>
                    {
                        new PatchChunk
                        {
                            Name = "update_proxy_chain_config 支持全订阅节点扫描、完整注入与链条绑定",
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
                                      "            // [CVR-PATCH] 跨订阅节点全量扫描：收集订阅 profiles 目录与 proxy-providers 下所有 yaml 文件\n" +
                                      "            let mut search_paths: Vec<std::path::PathBuf> = Vec::new();\n" +
                                      "            if let Ok(profiles_dir) = crate::utils::dirs::app_profiles_dir() {\n" +
                                      "                if let Ok(entries) = std::fs::read_dir(profiles_dir) {\n" +
                                      "                    for entry in entries.flatten() {\n" +
                                      "                        let path = entry.path();\n" +
                                      "                        if path.extension().and_then(|s| s.to_str()) == Some(\"yaml\") {\n" +
                                      "                            search_paths.push(path);\n" +
                                      "                        }\n" +
                                      "                    }\n" +
                                      "                }\n" +
                                      "            }\n" +
                                      "\n" +
                                      "            let app_home = crate::utils::dirs::app_home_dir().ok();\n" +
                                      "            if let Some(Value::Mapping(providers)) = config.get(\"proxy-providers\") {\n" +
                                      "                for p in providers.values().filter_map(|p| p.get(\"path\").and_then(Value::as_str)) {\n" +
                                      "                    let path = std::path::Path::new(p);\n" +
                                      "                    if path.is_absolute() {\n" +
                                      "                        search_paths.push(path.to_path_buf());\n" +
                                      "                    } else if let Some(home) = app_home.as_ref() {\n" +
                                      "                        search_paths.push(home.join(path));\n" +
                                      "                    }\n" +
                                      "                }\n" +
                                      "            }\n" +
                                      "\n" +
                                      "            // 针对链中每一个节点（包括入口节点与后续节点），若不在当前主配置中，则从订阅文件中找到并完整注入\n" +
                                      "            if let Some(Value::Sequence(proxies)) = config.get_mut(\"proxies\") {\n" +
                                      "                for (i, dialer_proxy) in dialer_proxies.iter().enumerate() {\n" +
                                      "                    let exists = proxies.iter().any(|p| p.get(\"name\") == Some(dialer_proxy));\n" +
                                      "                    if !exists {\n" +
                                      "                        for path in &search_paths {\n" +
                                      "                            if let Ok(content) = std::fs::read_to_string(path)\n" +
                                      "                                && let Ok(yaml_data) = serde_yaml_ng::from_str::<Mapping>(&content)\n" +
                                      "                                && let Some(Value::Sequence(p_nodes)) = yaml_data.get(\"proxies\")\n" +
                                      "                            {\n" +
                                      "                                if let Some(found_node) = p_nodes.iter().find(|n| n.get(\"name\") == Some(dialer_proxy)) {\n" +
                                      "                                    if let Some(mut cloned_map) = found_node.as_mapping().cloned() {\n" +
                                      "                                        if i != 0 && let Some(prev_proxy) = dialer_proxies.get(i - 1) {\n" +
                                      "                                            cloned_map.insert(\"dialer-proxy\".into(), prev_proxy.to_owned());\n" +
                                      "                                        } else {\n" +
                                      "                                            cloned_map.remove(\"dialer-proxy\");\n" +
                                      "                                        }\n" +
                                      "                                        cloned_map.insert(\"_injected_chain_proxy\".into(), Value::Bool(true));\n" +
                                      "                                        proxies.push(Value::Mapping(cloned_map));\n" +
                                      "                                        break;\n" +
                                      "                                    }\n" +
                                      "                                }\n" +
                                      "                            }\n" +
                                      "                        }\n" +
                                      "                    }\n" +
                                      "                }\n" +
                                      "            }\n" +
                                      "\n" +
                                      "            // 统一为链式代理节点绑定正确的上游 dialer-proxy（入口节点移除 dialer-proxy）\n" +
                                      "            if let Some(Value::Sequence(proxies)) = config.get_mut(\"proxies\") {\n" +
                                      "                for (i, dialer_proxy) in dialer_proxies.iter().enumerate() {\n" +
                                      "                    if let Some(Value::Mapping(proxy)) =\n" +
                                      "                        proxies.iter_mut().find(|proxy| proxy.get(\"name\") == Some(dialer_proxy))\n" +
                                      "                    {\n" +
                                      "                        if i != 0 && let Some(prev_proxy) = dialer_proxies.get(i - 1) {\n" +
                                      "                            proxy.insert(\"dialer-proxy\".into(), prev_proxy.to_owned());\n" +
                                      "                        } else {\n" +
                                      "                            proxy.remove(\"dialer-proxy\");\n" +
                                      "                        }\n" +
                                      "                    }\n" +
                                      "                }\n" +
                                      "            }\n" +
                                      "        }\n" +
                                      "    }"
                        }
                    }
                }
            };
        }
    }
}
