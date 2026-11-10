// @vitest-environment happy-dom
/**
 * Component tests for the version notice: it is shown only on an older release and on the development
 * docs, it stays an accessible note with a link to the latest docs, and the theme renders it before the
 * top bar, with the height the layout reserves for it kept in the component instead of the page head.
 */
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import { VersionKind } from '../.vitepress/theme/versionLinks'
import VersionBanner from '../.vitepress/theme/components/VersionBanner.vue'

/** The theme data, page and language the mocked `useData` returns. */
const data = vi.hoisted(() => ({ theme: null as unknown, page: null as unknown, lang: null as unknown }))

vi.mock('vitepress', () => ({
  useData: () => data,
}))

/** Address of the latest docs in the tests. */
const LATEST_URL = 'https://example.test/docs'

/**
 * Mounts the banner for a build of the given kind.
 * @param kind The kind of the build.
 * @param pageLatestUrl The address of the page in the latest docs, as the config writes it.
 * @returns The mounted wrapper.
 */
const mountFor = (kind: VersionKind, pageLatestUrl?: string) => {
  data.theme = ref({ docsVersion: { kind, label: 'v0.3', latestUrl: LATEST_URL } })
  data.page = ref({ frontmatter: pageLatestUrl ? { docsLatestUrl: pageLatestUrl } : {} })
  data.lang = ref('en-US')
  return mount(VersionBanner)
}

/** Source of the component, read to check its styles. */
const source = readFileSync(resolve(__dirname, '../.vitepress/theme/components/VersionBanner.vue'), 'utf8')

describe('VersionBanner', () => {
  it('renders nothing for the latest release', () => {
    expect(mountFor(VersionKind.Latest).find('.version-banner').exists()).toBe(false)
  })

  it('names the older release and links to the same page in the latest docs', () => {
    const banner = mountFor(VersionKind.Release, `${LATEST_URL}/staff/orders`).get('.version-banner')
    expect(banner.attributes('role')).toBe('note')
    expect(banner.text()).toContain('This is the documentation of v0.3, not the latest release.')
    expect(banner.get('a').attributes('href')).toBe(`${LATEST_URL}/staff/orders`)
    expect(banner.get('a').text()).toBe('Read the latest version')
  })

  it('tells the reader of next that it is unreleased and falls back to the latest home', () => {
    const banner = mountFor(VersionKind.Next).get('.version-banner')
    expect(banner.text()).toContain('This is the documentation of the next, unreleased version.')
    expect(banner.get('a').attributes('href')).toBe(LATEST_URL)
  })

  it('reserves its own height for the layout only while it is on the page', () => {
    expect(source).toContain(':root:has(.version-banner)')
    expect(source).toMatch(/--vp-layout-top-height: 40px/)
    expect(source).toMatch(/@media \(max-width: 720px\)[\s\S]*--vp-layout-top-height: 64px/)
    expect(source).toMatch(/position: fixed/)
  })
})

describe('theme layout', () => {
  /** Source of the theme entry. */
  const theme = readFileSync(resolve(__dirname, '../.vitepress/theme/index.ts'), 'utf8')
  /** Source of the site config. */
  const config = readFileSync(resolve(__dirname, '../.vitepress/config.ts'), 'utf8')

  it('renders the banner in the slot before the top bar', () => {
    expect(theme).toMatch(/'layout-top': \(\) => h\(VersionBanner\)/)
  })

  it('writes no style into the page head', () => {
    expect(config).not.toMatch(/\['style'/)
  })
})
