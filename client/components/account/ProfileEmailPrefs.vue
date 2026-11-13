<template>
  <UiGrid cols="2" gap="sm" data-testid="account-email-prefs">
    <UiCheckbox
      v-for="pref in options"
      :id="`${idPrefix}-pref-${pref.key}`"
      :key="pref.key"
      :label="pref.label"
      :description="pref.description"
      :model-value="modelValue[pref.key]"
      :disabled="disabled"
      :testid="`account-pref-${pref.key}`"
      @update:model-value="onToggle(pref.key, $event)"
    />
  </UiGrid>
</template>

<script setup lang="ts">
/**
 * ProfileEmailPrefs: the six email categories as checkboxes, read-only or editable.
 *
 * The view of the profile shows them disabled; the edit form shows them live. Both share this
 * list so the labels cannot drift apart.
 *
 * @module components/account/ProfileEmailPrefs
 */

import { EmailPreference } from '~/types/emailpreference'
import type { EmailPreferenceFlags } from '~/utils/emailPreferences'

/** Props of {@link ProfileEmailPrefs}. */
interface Props {
  /** One boolean per category. */
  modelValue: EmailPreferenceFlags
  /** Shows the boxes without letting them be changed. */
  disabled?: boolean
  /** Prefix that keeps checkbox ids unique when two lists are on screen. */
  idPrefix?: string
}

/** The props, with defaults. */
const props = withDefaults(defineProps<Props>(), {
  disabled: false,
  idPrefix: 'prefs'
})

/** Events of {@link ProfileEmailPrefs}. */
const emit = defineEmits<{
  /** The flags after one box changed. */
  'update:modelValue': [value: EmailPreferenceFlags]
}>()

/** Translator for the option labels. */
const { t } = useI18n()

/** The categories with their translated label and description, in display order. */
const options = computed(() => [
  { key: EmailPreference.General, label: t('client.profile.prefGeneral'), description: t('client.profile.prefGeneralDesc') },
  { key: EmailPreference.Invoice, label: t('client.profile.prefInvoice'), description: t('client.profile.prefInvoiceDesc') },
  { key: EmailPreference.Support, label: t('client.profile.prefSupport'), description: t('client.profile.prefSupportDesc') },
  { key: EmailPreference.Product, label: t('client.profile.prefProduct'), description: t('client.profile.prefProductDesc') },
  { key: EmailPreference.Domain, label: t('client.profile.prefDomain'), description: t('client.profile.prefDomainDesc') },
  { key: EmailPreference.Affiliate, label: t('client.profile.prefAffiliate'), description: t('client.profile.prefAffiliateDesc') }
])

/**
 * Emits the flags with one category changed.
 *
 * @param key - The category that changed.
 * @param value - Its new state.
 */
const onToggle = (key: EmailPreference, value: boolean): void => {
  emit('update:modelValue', { ...props.modelValue, [key]: value })
}
</script>
