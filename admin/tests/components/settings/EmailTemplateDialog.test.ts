/**
 * Tests for EmailTemplateDialog: each language tab shows its own stored version, a language with
 * no version starts blank and says so, and submit emits the language on screen.
 */
import { describe, expect, it } from 'vitest'
import EmailTemplateDialog from '@/components/settings/EmailTemplateDialog.vue'
import { ContentLanguage } from '@/locales/contentLanguage'
import type { EmailTemplate } from '@/types/emailtemplate'
import { mountScreen } from '../../helpers/mount'

/** The English version of the template. */
const en: EmailTemplate = {
  id: 1, slug: 'welcome', language: ContentLanguage.En, subject: 'Welcome', body: '<p>Hi</p>', description: 'On sign-up', isActive: true,
}

/** The Russian version of the template. */
const ru: EmailTemplate = { ...en, id: 2, language: ContentLanguage.Ru, subject: 'Добро пожаловать', body: '<p>Привет</p>' }

/**
 * Reads the value of the native input behind a test id.
 *
 * @param wrapper - The mounted dialog.
 * @param testId - The `data-testid` of the field.
 * @returns The input's current value.
 */
const valueOf = (wrapper: Awaited<ReturnType<typeof mountScreen>>['wrapper'], testId: string): string =>
  (wrapper.get(`[data-testid="${testId}"]`).element as HTMLInputElement).value

describe('EmailTemplateDialog', () => {
  it('opens on the English version', async () => {
    const { wrapper } = await mountScreen(EmailTemplateDialog, { slug: 'welcome', versions: [en, ru], saving: false, error: null })

    expect(valueOf(wrapper, 'email-template-subject')).toBe('Welcome')
    expect(wrapper.find('[data-testid="email-template-missing"]').exists()).toBe(false)
  })

  it('shows the Russian version on the Russian tab', async () => {
    const { wrapper } = await mountScreen(EmailTemplateDialog, { slug: 'welcome', versions: [en, ru], saving: false, error: null })

    await wrapper.get('[data-testid="email-template-tab-ru"]').trigger('click')

    expect(valueOf(wrapper, 'email-template-subject')).toBe('Добро пожаловать')
  })

  it('starts a missing language blank, says so, and saves it as a new version', async () => {
    const { wrapper } = await mountScreen(EmailTemplateDialog, { slug: 'welcome', versions: [en, ru], saving: false, error: null })

    await wrapper.get('[data-testid="email-template-tab-hy"]').trigger('click')
    expect(valueOf(wrapper, 'email-template-subject')).toBe('')
    expect(wrapper.find('[data-testid="email-template-missing"]').exists()).toBe(true)

    await wrapper.get('[data-testid="email-template-subject"]').setValue(' Բարի գալուստ ')
    await wrapper.get('[data-testid="email-template-body"]').setValue('<p>Բարև</p>')
    await wrapper.get('[data-testid="email-template-form"]').trigger('submit')

    expect(wrapper.emitted('save')).toEqual([[
      ContentLanguage.Hy, null, { subject: 'Բարի գալուստ', body: '<p>Բարև</p>', description: null },
    ]])
  })

  it('saves a stored version under its id', async () => {
    const { wrapper } = await mountScreen(EmailTemplateDialog, { slug: 'welcome', versions: [en, ru], saving: false, error: null })

    await wrapper.get('[data-testid="email-template-form"]').trigger('submit')

    expect(wrapper.emitted('save')).toEqual([[
      ContentLanguage.En, 1, { subject: 'Welcome', body: '<p>Hi</p>', description: 'On sign-up' },
    ]])
  })

  it('shows the API sentence of a refused save', async () => {
    const { wrapper } = await mountScreen(EmailTemplateDialog, { slug: 'welcome', versions: [en], saving: false, error: 'Already exists.' })

    expect(wrapper.get('[data-testid="email-template-error"]').text()).toContain('Already exists.')
  })

  it('emits close from Cancel', async () => {
    const { wrapper } = await mountScreen(EmailTemplateDialog, { slug: 'welcome', versions: [en], saving: false, error: null })

    await wrapper.get('[data-testid="email-template-cancel"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
