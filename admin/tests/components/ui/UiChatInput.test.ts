/**
 * Unit tests for UiChatInput: the typed text goes out through v-model, Enter asks to send while Shift+Enter and an
 * input-method Enter do not, the field fits its text, the count shows only near the limit, and the action slot sits inside.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import UiChatInput from '@/components/ui/UiChatInput.vue'

/**
 * Mounts the field.
 *
 * @param props - Props over the defaults.
 * @returns The wrapper.
 */
const mountInput = (props: Record<string, unknown> = {}) =>
  mount(UiChatInput, {
    props: { modelValue: '', placeholder: 'Write here', label: 'Your message', testid: 'field', ...props },
    slots: { action: '<button data-testid="action">Go</button>' },
  })

describe('UiChatInput', () => {
  afterEach(() => {
    // The stubs of the size-observer test must not leak into the tests after it.
    vi.unstubAllGlobals()
  })

  it('names the field and shows the hint', () => {
    const field = mountInput().get('[data-testid="field"]')

    expect(field.attributes('aria-label')).toBe('Your message')
    expect(field.attributes('placeholder')).toBe('Write here')
    expect(field.attributes('rows')).toBe('1')
  })

  it('emits the typed text', async () => {
    const wrapper = mountInput()

    await wrapper.get('[data-testid="field"]').setValue('Hello')

    expect(wrapper.emitted('update:modelValue')).toEqual([['Hello']])
  })

  it('asks to send on Enter, not on Shift+Enter, and not while composing', async () => {
    const wrapper = mountInput({ modelValue: 'Hello' })
    const field = wrapper.get('[data-testid="field"]')

    await field.trigger('keydown', { key: 'Enter', shiftKey: true })
    await field.trigger('keydown', { key: 'Enter', isComposing: true })
    expect(wrapper.emitted('submit')).toBeUndefined()

    await field.trigger('keydown', { key: 'Enter' })
    expect(wrapper.emitted('submit')).toHaveLength(1)
  })

  it('fits its height to its text', async () => {
    const wrapper = mountInput()
    const element = wrapper.get('[data-testid="field"]').element as HTMLTextAreaElement
    Object.defineProperty(element, 'scrollHeight', { configurable: true, value: 72 })

    await wrapper.setProps({ modelValue: 'a\nb\nc' })
    await wrapper.vm.$nextTick()

    expect(element.style.height).toBe('72px')
    expect(element.classList.contains('max-h-36')).toBe(true)
  })

  it('keeps its automatic height while it is not shown, and fits once it is', async () => {
    /** Callback the component registered on its size observer. */
    let report: () => void = () => {}
    /** The fits the component asked for in the next frame, run by the test. */
    const frames: FrameRequestCallback[] = []
    vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback): number => frames.push(callback))
    vi.stubGlobal('cancelAnimationFrame', (): void => {})
    vi.stubGlobal('ResizeObserver', class {
      /** Remembers the component's callback. */
      constructor(callback: () => void) {
        report = callback
      }

      /** Accepts the element. */
      observe = (): void => {}

      /** Stops watching. */
      disconnect = (): void => {}
    })
    const wrapper = mountInput({ modelValue: 'a\nb' })
    const element = wrapper.get('[data-testid="field"]').element as HTMLTextAreaElement

    expect(element.scrollHeight).toBe(0)
    expect(element.style.height).toBe('auto')

    Object.defineProperty(element, 'scrollHeight', { configurable: true, value: 48 })
    report()

    // The observer's callback writes nothing: the fit waits for the next frame, outside the observer's delivery.
    expect(element.style.height).toBe('auto')
    expect(frames).toHaveLength(1)
    frames[0]!(0)

    expect(element.style.height).toBe('48px')
  })

  it('shows the count only from four fifths of the limit', async () => {
    const wrapper = mountInput({ maxLength: 10, modelValue: 'abcdefg' })
    expect(wrapper.find('[data-testid="ui-chat-input-count"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="field"]').attributes('maxlength')).toBe('10')

    await wrapper.setProps({ modelValue: 'abcdefgh' })

    expect(wrapper.get('[data-testid="ui-chat-input-count"]').text()).toBe('8/10')
  })

  it('draws the action inside the frame, dims a disabled field and focuses it on request', () => {
    const wrapper = mount(UiChatInput, {
      props: { modelValue: '', placeholder: 'Write here', label: 'Your message', testid: 'field' },
      slots: { action: '<button data-testid="action">Go</button>' },
      attachTo: document.body,
    })

    /** The component's exposed handle. */
    const exposed = wrapper.vm as unknown as { focus: () => void }

    expect(wrapper.find('[data-testid="action"]').exists()).toBe(true)
    exposed.focus()
    expect(document.activeElement).toBe(wrapper.get('[data-testid="field"]').element)
    wrapper.unmount()
  })

  it('dims and blocks a disabled field', () => {
    const wrapper = mountInput({ disabled: true })

    expect(wrapper.classes()).toContain('cursor-not-allowed')
    expect(wrapper.get('[data-testid="field"]').attributes('disabled')).toBeDefined()
  })
})
