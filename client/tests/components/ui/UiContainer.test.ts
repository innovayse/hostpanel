/**
 * Unit tests for UiContainer: it must always carry the shared container class
 * and render its slot, in the requested tag.
 */
import { describe, it, expect } from 'vitest'
import { createSSRApp, h } from 'vue'
import { renderToString } from 'vue/server-renderer'
import Container from '~/components/ui/UiContainer.vue'
import { UiBlockTag } from '~/utils/ui/enums'

/**
 * Renders the container to HTML with the given props and default slot text.
 *
 * @param props Props passed to the container.
 * @param text Text placed in the default slot.
 * @returns The rendered markup, without the SSR slot-fragment comment markers.
 */
const render = async (props: Record<string, unknown> = {}, text = ''): Promise<string> => {
  const html = await renderToString(createSSRApp({ render: () => h(Container, props, { default: () => text }) }))
  return html.replace(/<!--[[\]]-->/g, '')
}

describe('UiContainer', () => {
  it('renders a div with the shared container class and the slot', async () => {
    const html = await render({}, 'content')

    expect(html).toBe('<div class="container-custom">content</div>')
  })

  it('renders the requested tag', async () => {
    const html = await render({ as: UiBlockTag.Section })

    expect(html).toBe('<section class="container-custom"></section>')
  })
})
