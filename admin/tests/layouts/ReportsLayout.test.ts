/**
 * Tests for ReportsLayout: the report list beside the routed report.
 */
import { describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { h } from 'vue'
import { createMemoryHistory, createRouter, RouterView } from 'vue-router'
import ReportsLayout from '@/layouts/ReportsLayout.vue'
import { createTestI18n } from '../helpers/i18n'

describe('ReportsLayout', () => {
  it('shows the report sidebar and the report the router is at', async () => {
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        {
          path: '/reports',
          component: ReportsLayout,
          children: [{ path: 'clients', component: { render: () => h('p', { 'data-testid': 'report-body' }, 'the report') } }],
        },
        // The sidebar links to every other report; they are not under test, so a blank page answers them.
        { path: '/:pathMatch(.*)*', component: { render: () => null } },
      ],
    })
    await router.push('/reports/clients')
    await router.isReady()
    const wrapper = mount({ render: () => h(RouterView) }, { global: { plugins: [router, createTestI18n(), createPinia()] } })
    await flushPromises()

    expect(wrapper.find('[data-testid="reports-layout"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="reports-nav-clients"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="report-body"]').text()).toBe('the report')
    // The narrow-screen strip lists the same reports and opens the chosen one.
    expect(wrapper.find('[data-testid="reports-tab-clients"]').attributes('aria-selected')).toBe('true')
    await wrapper.get('[data-testid="reports-tab-new-customers"]').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/reports/new-customers')
  })
})
