<script setup lang="ts">
/**
 * Full-page form for creating or editing a storefront server location.
 *
 * Detects edit mode from the route params and loads the location when editing. The name, city and
 * latency note are translated, so they sit in en / ru / hy tabs. Asks before leaving with unsaved
 * edits.
 */
import { computed, reactive } from 'vue'
import { useI18n } from 'vue-i18n'
import SettingsSection from '@/components/settings/SettingsSection.vue'
import StorefrontFormShell from '@/components/storefront/StorefrontFormShell.vue'
import UiField from '@/components/ui/UiField.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import UiText from '@/components/ui/UiText.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { useStorefrontForm } from '@/composables/useStorefrontForm'
import { useStorefrontLocaleTabs } from '@/composables/useStorefrontLocaleTabs'
import { blankTranslations, collectTranslations, STOREFRONT_LOCALES } from '@/lib/storefrontTranslations'
import { useLocationsStore } from '@/stores/locations'
import type { ServerLocation, ServerLocationPayload } from '@/types/serverlocation'
import { AppRoute } from '@/lib/routes'

const { t } = useI18n()

/** Length of an ISO 3166 alpha-2 country code. */
const COUNTRY_CODE_LENGTH = 2

/** Locations store: loads, creates and updates the location. */
const store = useLocationsStore()

/** The text fields of one locale, all strings so the inputs never bind null. */
interface LocationTextForm {
  /** Location name. */
  name: string
  /** City. */
  city: string
  /** Short latency note. */
  latencyNote: string
}

/**
 * Creates a blank text form.
 *
 * @returns Empty name, city and latency note.
 */
const blankText = (): LocationTextForm => ({ name: '', city: '', latencyNote: '' })

/** The form state; any change marks the form dirty. */
const form = reactive({
  countryCode: '',
  sortOrder: 0,
  isActive: true,
  translations: blankTranslations(blankText),
})

/**
 * Copies a stored location into the form; a missing city or note becomes an empty string.
 *
 * @param item - The stored location.
 */
const populate = (item: ServerLocation): void => {
  form.countryCode = item.countryCode
  form.sortOrder = item.sortOrder
  form.isActive = item.isActive
  const forms = blankTranslations(blankText)
  for (const locale of STOREFRONT_LOCALES) {
    const text = item.translations[locale]
    if (text) {
      forms[locale] = { name: text.name, city: text.city ?? '', latencyNote: text.latencyNote ?? '' }
    }
  }
  form.translations = forms
}

/**
 * Builds the payload from the form: an upper-case code, trimmed text, and only the locales with a name.
 *
 * @returns The create / update payload.
 */
const build = (): ServerLocationPayload => ({
  countryCode: form.countryCode.trim().toUpperCase(),
  sortOrder: form.sortOrder,
  isActive: form.isActive,
  translations: collectTranslations(
    form.translations,
    (text) => text.name.trim() !== '',
    (text) => ({ name: text.name.trim(), city: text.city.trim(), latencyNote: text.latencyNote.trim() }),
  ),
})

/** The locale tab state. */
const { activeLocale, tabs, select } = useStorefrontLocaleTabs('location-form-tab')

/** The create / edit flow: loading, saving, the dirty flag. */
const flow = useStorefrontForm<ServerLocation, ServerLocationPayload>({ store, form, listPath: AppRoute.StorefrontLocations, populate, build })

/** The trail above the form. */
const breadcrumb = computed(() => [
  { label: t('storefront.locations.title'), to: AppRoute.StorefrontLocations },
  { label: flow.isEditMode.value ? t('storefront.locations.editTitle') : t('storefront.locations.createTitle') },
])
</script>

<template>
  <StorefrontFormShell
    testid="location"
    :breadcrumb="breadcrumb"
    :save-label="flow.isEditMode.value ? t('shared.saveChanges') : t('storefront.locations.create')"
    :loading="flow.loadingEntry.value"
    :saving="flow.saving.value"
    :dirty="flow.isDirty.value"
    :error="flow.saveError.value"
    :load-error="flow.loadError.value"
    @retry="flow.reload"
    @save="flow.save"
    @cancel="flow.cancel"
  >
    <SettingsSection :title="t('storefront.form.general')">
      <UiField :label="t('storefront.locations.country')" :error="store.fieldErrors.countryCode">
        <template #default="{ id }">
          <UiInput
            :id="id"
            v-model="form.countryCode"
            type="text"
            :maxlength="COUNTRY_CODE_LENGTH"
            :placeholder="t('storefront.locations.countryPlaceholder')"
            data-testid="location-form-country"
          />
        </template>
      </UiField>

      <UiField :label="t('storefront.form.sortOrder')" :error="store.fieldErrors.sortOrder">
        <UiNumberInput v-model="form.sortOrder" :min="0" placeholder="0" data-testid="location-form-sort-order" />
      </UiField>

      <UiStack direction="row" align="center" justify="between">
        <UiStack gap="xs">
          <UiText tone="primary" size="md">{{ t('shared.active') }}</UiText>
          <UiText tone="muted" size="sm">{{ t('storefront.locations.activeHint') }}</UiText>
        </UiStack>
        <UiToggle v-model="form.isActive" data-testid="location-form-active" />
      </UiStack>
    </SettingsSection>

    <SettingsSection :title="t('storefront.form.translations')">
      <UiTabs :items="tabs" :model-value="activeLocale" @update:model-value="select" />

      <UiField :label="t('storefront.locations.name')" :error="store.fieldErrors.translations">
        <template #default="{ id }">
          <UiInput :id="id" v-model="form.translations[activeLocale].name" type="text" :placeholder="t('storefront.locations.namePlaceholder')" data-testid="location-form-name" />
        </template>
      </UiField>

      <UiField :label="t('storefront.locations.city')">
        <template #default="{ id }">
          <UiInput :id="id" v-model="form.translations[activeLocale].city" type="text" :placeholder="t('storefront.locations.cityPlaceholder')" data-testid="location-form-city" />
        </template>
      </UiField>

      <UiField :label="t('storefront.locations.latencyNote')">
        <template #default="{ id }">
          <UiInput :id="id" v-model="form.translations[activeLocale].latencyNote" type="text" :placeholder="t('storefront.locations.latencyNotePlaceholder')" data-testid="location-form-latency-note" />
        </template>
      </UiField>
    </SettingsSection>
  </StorefrontFormShell>
</template>
