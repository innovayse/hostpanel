/**
 * Unit tests for useActiveIntegrationCategory: the category the route stands for.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount, flushPromises } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { useActiveIntegrationCategory } from '@/composables/useActiveIntegrationCategory'

/**
 * Mounts a probe at a path and returns the category the composable reports.
 *
 * @param path - The route to stand on.
 * @returns The active category key, or undefined.
 */
const activeAt = async (path: string): Promise<string | undefined> => {
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/:pathMatch(.*)*', component: { render: () => null } }] })
  await router.push(path)
  await router.isReady()
  let active: string | undefined
  const probe = defineComponent({
    setup() {
      const key = useActiveIntegrationCategory(() => ['payments', 'email'])
      return () => h('span', (active = key.value) ?? '')
    },
  })
  mount(probe, { global: { plugins: [router] } })
  await flushPromises()
  return active
}

describe('useActiveIntegrationCategory', () => {
  it('reads the category from its page, and payments from the section root', async () => {
    expect(await activeAt('/integrations/email')).toBe('email')
    expect(await activeAt('/integrations')).toBe('payments')
  })

  it('has no category outside a category page', async () => {
    expect(await activeAt('/integrations/some-slug/configure')).toBeUndefined()
  })
})
