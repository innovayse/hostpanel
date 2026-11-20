<script setup lang="ts">
/**
 * Full-page form for creating or editing a storefront FAQ entry.
 *
 * Detects edit mode from the route params, loads the entry when editing, and carries the question
 * and answer in one tab per content language. Asks before leaving with unsaved edits.
 */
import { computed, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import StorefrontFormShell from '@/components/storefront/StorefrontFormShell.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLocalizedField from '@/components/ui/UiLocalizedField.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSchemaForm from '@/components/ui/UiSchemaForm.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { useStorefrontForm } from '@/composables/useStorefrontForm'
import { blankTranslations, collectTranslations, filledLocales, formsFromTranslations, STOREFRONT_LOCALES } from '@/lib/storefrontTranslations'
import { ContentLanguage } from '@/locales/contentLanguage'
import { useFaqsStore } from '@/stores/faqs'
import type { Faq, FaqPayload, FaqTranslation } from '@/types/faq'
import { FaqCategory } from '@/types/storefrontenums'
import { AppRoute } from '@/lib/routes'
import schema from '@/views/FaqFormView.schema'

const { t } = useI18n()

/** FAQ store: loads, creates and updates the entry. */
const store = useFaqsStore()

/** The help line under the active switch, by field key. */
const hints = computed(() => ({ isActive: t('storefront.faqs.activeHint') }))

/** The category choices. */
const categoryOptions = computed(() => [
  { value: FaqCategory.General, label: t('storefront.faqs.categories.general') },
  { value: FaqCategory.Products, label: t('storefront.faqs.categories.products') },
])

/**
 * Creates a blank question and answer form.
 *
 * @returns An empty question and answer.
 */
const blankText = (): FaqTranslation => ({ question: '', answer: '' })

/** The form state; any change marks the form dirty. */
const form = reactive({
  category: FaqCategory.General,
  sortOrder: 0,
  isActive: true,
  translations: blankTranslations(blankText),
})

/** The language whose fields are on screen. */
const activeLocale = ref<ContentLanguage>(ContentLanguage.En)

/** Whether each language has its text written, for the markers on the tabs. */
const filled = computed(() => filledLocales(form.translations, isFilled))

/**
 * Copies a stored entry into the form.
 *
 * @param item - The stored entry.
 */
const populate = (item: Faq): void => {
  form.category = item.category
  form.sortOrder = item.sortOrder
  form.isActive = item.isActive
  form.translations = formsFromTranslations(item.translations, blankText)
}

/**
 * Tells whether a language's text is written: its main field is not blank.
 *
 * @param text - One language's text.
 * @returns True when the language is worth sending and its tab needs no marker.
 */
const isFilled = (text: FaqTranslation): boolean => text.question.trim() !== ''

/**
 * Builds the payload from the form: trimmed text, and only the locales with a question.
 *
 * @returns The create / update payload.
 */
const build = (): FaqPayload => ({
  category: form.category,
  sortOrder: form.sortOrder,
  isActive: form.isActive,
  translations: collectTranslations(
    form.translations,
    isFilled,
    (text) => ({ question: text.question.trim(), answer: text.answer.trim() }),
  ),
})

/** The create / edit flow: loading, saving, the dirty flag. */
const flow = useStorefrontForm<Faq, FaqPayload>({ store, form, listPath: AppRoute.StorefrontFaqs, populate, build })

</script>

<template>
  <StorefrontFormShell
    testid="faq"
    :title="flow.isEditMode.value ? t('storefront.faqs.edit') : t('storefront.faqs.create')"
    :save-label="flow.isEditMode.value ? t('shared.saveChanges') : t('storefront.faqs.create')"
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
      <template #field-category>
        <UiSelect v-model="form.category" :options="categoryOptions" :placeholder="t('storefront.faqs.category')" data-testid="faq-form-category" />
      </template>

      <template #field-sortOrder>
        <UiNumberInput v-model="form.sortOrder" :min="0" placeholder="0" data-testid="faq-form-sort-order" />
      </template>

      <template #field-isActive="{ id }">
        <UiToggle :id="id" v-model="form.isActive" data-testid="faq-form-active" />
      </template>

      <template #field-locale>
        <UiLocalizedField v-model="activeLocale" :locales="STOREFRONT_LOCALES" :filled="filled" testid="faq-form-tab" />
      </template>

      <template #field-translations="{ id }">
        <UiInput
          :id="id"
          v-model="form.translations[activeLocale].question"
          type="text"
          :placeholder="t('storefront.faqs.questionPlaceholder')"
          data-testid="faq-form-question"
        />
      </template>

      <template #field-answer="{ id }">
        <UiTextarea
          :id="id"
          v-model="form.translations[activeLocale].answer"
          rows="6"
          :placeholder="t('storefront.faqs.answerPlaceholder')"
          data-testid="faq-form-answer"
          class="resize-none"
        />
      </template>
    </UiSchemaForm>
  </StorefrontFormShell>
</template>
