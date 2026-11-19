/**
 * Tests of ClientStatementView: it mounts, shows its title and data, and shows the refusal sentence when the API
 * refuses; once a statement is asked for it draws the blocks of its schema, while the statement loads and when it is read.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import { ReportStatementLineType } from '@/types/reportclientstatement'
import ClientStatementView from '@/views/ClientStatementView.vue'
import schema from '@/views/ClientStatementView.schema'
import { json } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { expectSchemaBlocks, runReportCase } from '../helpers/reportViews'
import { stubRouteTable } from '../helpers/routes'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportCase({
  name: 'ClientStatementView',
  view: ClientStatementView,
  routes: {},
  expected: ['Client Account Register Balance', 'Select Client'],
})

describe('ClientStatementView schema', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('asks to choose a client, with the schema filters in place, before a statement is asked for', async () => {
    stubRouteTable({ 'GET /reports/client-picker': () => json([{ id: 1, name: 'Acme' }]) })
    const wrapper = await mountView(ClientStatementView)

    expect(wrapper.text()).toContain('Select a client')
    expect(wrapper.text()).toContain('Date Range')
    expect(wrapper.findAll('[data-testid="report-print"]')).toHaveLength(schema.actions?.length ?? 0)
  })

  it('draws the schema table while the statement loads and when it is read', async () => {
    let finish: () => void = () => undefined
    stubRouteTable({
      'GET /reports/client-picker': () => json([{ id: 1, name: 'Acme' }]),
      'GET /reports/client-statement': () => new Promise<Response>((resolve) => {
        finish = () => resolve(json({
          clientId: 1,
          clientName: 'Acme',
          balances: [{ currency: 'USD', previousBalance: 0, endingBalance: 15 }],
          lines: [{ type: ReportStatementLineType.Invoice, date: '2026-01-01', invoiceId: 42, description: null, currency: 'USD', debit: 15, credit: 0, balance: 15 }],
        }))
      }),
    })
    const wrapper = await mountView(ClientStatementView)
    // Pick the client through the picker, as an operator does: open the list and choose the row (teleports are inlined in tests).
    await wrapper.getComponent(UiClientSelect).get('button').trigger('click')
    await flushPromises()
    const row = wrapper.findAll('[data-testid="ui-select-option"]').find(option => option.text().includes('Acme'))
    await row?.trigger('mousedown')
    await flushPromises()

    await wrapper.get('[data-testid="report-generate"]').trigger('click')
    await flushPromises()
    expectSchemaBlocks(wrapper, schema, { title: true, table: true, columns: true })

    finish()
    await flushPromises()
    expectSchemaBlocks(wrapper, schema, { title: true, table: true, columns: true })
    expect(wrapper.text()).toContain('Ending Balance')
  })
})
