/**
 * Unit tests for UiToastHost: it is a polite live region that is always present, draws one toast per entry of the
 * store in its tone, and a toast's close button dismisses it.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import UiToastHost from '@/components/ui/UiToastHost.vue'
import { i18n } from '@/i18n'
import { useToastStore } from '@/stores/toast'
import { UiTone } from '@/utils/ui/tone'

/**
 * Mounts the host on the real i18n instance.
 *
 * @returns The wrapper.
 */
const mountHost = () => mount(UiToastHost, { global: { plugins: [i18n] } })

describe('UiToastHost', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('is an empty polite live region while no toast is on screen', () => {
    const host = mountHost().get('[data-testid="ui-toast-host"]')

    expect(host.attributes('role')).toBe('status')
    expect(host.attributes('aria-live')).toBe('polite')
    expect(host.findAll('[data-testid="ui-toast"]')).toHaveLength(0)
  })

  it('spans the width minus the gutter above the safe area on a phone, and is a corner card from sm up', () => {
    const classes = mountHost().get('[data-testid="ui-toast-host"]').classes()

    expect(classes).toEqual(expect.arrayContaining(['inset-x-4', 'bottom-[calc(1rem+env(safe-area-inset-bottom))]', 'sm:inset-x-auto', 'sm:right-4', 'sm:w-96']))
  })

  it('draws one toast per entry with its message and tone', async () => {
    const wrapper = mountHost()
    useToastStore().push('Saved')
    useToastStore().push('Heads up', UiTone.Warning)
    await wrapper.vm.$nextTick()

    const toasts = wrapper.findAll('[data-testid="ui-toast"]')
    expect(toasts.map((toast) => toast.text())).toEqual(['Saved', 'Heads up'])
    expect(toasts.map((toast) => toast.attributes('data-tone'))).toEqual([UiTone.Success, UiTone.Warning])
  })

  it('dismisses a toast through its close button', async () => {
    const wrapper = mountHost()
    useToastStore().push('Saved')
    await wrapper.vm.$nextTick()

    await wrapper.get('[data-testid="ui-toast-close"]').trigger('click')

    expect(wrapper.findAll('[data-testid="ui-toast"]')).toHaveLength(0)
    expect(useToastStore().toasts).toEqual([])
  })
})
