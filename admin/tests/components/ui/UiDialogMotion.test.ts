/**
 * Unit tests for how UiDialog and UiDrawer behave around their animation: focus goes to the first control only when
 * the enter has ended, a request made while the surface leaves is ignored, and a surface removed by its parent's `v-if`
 * gives the page and the focus back at once. The real transition runs, so the leave takes a frame and can be observed.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import { defineComponent, h, nextTick, ref } from 'vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiDrawer from '@/components/ui/UiDrawer.vue'
import UiTransition from '@/components/ui/UiTransition.vue'
import { createTestI18n } from '../../helpers/i18n'

/** The global options: the real transition and an inlined teleport, so the panel is queryable. */
const global = { plugins: [createTestI18n()], stubs: { teleport: true, transition: false } }

afterEach(() => {
  vi.unstubAllGlobals()
  document.body.innerHTML = ''
  document.body.style.overflow = ''
})

/**
 * Starts a leave the way the transition does, by calling the `before-leave` hook the surface gave it, and keeps the
 * panel in the page, as a leave in progress does. jsdom has no stylesheet, so a real leave would end within a frame
 * and could not be observed.
 *
 * @param wrapper - The mounted surface.
 */
const startLeaving = (wrapper: VueWrapper): void => {
  const hook = wrapper.findComponent(UiTransition).vm.$attrs.onBeforeLeave as () => void
  hook()
}

/**
 * Cancels a leave the way the transition does when the surface is shown again while it leaves, by calling the
 * `leave-cancelled` hook the surface gave it.
 *
 * @param wrapper - The mounted surface.
 */
const cancelLeaving = (wrapper: VueWrapper): void => {
  const hook = wrapper.findComponent(UiTransition).vm.$attrs.onLeaveCancelled as () => void
  hook()
}

/**
 * Waits for the transition's frames to pass.
 *
 * @returns A promise that settles after a few frames.
 */
const frames = async (): Promise<void> => {
  await new Promise(resolve => setTimeout(resolve, 60))
  await flushPromises()
}

/**
 * Finds an element in the page by test id. A leaving panel is no longer part of the wrapper's rendered tree, but it is
 * still in the page, so these tests read the page.
 *
 * @param id - The `data-testid`.
 * @returns The element, or null when the page has none.
 */
const byTestId = (id: string): HTMLElement | null => document.querySelector<HTMLElement>(`[data-testid="${id}"]`)

/**
 * Presses Escape on the panel in the page.
 *
 * @returns A promise that settles once the page has updated.
 */
const pressEscape = async (): Promise<void> => {
  document.querySelector('[role="dialog"]')?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }))
  await nextTick()
}

/**
 * Clicks an element of the page by test id.
 *
 * @param id - The `data-testid`.
 * @returns A promise that settles once the page has updated.
 */
const clickTestId = async (id: string): Promise<void> => {
  byTestId(id)?.click()
  await nextTick()
}

/**
 * Takes over the animation frames, so a leave of the real transition stays in progress until the test lets it end.
 * The transition waits two frames before it can end, which a test can neither see nor stop otherwise.
 *
 * @returns `flush`, which runs every frame asked for so far (and those they ask for) and lets the page update.
 */
const holdFrames = (): { flush: () => Promise<void> } => {
  /** Callbacks waiting for their frame. */
  const queue: FrameRequestCallback[] = []
  vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback): number => queue.push(callback))
  return {
    flush: async (): Promise<void> => {
      while (queue.length > 0) {
        queue.shift()?.(0)
      }
      await flushPromises()
    },
  }
}

/** The real transition and the real teleport to `<body>`, as in the app: a leaving panel then stays in the page. */
const realGlobal = { plugins: [createTestI18n()], stubs: { transition: false } }

/** The same, with the transition stubbed, so a leave only starts and ends when the test says. */
const heldGlobal = { plugins: [createTestI18n()], stubs: { teleport: true } }

/**
 * Mounts a confirm dialog that is open, with a button inside to take the focus.
 *
 * @param held - Whether the transition is stubbed, so the test decides when a leave starts.
 * @returns The wrapper.
 */
const mountOpenDialog = (held = false) =>
  mount(UiDialog, {
    props: { open: true, variant: 'confirm', title: 'Delete', message: 'Sure?' },
    slots: { default: '<input id="first-field" />' },
    attachTo: document.body,
    global: held ? heldGlobal : global,
  })

describe('UiDialog and its animation', () => {
  it('puts focus on the first control only once the enter has ended', async () => {
    const wrapper = mountOpenDialog()
    await nextTick()
    await nextTick()

    // Right after opening the panel itself holds focus, so Escape already works while it appears.
    expect(document.activeElement).toBe(wrapper.get('[role="dialog"]').element)

    await frames()
    expect(document.activeElement).not.toBe(wrapper.get('[role="dialog"]').element)
    expect(wrapper.get('[role="dialog"]').element.contains(document.activeElement)).toBe(true)
    wrapper.unmount()
  })

  it('asks to close only once while it is leaving, whichever way it is asked', async () => {
    const wrapper = mountOpenDialog(true)
    await frames()

    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    expect(wrapper.emitted('close')).toHaveLength(1)

    // The parent hides it; the panel stays in the page for the leave, and every way of asking is dead.
    startLeaving(wrapper)
    await nextTick()
    const panel = wrapper.find('[role="dialog"]')
    expect(panel.exists(), 'the panel is still in the page while it leaves').toBe(true)
    await panel.trigger('keydown', { key: 'Escape' })
    await wrapper.get('[data-testid="dialog-close"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await wrapper.get('.fixed').trigger('click')
    expect(wrapper.emitted('close')).toHaveLength(1)
    expect(wrapper.emitted('confirm')).toBeUndefined()
    wrapper.unmount()
  })

  it('can be asked again after a leave is cancelled by showing it again', async () => {
    const wrapper = mountOpenDialog(true)
    await frames()

    startLeaving(wrapper)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    expect(wrapper.emitted('confirm')).toBeUndefined()

    cancelLeaving(wrapper)
    await nextTick()
    await wrapper.get('[data-testid="dialog-close"]').trigger('click')
    await wrapper.get('[role="dialog"]').trigger('keydown', { key: 'Escape' })
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    expect(wrapper.emitted('close')).toHaveLength(2)
    expect(wrapper.emitted('confirm')).toHaveLength(1)
    wrapper.unmount()
  })

  it('can be asked again when it is shown again', async () => {
    const wrapper = mountOpenDialog()
    await frames()
    await wrapper.setProps({ open: false })
    await frames()
    await wrapper.setProps({ open: true })
    await frames()

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    expect(wrapper.emitted('confirm')).toHaveLength(1)
    wrapper.unmount()
  })

  it('keeps working when it is shown again during its leave, with the real transition and a v-if surface', async () => {
    const frame = holdFrames()
    const wrapper = mount(UiDialog, {
      props: { open: true, variant: 'confirm', title: 'Delete', message: 'Sure?' },
      attachTo: document.body,
      global: realGlobal,
    })
    await frame.flush()

    await wrapper.setProps({ open: false })
    // The leave has started: the old panel is still in the page and every request is refused.
    expect(document.querySelectorAll('[role="dialog"]')).toHaveLength(1)
    await clickTestId('dialog-close')
    expect(wrapper.emitted('close')).toBeUndefined()

    await wrapper.setProps({ open: true })
    await frame.flush()

    // The old element is gone, a fresh one took its place, and Escape, the close button and Confirm work.
    expect(document.querySelectorAll('[role="dialog"]')).toHaveLength(1)
    await pressEscape()
    expect(wrapper.emitted('close')).toHaveLength(1)
    await clickTestId('dialog-close')
    expect(wrapper.emitted('close')).toHaveLength(2)
    await clickTestId('dialog-confirm')
    expect(wrapper.emitted('confirm')).toHaveLength(1)
    wrapper.unmount()
  })

  it('does not take focus from a control the user already focused during the enter', async () => {
    const wrapper = mount(UiDialog, {
      props: { open: true, title: 'Edit' },
      slots: { default: '<input id="first-field" /><input id="second-field" />' },
      attachTo: document.body,
      global,
    })
    await nextTick()
    await nextTick()
    const second = document.getElementById('second-field')
    second?.focus()

    await frames()

    expect(document.activeElement).toBe(second)
    wrapper.unmount()
  })

  it('gives back the page scroll and the focus at once when a parent removes it with v-if', async () => {
    const opener = document.createElement('button')
    document.body.append(opener)
    opener.focus()
    /** Whether the parent shows the dialog. */
    const show = ref(true)
    const parent = mount(
      defineComponent({ render: () => (show.value ? h(UiDialog, { title: 'Edit' }, () => h('input')) : h('p', 'gone')) }),
      { attachTo: document.body, global },
    )
    await frames()
    expect(document.body.style.overflow).toBe('hidden')

    show.value = false
    await nextTick()

    expect(parent.find('[role="dialog"]').exists()).toBe(false)
    expect(document.body.style.overflow).toBe('')
    expect(document.activeElement).toBe(opener)
    parent.unmount()
  })
})

describe('UiDrawer and its animation', () => {
  it('asks to close only once while it is leaving', async () => {
    const wrapper = mount(UiDrawer, { props: { open: true, title: 'Filters' }, attachTo: document.body, global: heldGlobal })
    await frames()

    await wrapper.get('[data-testid="drawer-close"]').trigger('click')
    expect(wrapper.emitted('close')).toHaveLength(1)

    startLeaving(wrapper)
    await nextTick()
    const close = wrapper.find('[data-testid="drawer-close"]')
    expect(close.exists(), 'the panel is still in the page while it leaves').toBe(true)
    await close.trigger('click')
    await wrapper.get('[role="dialog"]').trigger('keydown', { key: 'Escape' })
    expect(wrapper.emitted('close')).toHaveLength(1)
    wrapper.unmount()
  })

  it('can be closed and shows its close button again after a leave is cancelled', async () => {
    const wrapper = mount(UiDrawer, { props: { open: true, title: 'Filters' }, attachTo: document.body, global: heldGlobal })
    await frames()
    // The enter has not ended under the stubbed transition, so the button starts hidden; the hook below settles it.
    const onEntered = wrapper.findComponent(UiTransition).vm.$attrs.onAfterEnter as () => void
    onEntered()
    await nextTick()
    expect(wrapper.get('[data-testid="drawer-close"]').classes()).toContain('opacity-100')

    startLeaving(wrapper)
    await nextTick()
    expect(wrapper.get('[data-testid="drawer-close"]').classes()).toContain('invisible')
    await wrapper.get('[data-testid="drawer-close"]').trigger('click')
    expect(wrapper.emitted('close')).toBeUndefined()

    cancelLeaving(wrapper)
    await nextTick()
    expect(wrapper.get('[data-testid="drawer-close"]').classes()).toContain('opacity-100')
    await wrapper.get('[data-testid="drawer-close"]').trigger('click')
    await wrapper.get('[role="dialog"]').trigger('keydown', { key: 'Escape' })
    expect(wrapper.emitted('close')).toHaveLength(2)
    wrapper.unmount()
  })

  it('keeps working when it is shown again during its leave, with the real transition and a v-if surface', async () => {
    const frame = holdFrames()
    const wrapper = mount(UiDrawer, { props: { open: true, title: 'Filters' }, attachTo: document.body, global: realGlobal })
    await frame.flush()
    expect(byTestId('drawer-close')?.classList.contains('opacity-100')).toBe(true)

    await wrapper.setProps({ open: false })
    expect(document.querySelectorAll('[role="dialog"]')).toHaveLength(1)
    await clickTestId('drawer-close')
    expect(wrapper.emitted('close')).toBeUndefined()

    await wrapper.setProps({ open: true })
    await frame.flush()

    // A fresh panel replaced the old one; it has settled, so the close button shows, and Escape and the button work.
    expect(document.querySelectorAll('[role="dialog"]')).toHaveLength(1)
    expect(byTestId('drawer-close')?.classList.contains('opacity-100')).toBe(true)
    await pressEscape()
    expect(wrapper.emitted('close')).toHaveLength(1)
    await clickTestId('drawer-close')
    expect(wrapper.emitted('close')).toHaveLength(2)
    wrapper.unmount()
  })

  it('moves focus to the first control only after the close button is shown, and not at all when the user focused a control', async () => {
    const wrapper = mount(UiDrawer, {
      props: { open: true, title: 'Filters' },
      slots: { default: '<input id="drawer-field" />' },
      attachTo: document.body,
      global: heldGlobal,
    })
    await nextTick()
    await nextTick()
    const onEntered = wrapper.findComponent(UiTransition).vm.$attrs.onAfterEnter as () => Promise<void>

    // Not synchronously: the button is still invisible until the settled state has rendered.
    const pending = onEntered()
    expect(document.activeElement).toBe(wrapper.get('[role="dialog"]').element)
    await pending
    expect(document.activeElement).toBe(wrapper.get('[data-testid="drawer-close"]').element)

    // A control the user focused during the enter keeps focus.
    const field = document.getElementById('drawer-field')
    field?.focus()
    await onEntered()
    expect(document.activeElement).toBe(field)
    wrapper.unmount()
  })

  it('slides in from its own edge', () => {
    const wrapper = mount(UiDrawer, { props: { open: true, title: 'Filters', side: 'left' }, attachTo: document.body, global })

    expect(wrapper.get('[role="dialog"]').classes()).toContain('group-[.ui-from]/overlay:-translate-x-full')
    wrapper.unmount()
  })
})
