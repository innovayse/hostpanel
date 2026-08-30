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

/*
 * `getGt` and `getGroupFeatures` used to live here.
 *
 * `getGt(p, field)` read `p.group_translations.translated_<field>`, then
 * `p.translated_<field>`, then `p.<field>`; `getGroupFeatures(p)` read
 * `p.group_features[].translated_feature`. Neither field is on `ProductDto`, and
 * `GET /api/portal/public/products` — verified against a live backend — sends only
 * `ProductDto`'s own fields, so the first two branches of `getGt` never resolved and
 * `getGroupFeatures` always returned `[]`. Callers now read the base field directly.
 *
 * Structured, per-product specification lines do exist, but as their own resource:
 * `GET /api/portal/public/product-features` (`ProductFeature` — label/value/sortOrder),
 * reached through `useCatalogApi().loadProductFeatures(groupId)`. That is what a caller
 * wanting `group_features` wants today.
 *
 * Localised product copy does not exist anywhere in the backend: there is no product
 * translations table and no `lang` handling in `ProductsController`. `Slide` is the only
 * entity with a `*Translation` sibling, and `loadSlides(lang)` is the one place a `lang`
 * parameter still means something. Making product copy translatable is a backend change —
 * a `ProductTranslation` entity alongside `SlideTranslation` — not a frontend one.
 */

/** Normalize a product name to a slug key: "SmartLearn System" → "smartlearn-system" */
export function nameToKey(name: string): string {
  return name.toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, '')
}

const ENTITIES: Record<string, string> = {
  amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", nbsp: ' ', '#39': "'", '#x27': "'",
}

/**
 * Flatten a WHMCS description into plain text lines.
 *
 * WHMCS lets each product's description be edited as HTML, so the same field
 * arrives either as newline-separated plain text or as one long line with
 * <br /> between items — the two are indistinguishable to whoever typed them,
 * and both are in use across the catalogue. Line-based parsing sees the HTML
 * variant as a single unbroken line, which is how raw "<strong>…<br />" ended
 * up printed on the product cards. Convert the block-level tags back into the
 * newlines they stand for, drop the rest, and the parser below works on either.
 */
function htmlToLines(text: string): string {
  return text
    .replace(/<br\s*\/?>/gi, '\n')
    .replace(/<\/(p|div|li|ul|ol|h[1-6])\s*>/gi, '\n')
    .replace(/<li\b[^>]*>/gi, '\n• ')
    .replace(/<[^>]+>/g, '')
    .replace(/&(#x?[0-9a-f]+|[a-z]+);/gi, (m, e: string) => ENTITIES[e.toLowerCase()] ?? m)
}

/**
 * Bullet markers seen across the catalogue: •, -, * and both check marks.
 *
 * U+2713 ✓ and U+2714 ✔ sit one codepoint apart and look identical at body size.
 * Only the first was listed, and the catalogue is written with the second, so no
 * feature line was ever recognised as one: every item fell through to the summary,
 * and a card printed one long paragraph with check marks running through it.
 */
const BULLET = /^[\u2022\u2713\u2714\-*]\s*/

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

  for (const line of htmlToLines(text).split(/\r?\n/)) {
    const t = line.trim()
    const isHeader = /:\s*$/.test(t)

    if (!pastSummary) {
      if (!t) {
        if (summaryLines.length) pastSummary = true
      } else if (isHeader) {
        pastSummary = true
      } else if (BULLET.test(t)) {
        pastSummary = true
        features.push(t.replace(BULLET, '').trim())
      } else {
        summaryLines.push(t)
      }
    } else {
      if (!t || isHeader) continue
      features.push(t.replace(BULLET, '').trim())
    }
  }

  return { summary: summaryLines.join(' '), features }
}
