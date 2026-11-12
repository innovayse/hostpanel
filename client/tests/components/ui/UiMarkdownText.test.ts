/**
 * Unit tests for UiMarkdownText: the subset becomes real elements with the bubble's classes, and an XSS corpus never
 * reaches the DOM as an element, an attribute or an address.
 *
 * @module tests/components/ui/UiMarkdownText.test
 */

import { describe, expect, it, vi } from 'vitest'
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

describe('UiMarkdownText code blocks and caret', () => {
  /**
   * Renders a text with the copy button named.
   *
   * @param text - The text as written.
   * @param extra - Further props.
   * @returns The mounted wrapper.
   */
  const renderCopyable = (text: string, extra: Record<string, unknown> = {}) =>
    mountWithKit(UiMarkdownText, { props: { text, copyLabel: 'Copy code', copiedLabel: 'Copied', ...extra } })

  it('gives a code block a copy button that copies its code, and none to other text', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined)
    vi.stubGlobal('navigator', { clipboard: { writeText } })
    const wrapper = renderCopyable('Run:\n```\nnpm install\nnpm test\n```')

    expect(wrapper.findAll('[data-testid="ui-markdown-copy"]')).toHaveLength(1)
    expect(wrapper.get('[data-testid="ui-markdown-copy"]').attributes('aria-label')).toBe('Copy code')
    await wrapper.get('[data-testid="ui-markdown-copy"]').trigger('click')
    expect(writeText).toHaveBeenCalledWith('npm install\nnpm test')
    vi.unstubAllGlobals()
  })

  it('draws no copy button unless the caller names it', () => {
    const wrapper = render('```\ncode\n```')

    expect(wrapper.find('[data-testid="ui-markdown-copy"]').exists()).toBe(false)
  })

  it('ends the reply in a caret while it is being written and not after', async () => {
    const wrapper = renderCopyable('Writing', { caret: true })
    expect(wrapper.get('p span[aria-hidden="true"]').classes()).toContain('animate-pulse')

    await wrapper.setProps({ caret: false })

    expect(wrapper.find('span').exists()).toBe(false)
  })
})
