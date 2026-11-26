<script setup lang="ts">
/**
 * One value that is written per language: a tab strip with a tab for every content language
 * (`STOREFRONT_LOCALES`, the one shared list, the same languages the interface and the language switcher
 * ship) and, under it, the control of the language on screen.
 *
 * With a default slot it draws the whole field (caption, strip, the slot, the hint or the active
 * language's refusal) and the slot receives `{ locale, id, invalid, describedby }` to bind on the control.
 * Without a slot it draws the strip alone, for a form that has several fields per language and shows
 * them itself from the selected `v-model` locale. Either way a tab whose value is still empty carries
 * a small marker (`filled`), a tab whose language the server refused carries a red one (`errors`), and
 * the strip jumps to the first refused language when the refusal is not on the one on screen.
 */
import { computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiField from '@/components/ui/UiField.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import { STOREFRONT_LOCALES } from '@/lib/storefrontTranslations'
import type { ContentLanguage } from '@/locales/contentLanguage'
import { UiTabMarker } from '@/utils/ui/tabs'
import type { UiTabItem } from '@/utils/ui/tabs'

const { t } = useI18n()

/** Props for UiLocalizedField. */
const props = withDefaults(defineProps<{
  /** Caption above the strip; only drawn with a default slot. */
  label?: string
  /** Help line under the control while the language on screen has no refusal. */
  hint?: string
  /** Marks the caption with an asterisk. */
  required?: boolean
  /** Draws a skeleton instead of the control while the value is still loading. */
  loading?: boolean
  /** Whether each language holds a value; a language that does not gets the "empty" marker. */
  filled: Record<ContentLanguage, boolean>
  /** The sentence the server refused each language with; such a tab gets the red marker. */
  errors?: Partial<Record<ContentLanguage, string>>
  /** Prefix of the tab test ids: `<testid>-<language code>`, e.g. `<testid>-en`, `<testid>-de`. */
  testid?: string
}>(), {
  label: undefined,
  hint: undefined,
  required: false,
  loading: false,
  /**
   * Default for the refusals by language.
   *
   * @returns A new empty map, so instances never share one.
   */
  errors: () => ({}),
  testid: 'localized-field-tab',
})

/** The language whose control is on screen when the parent binds it (two-way, `v-model`); without a binding the first language shows. */
const selected = defineModel<ContentLanguage>()

/** The language on screen: the bound one, else the first of the strip. */
const active = computed<ContentLanguage>({
  /**
   * Reads the language on screen.
   *
   * @returns The bound language, or the first one of the strip.
   */
  get: () => selected.value ?? STOREFRONT_LOCALES[0],
  /**
   * Switches the language on screen.
   *
   * @param value - The language to show.
   */
  set: (value) => {
    selected.value = value
  },
})

/** One tab per language, with the marker its state calls for (a refusal wins over an empty value). */
const tabs = computed<UiTabItem[]>(() => STOREFRONT_LOCALES.map((locale) => {
  const item: UiTabItem = { value: locale, label: t(`storefront.locales.${locale}`), testid: `${props.testid}-${locale}` }
  if (props.errors[locale]) {
    return { ...item, marker: UiTabMarker.Invalid, markerLabel: t('ui.uiLocalizedField.invalidTab') }
  }
  if (!props.filled[locale]) {
    return { ...item, marker: UiTabMarker.Empty, markerLabel: t('ui.uiLocalizedField.emptyTab') }
  }
  return item
}))

/**
 * Switches the language from the value the strip reported.
 *
 * @param value - The tab value the strip reported.
 */
const select = (value: string): void => {
  const locale = STOREFRONT_LOCALES.find((candidate) => candidate === value)
  if (locale !== undefined) {
    active.value = locale
  }
}

/** Moves to the first refused language when a refusal arrives and the one on screen has none. */
watch(() => props.errors, (errors) => {
  if (errors[active.value]) {
    return
  }
  const refused = STOREFRONT_LOCALES.find((locale) => errors[locale])
  if (refused !== undefined) {
    active.value = refused
  }
})
</script>

<template>
  <UiTabs v-if="!$slots.default" :items="tabs" :model-value="active" @update:model-value="select" />

  <UiField v-else :label="label" :hint="hint" :required="required" :loading="loading" :error="errors[active]">
    <template #default="{ id, invalid, describedby }">
      <div class="flex min-w-0 flex-col gap-2">
        <UiTabs :items="tabs" :model-value="active" @update:model-value="select" />
        <slot :locale="active" :id="id" :invalid="invalid" :describedby="describedby" />
      </div>
    </template>
  </UiField>
</template>
