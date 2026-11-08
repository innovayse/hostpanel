/**
 * Per page SEO for the Hostpanel documentation site: description, keywords, canonical link, Open Graph
 * and Twitter tags with per section share images, JSON-LD and the sitemap filter. Nothing here knows a host name; the public address
 * comes in as `siteUrl`, which the site config builds from `DOCS_SITE_URL` and `DOCS_BASE`.
 */
import { existsSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import type { HeadConfig, PageData, SiteConfig, TransformContext } from 'vitepress'
import { canonicalOf, hasSitemap, isNoindexByVersion, latestLinkOf } from './versions'
import { VersionKind } from './theme/versionLinks'
import type { DocsVersion } from './versions'

/** The sitemap entry shape the sitemap option hands to `transformItems`. */
type SitemapEntry = { url: string }

/** Longest description kept in the page head, in characters. */
const MAX_DESCRIPTION = 160

/** Shortest paragraph that is worth using as a description, in characters. */
const MIN_DESCRIPTION = 40

/** Display name of each top level section, used for breadcrumbs. */
const SECTION_NAMES: Record<string, string> = {
  'get-started': 'Get started',
  operators: 'Operators',
  staff: 'Staff',
  customers: 'Customers',
  payments: 'Payments',
  servers: 'Servers and SSH',
  migration: 'Migration',
  developers: 'Developers',
  'api-reference': 'API reference',
  changelog: 'Changelog',
}

/** Description used when a page has neither a frontmatter description nor a usable first paragraph. */
const SECTION_DESCRIPTIONS: Record<string, string> = {
  'get-started': 'Get started with Hostpanel, the open source web hosting control panel: requirements and a quick start.',
  operators: 'Install, upgrade, back up and configure Hostpanel, the open source web hosting control panel.',
  staff: 'Staff guide to Hostpanel: clients, orders, billing, products, domains and support tickets.',
  customers: 'Customer guide to the Hostpanel client portal: orders, invoices, services, domains and tickets.',
  payments: 'Set up payment gateways and online payments in Hostpanel, the open source web hosting control panel.',
  servers: 'Connect servers, manage SSH keys and provision hosting accounts with Hostpanel.',
  migration: 'Move your hosting business to Hostpanel and read the import report.',
  developers: 'Developer documentation for Hostpanel: architecture, local setup, tests and plugins.',
  'api-reference': 'Hostpanel REST API reference: authentication, errors, languages, rate limits and every endpoint.',
  changelog: 'What changed in each Hostpanel release.',
}

/** Description used for pages outside every known section. */
const DEFAULT_DESCRIPTION = 'Documentation for Hostpanel, the open source web hosting control panel.'

/** Name shown as the site in link previews. */
const SITE_NAME = 'Hostpanel'

/** Share image sizes, in the order the tags are written. The first one is what most crawlers use. */
const OG_SIZES: { width: number; height: number }[] = [
  { width: 1200, height: 630 },
  { width: 1200, height: 675 },
  { width: 1200, height: 1200 },
  { width: 600, height: 315 },
]

/** The size the Twitter card points at. */
const TWITTER_SIZE = { width: 1200, height: 675 }

/** File name prefix of the share images of the home page and of pages outside every section. */
const DEFAULT_IMAGE_KEY = 'default'

/** Most keywords written for one page. */
const MAX_KEYWORDS = 10

/** Words that say nothing as a keyword when they come from a page title. */
const STOP_WORDS = new Set(['and', 'the', 'for', 'with', 'your', 'how', 'are', 'from', 'new'])

/** Keywords of each top level section, written before the words of the page title. */
const SECTION_KEYWORDS: Record<string, string[]> = {
  'get-started': ['hostpanel', 'hosting control panel', 'open source', 'quick start'],
  operators: ['hostpanel', 'install', 'upgrade', 'backup', 'configuration'],
  staff: ['hostpanel', 'hosting billing', 'admin guide', 'support tickets'],
  customers: ['hostpanel', 'client portal', 'hosting account', 'invoices'],
  payments: ['hostpanel', 'payment gateways', 'online payments', 'billing'],
  servers: ['hostpanel', 'server management', 'ssh keys', 'provisioning'],
  migration: ['hostpanel', 'migration', 'data import', 'hosting billing'],
  developers: ['hostpanel', 'developer guide', 'plugins', 'open source'],
  'api-reference': ['hostpanel', 'rest api', 'api reference', 'openapi'],
  changelog: ['hostpanel', 'changelog', 'release notes'],
}

/** Keywords of pages outside every section. */
const DEFAULT_KEYWORDS = ['hostpanel', 'hosting control panel', 'web hosting billing', 'open source']

/** Matches the container that marks a page as a stub. */
const WORK_IN_PROGRESS = /^:::\s*warning\s+Work in progress\b/im

/** Source path of the page VitePress builds for unknown addresses. */
const NOT_FOUND_PAGE = '404.md'

/** Matches the sitemap address of the 404 page, with or without the extension. */
const NOT_FOUND_URL = /(^|\/)404(\.html)?$/

/**
 * Sitemap style addresses of the pages that must stay out of the index, filled while pages are
 * rendered. The sitemap is written after every page has been rendered, so the set is complete then.
 */
const noindexUrls = new Set<string>()

/**
 * Turns a source path into the address VitePress writes into the sitemap: no `index.md`, no extension.
 * @param relativePath Path of the page source relative to the docs root, such as `staff/index.md`.
 * @returns The address without a leading slash, such as `staff/`; empty for the home page.
 */
const toUrlPath = (relativePath: string): string => {
  return relativePath.replace(/(^|\/)index\.md$/, '$1').replace(/\.md$/, '')
}

/** Named HTML entities that appear in the docs sources, with the character each one stands for. */
const NAMED_ENTITIES: Record<string, string> = {
  amp: '&',
  lt: '<',
  gt: '>',
  quot: '"',
  apos: "'",
  nbsp: ' ',
}

/**
 * Marks the place of a code span while the rest of a paragraph is cleaned. It is a private use
 * character, so it can never be part of the text or of an emphasis marker.
 */
const CODE_MARK = ''

/**
 * Turns HTML entities back into the characters they stand for, so the head writer escapes the text once.
 * @param text Text that may hold named or numeric entities such as `&amp;` or `&#39;`.
 * @returns The text with every known entity decoded; an unknown entity stays as written.
 */
const decodeEntities = (text: string): string => {
  return text.replace(/&(#x[0-9a-f]+|#\d+|[a-z]+);/gi, (match: string, body: string) => {
    if (body[0] === '#') {
      const code = body[1].toLowerCase() === 'x' ? parseInt(body.slice(2), 16) : parseInt(body.slice(1), 10)
      return Number.isInteger(code) && code > 0 && code <= 0x10ffff ? String.fromCodePoint(code) : match
    }
    return NAMED_ENTITIES[body.toLowerCase()] ?? match
  })
}

/**
 * Strips the markdown of one paragraph down to plain text. Code spans are taken out first and put back
 * last, so the text inside them (`PANEL_HOST_ADDRESSES`, `*.example.com`) is never read as emphasis.
 * Outside code, a marker only counts as emphasis when it hugs the words it wraps, and an underscore
 * only when it does not sit inside a word.
 * @param text The raw paragraph.
 * @returns Plain text with links, code marks, emphasis, comments and tags removed and entities decoded.
 */
export const toPlainText = (text: string): string => {
  const codeSpans: string[] = []
  const stripped = text
    .replace(/(`+)([\s\S]*?[^`])\1(?!`)/g, (_match: string, _ticks: string, code: string) => {
      codeSpans.push(code.trim())
      return `${CODE_MARK}${codeSpans.length - 1}${CODE_MARK}`
    })
    .replace(/<!--[\s\S]*?-->/g, '')
    .replace(/!\[[^\]]*\]\([^)]*\)/g, '')
    .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/<\/?[a-zA-Z][^>]*>/g, '')
    .replace(/\*\*(?=\S)(.+?)(?<=\S)\*\*/g, '$1')
    .replace(/(?<!\w)__(?=\S)(.+?)(?<=\S)__(?!\w)/g, '$1')
    .replace(/(?<![\w*])\*(?=[^\s*])(.+?)(?<=[^\s*])\*(?![\w*])/g, '$1')
    .replace(/(?<!\w)_(?=[^\s_])(.+?)(?<=[^\s_])_(?!\w)/g, '$1')
  return decodeEntities(stripped)
    .replace(new RegExp(`${CODE_MARK}(\\d+)${CODE_MARK}`, 'g'), (_match: string, index: string) => codeSpans[Number(index)])
    .replace(/\s+/g, ' ')
    .trim()
}

/**
 * Cuts a text to the description limit, at a sentence end when one fits, otherwise at a word.
 * @param text Plain text.
 * @returns The text, no longer than the limit.
 */
export const shorten = (text: string): string => {
  if (text.length <= MAX_DESCRIPTION) {
    return text
  }
  const head = text.slice(0, MAX_DESCRIPTION)
  const sentenceEnd = Math.max(head.lastIndexOf('. '), head.lastIndexOf('? '), head.lastIndexOf('! '))
  if (sentenceEnd >= MIN_DESCRIPTION) {
    return head.slice(0, sentenceEnd + 1)
  }
  const wordEnd = head.lastIndexOf(' ')
  return `${head.slice(0, wordEnd > 0 ? wordEnd : MAX_DESCRIPTION).replace(/[,;:\s]+$/, '')}.`
}

/**
 * Finds the first real paragraph of a markdown page. Frontmatter, headings, lists, tables, code,
 * containers (such as the stub warning), raw HTML, multi-line comments, horizontal rules, setext
 * headings, imports and includes are skipped.
 * @param source The markdown source of the page.
 * @returns The paragraph as plain text, or an empty string when the page has none.
 */
export const firstParagraph = (source: string): string => {
  const body = source.replace(/^---\r?\n[\s\S]*?\r?\n---\r?\n?/, '')
  let inFence = false
  let containerDepth = 0
  let inComment = false
  let lines: string[] = []

  /**
   * Closes the paragraph being collected.
   * @returns Its cleaned text when it is long enough to describe a page, otherwise an empty string.
   */
  const flush = (): string => {
    const text = toPlainText(lines.join(' '))
    lines = []
    return text.length >= MIN_DESCRIPTION && !text.includes('{{') ? text : ''
  }

  for (const raw of body.split(/\r?\n/)) {
    const line = raw.trim()
    if (inComment) {
      inComment = !line.includes('-->')
      continue
    }
    if (line.startsWith('<!--') && !line.includes('-->')) {
      flush()
      inComment = true
      continue
    }
    if (/^(=+|-+)$/.test(line) && lines.length > 0) {
      // A setext underline: the lines collected so far were a heading, not a paragraph.
      lines = []
      continue
    }
    if (/^([-*_])(\s*\1){2,}$/.test(line)) {
      // A horizontal rule ends the paragraph before it.
      const done = flush()
      if (done) {
        return done
      }
      continue
    }
    if (/^(```|~~~)/.test(line)) {
      flush()
      inFence = !inFence
      continue
    }
    if (inFence) {
      continue
    }
    if (line.startsWith(':::')) {
      flush()
      containerDepth = line === ':::' ? Math.max(0, containerDepth - 1) : containerDepth + 1
      continue
    }
    if (containerDepth > 0) {
      continue
    }
    const isBlock = line === '' || /^(#|\||>|<|-\s|\*\s|\+\s|\d+[.)]\s|import\s|\{#)/.test(line)
    if (isBlock) {
      const done = flush()
      if (done) {
        return done
      }
      continue
    }
    lines.push(line)
  }
  return flush()
}

/**
 * Reads the markdown source of a page.
 * @param siteConfig The resolved site config, which knows the source directory.
 * @param relativePath Path of the page relative to the docs root.
 * @returns The source text, or an empty string when the file cannot be found.
 */
const readSource = (siteConfig: SiteConfig, relativePath: string): string => {
  const file = resolve(siteConfig.srcDir, relativePath)
  return existsSync(file) ? readFileSync(file, 'utf8') : ''
}

/**
 * Splits a frontmatter keywords value into single keywords.
 * @param value The raw frontmatter value: a comma separated string or a list.
 * @returns The trimmed, non empty keywords.
 */
const toKeywordList = (value: unknown): string[] => {
  const parts = Array.isArray(value) ? value.map(String) : typeof value === 'string' ? value.split(',') : []
  return parts.map((part) => part.trim()).filter((part) => part !== '')
}

/**
 * Removes repeated keywords, ignoring case, and keeps the first ones.
 * @param keywords The candidate keywords in order of importance.
 * @returns At most `MAX_KEYWORDS` distinct keywords.
 */
const uniqueKeywords = (keywords: string[]): string[] => {
  const seen = new Set<string>()
  const result: string[] = []
  for (const keyword of keywords) {
    const key = keyword.toLowerCase()
    if (!seen.has(key)) {
      seen.add(key)
      result.push(keyword)
    }
  }
  return result.slice(0, MAX_KEYWORDS)
}

/**
 * Builds the keywords of a page: its frontmatter keywords, otherwise the section keywords followed by
 * the meaningful words of the page title.
 * @param frontmatterKeywords The raw `keywords` frontmatter value, if any.
 * @param sectionKey First path segment of the page, or an empty string for the home page.
 * @param title The page title.
 * @returns The keywords, distinct and capped.
 */
export const buildKeywords = (frontmatterKeywords: unknown, sectionKey: string, title: string): string[] => {
  const own = toKeywordList(frontmatterKeywords)
  if (own.length > 0) {
    return uniqueKeywords(own)
  }
  const words = title
    .toLowerCase()
    .split(/[^a-z0-9]+/)
    .filter((word) => word.length > 2 && !STOP_WORDS.has(word))
  return uniqueKeywords([...(SECTION_KEYWORDS[sectionKey] ?? DEFAULT_KEYWORDS), ...words])
}

/**
 * Names the share image set of a page after its section.
 * @param sectionKey First path segment of the page.
 * @returns The section key when it has images, otherwise the default set.
 */
const imageKeyOf = (sectionKey: string): string => {
  return SECTION_NAMES[sectionKey] ? sectionKey : DEFAULT_IMAGE_KEY
}

/**
 * Guesses the media type of an image from its extension.
 * @param file Path or URL of the image.
 * @returns The media type, `image/png` when the extension is unknown.
 */
const imageTypeOf = (file: string): string => {
  const extension = file.split('?')[0].split('.').pop()?.toLowerCase() ?? ''
  const types: Record<string, string> = {
    jpg: 'image/jpeg',
    jpeg: 'image/jpeg',
    webp: 'image/webp',
    gif: 'image/gif',
    svg: 'image/svg+xml',
  }
  return types[extension] ?? 'image/png'
}

/**
 * Escapes a JSON-LD document so it cannot close the script tag it sits in.
 * @param data The document.
 * @returns The JSON text.
 */
export const toJsonLd = (data: unknown): string => {
  return JSON.stringify(data).replace(/</g, '\\u003c')
}

/** The version of a build that is not versioned: it is its own latest, so no version rule applies. */
const UNVERSIONED: DocsVersion = { kind: VersionKind.Latest, label: '', latestUrl: '', versionsUrl: '', latestPages: null }

/**
 * Builds the SEO hooks of the site for one public address.
 * @param siteUrl Public origin plus base path of THIS build, without a trailing slash.
 * @param version Which version of the docs this build is; defaults to an unversioned build, which is the latest.
 * @returns The `transformPageData`, `transformHead` and sitemap `transformItems` hooks.
 */
export const createSeo = (siteUrl: string, version: DocsVersion = UNVERSIONED) => {
  /**
   * Fills the description of a page that has none and records the stubs that must stay out of the index.
   * @param pageData The page data VitePress built for one page.
   * @param ctx The build context, which carries the site config.
   */
  const transformPageData = (pageData: PageData, ctx: { siteConfig: SiteConfig }): void => {
    const source = readSource(ctx.siteConfig, pageData.relativePath)
    if (WORK_IN_PROGRESS.test(source)) {
      noindexUrls.add(toUrlPath(pageData.relativePath))
    }
    // The banner of an older release or of next links to this page in the latest docs.
    if (version.kind !== VersionKind.Latest) {
      pageData.frontmatter.docsLatestUrl = latestLinkOf(version, toUrlPath(pageData.relativePath))
    }
    if (pageData.description) {
      return
    }
    const section = pageData.relativePath.split('/')[0]
    pageData.description = shorten(firstParagraph(source)) || SECTION_DESCRIPTIONS[section] || DEFAULT_DESCRIPTION
  }

  /**
   * Builds the head tags that differ from page to page.
   * @param ctx The render context of one page.
   * @returns The tags to add: canonical, keywords, Open Graph with share images, Twitter, robots for stubs and JSON-LD.
   */
  const transformHead = (ctx: TransformContext): HeadConfig[] => {
    const { pageData } = ctx
    if (pageData.relativePath === NOT_FOUND_PAGE) {
      // The 404 page is served for any address: no canonical, no share tags and no structured data, only noindex.
      return [['meta', { name: 'robots', content: 'noindex, follow' }]]
    }
    const urlPath = toUrlPath(pageData.relativePath)
    const url = `${siteUrl}/${urlPath}`
    // What search engines should index: this page for the latest and next, the latest page for an older release.
    const canonical = canonicalOf(version, siteUrl, urlPath)
    const isHome = pageData.relativePath === 'index.md'
    const title = pageData.title || ctx.siteConfig.site.title
    const description = pageData.description || DEFAULT_DESCRIPTION
    const sectionKey = isHome ? '' : pageData.relativePath.split('/')[0]
    const sectionName = SECTION_NAMES[sectionKey]
    const imageAlt = sectionName ? `${SITE_NAME} Docs: ${sectionName}` : `${SITE_NAME} Docs`
    const frontmatterImage = typeof pageData.frontmatter.image === 'string' ? pageData.frontmatter.image.trim() : ''
    const customImage = /^https?:\/\//.test(frontmatterImage) ? frontmatterImage : `${siteUrl}/${frontmatterImage.replace(/^\/+/, '')}`
    const imageSet = OG_SIZES.map((size) => ({
      ...size,
      src: `${siteUrl}/og/${imageKeyOf(sectionKey)}-${size.width}x${size.height}.png`,
    }))
    const twitterImage = imageSet.find((image) => image.width === TWITTER_SIZE.width && image.height === TWITTER_SIZE.height)
    const modified = pageData.lastUpdated ? new Date(pageData.lastUpdated).toISOString() : ''
    const tags: HeadConfig[] = [
      ['link', { rel: 'canonical', href: canonical }],
      // VitePress writes its own description tag without escaping; a tag given here replaces it and is escaped once.
      ['meta', { name: 'description', content: description }],
      ['meta', { name: 'keywords', content: buildKeywords(pageData.frontmatter.keywords, sectionKey, title).join(', ') }],
      ['meta', { property: 'og:site_name', content: SITE_NAME }],
      ['meta', { property: 'og:locale', content: ctx.siteConfig.site.lang.replace('-', '_') }],
      ['meta', { property: 'og:type', content: isHome ? 'website' : 'article' }],
      ['meta', { property: 'og:title', content: title }],
      ['meta', { property: 'og:description', content: description }],
      ['meta', { property: 'og:url', content: canonical }],
      ['meta', { name: 'twitter:title', content: title }],
      ['meta', { name: 'twitter:description', content: description }],
    ]
    if (frontmatterImage) {
      // A page that sets its own image has one file, so its size is unknown and only the type and alt are written.
      tags.push(
        ['meta', { property: 'og:image', content: customImage }],
        ['meta', { property: 'og:image:type', content: imageTypeOf(customImage) }],
        ['meta', { property: 'og:image:alt', content: imageAlt }],
        ['meta', { name: 'twitter:image', content: customImage }],
        ['meta', { name: 'twitter:image:alt', content: imageAlt }],
      )
    } else {
      for (const image of imageSet) {
        tags.push(
          ['meta', { property: 'og:image', content: image.src }],
          ['meta', { property: 'og:image:width', content: String(image.width) }],
          ['meta', { property: 'og:image:height', content: String(image.height) }],
          ['meta', { property: 'og:image:type', content: 'image/png' }],
          ['meta', { property: 'og:image:alt', content: imageAlt }],
        )
      }
      tags.push(
        ['meta', { name: 'twitter:image', content: (twitterImage ?? imageSet[0]).src }],
        ['meta', { name: 'twitter:image:alt', content: imageAlt }],
      )
    }
    if (noindexUrls.has(urlPath) || isNoindexByVersion(version, urlPath)) {
      tags.push(['meta', { name: 'robots', content: 'noindex, follow' }])
    }
    if (isHome) {
      return tags
    }

    if (sectionName) {
      tags.push(['meta', { property: 'article:section', content: sectionName }])
    }
    if (modified) {
      tags.push(['meta', { property: 'article:modified_time', content: modified }])
    }
    const crumbs: { name: string; url: string }[] = [{ name: ctx.siteConfig.site.title, url: `${siteUrl}/` }]
    if (sectionName && !/^[^/]+\/index\.md$/.test(pageData.relativePath)) {
      crumbs.push({ name: sectionName, url: `${siteUrl}/${sectionKey}/` })
    }
    crumbs.push({ name: title, url })

    tags.push(
      [
        'script',
        { type: 'application/ld+json' },
        toJsonLd({
          '@context': 'https://schema.org',
          '@type': 'BreadcrumbList',
          itemListElement: crumbs.map((crumb, index) => ({
            '@type': 'ListItem',
            position: index + 1,
            name: crumb.name,
            item: crumb.url,
          })),
        }),
      ],
      [
        'script',
        { type: 'application/ld+json' },
        toJsonLd({
          '@context': 'https://schema.org',
          '@type': 'TechArticle',
          headline: title,
          description,
          url: canonical,
          mainEntityOfPage: canonical,
          image: frontmatterImage ? customImage : imageSet.map((image) => image.src),
          inLanguage: ctx.siteConfig.site.lang,
          ...(version.label ? { version: version.label } : {}),
          ...(modified ? { dateModified: modified } : {}),
        }),
      ],
    )
    return tags
  }

  /**
   * Leaves the stub pages and the 404 page out of the sitemap; an older release and next have no sitemap entries.
   * @param items The sitemap entries VitePress collected.
   * @returns The entries of the pages that may be indexed.
   */
  const sitemapItems = <T extends SitemapEntry>(items: T[]): T[] => {
    if (!hasSitemap(version)) {
      return []
    }
    return items.filter((item) => !noindexUrls.has(item.url) && !NOT_FOUND_URL.test(item.url))
  }

  return { transformPageData, transformHead, sitemapItems }
}
