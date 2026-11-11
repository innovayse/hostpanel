/**
 * Tests for useDocsGuide: returns null when docsUrl is absent or the route is unmapped,
 * and returns the full guide URL when both are set. Also verifies every mapped docs path
 * resolves to a real file under docs/en/ (English pages sit at the site root).
 */
import { existsSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import { useDocsGuide } from '@/composables/useDocsGuide'
import type { RouteLocationNormalizedLoaded } from 'vue-router'

vi.mock('@/lib/runtimeConfig', () => ({
  getRuntimeConfig: vi.fn(),
}))

vi.mock('vue-router', () => ({
  useRoute: vi.fn(),
}))

import { getRuntimeConfig } from '@/lib/runtimeConfig'
import { useRoute } from 'vue-router'
import { DOCS_GUIDES } from '@/lib/docsGuides'

/**
 * Makes `useRoute` answer with a route at the given path.
 *
 * @param path - The path of the current route, for example `/servers`.
 */
const mockRoute = (path: string): void => {
  vi.mocked(useRoute).mockReturnValue({ path } as RouteLocationNormalizedLoaded)
}

/**
 * Mounts a minimal component that calls `useDocsGuide` and captures the return value.
 * The non-null assertion is safe because `setup` runs synchronously during `mount`.
 */
const mountGuide = (): ReturnType<typeof useDocsGuide> => {
  let result: ReturnType<typeof useDocsGuide> | undefined
  mount(defineComponent({
    setup: () => {
      result = useDocsGuide()
      return () => h('div')
    },
  }))
  // setup() runs synchronously, so result is always assigned before mount returns
  return result!
}

describe('useDocsGuide', () => {
  it('returns null when docsUrl is empty', () => {
    vi.mocked(getRuntimeConfig).mockReturnValue({ docsUrl: '', basePath: '/', clientBaseUrl: '' })
    mockRoute('/servers')
    const { guideUrl } = mountGuide()
    expect(guideUrl.value).toBeNull()
  })

  it('returns null when the current route has no guide mapped', () => {
    vi.mocked(getRuntimeConfig).mockReturnValue({ docsUrl: 'https://docs.example.com', basePath: '/', clientBaseUrl: '' })
    mockRoute('/some/unknown/route')
    const { guideUrl } = mountGuide()
    expect(guideUrl.value).toBeNull()
  })

  it('returns the full guide URL when docsUrl and route both match', () => {
    vi.mocked(getRuntimeConfig).mockReturnValue({ docsUrl: 'https://docs.example.com', basePath: '/', clientBaseUrl: '' })
    mockRoute('/servers')
    const { guideUrl } = mountGuide()
    expect(guideUrl.value).toBe('https://docs.example.com/servers/servers')
  })

  it('returns the guide URL for the billing invoices route', () => {
    vi.mocked(getRuntimeConfig).mockReturnValue({ docsUrl: 'https://docs.example.com', basePath: '/', clientBaseUrl: '' })
    mockRoute('/billing/invoices')
    const { guideUrl } = mountGuide()
    expect(guideUrl.value).toBe('https://docs.example.com/staff/billing')
  })
})

describe('DOCS_GUIDES paths', () => {
  const docsRoot = resolve(__dirname, '../../../docs/en')

  it.each(Object.entries(DOCS_GUIDES))(
    'mapped path %s → %s resolves to a real .md file',
    (_route, docPath) => {
      const mdFile = resolve(docsRoot, `${docPath}.md`)
      expect(existsSync(mdFile), `missing: docs/en/${docPath}.md`).toBe(true)
    },
  )
})
