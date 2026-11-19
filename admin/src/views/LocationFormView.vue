<script setup lang="ts">
/**
 * Full-page form for creating or editing a storefront server location.
 *
 * Detects edit mode from the route params and loads the location when editing. The name, city and
 * latency note are translated, so they sit in en / ru / hy tabs. Asks before leaving with unsaved
 * edits.
 */
import { computed, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import StorefrontFormShell from '@/components/storefront/StorefrontFormShell.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLocalizedField from '@/components/ui/UiLocalizedField.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSchemaForm from '@/components/ui/UiSchemaForm.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { useStorefrontForm } from '@/composables/useStorefrontForm'
import { blankTranslations, collectTranslations, filledLocales, STOREFRONT_LOCALES } from '@/lib/storefrontTranslations'
import { ContentLanguage } from '@/locales/contentLanguage'
import { useLocationsStore } from '@/stores/locations'
import type { ServerLocation, ServerLocationPayload, ServerLocationTranslation } from '@/types/serverlocation'
import { AppRoute } from '@/lib/routes'
import schema from '@/views/LocationFormView.schema'

const { t } = useI18n()

/** Length of an ISO 3166 alpha-2 country code. */
const COUNTRY_CODE_LENGTH = 2

/** The help line under the active switch, by field key. */
const hints = computed(() => ({ isActive: t('storefront.locations.activeHint') }))

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
 * Tells whether a language's text is written: its main field is not blank.
 *
 * @param text - One language's text.
 * @returns True when the language is worth sending and its tab needs no marker.
 */
const isFilled = (text: ServerLocationTranslation): boolean => text.name.trim() !== ''

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
    isFilled,
    (text) => ({ name: text.name.trim(), city: text.city.trim(), latencyNote: text.latencyNote.trim() }),
  ),
})

/** The language whose fields are on screen. */
const activeLocale = ref<ContentLanguage>(ContentLanguage.En)

/** Whether each language has its text written, for the markers on the tabs. */
const filled = computed(() => filledLocales(form.translations, isFilled))

/** The create / edit flow: loading, saving, the dirty flag. */
const flow = useStorefrontForm<ServerLocation, ServerLocationPayload>({ store, form, listPath: AppRoute.StorefrontLocations, populate, build })

</script>

<template>
  <StorefrontFormShell
    testid="location"
    :title="flow.isEditMode.value ? t('storefront.locations.edit') : t('storefront.locations.create')"
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
    <UiSchemaForm :columns="schema.columns" :loading="flow.loadingEntry.value" :errors="store.fieldErrors" :hints="hints">
      <template #field-countryCode="{ id }">
        <UiInput
          :id="id"
          v-model="form.countryCode"
          type="text"
          :maxlength="COUNTRY_CODE_LENGTH"
          :placeholder="t('storefront.locations.countryPlaceholder')"
          data-testid="location-form-country"
        />
      </template>

      <template #field-sortOrder>
        <UiNumberInput v-model="form.sortOrder" :min="0" placeholder="0" data-testid="location-form-sort-order" />
      </template>

      <template #field-isActive="{ id }">
        <UiToggle :id="id" v-model="form.isActive" data-testid="location-form-active" />
      </template>

      <template #field-locale>
        <UiLocalizedField v-model="activeLocale" :locales="STOREFRONT_LOCALES" :filled="filled" testid="location-form-tab" />
      </template>

      <template #field-translations="{ id }">
        <UiInput :id="id" v-model="form.translations[activeLocale].name" type="text" :placeholder="t('storefront.locations.namePlaceholder')" data-testid="location-form-name" />
      </template>

      <template #field-city="{ id }">
        <UiInput :id="id" v-model="form.translations[activeLocale].city" type="text" :placeholder="t('storefront.locations.cityPlaceholder')" data-testid="location-form-city" />
      </template>

      <template #field-latencyNote="{ id }">
        <UiInput :id="id" v-model="form.translations[activeLocale].latencyNote" type="text" :placeholder="t('storefront.locations.latencyNotePlaceholder')" data-testid="location-form-latency-note" />
      </template>
    </UiSchemaForm>
  </StorefrontFormShell>
</template>
