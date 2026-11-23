/**
 * Unit tests for UiRichText: allowed markup becomes real elements with the kit's classes, and the
 * XSS corpus never reaches the DOM as an element, an attribute or an unsafe URL.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiRichText from '~/components/ui/UiRichText.vue'
import { UiRichTextTag } from '~/utils/ui/enums'
import { EXTERNAL_LINK_REL } from '~/utils/ui/richText'

/**
 * Renders an HTML body through the component.
 *
 * @param html Operator HTML.
 * @param as Wrapper tag.
 * @returns The mounted wrapper.
 */
const render = (html: string, as?: UiRichTextTag) => mountWithKit(UiRichText, { props: as ? { html, as } : { html } })

describe('UiRichText', () => {
  it('renders into a div by default and an empty body as an empty wrapper', () => {
    const wrapper = render('')

    expect(wrapper.element.tagName).toBe('DIV')
    expect(wrapper.element.childNodes).toHaveLength(0)
    expect(wrapper.attributes('data-testid')).toBe('ui-rich-text')
  })

  it('renders into the requested tag', () => {
    expect(render('hi', UiRichTextTag.Article).element.tagName).toBe('ARTICLE')
  })

  it('renders paragraphs, emphasis and lists as elements with classes', () => {
    const wrapper = render('<p>One <strong>two</strong></p><ul><li>a</li><li>b</li></ul>')

    expect(wrapper.find('p').classes()).toContain('mb-4')
    expect(wrapper.find('strong').text()).toBe('two')
    expect(wrapper.findAll('li')).toHaveLength(2)
    expect(wrapper.find('ul').classes()).toContain('list-disc')
  })

  it('renders safe links, opening off-site ones safely', () => {
    const wrapper = render('<a href="https://example.com/x" title="Go">out</a> <a href="/docs">in</a>')

    const [external, internal] = wrapper.findAll('a')
    expect(external?.attributes('href')).toBe('https://example.com/x')
    expect(external?.attributes('target')).toBe('_blank')
    expect(external?.attributes('rel')).toBe(EXTERNAL_LINK_REL)
    expect(internal?.attributes('href')).toBe('/docs')
    expect(internal?.attributes('target')).toBeUndefined()
  })

  it('escapes text instead of treating it as markup', () => {
    const wrapper = render('<p>1 &lt; 2 &amp;&amp; &lt;b&gt;not bold&lt;/b&gt;</p>')

    expect(wrapper.find('p').text()).toBe('1 < 2 && <b>not bold</b>')
    expect(wrapper.find('b').exists()).toBe(false)
  })

  it('drops script elements together with their content', () => {
    const wrapper = render('<p>safe</p><script>window.hacked = true</script>')

    expect(wrapper.find('script').exists()).toBe(false)
    expect(wrapper.text()).toBe('safe')
  })

  it('drops event handler attributes', () => {
    const wrapper = render('<p onclick="alert(1)" onmouseover="alert(2)">x</p><img src="https://example.com/a.png" onerror="alert(3)">')

    expect(wrapper.html()).not.toMatch(/onclick|onmouseover|onerror|alert/)
    expect(wrapper.find('img').attributes('src')).toBe('https://example.com/a.png')
  })

  it('drops javascript and data URLs from links and images', () => {
    const wrapper = render('<a href="javascript:alert(1)">a</a><a href=" jAvA\tScRiPt:alert(1)">b</a><img src="data:text/html;base64,AAAA">')

    expect(wrapper.html()).not.toMatch(/javascript|data:/i)
    wrapper.findAll('a').forEach((link) => expect(link.attributes('href')).toBeUndefined())
  })

  it('drops style, class and id attributes', () => {
    const wrapper = render('<p style="color:red" class="x" id="y">text</p>')

    expect(wrapper.find('p').attributes('style')).toBeUndefined()
    expect(wrapper.find('p').attributes('id')).toBeUndefined()
    expect(wrapper.find('p').classes()).not.toContain('x')
  })

  it('drops iframe, svg, object and style containers entirely', () => {
    const wrapper = render('<p>keep</p><iframe src="https://evil.example"></iframe><svg><circle onload="x()"/></svg><object data="x"></object><style>p{display:none}</style>')

    expect(wrapper.html()).not.toMatch(/iframe|svg|object|style|circle/i)
    expect(wrapper.text()).toBe('keep')
  })
})
