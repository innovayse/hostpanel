/**
 * Unit tests for the per page SEO helpers of the documentation site: plain text extraction, the
 * description cut, the first paragraph finder, keywords, JSON-LD and the 404 and sitemap rules.
 */
import { describe, expect, it } from 'vitest'
import type { PageData, SiteConfig, TransformContext } from 'vitepress'
import { buildKeywords, createSeo, firstParagraph, shorten, toJsonLd, toPlainText } from '../.vitepress/seo'

describe('toPlainText', () => {
  it('keeps underscores and asterisks inside code spans', () => {
    expect(toPlainText('Set `PANEL_HOST_ADDRESSES` and `*.example.com` and `A_B_C` now')).toBe(
      'Set PANEL_HOST_ADDRESSES and *.example.com and A_B_C now',
    )
  })

  it('does not read underscores inside a word as emphasis', () => {
    expect(toPlainText('use my_var_name here')).toBe('use my_var_name here')
  })

  it('removes emphasis that wraps words', () => {
    expect(toPlainText('**bold** and *italic* and _under_ and __strong__')).toBe('bold and italic and under and strong')
  })

  it('keeps a lone asterisk', () => {
    expect(toPlainText('match *.example.com and more')).toBe('match *.example.com and more')
  })

  it('keeps the text of links and drops images, tags and comments', () => {
    expect(toPlainText('[Docs](./x) ![shot](a.png) <b>bold</b> a <!-- note --> b')).toBe('Docs bold a b')
  })

  it('decodes entities once and leaves entities inside code as written', () => {
    expect(toPlainText('Servers &amp; SSH &lt;b&gt; &#39;q&#39; &#x41; `&amp;`')).toBe("Servers & SSH <b> 'q' A &amp;")
  })

  it('leaves an unknown entity as written', () => {
    expect(toPlainText('a &unknownthing; b')).toBe('a &unknownthing; b')
  })

  it('collapses whitespace', () => {
    expect(toPlainText('a \n  b\t c')).toBe('a b c')
  })
})

describe('shorten', () => {
  it('returns a short text unchanged', () => {
    expect(shorten('Short text.')).toBe('Short text.')
  })

  it('cuts at the last sentence end that fits', () => {
    const text = `First sentence that is long enough to count here. ${'word '.repeat(40)}`
    expect(shorten(text)).toBe('First sentence that is long enough to count here.')
  })

  it('cuts at a word and adds a full stop when no sentence end fits', () => {
    const result = shorten(`Short one. ${'word '.repeat(60)}`)
    expect(result.length).toBeLessThanOrEqual(160)
    expect(result.endsWith('word.')).toBe(true)
  })
})

describe('firstParagraph', () => {
  const long = 'This paragraph is long enough to describe a page well.'

  it('skips frontmatter and headings', () => {
    expect(firstParagraph(`---\ntitle: X\n---\n\n# Title\n\n${long}\n`)).toBe(long)
  })

  it('skips fenced code, lists, tables, containers and imports', () => {
    const source = [
      '```',
      'code line that is long enough to be a paragraph if read',
      '```',
      '- a list item that is long enough to be a paragraph',
      '| a | table row that is long enough to be a paragraph |',
      '::: warning',
      'inside a container that is long enough to be a paragraph',
      ':::',
      'import X from "y" and some more text so that it is long enough',
      '',
      long,
    ].join('\n')
    expect(firstParagraph(source)).toBe(long)
  })

  it('skips a multi-line HTML comment', () => {
    const source = `# T\n\n<!--\nA comment line that is long enough to be read as a paragraph.\n-->\n\n${long}`
    expect(firstParagraph(source)).toBe(long)
  })

  it('stops a paragraph at a horizontal rule', () => {
    const first = 'A first paragraph line that is long enough to count'
    expect(firstParagraph(`${first}\n***\nsecond part that must not be joined`)).toBe(first)
  })

  it('skips a rule after a blank line', () => {
    expect(firstParagraph(`# T\n\n---\n\n${long}`)).toBe(long)
  })

  it('does not take a setext heading for a paragraph', () => {
    expect(firstParagraph(`Page title that is long enough to look like a paragraph\n===\n\n${long}`)).toBe(long)
    expect(firstParagraph(`Section title that is long enough to look like a paragraph\n---\n\n${long}`)).toBe(long)
  })

  it('decodes entities and handles CRLF', () => {
    expect(firstParagraph('Servers &amp; SSH keys are covered by this paragraph here.\r\n')).toBe(
      'Servers & SSH keys are covered by this paragraph here.',
    )
  })

  it('returns an empty string when the page has no paragraph', () => {
    expect(firstParagraph('# Only a heading\n\n- and a list\n')).toBe('')
  })
})

describe('buildKeywords', () => {
  it('prefers the frontmatter keywords, distinct ignoring case', () => {
    expect(buildKeywords('alpha, Beta, beta', 'staff', 'Ignored title')).toEqual(['alpha', 'Beta'])
    expect(buildKeywords(['one', 'two'], '', 'x')).toEqual(['one', 'two'])
  })

  it('caps the list', () => {
    const many = Array.from({ length: 15 }, (_value, index) => `k${index}`).join(',')
    expect(buildKeywords(many, '', 'x')).toHaveLength(10)
  })

  it('adds the meaningful title words after the section keywords', () => {
    expect(buildKeywords(undefined, 'migration', 'Moving from the old system')).toEqual([
      'hostpanel',
      'migration',
      'data import',
      'hosting billing',
      'moving',
      'old',
      'system',
    ])
  })

  it('uses the default keywords outside every section and never names an external product', () => {
    const keywords = buildKeywords(undefined, 'unknown', 'Hostpanel tour')
    expect(keywords.slice(0, 3)).toEqual(['hostpanel', 'hosting control panel', 'web hosting billing'])
    expect(buildKeywords(undefined, 'migration', 'Anything').join(' ')).not.toMatch(/whmcs/i)
  })
})

describe('toJsonLd', () => {
  it('cannot close the script tag it sits in', () => {
    const json = toJsonLd({ text: '</script><b>' })
    expect(json).not.toContain('<')
    expect(JSON.parse(json)).toEqual({ text: '</script><b>' })
  })
})

describe('createSeo', () => {
  const seo = createSeo('https://example.test/docs')

  /**
   * Builds the smallest render context the head hook reads.
   * @param relativePath Source path of the page.
   * @param title Page title.
   * @returns A context cast to the VitePress type.
   */
  const contextOf = (relativePath: string, title: string): TransformContext =>
    ({
      pageData: { relativePath, title, description: 'A description.', frontmatter: {} } as unknown as PageData,
      siteConfig: { site: { title: 'Hostpanel', lang: 'en-US' } },
    }) as unknown as TransformContext

  it('gives the 404 page noindex only: no canonical, no structured data', () => {
    const tags = seo.transformHead(contextOf('404.md', 'Not found'))
    expect(tags).toEqual([['meta', { name: 'robots', content: 'noindex, follow' }]])
  })

  it('gives an ordinary page a canonical link and JSON-LD', () => {
    const tags = seo.transformHead(contextOf('staff/orders.md', 'Orders & more'))
    expect(tags).toContainEqual(['link', { rel: 'canonical', href: 'https://example.test/docs/staff/orders' }])
    expect(tags).toContainEqual(['meta', { name: 'description', content: 'A description.' }])
    expect(tags.filter((tag) => tag[0] === 'script')).toHaveLength(2)
  })

  it('fills the description of a page without one from its section', () => {
    const pageData = { relativePath: 'staff/orders.md', description: '' } as unknown as PageData
    seo.transformPageData(pageData, { siteConfig: { srcDir: 'C:/path/that/does/not/exist' } as unknown as SiteConfig })
    expect(pageData.description).toContain('Staff guide')
  })

  it('keeps the 404 page out of the sitemap', () => {
    const items = [{ url: '' }, { url: 'staff/' }, { url: '404' }, { url: '404.html' }]
    expect(seo.sitemapItems(items)).toEqual([{ url: '', links: [] }, { url: 'staff/', links: [] }])
  })
})
