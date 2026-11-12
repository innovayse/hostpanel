/**
 * Tests for TaxRulesView: the table, the create and edit dialog with its refusal, and the delete
 * with its confirmation, all through the store at network level.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import TaxRulesView from '@/views/TaxRulesView.vue'
import UiCountrySelect from '@/components/ui/UiCountrySelect.vue'
import type { TaxRuleDto } from '@/types/taxrule'
import { json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'

/** A country-wide rule. */
const germany: TaxRuleDto = { id: 1, name: 'Germany VAT', country: 'DE', region: null, ratePercent: 19, isEnabled: true }

/** A disabled regional rule. */
const bavaria: TaxRuleDto = { id: 2, name: 'Bavaria VAT', country: 'DE', region: 'Bavaria', ratePercent: 7.5, isEnabled: false }

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('TaxRulesView', () => {
  it('lists the rules with country, region, rate and status', async () => {
    stubRouteTable({ 'GET /admin/tax-rules': () => json([germany, bavaria]) })
    const { wrapper } = await mountScreen(TaxRulesView)

    expect(wrapper.get('h1').text()).toBe('Tax Rules')
    const first = wrapper.get('[data-testid="tax-rule-row-1"]').text()
    expect(first).toContain('Germany VAT')
    expect(first).toContain('Germany')
    expect(first).toContain('Whole country')
    expect(first).toContain('19%')
    expect(first).toContain('Enabled')
    const second = wrapper.get('[data-testid="tax-rule-row-2"]').text()
    expect(second).toContain('Bavaria')
    expect(second).toContain('7.5%')
    expect(second).toContain('Disabled')
  })

  it('says so when there is no rule and shows a load refusal', async () => {
    stubRouteTable({ 'GET /admin/tax-rules': () => json([]) })
    const empty = await mountScreen(TaxRulesView)
    expect(empty.wrapper.text()).toContain('No tax rules yet')

    stubRouteTable({ 'GET /admin/tax-rules': () => problem('Tax rules are unavailable.', 503) })
    const failed = await mountScreen(TaxRulesView)
    expect(failed.wrapper.get('[data-testid="tax-rules-error"]').text()).toBe('Tax rules are unavailable.')
  })

  it('creates a rule through POST and closes the dialog', async () => {
    const mock = stubRouteTable({
      'GET /admin/tax-rules': () => json([germany]),
      'POST /admin/tax-rules': () => json(9, 201),
    })
    const { wrapper } = await mountScreen(TaxRulesView)

    await wrapper.get('[data-testid="tax-rules-add"]').trigger('click')
    await wrapper.get('[data-testid="tax-rule-form-name"]').setValue('France VAT')
    wrapper.getComponent(UiCountrySelect).vm.$emit('update:modelValue', 'FR')
    await wrapper.get('[data-testid="tax-rule-form"]').trigger('submit')
    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="tax-rule-form"]').exists()).toBe(false)
    })

    expect(requestsOf(mock)).toContain('POST /admin/tax-rules')
  })

  it('keeps the dialog open and shows the refusal of a save', async () => {
    stubRouteTable({
      'GET /admin/tax-rules': () => json([germany]),
      'POST /admin/tax-rules': () => problem('A rule for this country exists.', 409),
    })
    const { wrapper } = await mountScreen(TaxRulesView)

    await wrapper.get('[data-testid="tax-rules-add"]').trigger('click')
    await wrapper.get('[data-testid="tax-rule-form"]').trigger('submit')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="tax-rule-form-error"]').text()).toBe('A rule for this country exists.')
    })
  })

  it('opens the edit dialog with the stored values and saves through PUT', async () => {
    const mock = stubRouteTable({
      'GET /admin/tax-rules': () => json([germany, bavaria]),
      'PUT /admin/tax-rules/2': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(TaxRulesView)

    await wrapper.get('[data-testid="tax-rule-actions-2"]').trigger('click')
    await wrapper.get('[data-testid="tax-rule-edit-2"]').trigger('click')
    expect((wrapper.get('[data-testid="tax-rule-form-region"]').element as HTMLInputElement).value).toBe('Bavaria')

    await wrapper.get('[data-testid="tax-rule-form"]').trigger('submit')
    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="tax-rule-form"]').exists()).toBe(false)
    })

    expect(requestsOf(mock)).toContain('PUT /admin/tax-rules/2')
  })

  it('asks before deleting, then deletes and reports it', async () => {
    const mock = stubRouteTable({
      'GET /admin/tax-rules': () => json([germany]),
      'DELETE /admin/tax-rules/1': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(TaxRulesView)

    await wrapper.get('[data-testid="tax-rule-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="tax-rule-delete-1"]').trigger('click')
    expect(wrapper.get('[role="dialog"]').text()).toContain('Delete the tax rule Germany VAT?')
    expect(requestsOf(mock)).not.toContain('DELETE /admin/tax-rules/1')

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="tax-rules-result"]').text()).toBe('The tax rule was deleted.')
    })
    expect(requestsOf(mock)).toContain('DELETE /admin/tax-rules/1')
  })

  it('shows the refusal of a delete in the result banner', async () => {
    stubRouteTable({
      'GET /admin/tax-rules': () => json([germany]),
      'DELETE /admin/tax-rules/1': () => problem('The tax rule does not exist.', 404),
    })
    const { wrapper } = await mountScreen(TaxRulesView)

    await wrapper.get('[data-testid="tax-rule-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="tax-rule-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="tax-rules-result"]').text()).toBe('The tax rule does not exist.')
    })
  })
})
