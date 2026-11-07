/**
 * Unit tests for UiMarkdownText: the subset becomes real elements with the bubble's classes, and an XSS corpus never
 * reaches the DOM as an element, an attribute or an address.
 *
 * @module tests/components/ui/UiMarkdownText.test
 */

import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiMarkdownText from '~/components/ui/UiMarkdownText.vue'

/** Tags the subset may produce, the wrapper included. */
const OUTPUT_TAGS = new Set(['DIV', 'P', 'H4', 'UL', 'OL', 'LI', 'STRONG', 'EM', 'CODE', 'PRE'])

/**
 * Renders a text through the component.
 *
 * @param text - The text as written.
 * @returns The mounted wrapper.
 */
const render = (text: string) => mountWithKit(UiMarkdownText, { props: { text } })

describe('UiMarkdownText', () => {
  it('draws bold where the reply wrote it and shows no markers', () => {
    const wrapper = render('**Done.** Your *site* is live.')

    expect(wrapper.get('strong').text()).toBe('Done.')
    expect(wrapper.get('em').text()).toBe('site')
    expect(wrapper.text()).not.toContain('*')
  })

  it('draws a list and a heading with the classes of the subset', () => {
    const wrapper = render('# Plan\n- one\n- two')

    expect(wrapper.get('h4').classes()).toContain('font-semibold')
    expect(wrapper.get('ul').classes()).toContain('list-disc')
    expect(wrapper.findAll('li')).toHaveLength(2)
  })

  it('updates when more of the reply arrives', async () => {
    const wrapper = render('**par')
    expect(wrapper.find('strong').exists()).toBe(false)

    await wrapper.setProps({ text: '**partial**' })

    expect(wrapper.get('strong').text()).toBe('partial')
  })

  it.each([
    '<script>alert(1)</script>',
    '<img src=x onerror=alert(1)>',
    '**<img src=x onerror=alert(1)>**',
    '*<svg onload=alert(1)>*',
    '`<iframe src="javascript:alert(1)"></iframe>`',
    '- <a href="javascript:alert(1)">x</a>',
    '# <b onclick="alert(1)">x</b>',
    '```\n</pre><script>alert(1)</script>\n```',
    '[x](javascript:alert(1))',
    '![x](javascript:alert(1))',
    '[x](https://evil.example)',
    '&lt;script&gt;alert(1)&lt;/script&gt;',
    '**"><script>alert(1)</script>**',
  ])('keeps %j as visible text and creates no element or attribute from it', (attack) => {
    const wrapper = render(attack)
    const elements = [wrapper.element, ...wrapper.element.querySelectorAll('*')]

    for (const node of elements) {
      expect(OUTPUT_TAGS.has(node.tagName)).toBe(true)
      for (const attribute of node.getAttributeNames()) {
        expect(['class', 'data-testid', 'start']).toContain(attribute)
      }
    }
  })

  it('shows an HTML attack as its own text', () => {
    expect(render('<img src=x onerror=alert(1)>').text()).toBe('<img src=x onerror=alert(1)>')
  })
})
