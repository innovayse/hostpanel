/**
 * Unit tests for UiTransition: it hands the classes of its kind to the Vue transition.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent, h, nextTick, ref } from 'vue'
import UiTransition from '@/components/ui/UiTransition.vue'
import { transitionSet, UiTransitionKind } from '@/utils/ui/transition'

/**
 * Mounts a panel inside UiTransition, hidden until the test shows it.
 *
 * @param kind - The kind of surface.
 * @returns The wrapper and the flag that shows the panel.
 */
const mountPanel = (kind: UiTransitionKind) => {
  /** Whether the panel is shown. */
  const shown = ref(false)
  const wrapper = mount(
    defineComponent({
      render: () => h(UiTransition, { kind }, () => (shown.value ? h('div', { id: 'panel' }, 'panel') : undefined)),
    }),
    { attachTo: document.body, global: { stubs: { transition: false } } },
  )
  return { wrapper, shown }
}

describe('UiTransition', () => {
  it('starts the enter from the first class of its kind', async () => {
    const { wrapper, shown } = mountPanel(UiTransitionKind.Dropdown)
    shown.value = true
    await nextTick()

    const classes = wrapper.get('#panel').classes()
    for (const token of transitionSet[UiTransitionKind.Dropdown].enterFromClass.split(' ')) {
      expect(classes).toContain(token)
    }
    for (const token of transitionSet[UiTransitionKind.Dropdown].enterActiveClass.split(' ')) {
      expect(classes).toContain(token)
    }
    wrapper.unmount()
  })

  it('uses the classes of the kind it was given, not those of another', async () => {
    const { wrapper, shown } = mountPanel(UiTransitionKind.Collapse)
    shown.value = true
    await nextTick()

    expect(wrapper.get('#panel').classes()).toContain('grid-rows-[0fr]')
    expect(wrapper.get('#panel').classes()).not.toContain('scale-95')
    wrapper.unmount()
  })
})
