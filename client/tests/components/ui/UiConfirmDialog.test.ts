/**
 * Unit tests for UiConfirmDialog: the content, the confirm and cancel events, the danger and
 * warning looks, the loading state and the default labels.
 */
import { afterEach, describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiConfirmDialog from '~/components/ui/UiConfirmDialog.vue'
import { UiConfirmVariant } from '~/utils/ui/enums'

/** Wrappers mounted by the current test, unmounted afterwards so the scroll lock is released. */
const mounted: Array<{ unmount: () => void }> = []

/**
 * Mounts a confirm dialog and remembers it for cleanup.
 *
 * @param props Props for the dialog.
 * @returns The wrapper.
 */
const mountDialog = (props: Record<string, unknown> = {}) => {
  const wrapper = mountWithKit(UiConfirmDialog, { props: { open: true, title: 'Delete it?', ...props } })
  mounted.push(wrapper)
  return wrapper
}

describe('UiConfirmDialog', () => {
  afterEach(() => {
    mounted.splice(0).forEach((wrapper) => wrapper.unmount())
  })

  it('renders nothing while closed', () => {
    const wrapper = mountDialog({ open: false })

    expect(wrapper.find('[data-testid="ui-confirm-dialog"]').exists()).toBe(false)
  })

  it('shows the title and the description', () => {
    const wrapper = mountDialog({ description: 'This cannot be undone.' })

    expect(wrapper.find('h3').text()).toBe('Delete it?')
    expect(wrapper.text()).toContain('This cannot be undone.')
  })

  it('emits confirm and cancel from the buttons', async () => {
    const wrapper = mountDialog()

    await wrapper.find('[data-testid="ui-confirm-confirm"]').trigger('click')
    await wrapper.find('[data-testid="ui-confirm-cancel"]').trigger('click')

    expect(wrapper.emitted('confirm')).toHaveLength(1)
    expect(wrapper.emitted('cancel')).toHaveLength(1)
  })

  it('cancels on a backdrop click', async () => {
    const wrapper = mountDialog()

    await wrapper.find('[data-testid="ui-dialog-backdrop"]').trigger('click')

    expect(wrapper.emitted('cancel')).toHaveLength(1)
  })

  it('uses a red confirm button for the danger variant', () => {
    const wrapper = mountDialog({ variant: UiConfirmVariant.Danger })

    expect(wrapper.find('[data-testid="ui-confirm-confirm"]').classes()).toContain('bg-red-600')
  })

  it('uses the primary confirm button otherwise', () => {
    const wrapper = mountDialog({ variant: UiConfirmVariant.Warning })

    expect(wrapper.find('[data-testid="ui-confirm-confirm"]').classes()).toContain('bg-primary-700')
  })

  it('disables the confirm button while loading', () => {
    const wrapper = mountDialog({ loading: true })

    expect(wrapper.find('[data-testid="ui-confirm-confirm"]').attributes('disabled')).toBeDefined()
  })

  it('uses the translated default labels and accepts custom ones', () => {
    const defaults = mountDialog()
    const custom = mountDialog({ confirmLabel: 'Yes, delete', cancelLabel: 'Keep' })

    expect(defaults.find('[data-testid="ui-confirm-confirm"]').text()).toBe('common.confirm')
    expect(defaults.find('[data-testid="ui-confirm-cancel"]').text()).toBe('common.cancel')
    expect(custom.find('[data-testid="ui-confirm-confirm"]').text()).toBe('Yes, delete')
    expect(custom.find('[data-testid="ui-confirm-cancel"]').text()).toBe('Keep')
  })

  it('stays a centred card on a phone instead of a full-screen sheet', () => {
    const wrapper = mountDialog()

    expect(wrapper.find('[role="dialog"]').classes()).toContain('rounded-xl')
    expect(wrapper.find('[role="dialog"]').classes()).not.toContain('h-dvh')
  })
})
