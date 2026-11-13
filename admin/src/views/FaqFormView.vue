<script setup lang="ts">
/**
 * Full-page form for creating or editing a storefront FAQ entry.
 *
 * Detects edit mode from the route params, loads the entry when editing, and carries the question
 * and answer in en / ru / hy tabs. Asks before leaving with unsaved edits.
 */
import { computed, reactive } from 'vue'
import { useI18n } from 'vue-i18n'
import SettingsSection from '@/components/settings/SettingsSection.vue'
import StorefrontFormShell from '@/components/storefront/StorefrontFormShell.vue'
import UiField from '@/components/ui/UiField.vue'
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
import { useFaqsStore } from '@/stores/faqs'
import type { Faq, FaqPayload, FaqTranslation } from '@/types/faq'
import { FaqCategory } from '@/types/storefrontenums'
import { AppRoute } from '@/lib/routes'

const { t } = useI18n()

/** FAQ store: loads, creates and updates the entry. */
const store = useFaqsStore()

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

/** The locale tab state. */
const { activeLocale, tabs, select } = useStorefrontLocaleTabs('faq-form-tab')

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
    (text) => text.question.trim() !== '',
    (text) => ({ question: text.question.trim(), answer: text.answer.trim() }),
  ),
})

/** The create / edit flow: loading, saving, the dirty flag. */
const flow = useStorefrontForm<Faq, FaqPayload>({ store, form, listPath: AppRoute.StorefrontFaqs, populate, build })

/** The trail above the form. */
const breadcrumb = computed(() => [
  { label: t('storefront.faqs.title'), to: AppRoute.StorefrontFaqs },
  { label: flow.isEditMode.value ? t('storefront.faqs.editTitle') : t('storefront.faqs.createTitle') },
])
</script>

<template>
  <StorefrontFormShell
    testid="faq"
    :breadcrumb="breadcrumb"
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
    <SettingsSection :title="t('storefront.form.general')">
      <UiField :label="t('storefront.faqs.category')" :error="store.fieldErrors.category">
        <UiSelect v-model="form.category" :options="categoryOptions" :placeholder="t('storefront.faqs.category')" data-testid="faq-form-category" />
      </UiField>

      <UiField :label="t('storefront.form.sortOrder')" :error="store.fieldErrors.sortOrder">
        <UiNumberInput v-model="form.sortOrder" :min="0" placeholder="0" data-testid="faq-form-sort-order" />
      </UiField>

      <UiStack direction="row" align="center" justify="between">
        <UiStack gap="xs">
          <UiText tone="primary" size="md">{{ t('shared.active') }}</UiText>
          <UiText tone="muted" size="sm">{{ t('storefront.faqs.activeHint') }}</UiText>
        </UiStack>
        <UiToggle v-model="form.isActive" data-testid="faq-form-active" />
      </UiStack>
    </SettingsSection>

    <SettingsSection :title="t('storefront.form.translations')">
      <UiTabs :items="tabs" :model-value="activeLocale" @update:model-value="select" />

      <UiField :label="t('storefront.faqs.question')" :error="store.fieldErrors.translations">
        <template #default="{ id }">
          <UiInput
            :id="id"
            v-model="form.translations[activeLocale].question"
            type="text"
            :placeholder="t('storefront.faqs.questionPlaceholder')"
            data-testid="faq-form-question"
          />
        </template>
      </UiField>

      <UiField :label="t('storefront.faqs.answer')">
        <template #default="{ id }">
          <UiTextarea
            :id="id"
            v-model="form.translations[activeLocale].answer"
            rows="6"
            :placeholder="t('storefront.faqs.answerPlaceholder')"
            data-testid="faq-form-answer"
            class="resize-none"
          />
        </template>
      </UiField>
    </SettingsSection>
  </StorefrontFormShell>
</template>
