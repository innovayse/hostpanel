/**
 * Tests for the ticket filters every ticket list shares: the address is read back as the typed status, priority and
 * department the API takes, a value that is not one is ignored, and each applied filter is described for its chip.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h, ref } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { useListQuery } from '@/composables/useListQuery'
import { TicketFilterKey, useTicketFilters } from '@/composables/useTicketFilters'
import type { TicketFilters } from '@/composables/useTicketFilters'
import { TicketPriority, TicketStatus } from '@/types/ticket'
import { createTestI18n } from '../helpers/i18n'

/** The support departments the department chip is named from. */
const departments = ref([
  { id: 1, name: 'Support', email: 'support@example.test' },
  { id: 2, name: 'Billing', email: 'billing@example.test' },
])

/**
 * Mounts a screen that uses the composables, at the given address.
 *
 * @param url - The address the router starts at.
 * @returns A reader of the ticket filters the screen holds.
 */
const setup = async (url: string) => {
  let state: TicketFilters | null = null
  const Screen = defineComponent({
    setup() {
      const list = useListQuery(Object.values(TicketFilterKey))
      state = useTicketFilters(list, departments)
      return () => h('div')
    },
  })
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/tickets', component: Screen }] })
  await router.push(url)
  await router.isReady()
  mount({ render: () => h('div', [h(Screen)]) }, { global: { plugins: [router, createTestI18n()] } })
  await flushPromises()
  return () => state as unknown as TicketFilters
}

describe('useTicketFilters', () => {
  it('reads the status, the priority and the department from the address as the API takes them', async () => {
    const filters = await setup('/tickets?status=AwaitingReply&priority=High&departmentId=2')

    expect(filters().filters.value).toEqual({
      status: TicketStatus.AwaitingReply,
      priority: TicketPriority.High,
      departmentId: 2,
    })
  })

  it('leaves out a filter that is not set or holds a value that is not a member', async () => {
    const filters = await setup('/tickets?status=Answered&priority=Urgent&departmentId=abc')

    expect(filters().filters.value).toEqual({ status: undefined, priority: undefined, departmentId: undefined })
  })

  it('describes each applied filter for its chip, naming the department', async () => {
    const filters = await setup('/tickets?status=Closed&priority=Low&departmentId=2')

    expect(filters().chipSources()).toEqual([
      { key: 'status', label: 'Status', value: 'Closed' },
      { key: 'priority', label: 'Priority', value: 'Low' },
      { key: 'departmentId', label: 'Department', value: 'Billing' },
    ])
  })

  it('shows the department id on its chip while the departments have not loaded or the department is gone', async () => {
    const filters = await setup('/tickets?departmentId=9')

    expect(filters().chipSources().find((source) => source.key === 'departmentId')?.value).toBe('9')
  })

  it('describes a filter that is not set without a value', async () => {
    const filters = await setup('/tickets')

    expect(filters().chipSources().every((source) => source.value === undefined)).toBe(true)
  })
})
