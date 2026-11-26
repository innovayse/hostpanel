/**
 * Tests for the page trail helpers: the parents of a page come from the navigation's trail, the page itself is the
 * last step with no target, and a page inside a client starts with the client list, the client and its section.
 */
import { describe, expect, it } from 'vitest'
import { clientParents, clientSectionTrail, parentsOf, withCurrent } from '@/lib/pageCrumbs'
import { ClientSection } from '@/lib/routes'

/**
 * Translates a key by upper-casing it, so a test sees which key was asked for.
 *
 * @param key - A locale key.
 * @returns The key in capitals.
 */
const translate = (key: string): string => key.toUpperCase()

describe('parentsOf', () => {
  it('drops the last step of the navigation trail, the generic name of the page', () => {
    const trail = [{ label: 'Billing', to: '/billing' }, { label: 'Invoices', to: '/billing/invoices' }, { label: 'Details' }]

    expect(parentsOf(trail)).toEqual([{ label: 'Billing', to: '/billing' }, { label: 'Invoices', to: '/billing/invoices' }])
  })

  it('leaves nothing for a trail of one step', () => {
    expect(parentsOf([{ label: 'Admin' }])).toEqual([])
  })
})

describe('withCurrent', () => {
  it('ends the trail with the page itself, which has no target', () => {
    const trail = withCurrent([{ label: 'Orders', to: '/orders' }], '#1001')

    expect(trail).toEqual([{ label: 'Orders', to: '/orders' }, { label: '#1001' }])
    expect(trail[trail.length - 1]?.to).toBeUndefined()
  })

  it('marks the page as pending while its name is not known yet', () => {
    expect(withCurrent([{ label: 'Orders', to: '/orders' }], undefined)[1]).toEqual({ label: '', pending: true })
    expect(withCurrent([], '')[0]).toEqual({ label: '', pending: true })
  })
})

describe('clientParents', () => {
  it('names the client list, the client and the section, each leading to its own page', () => {
    const parents = clientParents({ clientId: 7, clientName: 'Ada Lovelace', section: ClientSection.Domains, translate })

    expect(parents).toEqual([
      { label: 'NAV.CLIENTS', to: '/clients' },
      { label: 'Ada Lovelace', to: '/clients/7' },
      { label: 'SHARED.DOMAINS', to: '/clients/7/domains' },
    ])
  })

  it('takes the caption of the section from the one list the tab strip is made of', () => {
    const parents = clientParents({ clientId: '9', clientName: 'Grace Hopper', section: ClientSection.Tickets, translate })

    expect(parents[2]).toEqual({ label: 'CLIENTS.CLIENTTICKETS.TICKETS', to: '/clients/9/tickets' })
  })
})

describe('clientSectionTrail', () => {
  it('ends at the section itself, which has no target', () => {
    const trail = clientSectionTrail({ clientId: 7, clientName: 'Ada Lovelace', section: ClientSection.Domains, translate })

    expect(trail).toEqual([
      { label: 'NAV.CLIENTS', to: '/clients' },
      { label: 'Ada Lovelace', to: '/clients/7' },
      { label: 'SHARED.DOMAINS' },
    ])
  })
})
