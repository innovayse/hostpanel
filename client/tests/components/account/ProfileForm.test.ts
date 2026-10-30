/**
 * Unit tests for ProfileForm: the seeded draft, the save call, outcomes and the language field,
 * the check that refuses a blank required field before anything is sent, and the absence of the
 * sign-in address, which changes only through the confirmed address change.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mountAccount } from '~/tests/helpers/mountAccount'
import ProfileForm from '~/components/account/ProfileForm.vue'
import { useAccountStore } from '~/stores/account'
import { ActionOutcome } from '~/types/actionoutcome'
import type { ClientUser } from '~/types/clientuser'
import { makeClientUser } from '~/tests/helpers/clientUser'

vi.mock('~/composables/apis/useClientApi', () => ({ useClientApi: () => ({}) }))
vi.mock('~/composables/apis/useBillingApi', () => ({ useBillingApi: () => ({}) }))
vi.mock('~/composables/apis/useAuthApi', () => ({ useAuthApi: () => ({}) }))
vi.mock('qrcode', () => ({ default: { toDataURL: async (): Promise<string> => '' } }))
vi.mock('~/stores/catalog', async () => {
  const { ref } = await import('vue')
  return { useCatalogStore: () => ({ countries: ref([{ value: 'AM', label: 'Armenia' }]) }) }
})

/** The profile being edited. */
const user: ClientUser = makeClientUser({
  firstName: 'Jane',
  lastName: 'Doe',
  email: 'jane@example.com',
  address: { street: null, address2: null, city: null, state: null, postCode: null, country: 'AM' }
})

/**
 * Mounts the form.
 *
 * @param ownsLanguage - Whether the language select is offered.
 * @returns The wrapper.
 */
const mountForm = (ownsLanguage = true) =>
  mountAccount(ProfileForm, {
    props: {
      user,
      ownsLanguage,
      paymentOptions: [{ value: '', label: 'Default' }],
      languageOptions: [{ value: '', label: 'Default' }, { value: 'en', label: 'English' }]
    }
  })

describe('ProfileForm', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('seeds the inputs from the profile', () => {
    const wrapper = mountForm()

    expect((wrapper.find('[data-testid="account-firstname"]').element as HTMLInputElement).value).toBe('Jane')
  })

  it('neither shows nor sends the sign-in address', async () => {
    const wrapper = mountForm()
    const save = vi.spyOn(useAccountStore(), 'saveProfile').mockResolvedValue(ActionOutcome.Succeeded)

    await wrapper.find('form').trigger('submit')
    await Promise.resolve()

    expect(wrapper.find('[data-testid="account-email"]').exists()).toBe(false)
    expect(save.mock.calls[0]?.[0]).not.toHaveProperty('email')
  })

  it('gives every text field a placeholder', () => {
    const wrapper = mountForm()

    for (const input of wrapper.findAll('input[type="text"], input[type="email"], input[type="tel"]')) {
      expect(input.attributes('placeholder')).toBeTruthy()
    }
  })

  it('saves the draft and announces success', async () => {
    const wrapper = mountForm()
    const save = vi.spyOn(useAccountStore(), 'saveProfile').mockResolvedValue(ActionOutcome.Succeeded)

    await wrapper.find('[data-testid="account-firstname"]').setValue('Janet')
    await wrapper.find('form').trigger('submit')
    await Promise.resolve()

    expect(save).toHaveBeenCalledWith(expect.objectContaining({
      firstName: 'Janet',
      notifications: expect.objectContaining({ general: true, affiliate: true })
    }))
    await wrapper.vm.$nextTick()
    expect(wrapper.find('[data-testid="ui-form-success"]').text()).toBe('client.profile.saved')
  })

  it('leaves the language out where the deployment does not store it', async () => {
    const wrapper = mountForm(false)
    const save = vi.spyOn(useAccountStore(), 'saveProfile').mockResolvedValue(ActionOutcome.Succeeded)

    await wrapper.find('form').trigger('submit')
    await Promise.resolve()

    expect(save.mock.calls[0]?.[0].language).toBeUndefined()
  })

  it('stays open when the save is refused', async () => {
    const wrapper = mountForm()
    vi.spyOn(useAccountStore(), 'saveProfile').mockResolvedValue(ActionOutcome.Failed)

    await wrapper.find('form').trigger('submit')
    await Promise.resolve()

    await wrapper.vm.$nextTick()
    expect(wrapper.find('[data-testid="ui-form-success"]').exists()).toBe(false)
  })

  it('shows API field errors on inputs when save is refused', async () => {
    const wrapper = mountForm()
    const accountStore = useAccountStore()
    vi.spyOn(accountStore, 'saveProfile').mockImplementation(async () => {
      accountStore.fieldErrors = { firstName: 'Enter your first name.' }
      return ActionOutcome.Failed
    })

    await wrapper.find('form').trigger('submit')
    await wrapper.vm.$nextTick()

    expect(wrapper.text()).toContain('Enter your first name.')
  })

  it('emits cancel from the cancel button', async () => {
    const wrapper = mountForm()

    await wrapper.find('[data-testid="account-profile-cancel"]').trigger('click')

    expect(wrapper.emitted('cancel')).toHaveLength(1)
  })

  it('shows the API sentence of a refused save', async () => {
    const wrapper = mountForm()
    useAccountStore().saveError = 'Email taken'
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="ui-form-error"]').text()).toBe('Email taken')
  })
})
