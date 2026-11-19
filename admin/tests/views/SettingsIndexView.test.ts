/**
 * Tests for SettingsIndexView: every group of the catalogue is drawn, every section is a link to its
 * route, and the grid collapses to one column on phones.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import SettingsIndexView from '@/views/SettingsIndexView.vue'
import schema from '@/views/SettingsIndexView.schema'
import { AppRoute } from '@/lib/routes'
import { SETTINGS_INDEX } from '@/lib/settingsIndex'
import { json } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { stubRouteTable } from '../helpers/routes'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('SettingsIndexView', () => {
  it('draws a titled group per catalogue group', async () => {
    stubRouteTable({ 'GET /admin/version': () => json({ version: '1.0.0' }) })
    const { wrapper } = await mountScreen(SettingsIndexView)

    expect(wrapper.get('h1').text()).toBe('Settings')
    for (const group of SETTINGS_INDEX) {
      expect(wrapper.find(`[data-testid="settings-group-${group.group}"]`).exists()).toBe(true)
    }
    expect(wrapper.get('[data-testid="settings-group-billing"] h2').text()).toBe('Billing')
  })

  it('has a skeleton card per catalogue group and a row per section, in the same order', () => {
    const cards = schema.columns.flat()

    expect(cards.map((card) => card.key)).toEqual(SETTINGS_INDEX.map((group) => group.group))
    cards.forEach((card, index) => {
      expect(card.rows?.length).toBe(SETTINGS_INDEX[index]?.entries.length)
    })
  })

  it('links every card to its section', async () => {
    stubRouteTable({ 'GET /admin/version': () => json({ version: '1.0.0' }) })
    const { wrapper } = await mountScreen(SettingsIndexView)

    expect(wrapper.get('[data-testid="settings-card-general"]').attributes('href')).toBe(AppRoute.SettingsGeneral)
    expect(wrapper.get('[data-testid="settings-card-currencies"]').attributes('href')).toBe(AppRoute.SettingsCurrencies)
    expect(wrapper.get('[data-testid="settings-card-fraud"]').attributes('href')).toBe(AppRoute.FraudSettings)
    expect(wrapper.get('[data-testid="settings-card-currencies"]').text()).toContain('Currencies')
  })

  it('lays the cards out in one column on phones', async () => {
    stubRouteTable({ 'GET /admin/version': () => json({ version: '1.0.0' }) })
    const { wrapper } = await mountScreen(SettingsIndexView)

    expect(wrapper.get('[data-testid="settings-group-storefront"] .grid').classes()).toContain('grid-cols-1')
  })
})
