/**
 * Unit tests for the per page SEO of each kind of documentation version: canonical, og:url, robots,
 * the image and structured data addresses, the version in the structured data and the sitemap.
 */
import { describe, expect, it } from 'vitest'
import type { HeadConfig, PageData, SiteConfig, TransformContext } from 'vitepress'
import { createSeo } from '../.vitepress/seo'
import { resolveVersion } from '../.vitepress/versions'

/** Pages the latest docs has. */
const PAGES = new Set(['', 'staff/orders'])
/** Public address of the latest docs. */
const LATEST = 'https://docs.test/docs'

/**
 * Builds the smallest render context the head hook reads.
 * @param relativePath Source path of the page.
 * @returns A context cast to the VitePress type.
 */
const contextOf = (relativePath: string): TransformContext =>
  ({
    pageData: { relativePath, title: 'Orders', description: 'A description.', frontmatter: {} } as unknown as PageData,
    siteConfig: { site: { title: 'Hostpanel', lang: 'en-US' } },
  }) as unknown as TransformContext

/**
 * Reads the JSON-LD documents out of a tag list.
 * @param tags The head tags.
 * @returns The parsed documents.
 */
const jsonLd = (tags: HeadConfig[]): Record<string, unknown>[] =>
  tags.filter((tag) => tag[0] === 'script').map((tag) => JSON.parse(tag[2] as string) as Record<string, unknown>)

/**
 * Whether a tag list carries a robots tag.
 * @param tags The head tags.
 * @returns True when one is present.
 */
const hasRobots = (tags: HeadConfig[]): boolean =>
  tags.some((tag) => tag[0] === 'meta' && (tag[1] as Record<string, string>).name === 'robots')

describe('SEO of the latest release', () => {
  const seo = createSeo(LATEST, resolveVersion({ version: 'v0.4', latestVersion: 'v0.4' }, null))

  it('is indexable, canonical to itself, versioned in the structured data and in the sitemap', () => {
    const tags = seo.transformHead(contextOf('staff/orders.md'))
    expect(tags).toContainEqual(['link', { rel: 'canonical', href: `${LATEST}/staff/orders` }])
    expect(tags).toContainEqual(['meta', { property: 'og:url', content: `${LATEST}/staff/orders` }])
    expect(hasRobots(tags)).toBe(false)
    expect(jsonLd(tags)[1]).toMatchObject({ '@type': 'TechArticle', version: 'v0.4' })
    expect(seo.sitemapItems([{ url: 'staff/orders' }])).toEqual([{ url: 'staff/orders' }])
  })
})

describe('SEO of an older release', () => {
  const siteUrl = `${LATEST}/v0.3`
  const seo = createSeo(siteUrl, resolveVersion({ version: 'v0.3', latestVersion: 'v0.4', latestUrl: LATEST }, PAGES))

  it('is canonical to the same page in the latest, with og:url following, and stays indexable', () => {
    const tags = seo.transformHead(contextOf('staff/orders.md'))
    expect(tags).toContainEqual(['link', { rel: 'canonical', href: `${LATEST}/staff/orders` }])
    expect(tags).toContainEqual(['meta', { property: 'og:url', content: `${LATEST}/staff/orders` }])
    expect(hasRobots(tags)).toBe(false)
  })

  it('uses its own base for images and breadcrumbs, and carries its version', () => {
    const tags = seo.transformHead(contextOf('staff/orders.md'))
    const images = tags.filter((tag) => tag[0] === 'meta' && (tag[1] as Record<string, string>).property === 'og:image')
    expect(images.length).toBeGreaterThan(0)
    for (const image of images) {
      expect((image[1] as Record<string, string>).content.startsWith(`${siteUrl}/og/`)).toBe(true)
    }
    const [crumbs, article] = jsonLd(tags)
    expect(JSON.stringify(crumbs)).toContain(`${siteUrl}/`)
    expect(article).toMatchObject({ version: 'v0.3', url: `${LATEST}/staff/orders` })
  })

  it('is canonical to the latest home and noindex, follow when the page is gone from the latest', () => {
    const tags = seo.transformHead(contextOf('staff/removed.md'))
    expect(tags).toContainEqual(['link', { rel: 'canonical', href: `${LATEST}/` }])
    expect(tags).toContainEqual(['meta', { name: 'robots', content: 'noindex, follow' }])
  })

  it('has no sitemap entries and tells the banner where the latest page is', () => {
    expect(seo.sitemapItems([{ url: 'staff/orders' }])).toEqual([])
    const pageData = { relativePath: 'staff/orders.md', description: 'x', frontmatter: {} } as unknown as PageData
    seo.transformPageData(pageData, { siteConfig: { srcDir: 'C:/path/that/does/not/exist' } as unknown as SiteConfig })
    expect(pageData.frontmatter.docsLatestUrl).toBe(`${LATEST}/staff/orders`)
  })
})

describe('SEO of next', () => {
  const siteUrl = `${LATEST}/next`
  const seo = createSeo(siteUrl, resolveVersion({ version: 'next', latestVersion: 'v0.4', latestUrl: LATEST }, PAGES))

  it('is noindex, follow, canonical to itself and out of the sitemap', () => {
    const tags = seo.transformHead(contextOf('staff/orders.md'))
    expect(tags).toContainEqual(['link', { rel: 'canonical', href: `${siteUrl}/staff/orders` }])
    expect(tags).toContainEqual(['meta', { name: 'robots', content: 'noindex, follow' }])
    expect(jsonLd(tags)[1]).toMatchObject({ version: 'next' })
    expect(seo.sitemapItems([{ url: 'staff/orders' }])).toEqual([])
  })
})
