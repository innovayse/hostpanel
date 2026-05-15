/**
 * Shared WHMCS helpers — auto-imported by Nuxt from utils/
 */

/** Maps locale code to the currency code used by WHMCS pricing */
export const currencyByLocale: Record<string, string> = { hy: 'AMD', ru: 'RUB', en: 'USD' }

/** All product group IDs shown on /products (hosting + SaaS) */
export const productGids = [1, 3, 4, 5, 6, 7, 8, 9]

/** Group IDs for SaaS-only families (no hosting) */
export const saasGids = [3, 4, 5, 6, 7, 8, 9]

/** Maps gid → config key (slug). Gids match WHMCS product groups. */
export const productGidToKey: Record<number, string> = {
  1: 'web-hosting',
  3: 'smartlearn-system',
  4: 'propsystem-pro',
  5: 'shopkit-pro',
  6: 'metricskit-pro',
  7: 'quickbite',
  8: 'kelpie-ai',
  9: 'taskero'
}

/** Visual + meta config for each product family, keyed by slug */
export const productConfig: Record<string, { name: string; icon: string; color: string; demoUrl?: string; learnMoreUrl: string }> = {
  'web-hosting':       { name: 'Web Hosting',       icon: 'cloud-check',           color: '#0ea5e9',                                             learnMoreUrl: '/hosting' },
  'smartlearn-system': { name: 'SmartLearn System', icon: 'school',                color: '#8b5cf6', demoUrl: '#',       learnMoreUrl: '/products/smartlearn-system' },
  'propsystem-pro':    { name: 'PropSystem Pro',    icon: 'office-building',       color: '#06b6d4', demoUrl: '#',       learnMoreUrl: '/products/propsystem-pro' },
  'shopkit-pro':       { name: 'ShopKit Pro',       icon: 'cart',                  color: '#f59e0b', demoUrl: '#',      learnMoreUrl: '/products/shopkit-pro' },
  'metricskit-pro':    { name: 'MetricsKit Pro',    icon: 'chart-bar',             color: '#ec4899', demoUrl: '#',       learnMoreUrl: '/products/metricskit-pro' },
  'quickbite':         { name: 'QuickBite',         icon: 'silverware-fork-knife', color: '#ef4444', demoUrl: '#', learnMoreUrl: '/products/quickbite' },
  'kelpie-ai':         { name: 'Kelpie AI',         icon: 'brain',                 color: '#7c3aed', demoUrl: 'https://kelpie-ai.ai',               learnMoreUrl: '/products/kelpie-ai' },
  'taskero':           { name: 'Taskero',           icon: 'view-dashboard',        color: '#10b981', demoUrl: '#',   learnMoreUrl: '/products/taskero' }
}

/**
 * Get a translated field from a WHMCS product object.
 * Checks group_translations first (group-level name/tagline/headline),
 * then the product-level translated_* field, then the base field.
 */
export function getGt(p: any, field: string): string {
  return p?.group_translations?.[`translated_${field}`]
    || p?.[`translated_${field}`]
    || p?.[field]
    || ''
}

/**
 * Extract structured features from a WHMCS product's `group_features` field.
 * Uses `translated_feature` when available, falls back to the base `feature` string.
 * Returns an empty array if the product has no group_features.
 *
 * @param p - Raw WHMCS product object
 * @returns Array of feature label strings
 */
export function getGroupFeatures(p: any): string[] {
  const features: any[] = Array.isArray(p?.group_features) ? p.group_features : []
  return features.map(f => f.translated_feature || f.feature || '').filter(Boolean)
}

/** Normalize a product name to a slug key: "SmartLearn System" → "smartlearn-system" */
export function nameToKey(name: string): string {
  return name.toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, '')
}

/**
 * Parse a WHMCS description text into a summary paragraph and a features list.
 * - Summary  = lines before the first blank line or section header
 * - Features = all subsequent non-empty lines (bullet prefix stripped)
 * - Lines ending with ":" are treated as section headers in any language and skipped
 */
export function parseDescription(text: string): { summary: string; features: string[] } {
  const summaryLines: string[] = []
  const features: string[] = []
  let pastSummary = false

  for (const line of text.split(/\r?\n/)) {
    const t = line.trim()
    const isHeader = /:\s*$/.test(t)

    if (!pastSummary) {
      if (!t) {
        if (summaryLines.length) pastSummary = true
      } else if (isHeader) {
        pastSummary = true
      } else if (t.startsWith('\u2022') || t.startsWith('-') || t.startsWith('*')) {
        pastSummary = true
        features.push(t.replace(/^[\u2022\-*]\s*/, '').trim())
      } else {
        summaryLines.push(t)
      }
    } else {
      if (!t || isHeader) continue
      features.push(t.replace(/^[\u2022\-*]\s*/, '').trim())
    }
  }

  return { summary: summaryLines.join(' '), features }
}
