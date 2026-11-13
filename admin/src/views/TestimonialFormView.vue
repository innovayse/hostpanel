<script setup lang="ts">
/**
 * Full-page form for creating or editing a storefront testimonial.
 *
 * Detects edit mode from the route params, loads the testimonial when editing, and carries the
 * role and quote in en / ru / hy tabs. Asks before leaving with unsaved edits.
 */
import { computed, reactive } from 'vue'
import { useI18n } from 'vue-i18n'
import SettingsSection from '@/components/settings/SettingsSection.vue'
import StorefrontFormShell from '@/components/storefront/StorefrontFormShell.vue'
import StorefrontImageField from '@/components/storefront/StorefrontImageField.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { useStorefrontForm } from '@/composables/useStorefrontForm'
import { useStorefrontLocaleTabs } from '@/composables/useStorefrontLocaleTabs'
import { blankTranslations, collectTranslations, formsFromTranslations } from '@/lib/storefrontTranslations'
import { useTestimonialsStore } from '@/stores/testimonials'
import type { Testimonial, TestimonialPayload, TestimonialTranslation } from '@/types/testimonial'
import { AppRoute } from '@/lib/routes'

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

/** The locale tab state. */
const { activeLocale, tabs, select } = useStorefrontLocaleTabs('testimonial-form-tab')

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
    (text) => text.quote.trim() !== '',
    (text) => ({ role: text.role.trim(), quote: text.quote.trim() }),
  ),
})

/** The create / edit flow: loading, saving, the dirty flag. */
const flow = useStorefrontForm<Testimonial, TestimonialPayload>({ store, form, listPath: AppRoute.StorefrontTestimonials, populate, build })

/** The trail above the form. */
const breadcrumb = computed(() => [
  { label: t('storefront.testimonials.title'), to: AppRoute.StorefrontTestimonials },
  { label: flow.isEditMode.value ? t('storefront.testimonials.editTitle') : t('storefront.testimonials.createTitle') },
])
</script>

<template>
  <StorefrontFormShell
    testid="testimonial"
    :breadcrumb="breadcrumb"
    :save-label="flow.isEditMode.value ? t('shared.saveChanges') : t('storefront.testimonials.create')"
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
      <UiGrid :columns="2">
        <UiField :label="t('storefront.testimonials.name')" :error="store.fieldErrors.name">
          <template #default="{ id }">
            <UiInput :id="id" v-model="form.name" type="text" :placeholder="t('storefront.testimonials.namePlaceholder')" data-testid="testimonial-form-name" />
          </template>
        </UiField>
        <UiField :label="t('storefront.testimonials.company')" :error="store.fieldErrors.company">
          <template #default="{ id }">
            <UiInput :id="id" v-model="form.company" type="text" :placeholder="t('storefront.testimonials.companyPlaceholder')" data-testid="testimonial-form-company" />
          </template>
        </UiField>
        <UiField :label="t('storefront.testimonials.rating')" :error="store.fieldErrors.rating">
          <UiSelect v-model="form.rating" :options="RATING_OPTIONS" :placeholder="t('storefront.testimonials.rating')" data-testid="testimonial-form-rating" />
        </UiField>
      </UiGrid>

      <StorefrontImageField
        v-model="form.avatarUrl"
        :label="t('storefront.testimonials.avatar')"
        :upload="store.uploadImage"
        :error="store.fieldErrors.avatarUrl"
        :upload-error="store.uploadError"
        testid="testimonial-form-avatar"
      />

      <UiField :label="t('storefront.testimonials.publishedAt')" :hint="t('storefront.testimonials.publishedAtHint')" :error="store.fieldErrors.publishedAt">
        <UiDatePicker v-model="form.publishedAt" :placeholder="t('shared.selectDate')" data-testid="testimonial-form-published-at" />
      </UiField>

      <UiField :label="t('storefront.form.sortOrder')" :error="store.fieldErrors.sortOrder">
        <UiNumberInput v-model="form.sortOrder" :min="0" placeholder="0" data-testid="testimonial-form-sort-order" />
      </UiField>

      <UiStack direction="row" align="center" justify="between">
        <UiStack gap="xs">
          <UiText tone="primary" size="md">{{ t('shared.active') }}</UiText>
          <UiText tone="muted" size="sm">{{ t('storefront.testimonials.activeHint') }}</UiText>
        </UiStack>
        <UiToggle v-model="form.isActive" data-testid="testimonial-form-active" />
      </UiStack>
    </SettingsSection>

    <SettingsSection :title="t('storefront.form.translations')">
      <UiTabs :items="tabs" :model-value="activeLocale" @update:model-value="select" />

      <UiField :label="t('storefront.testimonials.role')" :error="store.fieldErrors.translations">
        <template #default="{ id }">
          <UiInput :id="id" v-model="form.translations[activeLocale].role" type="text" :placeholder="t('storefront.testimonials.rolePlaceholder')" data-testid="testimonial-form-role" />
        </template>
      </UiField>

      <UiField :label="t('storefront.testimonials.quote')" :error="store.fieldErrors.translations">
        <template #default="{ id }">
          <UiTextarea
            :id="id"
            v-model="form.translations[activeLocale].quote"
            rows="4"
            :placeholder="t('storefront.testimonials.quotePlaceholder')"
            data-testid="testimonial-form-quote"
            class="resize-none"
          />
        </template>
      </UiField>
    </SettingsSection>
  </StorefrontFormShell>
</template>
