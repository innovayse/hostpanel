/**
 * Unit tests for UiMarkdownInline: each inline kind is drawn as its own element, text stays text, and a link
 * opens safely in a new tab.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiMarkdownInline from '@/components/ui/UiMarkdownInline.vue'
import { MarkdownInlineKind } from '@/utils/markdown'
import type { MarkdownInline } from '@/utils/markdown'

/**
 * Builds one inline piece.
 *
 * @param id - Its key.
 * @param kind - What it is.
 * @param text - The text shown.
 * @param href - The address of a link.
 * @returns The piece.
 */
const piece = (id: number, kind: MarkdownInlineKind, text: string, href: string | null = null): MarkdownInline => ({ id, kind, text, href })

describe('UiMarkdownInline', () => {
  it('draws every kind as its element and keeps plain text as text', () => {
    const wrapper = mount(UiMarkdownInline, {
      props: {
        nodes: [
          piece(1, MarkdownInlineKind.Text, 'a <b>tag</b> '),
          piece(2, MarkdownInlineKind.Strong, 'bold'),
          piece(3, MarkdownInlineKind.Emphasis, 'italic'),
          piece(4, MarkdownInlineKind.Code, 'code'),
        ],
      },
    })

    expect(wrapper.find('strong').text()).toBe('bold')
    expect(wrapper.find('em').text()).toBe('italic')
    expect(wrapper.find('code').text()).toBe('code')
    expect(wrapper.find('b').exists()).toBe(false)
    expect(wrapper.text()).toContain('a <b>tag</b>')
  })

  it('opens a link in a new tab without leaking the opener', () => {
    const wrapper = mount(UiMarkdownInline, {
      props: { nodes: [piece(1, MarkdownInlineKind.Link, 'docs', 'https://example.org/a')] },
    })
    const link = wrapper.get('a')

    expect(link.attributes('href')).toBe('https://example.org/a')
    expect(link.attributes('target')).toBe('_blank')
    expect(link.attributes('rel')).toBe('noopener noreferrer nofollow')
  })

  it('shows a link without an address as text', () => {
    const wrapper = mount(UiMarkdownInline, { props: { nodes: [piece(1, MarkdownInlineKind.Link, 'docs')] } })

    expect(wrapper.find('a').exists()).toBe(false)
    expect(wrapper.text()).toBe('docs')
  })
})
