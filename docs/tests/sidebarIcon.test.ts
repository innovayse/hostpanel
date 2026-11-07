/**
 * Unit tests for the sidebar title helpers: the HTML escaping and the icon span.
 */
import { describe, expect, it } from 'vitest'
import { escapeHtml, sidebarSectionText } from '../.vitepress/sidebarIcon'

describe('escapeHtml', () => {
  it('escapes ampersands and angle brackets', () => {
    expect(escapeHtml('Servers & SSH <b>')).toBe('Servers &amp; SSH &lt;b&gt;')
  })

  it('escapes an existing entity once more, because it only ever sees plain text', () => {
    expect(escapeHtml('&lt;')).toBe('&amp;lt;')
  })

  it('leaves plain text alone', () => {
    expect(escapeHtml('Get started')).toBe('Get started')
  })
})

describe('sidebarSectionText', () => {
  it('puts the icon span before the escaped title', () => {
    expect(sidebarSectionText('server', 'Servers & SSH')).toBe(
      '<span class="hp-sidebar-icon hp-sidebar-icon--server" aria-hidden="true"></span>Servers &amp; SSH',
    )
  })
})
