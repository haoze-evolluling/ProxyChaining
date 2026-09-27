import { CHAIN_RULES } from './patch-rules-chain.mjs';
import { UPDATE_RULES } from './patch-rules-update.mjs';

/**
 * 完整补丁规则集合（链式代理 + 禁用自动更新）
 */
export const PATCH_RULES = [
  ...CHAIN_RULES,
  ...UPDATE_RULES,
];
