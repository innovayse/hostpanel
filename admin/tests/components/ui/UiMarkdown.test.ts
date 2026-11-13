/**
 * Unit tests for UiMarkdown: Markdown drawn as ordinary elements, raw HTML kept as text, links only over
 * https and opened safely, and the empty text for a null or blank source.
 */
import { describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
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

describe('UiMarkdown caret', () => {
  it('ends the last paragraph, heading or list item of a reply that is still being written', () => {
    const paragraph = mount(UiMarkdown, { props: { source: 'First\n\nSecond', caret: true } })
    const list = mount(UiMarkdown, { props: { source: '- one\n- two', caret: true } })

    expect(paragraph.findAll('[data-testid="markdown-caret"]')).toHaveLength(1)
    expect(paragraph.findAll('p')[1]?.find('[data-testid="markdown-caret"]').exists()).toBe(true)
    expect(list.findAll('li')[1]?.find('[data-testid="markdown-caret"]').exists()).toBe(true)
    expect(list.findAll('li')[0]?.find('[data-testid="markdown-caret"]').exists()).toBe(false)
  })

  it('stands on its own line after a code block and for a reply with no text yet', () => {
    const afterCode = mount(UiMarkdown, { props: { source: '```\nrun\n```', caret: true } })
    const nothing = mount(UiMarkdown, { props: { source: '', caret: true } })

    expect(afterCode.get('pre').find('[data-testid="markdown-caret"]').exists()).toBe(false)
    expect(afterCode.findAll('[data-testid="markdown-caret"]')).toHaveLength(1)
    expect(nothing.findAll('[data-testid="markdown-caret"]')).toHaveLength(1)
  })

  it('shows no caret by default', () => {
    expect(mount(UiMarkdown, { props: { source: 'Done' } }).find('[data-testid="markdown-caret"]').exists()).toBe(false)
  })
})

describe('UiMarkdown code copy', () => {
  it('gives a code block a copy button that copies its code, once the caller names it', () => {
    const wrapper = mount(UiMarkdown, { props: { source: '```\nnpm run build\n```', copyLabel: 'Copy code', copiedLabel: 'Copied' } })
    const button = wrapper.get('[data-testid="markdown-copy"]')

    expect(button.attributes('aria-label')).toBe('Copy code')
    expect(wrapper.get('pre').classes()).toContain('pr-11')
  })

  it('names the copied state like the copy button when no copied label is given', async () => {
    vi.stubGlobal('navigator', { clipboard: { writeText: vi.fn().mockResolvedValue(undefined) } })
    const wrapper = mount(UiMarkdown, { props: { source: '```\nrun\n```', copyLabel: 'Copy code' } })

    await wrapper.get('[data-testid="markdown-copy"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="markdown-copy"]').attributes('aria-label')).toBe('Copy code')
    vi.unstubAllGlobals()
  })

  it('draws no button without a label', () => {
    const wrapper = mount(UiMarkdown, { props: { source: '```\nrun\n```' } })

    expect(wrapper.find('[data-testid="markdown-copy"]').exists()).toBe(false)
    expect(wrapper.get('pre').classes()).not.toContain('pr-11')
  })
})

describe('UiMarkdown without links', () => {
  it('keeps a link as its text when links are off', () => {
    const wrapper = mount(UiMarkdown, { props: { source: 'see [ok](https://example.org/a) **now**', links: false } })

    expect(wrapper.find('a').exists()).toBe(false)
    expect(wrapper.text()).toBe('see ok now')
    expect(wrapper.get('strong').text()).toBe('now')
  })
})
