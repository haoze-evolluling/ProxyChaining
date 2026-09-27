import path from 'node:path';

/**
 * 跨订阅链式代理补丁规则
 */
export const CHAIN_RULES = [
  {
    id: 'proxy-chain-model',
    relPath: path.join('src', 'components', 'proxy', 'proxy-chain-model.ts'),
    description: '跨订阅节点在当前视图中未匹配时保留原有 recordId 与信息，防止连接按钮被禁用或节点置灰',
    chunks: [
      {
        name: 'rebindProxyChainItems 保留跨订阅节点的已有属性',
        find: `    const record =
      rebound === undefined ? undefined : getRecord(proxyView, rebound.recordId)
    return {
      ...item,
      recordId: rebound?.recordId,
      source: rebound?.source ?? item.source,
      type: rebound?.type ?? item.type,
      delay: record?.history.at(-1)?.delay,
    }`,
        replace: `    const record =
      rebound === undefined ? undefined : getRecord(proxyView, rebound.recordId)
    return {
      ...item,
      // [CVR-PATCH] 跨订阅支持：节点不在当前活跃视图中时，保留既有 recordId 与信息，防止按钮被禁用或节点置灰
      recordId: rebound?.recordId ?? item.recordId ?? item.name,
      source: rebound?.source ?? item.source,
      type: rebound?.type ?? item.type,
      delay: record?.history.at(-1)?.delay ?? item.delay,
    }`,
      },
    ],
  },
  {
    id: 'proxy-groups-chain',
    relPath: path.join('src', 'components', 'proxy', 'proxy-groups-chain.tsx'),
    description: '解除切换分组时清空代理链的限制，并使用全量候选节点避免跨订阅节点置灰',
    chunks: [
      {
        name: '使用全量节点作为候选节点，防止切换分组时已选节点失去 recordId',
        find: `  const candidateNodes = useMemo(
    () =>
      renderList.flatMap((item) => {
        const occurrences = item.memberCol ?? (item.member ? [item.member] : [])
        return occurrences.flatMap(({ member }) =>
          member.kind === 'node' ? [member.node] : [],
        )
      }),
    [renderList],
  )

  const currentProxyChain = useMemo(
    () =>
      proxyView
        ? rebindProxyChainItems(proxyChain, candidateNodes, proxyView)
        : proxyChain.map((item) => ({
            ...item,
            recordId: undefined,
            delay: undefined,
          })),
    [candidateNodes, proxyChain, proxyView],
  )`,
        replace: `  const candidateNodes = useMemo(
    () =>
      renderList.flatMap((item) => {
        const occurrences = item.memberCol ?? (item.member ? [item.member] : [])
        return occurrences.flatMap(({ member }) =>
          member.kind === 'node' ? [member.node] : [],
        )
      }),
    [renderList],
  )

  // [CVR-PATCH] 全量候选节点：解除跨组/跨订阅时节点因不在当前视图列表而被置灰的问题
  const allCandidateNodes = useMemo(
    () => (proxyView ? Object.values(proxyView.records) : candidateNodes),
    [proxyView, candidateNodes],
  )

  const currentProxyChain = useMemo(
    () =>
      proxyView
        ? rebindProxyChainItems(proxyChain, allCandidateNodes, proxyView)
        : proxyChain.map((item) => ({
            ...item,
            recordId: undefined,
            delay: undefined,
          })),
    [allCandidateNodes, proxyChain, proxyView],
  )`,
      },
      {
        name: '移除切换代理组时强制清空代理链的代码',
        find: `  const handleGroupSelect = (groupName: string) => {
    onGroupSelect(groupName)
    handleGroupMenuClose()

    if (mode === 'rule') {
      updateProxyChainConfigInRuntime(null)
      localStorage.removeItem('proxy-chain-group')
      localStorage.removeItem('proxy-chain-exit-node')
      localStorage.removeItem('proxy-chain-items')
      setProxyChain([])
    }
  }`,
        replace: `  const handleGroupSelect = (groupName: string) => {
    onGroupSelect(groupName)
    handleGroupMenuClose()

    // [CVR-PATCH] 跨订阅支持：切换分组时不自动清空代理链，允许将不同订阅/分组的节点加入同一条链
  }`,
      },
      {
        name: 'handleChangeProxy 添加节点时使用 allCandidateNodes 重新绑定',
        find: `  const handleChangeProxy = useCallback(
    (_group: ProxyGroupView, member: ResolvedProxyMember) => {
      if (!isInteractableMember(member) || member.kind !== 'node') return
      const { node } = member
      setProxyChain((prev) => {
        const current = proxyView
          ? rebindProxyChainItems(prev, candidateNodes, proxyView)
          : prev`,
        replace: `  const handleChangeProxy = useCallback(
    (_group: ProxyGroupView, member: ResolvedProxyMember) => {
      if (!isInteractableMember(member) || member.kind !== 'node') return
      const { node } = member
      setProxyChain((prev) => {
        // [CVR-PATCH] 跨订阅支持：使用全局节点列表重新绑定，防止上一个分组的节点丢失 recordId
        const current = proxyView
          ? rebindProxyChainItems(prev, allCandidateNodes, proxyView)
          : prev`,
      },
      {
        name: 'handleChangeProxy 依赖项补充 allCandidateNodes',
        find: `    [candidateNodes, proxyView, t],
  )`,
        replace: `    [allCandidateNodes, candidateNodes, proxyView, t],
  )`,
      },
      {
        name: '持久化代理链到 localStorage 时保留 recordId 与 source 信息，避免刷新重绑失败',
        find: `      const persistedChain = currentProxyChain.map(
        ({ id, name, type, delay }) => ({
          id,
          name,
          type,
          delay,
        }),
      )`,
        replace: `      const persistedChain = currentProxyChain.map(
        ({ id, name, type, delay, recordId, source }) => ({
          id,
          name,
          type,
          delay,
          recordId,
          source,
        }),
      )`,
      },
    ],
  },
  {
    id: 'proxy-chain',
    relPath: path.join('src', 'components', 'proxy', 'proxy-chain.tsx'),
    description: '在代理链面板中全局寻找候选节点，避免置灰；放宽连接校验并智能匹配出口组',
    chunks: [
      {
        name: '候选节点范围扩大为全部 ProxyNodeView，彻底解除置灰与禁用',
        find: `  const candidates = useMemo(() => {
    if (!proxyView) return []
    if (mode === 'rule' && selectedGroup) {
      return selectRuleChainMembers(proxyView, selectedGroup).flatMap(
        ({ member }) => (member.kind === 'node' ? [member.node] : []),
      )
    }
    if (!runtimeConfig) return []
    const runtimeProxies = (
      runtimeConfig as RuntimeConfigWithProxySequence | null
    )?.proxies
    return selectGlobalChainNodes(proxyView, runtimeProxies)
  }, [mode, proxyView, runtimeConfig, selectedGroup])`,
        replace: `  // [CVR-PATCH] 跨订阅支持：候选节点包含 proxyView 中的全部节点，避免跨组/跨订阅节点 recordId 丢失而置灰
  const candidates = useMemo(() => {
    if (!proxyView) return []
    return Object.values(proxyView.records)
  }, [proxyView])`,
      },
      {
        name: 'isConnected 判断支持跨组记录的 proxy-chain-group',
        find: `    if (!selectedGroup) {
      return false
    }

    const proxyChainGroup = proxyView.groups.find(
      (group) => group.name === selectedGroup,
    )

    return proxyChainGroup?.now === lastNode.name
  }, [proxyView, currentProxyChain, mode, selectedGroup])`,
        replace: `    if (!selectedGroup) {
      return false
    }

    // [CVR-PATCH] 优先使用存储的代理链连接组，防止切换浏览其他分组时连接状态判断错误
    const chainGroupName =
      localStorage.getItem('proxy-chain-group') || selectedGroup
    const proxyChainGroup = proxyView.groups.find(
      (group) => group.name === chainGroupName,
    )

    return proxyChainGroup?.now === lastNode.name
  }, [proxyView, currentProxyChain, mode, selectedGroup])`,
      },
      {
        name: 'handleConnect 动态解析出口节点所属代理组，避免跨订阅连接报错',
        find: `      // 根据模式确定使用的代理组名称
      if (mode !== 'global' && !selectedGroup) {
        throw new Error('规则模式下必须选择代理组')
      }

      const targetGroup = mode === 'global' ? 'GLOBAL' : selectedGroup

      await selectNodeForGroup(targetGroup || 'GLOBAL', lastNode.name)
      // The chain moves the group like any other selection, so the profile has to learn about
      // it: what the profile holds is what gets re-applied the next time the core starts.
      recordSelection(targetGroup || 'GLOBAL', lastNode.name)
      localStorage.setItem('proxy-chain-group', targetGroup || 'GLOBAL')
      localStorage.setItem('proxy-chain-exit-node', lastNode.name)`,
        replace: `      // 根据模式确定使用的代理组名称
      if (mode !== 'global' && !selectedGroup) {
        throw new Error('规则模式下必须选择代理组')
      }

      let targetGroup = mode === 'global' ? 'GLOBAL' : selectedGroup

      // [CVR-PATCH] 跨订阅支持：如果当前 selectedGroup 不包含出口节点，自动寻找包含出口节点的代理组
      if (mode !== 'global' && proxyView) {
        const currentGroupObj = proxyView.groups.find(
          (g) => g.name === targetGroup,
        )
        const hasNode = currentGroupObj?.members.some(
          (m) => m.name === lastNode.name,
        )
        if (!hasNode) {
          const matchingGroup = proxyView.groups.find((g) =>
            g.members.some((m) => m.name === lastNode.name),
          )
          if (matchingGroup) {
            targetGroup = matchingGroup.name
          }
        }
      }

      await selectNodeForGroup(targetGroup || 'GLOBAL', lastNode.name)
      // The chain moves the group like any other selection, so the profile has to learn about
      // it: what the profile holds is what gets re-applied the next time the core starts.
      recordSelection(targetGroup || 'GLOBAL', lastNode.name)
      localStorage.setItem('proxy-chain-group', targetGroup || 'GLOBAL')
      localStorage.setItem('proxy-chain-exit-node', lastNode.name)`,
      },
      {
        name: '解除连接节点校验必须带有 recordId 的硬限制，允许跨订阅节点连接',
        find: `    if (
      currentProxyChain.length < 2 ||
      currentProxyChain.some(({ recordId }) => !recordId)
    ) {
      alert(t('proxies.page.chain.minimumNodes'))
      return
    }`,
        replace: `    // [CVR-PATCH] 跨订阅支持：节点校验放宽，只要节点存在合法名称即可发起代理链连接
    if (
      currentProxyChain.length < 2 ||
      currentProxyChain.some((item) => !item || !item.name)
    ) {
      alert(t('proxies.page.chain.minimumNodes'))
      return
    }`,
      },
      {
        name: '解除连接按钮 disabled 判定中对 recordId 的依赖',
        find: `            disabled={
              isConnecting ||
              (!isConnected &&
                (currentProxyChain.length < 2 ||
                  currentProxyChain.some(
                    ({ recordId }) => recordId === undefined,
                  ) ||
                  (mode === 'global' && proxyView?.global === null) ||
                  (mode !== 'global' && !selectedGroup)))
            }`,
        replace: `            // [CVR-PATCH] 跨订阅支持：只要节点有名字即可启用连接按钮，不再因 recordId 未在当前活跃视图中而变灰禁用
            disabled={
              isConnecting ||
              (!isConnected &&
                (currentProxyChain.length < 2 ||
                  currentProxyChain.some((item) => !item || !item.name) ||
                  (mode === 'global' && proxyView?.global === null) ||
                  (mode !== 'global' && !selectedGroup)))
            }`,
      },
    ],
  },
  {
    id: 'proxy-view-types',
    relPath: path.join('src', 'types', 'proxy-view.ts'),
    description: '解除全局链式代理节点仅允许 core 来源的限制，使订阅提供者节点在全局模式下可用',
    chunks: [
      {
        name: 'selectGlobalChainNodes 包含全部节点',
        find: `export const selectGlobalChainNodes = (
  view: ProxyViewV1,
  runtimeProxies: unknown,
) =>
  view.global === null ? [] : selectRuntimeStandaloneNodes(view, runtimeProxies)`,
        replace: `export const selectGlobalChainNodes = (
  view: ProxyViewV1,
  runtimeProxies: unknown,
) => {
  if (view.global === null) return []
  // [CVR-PATCH] 跨订阅支持：全局模式下展示全部可用节点，允许订阅节点参与全局链式代理
  const allNodes = Object.values(view.records)
  return allNodes.length > 0
    ? allNodes
    : selectRuntimeStandaloneNodes(view, runtimeProxies)
}`,
      },
      {
        name: 'rebindNode 优雅降级匹配与容错，避免跨订阅或同名节点 recordId 丢失置灰',
        find: `export function rebindNode(
  candidates: readonly ProxyNodeView[],
  binding: ProxyNodeBinding,
) {
  const matches = candidates.filter(
    (node) =>
      node.name === binding.name &&
      (binding.source === undefined || sameSource(node.source, binding.source)),
  )
  const unique = new Map(matches.map((node) => [node.recordId, node]))
  return unique.size === 1 ? unique.values().next().value : undefined
}`,
        replace: `export function rebindNode(
  candidates: readonly ProxyNodeView[],
  binding: ProxyNodeBinding,
) {
  let matches = candidates.filter(
    (node) =>
      node.name === binding.name &&
      (binding.source === undefined || sameSource(node.source, binding.source)),
  )
  if (matches.length === 0 && binding.source !== undefined) {
    matches = candidates.filter((node) => node.name === binding.name)
  }
  const unique = new Map(matches.map((node) => [node.recordId, node]))
  return unique.size >= 1 ? unique.values().next().value : undefined
}`,
      },
    ],
  },
  {
    id: 'runtime-backend',
    relPath: path.join('src-tauri', 'src', 'config', 'runtime.rs'),
    description: '增强后端运行时链式代理更新逻辑，全面扫描 profiles 订阅与 providers 注入 dialer-proxy',
    chunks: [
      {
        name: 'update_proxy_chain_config 支持全订阅节点扫描、完整注入与链条绑定',
        find: `    /// Rebuilds \`dialer-proxy\` links from an ordered proxy chain, or removes them for \`None\`.
    #[inline]
    pub fn update_proxy_chain_config(&mut self, proxy_chain_config: Option<Value>) {
        let config = if let Some(config) = self.config.as_mut() {
            config
        } else {
            return;
        };

        if let Some(Value::Sequence(proxies)) = config.get_mut("proxies") {
            proxies.iter_mut().for_each(|proxy| {
                if let Some(proxy) = proxy.as_mapping_mut()
                    && proxy.get("dialer-proxy").is_some()
                {
                    proxy.remove("dialer-proxy");
                }
            });
        }

        if let Some(Value::Sequence(dialer_proxies)) = proxy_chain_config
            && let Some(Value::Sequence(proxies)) = config.get_mut("proxies")
        {
            for (i, dialer_proxy) in dialer_proxies.iter().enumerate() {
                if let Some(Value::Mapping(proxy)) =
                    proxies.iter_mut().find(|proxy| proxy.get("name") == Some(dialer_proxy))
                    && i != 0
                    && let Some(dialer_proxy) = dialer_proxies.get(i - 1)
                {
                    proxy.insert("dialer-proxy".into(), dialer_proxy.to_owned());
                }
            }
        }
    }`,
        replace: `    /// Rebuilds \`dialer-proxy\` links from an ordered proxy chain, or removes them for \`None\`.
    #[inline]
    pub fn update_proxy_chain_config(&mut self, proxy_chain_config: Option<Value>) {
        let config = if let Some(config) = self.config.as_mut() {
            config
        } else {
            return;
        };

        // [CVR-PATCH] 清理之前注入的临时跨订阅代理节点与 dialer-proxy
        if let Some(Value::Sequence(proxies)) = config.get_mut("proxies") {
            proxies.retain(|proxy| {
                proxy.get("_injected_chain_proxy").and_then(Value::as_bool) != Some(true)
            });
            proxies.iter_mut().for_each(|proxy| {
                if let Some(proxy) = proxy.as_mapping_mut()
                    && proxy.get("dialer-proxy").is_some()
                {
                    proxy.remove("dialer-proxy");
                }
            });
        }

        if let Some(Value::Sequence(dialer_proxies)) = proxy_chain_config {
            if !config.contains_key("proxies") {
                config.insert("proxies".into(), Value::Sequence(Vec::new()));
            }

            // [CVR-PATCH] 跨订阅节点全量扫描：收集订阅 profiles 目录与 proxy-providers 下所有 yaml 文件
            let mut search_paths: Vec<std::path::PathBuf> = Vec::new();
            if let Ok(profiles_dir) = crate::utils::dirs::app_profiles_dir() {
                if let Ok(entries) = std::fs::read_dir(profiles_dir) {
                    for entry in entries.flatten() {
                        let path = entry.path();
                        if path.extension().and_then(|s| s.to_str()) == Some("yaml") {
                            search_paths.push(path);
                        }
                    }
                }
            }

            let app_home = crate::utils::dirs::app_home_dir().ok();
            if let Some(Value::Mapping(providers)) = config.get("proxy-providers") {
                for p in providers.values().filter_map(|p| p.get("path").and_then(Value::as_str)) {
                    let path = std::path::Path::new(p);
                    if path.is_absolute() {
                        search_paths.push(path.to_path_buf());
                    } else if let Some(home) = app_home.as_ref() {
                        search_paths.push(home.join(path));
                    }
                }
            }

            // 针对链中每一个节点（包括入口节点与后续节点），若不在当前主配置中，则从订阅文件中找到并完整注入
            if let Some(Value::Sequence(proxies)) = config.get_mut("proxies") {
                for (i, dialer_proxy) in dialer_proxies.iter().enumerate() {
                    let exists = proxies.iter().any(|p| p.get("name") == Some(dialer_proxy));
                    if !exists {
                        for path in &search_paths {
                            if let Ok(content) = std::fs::read_to_string(path)
                                && let Ok(yaml_data) = serde_yaml_ng::from_str::<Mapping>(&content)
                                && let Some(Value::Sequence(p_nodes)) = yaml_data.get("proxies")
                            {
                                if let Some(found_node) = p_nodes.iter().find(|n| n.get("name") == Some(dialer_proxy)) {
                                    if let Some(mut cloned_map) = found_node.as_mapping().cloned() {
                                        if i != 0 && let Some(prev_proxy) = dialer_proxies.get(i - 1) {
                                            cloned_map.insert("dialer-proxy".into(), prev_proxy.to_owned());
                                        } else {
                                            cloned_map.remove("dialer-proxy");
                                        }
                                        cloned_map.insert("_injected_chain_proxy".into(), Value::Bool(true));
                                        proxies.push(Value::Mapping(cloned_map));
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // 统一为链式代理节点绑定正确的上游 dialer-proxy（入口节点移除 dialer-proxy）
            if let Some(Value::Sequence(proxies)) = config.get_mut("proxies") {
                for (i, dialer_proxy) in dialer_proxies.iter().enumerate() {
                    if let Some(Value::Mapping(proxy)) =
                        proxies.iter_mut().find(|proxy| proxy.get("name") == Some(dialer_proxy))
                    {
                        if i != 0 && let Some(prev_proxy) = dialer_proxies.get(i - 1) {
                            proxy.insert("dialer-proxy".into(), prev_proxy.to_owned());
                        } else {
                            proxy.remove("dialer-proxy");
                        }
                    }
                }
            }
        }
    }`,
      },
    ],
  },
];
