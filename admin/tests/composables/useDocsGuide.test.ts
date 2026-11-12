/**
 * Tests for useDocsGuide: returns null when docsUrl is absent or the route is unmapped,
 * and returns the full guide URL when both are set. Also verifies every mapped docs path
 * resolves to a real file under docs/en/ (English pages sit at the site root). The docs address
 * comes from the real runtime config and the route from a real memory router.
 */
import { existsSync } from 'node:fs'
import { resolve } from 'node:path'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { useDocsGuide } from '@/composables/useDocsGuide'
import { DOCS_GUIDES } from '@/lib/docsGuides'
import { stubDocsUrl } from '../helpers/runtimeConfig'

/**
 * Mounts a minimal component that calls `useDocsGuide` under a real memory router at the given
 * path and captures the return value.
 *
 * @param path - The path of the current route, for example `/servers`.
 * @returns What `useDocsGuide` returned.
 */
const mountGuide = async (path: string): Promise<ReturnType<typeof useDocsGuide>> => {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/:pathMatch(.*)*', component: { render: () => null } }],
  })
  await router.push(path)
  await router.isReady()
  let result: ReturnType<typeof useDocsGuide> | undefined
  mount(defineComponent({
    setup: () => {
      result = useDocsGuide()
      return () => h('div')
    },
  }), { global: { plugins: [router] } })
  if (result === undefined) {
    throw new Error('useDocsGuide did not run during setup')
  }
  return result
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useDocsGuide', () => {
  it('returns null when docsUrl is empty', async () => {
    stubDocsUrl('')
    const { guideUrl } = await mountGuide('/servers')
    expect(guideUrl.value).toBeNull()
  })

  it('returns null when the current route has no guide mapped', async () => {
    stubDocsUrl('https://docs.example.com')
    const { guideUrl } = await mountGuide('/some/unknown/route')
    expect(guideUrl.value).toBeNull()
  })

  it('returns the full guide URL when docsUrl and route both match', async () => {
    stubDocsUrl('https://docs.example.com')
    const { guideUrl } = await mountGuide('/servers')
    expect(guideUrl.value).toBe('https://docs.example.com/servers/servers')
  })

  it('returns the guide URL for the billing invoices route', async () => {
    stubDocsUrl('https://docs.example.com')
    const { guideUrl } = await mountGuide('/billing/invoices')
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
