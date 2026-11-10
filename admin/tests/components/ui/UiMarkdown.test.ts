/**
 * Unit tests for UiMarkdown: Markdown drawn as ordinary elements, raw HTML kept as text, links only over
 * https and opened safely, and the empty text for a null or blank source.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiMarkdown from '@/components/ui/UiMarkdown.vue'

describe('UiMarkdown', () => {
  it('draws headings, paragraphs, lists, quotes and code as elements', () => {
    const wrapper = mount(UiMarkdown, {
      props: { source: '## Added\n\nText\n\n- one\n- two\n\n1. a\n\n> q\n\n```\ncode\n```' },
    })

    expect(wrapper.find('p.font-semibold').text()).toBe('Added')
    expect(wrapper.findAll('ul li')).toHaveLength(2)
    expect(wrapper.findAll('ol li')).toHaveLength(1)
    expect(wrapper.find('blockquote').text()).toBe('q')
    expect(wrapper.find('pre code').text()).toBe('code')
    expect(wrapper.find('h1,h2,h3,h4,h5,h6').exists()).toBe(false)
  })

  it('never turns source text into markup', () => {
    const wrapper = mount(UiMarkdown, {
      props: { source: '<img src=x onerror=alert(1)> <script>bad()</script>\n\n```\n<b>x</b>\n```' },
    })

    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.find('script').exists()).toBe(false)
    expect(wrapper.find('b').exists()).toBe(false)
    expect(wrapper.text()).toContain('<img src=x onerror=alert(1)>')
    expect(wrapper.text()).toContain('<b>x</b>')
  })

  it('links an https address in a new tab with a safe rel, and shows any other address as text', () => {
    const wrapper = mount(UiMarkdown, {
      props: { source: '[ok](https://example.org/a) [bad](javascript:alert(1))' },
    })
    const links = wrapper.findAll('a')

    expect(links).toHaveLength(1)
    expect(links[0]?.attributes('href')).toBe('https://example.org/a')
    expect(links[0]?.attributes('target')).toBe('_blank')
    expect(links[0]?.attributes('rel')).toContain('noopener')
    expect(wrapper.text()).toContain('[bad](javascript:alert(1))')
  })

  it('shows the empty text for null and blank sources, and nothing without one', () => {
    expect(mount(UiMarkdown, { props: { source: null, empty: 'No release notes.' } }).text()).toBe('No release notes.')
    expect(mount(UiMarkdown, { props: { source: '  \n', empty: 'No release notes.' } }).text()).toBe('No release notes.')
    expect(mount(UiMarkdown, { props: { source: null } }).text()).toBe('')
  })
})
