import path from 'node:path';

/**
 * 禁用自动更新补丁规则
 */
export const UPDATE_RULES = [
  {
    id: 'disable-update-service',
    relPath: path.join('src', 'services', 'update.ts'),
    description: '屏蔽在线检查更新服务与请求，直接返回最新状态，防止拉取官方更新包',
    chunks: [
      {
        name: 'checkUpdateSafe 默认屏蔽检查更新请求并返回 null',
        find: `export const checkUpdateSafe = async (
  options?: CheckOptions,
): Promise<Update | null> => {
  const result = await check(options ?? {})
  if (!result) return null`,
        replace: `export const checkUpdateSafe = async (
  _options?: CheckOptions,
): Promise<Update | null> => {
  // [CVR-PATCH] 默认屏蔽在线检查更新，防止官方更新覆盖自定义构建版本
  return null
}

export const checkUpdateInternal = async (
  options?: CheckOptions,
): Promise<Update | null> => {
  const result = await check(options ?? {})
  if (!result) return null`,
      },
    ],
  },
  {
    id: 'disable-update-hook',
    relPath: path.join('src', 'hooks', 'use-update.ts'),
    description: '默认关闭前端自动检查更新逻辑与定时请求，避免覆盖自定义版本',
    chunks: [
      {
        name: 'shouldCheck 默认仅在显式为 true 时触发更新',
        find: `  const { verge } = useVerge()
  const { auto_check_update } = verge || {}

  const shouldCheck = enabled && auto_check_update !== false`,
        replace: `  const { verge } = useVerge()
  const { auto_check_update } = verge || {}

  // [CVR-PATCH] 默认关闭自动检查更新，避免官方升级覆盖自定义补丁版本
  const shouldCheck = enabled && auto_check_update === true`,
      },
    ],
  },
  {
    id: 'disable-update-ui',
    relPath: path.join('src', 'components', 'setting', 'mods', 'misc-viewer.tsx'),
    description: '将设置中心杂项中的自动检查更新开关默认值设为关闭 (false)',
    chunks: [
      {
        name: '初始状态中 autoCheckUpdate 默认为 false',
        find: `    appLogMaxCount: 12,
    autoCloseConnection: true,
    autoCheckUpdate: true,
    enableBuiltinEnhanced: true,`,
        replace: `    appLogMaxCount: 12,
    autoCloseConnection: true,
    // [CVR-PATCH] 默认关闭自动检查更新
    autoCheckUpdate: false,
    enableBuiltinEnhanced: true,`,
      },
      {
        name: '配置回退中 autoCheckUpdate 默认为 false',
        find: `        autoCloseConnection: verge?.auto_close_connection ?? true,
        autoCheckUpdate: verge?.auto_check_update ?? true,
        enableBuiltinEnhanced: verge?.enable_builtin_enhanced ?? true,`,
        replace: `        autoCloseConnection: verge?.auto_close_connection ?? true,
        // [CVR-PATCH] 默认关闭自动检查更新
        autoCheckUpdate: verge?.auto_check_update ?? false,
        enableBuiltinEnhanced: verge?.enable_builtin_enhanced ?? true,`,
      },
    ],
  },
  {
    id: 'disable-update-config',
    relPath: path.join('src-tauri', 'src', 'config', 'verge.rs'),
    description: '将后端配置默认 auto_check_update 设为 false',
    chunks: [
      {
        name: '后端 verge.rs 默认 auto_check_update 设为 false',
        find: `            auto_close_connection: Some(true),
            auto_check_update: Some(true),
            enable_builtin_enhanced: Some(true),`,
        replace: `            auto_close_connection: Some(true),
            // [CVR-PATCH] 默认关闭自动检查更新
            auto_check_update: Some(false),
            enable_builtin_enhanced: Some(true),`,
      },
    ],
  },
  {
    id: 'disable-update-silent',
    relPath: path.join('src-tauri', 'src', 'core', 'updater.rs'),
    description: '静默更新检测默认关闭，且在启动时清理残留缓存',
    chunks: [
      {
        name: 'SilentUpdater check_and_download 默认不自动下载更新',
        find: `        let auto_check = Config::verge().await.latest_arc().auto_check_update.unwrap_or(true);
        if !auto_check {
            logging!(debug, Type::System, "Silent update skipped: auto_check_update is false");
            return Ok(());
        }`,
        replace: `        // [CVR-PATCH] 默认不自动执行后台静默检查与下载更新
        let auto_check = Config::verge().await.latest_arc().auto_check_update.unwrap_or(false);
        if !auto_check {
            logging!(debug, Type::System, "Silent update skipped: auto_check_update is false");
            return Ok(());
        }`,
      },
    ],
  },
];
