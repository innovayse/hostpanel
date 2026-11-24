<script setup lang="ts">
/**
 * Full-page form for creating or editing a storefront testimonial.
 *
 * Detects edit mode from the route params, loads the testimonial when editing, and carries the
 * role and quote in en / ru / hy tabs. Asks before leaving with unsaved edits.
 */
import { computed, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import StorefrontFormShell from '@/components/storefront/StorefrontFormShell.vue'
import StorefrontImageField from '@/components/storefront/StorefrontImageField.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLocalizedField from '@/components/ui/UiLocalizedField.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSchemaForm from '@/components/ui/UiSchemaForm.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { useStorefrontForm } from '@/composables/useStorefrontForm'
import { blankTranslations, collectTranslations, filledLocales, formsFromTranslations, translationErrors, STOREFRONT_LOCALES } from '@/lib/storefrontTranslations'
import { ContentLanguage } from '@/locales/contentLanguage'
import { useTestimonialsStore } from '@/stores/testimonials'
import type { Testimonial, TestimonialPayload, TestimonialTranslation } from '@/types/testimonial'
import { AppRoute } from '@/lib/routes'
import schema from '@/views/TestimonialFormView.schema'

const { t } = useI18n()

/** Testimonials store: loads, creates and updates the testimonial and uploads its avatar. */
const store = useTestimonialsStore()

/** The rating choices, 1 to 5. */
const RATING_OPTIONS = [1, 2, 3, 4, 5].map((value) => ({ value, label: String(value) }))

/** Rating a new testimonial starts with. */
const DEFAULT_RATING = 5

/**
 * Creates a blank quote form.
 *
 * @returns An empty role and quote.
 */
const blankQuote = (): TestimonialTranslation => ({ role: '', quote: '' })

/** The form state; any change marks the form dirty. */
const form = reactive({
  name: '',
  company: '',
  avatarUrl: '',
  rating: DEFAULT_RATING,
  sortOrder: 0,
  isActive: true,
  publishedAt: null as string | null,
  translations: blankTranslations(blankQuote),
})

/** The language whose fields are on screen. */
const activeLocale = ref<ContentLanguage>(ContentLanguage.En)

/** Whether each language has its text written, for the markers on the tabs. */
const filled = computed(() => filledLocales(form.translations, isFilled))

/**
 * Copies a stored testimonial into the form.
 *
 * @param item - The stored testimonial.
 */
const populate = (item: Testimonial): void => {
  form.name = item.name
  form.company = item.company ?? ''
  form.avatarUrl = item.avatarUrl ?? ''
  form.rating = item.rating
  form.sortOrder = item.sortOrder
  form.isActive = item.isActive
  // The picker works in whole days; the API sends a full ISO instant.
  form.publishedAt = item.publishedAt ? item.publishedAt.slice(0, 10) : null
  form.translations = formsFromTranslations(item.translations, blankQuote)
}

/**
 * Tells whether a language's text is written: its main field is not blank.
 *
 * @param text - One language's text.
 * @returns True when the language is worth sending and its tab needs no marker.
 */
const isFilled = (text: TestimonialTranslation): boolean => text.quote.trim() !== ''

/**
 * Builds the payload from the form: trimmed text, and only the locales with a quote.
 *
 * @returns The create / update payload.
 */
const build = (): TestimonialPayload => ({
  name: form.name.trim(),
  company: form.company.trim(),
  avatarUrl: form.avatarUrl,
  rating: form.rating,
  sortOrder: form.sortOrder,
  isActive: form.isActive,
  publishedAt: form.publishedAt ? `${form.publishedAt}T00:00:00Z` : null,
  translations: collectTranslations(
    form.translations,
    isFilled,
    (text) => ({ role: text.role.trim(), quote: text.quote.trim() }),
  ),
})

/** The create / edit flow: loading, saving, the dirty flag. */
const flow = useStorefrontForm<Testimonial, TestimonialPayload>({ store, form, listPath: AppRoute.StorefrontTestimonials, populate, build })


/** The sentences under the fields: the store's own, with the translations refusal under the role and the quote. */
const errors = computed(() => translationErrors(store.fieldErrors, ['role', 'quote']))

/** The help lines under the fields. */
const hints = computed(() => ({
  publishedAt: t('storefront.testimonials.publishedAtHint'),
  isActive: t('storefront.testimonials.activeHint'),
}))
</script>

<template>
  <StorefrontFormShell
    testid="testimonial"
    :title="flow.isEditMode.value ? t('storefront.testimonials.edit') : t('storefront.testimonials.create')"
    :save-label="flow.isEditMode.value ? t('shared.save') : t('storefront.testimonials.create')"
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
      <template #field-name="{ id }">
        <UiInput :id="id" v-model="form.name" type="text" :placeholder="t('storefront.testimonials.namePlaceholder')" data-testid="testimonial-form-name" />
      </template>

      <template #field-company="{ id }">
        <UiInput :id="id" v-model="form.company" type="text" :placeholder="t('storefront.testimonials.companyPlaceholder')" data-testid="testimonial-form-company" />
      </template>

      <template #field-rating>
        <UiSelect v-model="form.rating" :options="RATING_OPTIONS" :placeholder="t('storefront.testimonials.rating')" data-testid="testimonial-form-rating" />
      </template>

      <template #field-avatarUrl>
        <StorefrontImageField
          v-model="form.avatarUrl"
          :alt="t('storefront.testimonials.avatar')"
          :upload="store.uploadImage"
          :upload-error="store.uploadError"
          testid="testimonial-form-avatar"
        />
      </template>

      <template #field-publishedAt>
        <UiDatePicker v-model="form.publishedAt" :placeholder="t('shared.selectDate')" data-testid="testimonial-form-published-at" />
      </template>

      <template #field-sortOrder>
        <UiNumberInput v-model="form.sortOrder" :min="0" placeholder="0" data-testid="testimonial-form-sort-order" />
      </template>

      <template #field-isActive>
        <UiToggle v-model="form.isActive" data-testid="testimonial-form-active" />
      </template>

      <template #field-locales>
        <UiLocalizedField v-model="activeLocale" :locales="STOREFRONT_LOCALES" :filled="filled" testid="testimonial-form-tab" />
      </template>

      <template #field-role="{ id }">
        <UiInput :id="id" v-model="form.translations[activeLocale].role" type="text" :placeholder="t('storefront.testimonials.rolePlaceholder')" data-testid="testimonial-form-role" />
      </template>

      <template #field-quote="{ id }">
        <UiTextarea
          :id="id"
          v-model="form.translations[activeLocale].quote"
          rows="4"
          :placeholder="t('storefront.testimonials.quotePlaceholder')"
          data-testid="testimonial-form-quote"
          class="resize-none"
        />
      </template>
    </UiSchemaForm>
  </StorefrontFormShell>
</template>
