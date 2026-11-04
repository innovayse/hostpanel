/**
 * Tests for useDocsGuide: returns null when docsUrl is absent or the route is unmapped,
 * and returns the full guide URL when both are set.
 */
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

const mockRoute = (path: string): void => {
  vi.mocked(useRoute).mockReturnValue({ path } as RouteLocationNormalizedLoaded)
}

const mountGuide = (): ReturnType<typeof useDocsGuide> => {
  let result: ReturnType<typeof useDocsGuide> | undefined
  mount(defineComponent({
    setup: () => {
      result = useDocsGuide()
      return () => h('div')
    },
  }))
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
