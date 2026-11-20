/**
 * Unit tests for UiLocalizedField: one tab per shipped language, the markers for an empty or a
 * refused language, the slot of the language on screen, the strip-only form and the jump to a refusal.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h, ref } from 'vue'
import UiLocalizedField from '@/components/ui/UiLocalizedField.vue'
import { STOREFRONT_LOCALES } from '@/lib/storefrontTranslations'
import { ContentLanguage } from '@/locales/contentLanguage'
import { MessageLanguage } from '@/locales/messages'
import { mountWithApp } from '../../helpers/mount'

/** Every language filled. */
const allFilled = { [ContentLanguage.En]: true, [ContentLanguage.Ru]: true, [ContentLanguage.Hy]: true, [ContentLanguage.De]: true, [ContentLanguage.Es]: true }

/** Props every mount starts from. */
const base = { locales: STOREFRONT_LOCALES, filled: allFilled, testid: 'demo-tab' }

/**
 * Mounts the field with a slot that prints the language on screen.
 *
 * @param props - Props to add to the base ones.
 * @returns The wrapper.
 */
const mountField = (props: Record<string, unknown> = {}) =>
  mountWithApp(UiLocalizedField, {
    props: { ...base, ...props },
    slots: { default: ({ locale }: { locale: string }) => h('p', { 'data-testid': 'body' }, locale) },
  })

describe('UiLocalizedField', () => {
  it('draws a tab per language, English first, with the given test ids', () => {
    const wrapper = mountField()

    expect(wrapper.findAll('[role="tab"]').map((tab) => [tab.attributes('data-testid'), tab.text()])).toEqual([
      ['demo-tab-en', 'English'],
      ['demo-tab-ru', 'Russian'],
      ['demo-tab-hy', 'Armenian'],
      ['demo-tab-de', 'German'],
      ['demo-tab-es', 'Spanish'],
    ])
  })

  it('draws only the languages it is given, as a setting kept per admin language needs', () => {
    const locales = [MessageLanguage.En, MessageLanguage.Ru, MessageLanguage.Hy]
    const wrapper = mountField({ locales })

    expect(wrapper.findAll('[role="tab"]').map((tab) => tab.text())).toEqual(['English', 'Russian', 'Armenian'])
  })

  it('hands the slot the language on screen and switches it from a tab', async () => {
    const wrapper = mountField({ label: 'Address' })
    expect(wrapper.get('[data-testid="body"]').text()).toBe('en')
    expect(wrapper.text()).toContain('Address')

    await wrapper.get('[data-testid="demo-tab-hy"]').trigger('click')

    expect(wrapper.get('[data-testid="body"]').text()).toBe('hy')
  })

  it('marks a language with no value and leaves a filled one bare', () => {
    const wrapper = mountField({ filled: { ...allFilled, [ContentLanguage.Ru]: false } })

    expect(wrapper.find('[data-testid="demo-tab-ru-empty"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="demo-tab-ru-empty"]').attributes('aria-label')).toBe('Not filled in')
    expect(wrapper.find('[data-testid="demo-tab-en-empty"]').exists()).toBe(false)
  })

  it('lets a refusal win over the empty mark and shows its sentence only under that language', async () => {
    const wrapper = mountField({
      filled: { ...allFilled, [ContentLanguage.Ru]: false },
      errors: { [ContentLanguage.Ru]: 'Too long.' },
    })

    expect(wrapper.find('[data-testid="demo-tab-ru-invalid"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="demo-tab-ru-empty"]').exists()).toBe(false)
  })

  it('jumps to the first refused language when a refusal arrives for another one', async () => {
    const wrapper = mountField()
    expect(wrapper.get('[data-testid="body"]').text()).toBe('en')

    await wrapper.setProps({ errors: { [ContentLanguage.Hy]: 'Refused.' } })

    expect(wrapper.get('[data-testid="body"]').text()).toBe('hy')
    expect(wrapper.text()).toContain('Refused.')
  })

  it('stays on the language on screen when the refusal is on it', async () => {
    const wrapper = mountField()
    await wrapper.get('[data-testid="demo-tab-ru"]').trigger('click')

    await wrapper.setProps({ errors: { [ContentLanguage.Ru]: 'Bad.', [ContentLanguage.Hy]: 'Bad too.' } })

    expect(wrapper.get('[data-testid="body"]').text()).toBe('ru')
  })

  it('draws the strip alone without a slot and follows the bound language', async () => {
    /** The language the strip is bound to. */
    const active = ref<ContentLanguage>(ContentLanguage.En)
    /**
     * Follows the strip, as v-model does.
     *
     * @param value - The language the strip reported.
     */
    const follow = (value: ContentLanguage | undefined): void => {
      active.value = value ?? ContentLanguage.En
    }
    /** Parent that binds the strip to the active language, as a form does. */
    const host = defineComponent({
      /**
       * Renders the strip bound to the active language.
       *
       * @returns The render function.
       */
      setup: () => () => h(UiLocalizedField, { ...base, modelValue: active.value, 'onUpdate:modelValue': follow }),
    })
    const wrapper = mountWithApp(host)
    expect(wrapper.find('[data-testid="body"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="demo-tab-en"]').attributes('aria-selected')).toBe('true')

    await wrapper.get('[data-testid="demo-tab-ru"]').trigger('click')

    expect(active.value).toBe(ContentLanguage.Ru)
    expect(wrapper.get('[data-testid="demo-tab-ru"]').attributes('aria-selected')).toBe('true')
  })
})
