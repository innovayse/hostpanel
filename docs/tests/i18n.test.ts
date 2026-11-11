/**
 * Tests for the languages of the documentation: the language files carry the same keys, every sidebar link
 * exists in every language (as a translated page or as a generated placeholder), the placeholders are
 * written correctly, and the SEO hooks write `hreflang`, canonical and `noindex` as the rules say.
 */
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterEach, describe, expect, it } from 'vitest'
import type { PageData, SiteConfig, TransformContext } from 'vitepress'
import glossary from '../.vitepress/i18n/glossary.json'
import { DEFAULT_LOCALE, isTranslatable, LOCALE_KEYS, LocaleKey, localeOfPath, localizeLink, stripLocale, TRANSLATED_LOCALES, UNTRANSLATED, withLocale } from '../.vitepress/i18n/locales'
import { buildApiSidebar, buildLocaleThemeConfig, buildMainSidebar, buildNav, buildSearchTranslations, linkIn, sidebarLinks } from '../.vitepress/i18n/themeConfig'
import { format, localeOfLang, stringsFor, UI_STRINGS } from '../.vitepress/i18n/ui'
import { createSeo } from '../.vitepress/seo'
import {
  buildFallbackSource,
  generateFallbacks,
  isUntranslated,
  listEnglishPages,
  pageKey,
  readFallbackText,
  readSettings,
  rewriteTarget,
} from '../scripts/lib/localeFallbacks.mjs'

/** The docs folder. */
const docsRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')

/**
 * Lists every key path of an object, so two language files can be compared.
 * @param value The object.
 * @param prefix The path so far.
 * @returns Key paths such as `components.notFound.title`; arrays count as one leaf.
 */
const keyPaths = (value: unknown, prefix = ''): string[] => {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    return [prefix]
  }
  return Object.entries(value as Record<string, unknown>).flatMap(([key, child]) => keyPaths(child, prefix ? `${prefix}.${key}` : key))
}

describe('language files', () => {
  const english = keyPaths(UI_STRINGS[LocaleKey.En]).sort()

  it.each(TRANSLATED_LOCALES.map((locale) => [locale]))('%s has exactly the keys of English', (locale) => {
    expect(keyPaths(UI_STRINGS[locale]).sort()).toEqual(english)
  })

  it.each(LOCALE_KEYS.map((locale) => [locale]))('%s has no empty text', (locale) => {
    const empty = keyPaths(UI_STRINGS[locale]).filter((path) => {
      const value = path.split('.').reduce<unknown>((node, key) => (node as Record<string, unknown>)[key], UI_STRINGS[locale])
      return typeof value === 'string' && value.trim() === ''
    })
    expect(empty).toEqual([])
  })

  it.each(TRANSLATED_LOCALES.map((locale) => [locale]))('%s keeps the placeholders of every text', (locale) => {
    /**
     * Lists the `{name}` placeholders of a text.
     * @param text The text.
     * @returns The placeholders, sorted.
     */
    const placeholders = (text: string): string[] => (text.match(/\{\w+\}/g) ?? []).sort()
    for (const path of english) {
      /**
       * Reads the text at the current key path.
       * @param strings A language file.
       * @returns The value at the path.
       */
      const read = (strings: object): unknown => path.split('.').reduce<unknown>((node, key) => (node as Record<string, unknown>)[key], strings)
      const reference = read(UI_STRINGS[LocaleKey.En])
      if (typeof reference === 'string') {
        expect(placeholders(String(read(UI_STRINGS[locale])))).toEqual(placeholders(reference))
      }
    }
  })

  it('has a title for every link of the sidebar in every language', () => {
    for (const locale of LOCALE_KEYS) {
      for (const link of sidebarLinks()) {
        expect((UI_STRINGS[locale].pages as Record<string, string>)[link], `${locale} ${link}`).toBeTruthy()
      }
    }
  })

  it('translates the search box texts of every language', () => {
    for (const locale of TRANSLATED_LOCALES) {
      expect(buildSearchTranslations(stringsFor(locale))).not.toEqual(buildSearchTranslations(stringsFor(DEFAULT_LOCALE)))
    }
  })

  it('has every core term of the glossary in all three languages', () => {
    expect(glossary.terms.length).toBeGreaterThan(20)
    for (const term of glossary.terms) {
      for (const locale of LOCALE_KEYS) {
        expect((term as Record<string, string>)[locale], `${term.en} in ${locale}`).toBeTruthy()
      }
    }
  })
})

describe('language helpers', () => {
  it('tells the language of a path', () => {
    expect(localeOfPath('ru/staff/orders.md')).toBe(LocaleKey.Ru)
    expect(localeOfPath('/hy/')).toBe(LocaleKey.Hy)
    expect(localeOfPath('staff/orders')).toBe(LocaleKey.En)
    expect(localeOfPath('russia/page')).toBe(LocaleKey.En)
  })

  it('strips and adds the language folder', () => {
    expect(stripLocale('ru/staff/orders')).toBe('staff/orders')
    expect(stripLocale('ru/')).toBe('')
    expect(withLocale(LocaleKey.Hy, 'staff/orders')).toBe('hy/staff/orders')
    expect(withLocale(LocaleKey.Ru, '')).toBe('ru/')
    expect(withLocale(LocaleKey.En, '/staff/')).toBe('staff/')
    expect(localizeLink(LocaleKey.Ru, '/')).toBe('/ru/')
  })

  it('reads the language of a lang attribute', () => {
    expect(localeOfLang('ru-RU')).toBe(LocaleKey.Ru)
    expect(localeOfLang('hy')).toBe(LocaleKey.Hy)
    expect(localeOfLang(undefined)).toBe(LocaleKey.En)
    expect(localeOfLang('fr-FR')).toBe(LocaleKey.En)
  })

  it('fills placeholders', () => {
    expect(format('Version {version}', { version: 'v0.3' })).toBe('Version v0.3')
    expect(format('Keep {other}', {})).toBe('Keep {other}')
  })

  it('agrees with the build script on which pages are not translated', () => {
    for (const path of ['components.md', 'api-reference/_partials/error-codes.md', 'staff/orders.md', 'index.md']) {
      expect(isTranslatable(path)).toBe(!isUntranslated(path, [...UNTRANSLATED]))
    }
  })
})

describe('navigation and sidebar', () => {
  it('keeps the English addresses unchanged', () => {
    const nav = buildNav(LocaleKey.En, stringsFor(LocaleKey.En), '')
    expect(nav.map((entry) => entry.link)).toEqual(['/', '/get-started/', '/operators/', '/api-reference/', '/changelog/'])
  })

  it.each(TRANSLATED_LOCALES.map((locale) => [locale]))('puts every page link of %s below its folder', (locale) => {
    const strings = stringsFor(locale)
    const links = [...buildNav(locale, strings, ''), ...buildMainSidebar(locale, strings).flatMap((group) => group.items ?? [])].map((entry) => entry.link ?? '')
    for (const link of links) {
      if (link === '/components') {
        continue
      }
      expect(link.startsWith(`/${locale}/`), link).toBe(true)
    }
  })

  it('keeps a page that is never translated at its English address', () => {
    expect(linkIn(LocaleKey.Ru, '/components')).toBe('/components')
  })

  it('translates and localizes the generated endpoint groups', () => {
    const generated = [{ text: 'Admin endpoints', items: [{ text: 'Clients', items: [{ text: 'List', link: '/api-reference/admin/clients' }] }] }]
    const sidebar = buildApiSidebar(LocaleKey.Ru, stringsFor(LocaleKey.Ru), generated)
    const group = sidebar[1]
    expect(group.text).toContain('Конечные точки администратора')
    expect(group.items?.[0].items?.[0].link).toBe('/ru/api-reference/admin/clients')
  })

  it('builds the labels of the default theme for every language', () => {
    for (const locale of LOCALE_KEYS) {
      const theme = buildLocaleThemeConfig(locale, stringsFor(locale), { demoUrl: '', generated: [], editPattern: 'x', copyright: 'c' })
      expect(theme.outline.label).toBe(stringsFor(locale).theme.outlineTitle)
      expect(theme.docFooter.next).toBe(stringsFor(locale).theme.docFooterNext)
      expect(Object.keys(theme.sidebar)).toEqual([linkIn(locale, '/api-reference/'), linkIn(locale, '/')])
    }
  })

  it('adds the demo link only when there is a demo', () => {
    expect(buildNav(LocaleKey.Hy, stringsFor(LocaleKey.Hy), 'https://demo.test').pop()).toEqual({ text: stringsFor(LocaleKey.Hy).nav.demo, link: 'https://demo.test' })
    expect(buildNav(LocaleKey.Hy, stringsFor(LocaleKey.Hy), '')).toHaveLength(5)
  })
})

describe('the real tree', () => {
  const settings = readSettings()
  const pages = listEnglishPages(docsRoot, settings)
  const keys = new Set(pages.map(pageKey))

  it('has an English page for every sidebar link', () => {
    for (const link of sidebarLinks()) {
      const key = link.replace(/^\//, '')
      // A page that is never translated is not in the list of English pages, but its file exists.
      const exists = isTranslatable(link) ? keys.has(key) || keys.has(`${key}/`) : existsSync(join(docsRoot, 'en', `${key}.md`))
      expect(exists, link).toBe(true)
    }
  })

  it('has every sidebar link in each language as a translated page or as a generated placeholder', () => {
    for (const locale of TRANSLATED_LOCALES) {
      const own = new Set(pages.filter((page) => existsSync(join(docsRoot, settings.locales[locale].folder, page))).map(pageKey))
      for (const link of sidebarLinks()) {
        const key = link.replace(/^\//, '')
        if (!isTranslatable(link)) {
          continue
        }
        // Every English page gets a placeholder unless the language has the page, so either way the page exists.
        expect(keys.has(key) || keys.has(`${key}/`) || own.has(key), `${locale} ${link}`).toBe(true)
      }
    }
  })

  it('translates the home page into every language', () => {
    for (const locale of TRANSLATED_LOCALES) {
      const source = readFileSync(join(docsRoot, locale, 'index.md'), 'utf8')
      expect(source).toContain('layout: home')
      expect(source).toContain(`link: /${locale}/get-started/`)
    }
  })

  it('has no translated page without an English page', () => {
    for (const locale of TRANSLATED_LOCALES) {
      const folder = join(docsRoot, locale)
      for (const page of listEnglishPages(docsRoot, { ...settings, locales: { ...settings.locales, en: { ...settings.locales.en, folder: locale } } })) {
        expect(existsSync(join(docsRoot, 'en', page)), `${locale}/${page}`).toBe(true)
      }
      expect(existsSync(folder)).toBe(true)
    }
  })
})

describe('placeholder pages', () => {
  const made: string[] = []

  afterEach(() => {
    for (const dir of made.splice(0)) {
      rmSync(dir, { recursive: true, force: true })
    }
  })

  /**
   * Writes a small docs tree to a temporary folder.
   * @param files Path below the docs folder to file text.
   * @returns The temporary docs folder.
   */
  const treeOf = (files: Record<string, string>): string => {
    const root = mkdtempSync(join(tmpdir(), 'docs-i18n-'))
    made.push(root)
    for (const [path, text] of Object.entries(files)) {
      mkdirSync(dirname(join(root, path)), { recursive: true })
      writeFileSync(join(root, path), text)
    }
    return root
  }

  it('writes a placeholder for each page a language lacks and none for a page it has', () => {
    const root = treeOf({
      'en/index.md': '---\nlayout: home\n---\n',
      'en/staff/orders.md': '---\ntitle: Orders\n---\n\n# Orders\n\nText.\n',
      'en/components.md': '# Components\n',
      'en/api-reference/_partials/x.md': 'partial\n',
      'ru/index.md': '---\nlayout: home\n---\n',
    })
    const { created, orphans } = generateFallbacks(root)
    expect(created).toEqual({ ru: 1, hy: 2 })
    expect(orphans).toEqual([])
    expect(existsSync(join(root, '_fallback', 'ru', 'staff', 'orders.md'))).toBe(true)
    expect(existsSync(join(root, '_fallback', 'ru', 'index.md'))).toBe(false)
    expect(existsSync(join(root, '_fallback', 'hy', 'index.md'))).toBe(true)
    expect(existsSync(join(root, '_fallback', 'ru', 'components.md'))).toBe(false)
  })

  it('reports a translation that has no English page', () => {
    const root = treeOf({ 'en/index.md': '# Home\n', 'ru/extra.md': '# Extra\n' })
    expect(generateFallbacks(root).orphans).toEqual(['ru/extra.md'])
  })

  it('writes the notice in the language, the English body and the fallback marker', () => {
    const root = treeOf({ 'en/staff/orders.md': '---\ntitle: Orders\n---\n\n# Orders\n\nText.\n' })
    generateFallbacks(root)
    const ru = readFileSync(join(root, '_fallback', 'ru', 'staff', 'orders.md'), 'utf8')
    const text = readFallbackText('ru')
    expect(ru).toContain('title: Orders')
    expect(ru).toContain('i18nFallback: true')
    expect(ru).toContain(`::: warning ${text.title}`)
    expect(ru).toContain('[' + text.original + '](/staff/orders)')
    expect(ru).toContain('# Orders\n\nText.')
  })

  it('keeps the reader in the language with links and leaves code alone', () => {
    const keysOf = new Set(['staff/', 'staff/orders', 'operators/backup'])
    const source = [
      '# Page',
      '',
      '[Orders](/staff/orders#top) and [Backup](../operators/backup.md) and [Home](/staff/) and `[x](/staff/orders)`.',
      '',
      '```md',
      '[code](/staff/orders)',
      '```',
      '',
      '[GitHub](https://example.test/staff/orders) [File](/openapi.json) [Missing](/components) [Done](/ru/staff/orders)',
      '',
      '<!--@include: ./_partials/part.md-->',
    ].join('\n')
    const result = buildFallbackSource(source, 'staff/page.md', 'ru', readFallbackText('ru'), keysOf, ['ru', 'hy'])
    expect(result).toContain('[Orders](/ru/staff/orders#top)')
    expect(result).toContain('[Backup](/ru/operators/backup)')
    expect(result).toContain('[Home](/ru/staff/)')
    expect(result).toContain('`[x](/staff/orders)`')
    expect(result).toContain('[code](/staff/orders)')
    expect(result).toContain('(https://example.test/staff/orders)')
    expect(result).toContain('(/openapi.json)')
    expect(result).toContain('[Missing](/components)')
    expect(result).toContain('[Done](/ru/staff/orders)')
    expect(result).toContain('<!--@include: @/en/staff/_partials/part.md-->')
  })

  it('points a relative link to a page that is never translated at its English address', () => {
    expect(rewriteTarget('../components.md', 'staff/page.md', 'hy', new Set(['staff/']), ['ru', 'hy'])).toBe('/components')
  })
})

describe('hreflang, canonical and noindex', () => {
  const languages = {
    [LocaleKey.En]: new Set(['', 'staff/', 'staff/orders', 'staff/clients']),
    [LocaleKey.Ru]: new Set(['', 'staff/orders']),
    [LocaleKey.Hy]: new Set(['']),
  }
  const seo = createSeo('https://example.test/docs', undefined, languages)

  /**
   * Builds the smallest render context the head hook reads.
   * @param relativePath Address-side path of the page (the English folder is already removed).
   * @param frontmatter The front matter of the page.
   * @returns A context cast to the VitePress type.
   */
  const contextOf = (relativePath: string, frontmatter: Record<string, unknown> = {}): TransformContext =>
    ({
      pageData: { relativePath, title: 'Orders', description: 'A description.', frontmatter } as unknown as PageData,
      siteConfig: { site: { title: 'Hostpanel', lang: 'en-US' } },
    }) as unknown as TransformContext

  /**
   * The alternate links of a head.
   * @param relativePath Path of the page.
   * @returns The `hreflang` to address pairs.
   */
  const alternatesOf = (relativePath: string): Record<string, string> => {
    const tags = seo.transformHead(contextOf(relativePath))
    return Object.fromEntries(tags.filter((tag) => tag[1].rel === 'alternate').map((tag) => [tag[1].hreflang, tag[1].href]))
  }

  it('links a translated page to every language that has it, and x-default to English', () => {
    const expected = {
      en: 'https://example.test/docs/staff/orders',
      ru: 'https://example.test/docs/ru/staff/orders',
      'x-default': 'https://example.test/docs/staff/orders',
    }
    expect(alternatesOf('staff/orders.md')).toEqual(expected)
    expect(alternatesOf('ru/staff/orders.md')).toEqual(expected)
  })

  it('links the home pages of all three languages', () => {
    expect(Object.keys(alternatesOf('index.md')).sort()).toEqual(['en', 'hy', 'ru', 'x-default'])
    expect(alternatesOf('hy/index.md').hy).toBe('https://example.test/docs/hy/')
  })

  it('writes no alternate for a page that has no translation', () => {
    expect(alternatesOf('staff/clients.md')).toEqual({})
  })

  it('gives a placeholder page noindex, a canonical to English and no alternates', () => {
    const tags = seo.transformHead(contextOf('hy/staff/orders.md', { i18nFallback: true }))
    expect(tags).toContainEqual(['link', { rel: 'canonical', href: 'https://example.test/docs/staff/orders' }])
    expect(tags).toContainEqual(['meta', { name: 'robots', content: 'noindex, follow' }])
    expect(tags.filter((tag) => tag[1].rel === 'alternate')).toEqual([])
  })

  it('does not index a translated page as noindex', () => {
    const tags = seo.transformHead(contextOf('ru/staff/orders.md'))
    expect(tags).toContainEqual(['link', { rel: 'canonical', href: 'https://example.test/docs/ru/staff/orders' }])
    expect(tags).not.toContainEqual(['meta', { name: 'robots', content: 'noindex, follow' }])
    expect(tags).toContainEqual(['meta', { property: 'og:locale', content: 'ru_RU' }])
  })

  it('describes a page by the section name of its own language', () => {
    const pageData = { relativePath: 'ru/staff/orders.md', description: '', frontmatter: {} } as unknown as PageData
    seo.transformPageData(pageData, { siteConfig: { srcDir: 'C:/path/that/does/not/exist' } as unknown as SiteConfig })
    expect(pageData.description).toContain('Руководство для сотрудников')
  })

  it('keeps placeholders out of the sitemap and lists the other languages of a translated page', () => {
    const noindex = createSeo('https://example.test/docs', undefined, languages)
    const pageData = { relativePath: 'hy/staff/orders.md', description: 'x', frontmatter: { i18nFallback: true } } as unknown as PageData
    noindex.transformPageData(pageData, { siteConfig: { srcDir: 'C:/path/that/does/not/exist' } as unknown as SiteConfig })
    const items = noindex.sitemapItems([{ url: 'staff/orders' }, { url: 'ru/staff/orders' }, { url: 'hy/staff/orders' }, { url: 'staff/clients' }])
    expect(items.map((item) => item.url)).toEqual(['staff/orders', 'ru/staff/orders', 'staff/clients'])
    const links = (items[0] as { links?: { lang: string; url: string }[] }).links
    expect(links?.map((link) => link.lang)).toEqual(['en', 'ru', 'x-default'])
    expect((items[2] as { links?: unknown }).links).toEqual([])
  })
})
