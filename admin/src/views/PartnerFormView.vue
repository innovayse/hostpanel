<script setup lang="ts">
/**
 * Full-page form for creating or editing a storefront partner.
 *
 * Detects edit mode from the route params and loads the partner when editing. The partner name is
 * translated, so it sits in en / ru / hy tabs. Asks before leaving with unsaved edits.
 */
import { computed, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import StorefrontFormShell from '@/components/storefront/StorefrontFormShell.vue'
import StorefrontImageField from '@/components/storefront/StorefrontImageField.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLocalizedField from '@/components/ui/UiLocalizedField.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSchemaForm from '@/components/ui/UiSchemaForm.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { useStorefrontForm } from '@/composables/useStorefrontForm'
import { blankTranslations, collectTranslations, filledLocales, formsFromTranslations, translationErrors } from '@/lib/storefrontTranslations'
import { MessageLanguage } from '@/locales/messages'
import { usePartnersStore } from '@/stores/partners'
import type { Partner, PartnerPayload, PartnerTranslation } from '@/types/partner'
import { AppRoute } from '@/lib/routes'
import schema from '@/views/PartnerFormView.schema'

const { t } = useI18n()

/** Partners store: loads, creates and updates the partner and uploads its logo. */
const store = usePartnersStore()

/**
 * Creates a blank name form.
 *
 * @returns An empty name.
 */
const blankName = (): PartnerTranslation => ({ name: '' })

/** The form state; any change marks the form dirty. */
const form = reactive({
  logoUrl: '',
  url: '',
  sortOrder: 0,
  isActive: true,
  translations: blankTranslations(blankName),
})

/**
 * Copies a stored partner into the form.
 *
 * @param item - The stored partner.
 */
const populate = (item: Partner): void => {
  form.logoUrl = item.logoUrl ?? ''
  form.url = item.url ?? ''
  form.sortOrder = item.sortOrder
  form.isActive = item.isActive
  form.translations = formsFromTranslations(item.translations, blankName)
}

/**
 * Tells whether a language's text is written: its main field is not blank.
 *
 * @param text - One language's text.
 * @returns True when the language is worth sending and its tab needs no marker.
 */
const isFilled = (text: PartnerTranslation): boolean => text.name.trim() !== ''

/**
 * Builds the payload from the form: trimmed text, and only the locales with a name.
 *
 * @returns The create / update payload.
 */
const build = (): PartnerPayload => ({
  logoUrl: form.logoUrl,
  url: form.url.trim(),
  sortOrder: form.sortOrder,
  isActive: form.isActive,
  translations: collectTranslations(
    form.translations,
    isFilled,
    (text) => ({ name: text.name.trim() }),
  ),
})

/** The language whose fields are on screen. */
const activeLocale = ref<MessageLanguage>(MessageLanguage.En)

/** Whether each language has its text written, for the markers on the tabs. */
const filled = computed(() => filledLocales(form.translations, isFilled))

/** The create / edit flow: loading, saving, the dirty flag. */
const flow = useStorefrontForm<Partner, PartnerPayload>({ store, form, listPath: AppRoute.StorefrontPartners, populate, build })


/** The sentences under the fields: the store's own, with the translations refusal under the name. */
const errors = computed(() => translationErrors(store.fieldErrors, ['name']))

/** The help lines under the fields. */
const hints = computed(() => ({ isActive: t('storefront.partners.activeHint') }))
</script>

<template>
  <StorefrontFormShell
    testid="partner"
    :title="flow.isEditMode.value ? t('storefront.partners.edit') : t('storefront.partners.create')"
    :save-label="flow.isEditMode.value ? t('shared.saveChanges') : t('storefront.partners.create')"
    :loading="flow.loadingEntry.value"
    :saving="flow.saving.value"
    :dirty="flow.isDirty.value"
    :error="flow.saveError.value"
    :load-error="flow.loadError.value"
    @retry="flow.reload"
    @save="flow.save"
    @cancel="flow.cancel"
  >
    <UiSchemaForm :columns="schema.columns" :loading="flow.loadingEntry.value" :errors="errors" :hints="hints" class="mb-5">
      <template #field-logoUrl>
        <StorefrontImageField
          v-model="form.logoUrl"
          :alt="t('storefront.partners.logo')"
          :upload="store.uploadImage"
          :upload-error="store.uploadError"
          testid="partner-form-logo"
        />
      </template>

      <template #field-url="{ id }">
        <UiInput :id="id" v-model="form.url" type="text" :placeholder="t('storefront.partners.websitePlaceholder')" data-testid="partner-form-url" />
      </template>

      <template #field-sortOrder>
        <UiNumberInput v-model="form.sortOrder" :min="0" placeholder="0" data-testid="partner-form-sort-order" />
      </template>

      <template #field-isActive>
        <UiToggle v-model="form.isActive" data-testid="partner-form-active" />
      </template>

      <template #field-locales>
        <UiLocalizedField v-model="activeLocale" :filled="filled" testid="partner-form-tab" />
      </template>

      <template #field-name="{ id }">
        <UiInput :id="id" v-model="form.translations[activeLocale].name" type="text" :placeholder="t('storefront.partners.namePlaceholder')" data-testid="partner-form-name" />
      </template>
    </UiSchemaForm>
  </StorefrontFormShell>
</template>
