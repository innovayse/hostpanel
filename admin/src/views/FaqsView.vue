<script setup lang="ts">
/**
 * Storefront FAQ list: the questions and answers shown on the portal.
 *
 * Shows the entries ordered by sort order with drag-and-drop reordering, category and status, and
 * navigation to the dedicated create and edit pages. Deleting asks first.
 */
import { computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import StorefrontListTable from '@/components/storefront/StorefrontListTable.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiText from '@/components/ui/UiText.vue'
import { useStorefrontList } from '@/composables/useStorefrontList'
import { displayTranslation } from '@/lib/storefrontTranslations'
import { useFaqsStore } from '@/stores/faqs'
import type { Faq } from '@/types/faq'
import { storefrontTestIds } from '@/types/storefront'
import { FaqCategory } from '@/types/storefrontenums'
import { AppRoute, createPath, editPath } from '@/lib/routes'

const { t } = useI18n()

/** Test ids of the page. */
const testids = storefrontTestIds('faqs')

/** FAQ store: the list, the loading flag and the refusal sentences. */
const store = useFaqsStore()

/** Delete confirmation and reorder state of the page. */
const list = useStorefrontList<Faq>(store)

/** Label key of each category. */
const CATEGORY_LABEL_KEYS: Record<FaqCategory, string> = {
  [FaqCategory.General]: 'storefront.faqs.categories.general',
  [FaqCategory.Products]: 'storefront.faqs.categories.products',
}

/** Column captions between the drag handle and the actions. */
const columns = computed<string[]>(() => [
  t('storefront.faqs.question'),
  t('storefront.faqs.category'),
  t('shared.status'),
])

/**
 * Label of a category; a category this build does not know is shown as received.
 *
 * @param category - Category as the API sent it.
 * @returns The translated label, or the category itself.
 */
const categoryLabel = (category: string): string => {
  const key = CATEGORY_LABEL_KEYS[category as FaqCategory]
  return key ? t(key) : category
}

/**
 * The question shown for an entry, falling back to its ID when no translation exists.
 *
 * @param item - The FAQ entry.
 * @returns The question text.
 */
const questionOf = (item: Faq): string =>
  displayTranslation(item.translations)?.question || t('storefront.faqs.entryNumber', { id: item.id })

/** The question shown for the staged entry. */
const deleteMessage = computed<string>(() =>
  t('storefront.faqs.deleteMessage', { name: list.deleteTarget.value ? questionOf(list.deleteTarget.value) : '' }),
)

onMounted(async () => {
  // Errors of another page must not show on this list.
  store.resetErrors()
  await store.fetchAll()
})

/** Route of the create page, opened by the Add link. */
const createRoute = createPath(AppRoute.StorefrontFaqs)

/**
 * Route of the edit page of an entry, opened by its row action.
 *
 * @param item - The entry to edit.
 * @returns The path of its edit page.
 */
const editRoute = (item: Faq): string => editPath(AppRoute.StorefrontFaqs, item.id)
</script>

<template>
  <StorefrontListTable
    :testids="testids"
    :title="t('storefront.faqs.title')"
    :subtitle="t('storefront.faqs.subtitle')"
    :add-label="t('storefront.faqs.add')"
    :empty-label="t('storefront.faqs.empty')"
    :delete-title="t('storefront.faqs.deleteTitle')"
    :edit-label="t('storefront.faqs.edit')"
    :delete-label="t('storefront.faqs.delete')"
    :move-up-label="t('storefront.list.moveUp')"
    :move-down-label="t('storefront.list.moveDown')"
    :columns="columns"
    :items="store.items"
    :loading="store.loading"
    :error="store.error"
    :action-error="store.actionError"
    :outcome="list.outcome.value"
    :saving-order="list.savingOrder.value"
    :delete-target="list.deleteTarget.value"
    :deleting="list.deleting.value"
    :delete-message="deleteMessage"
    :add-to="createRoute"
    :edit-to="editRoute"
    @stage-delete="list.stageDelete"
    @confirm-delete="list.confirmDelete"
    @cancel-delete="list.cancelDelete"
    @reorder="list.saveOrder"
    @retry="store.fetchAll"
  >
    <template #row="{ item }">
      <UiTableCell>
        <UiText tone="primary" size="lg" class="font-medium">{{ questionOf(item) }}</UiText>
      </UiTableCell>
      <UiTableCell>
        <UiBadge tone="primary" small>{{ categoryLabel(item.category) }}</UiBadge>
      </UiTableCell>
      <UiTableCell>
        <UiBadge :tone="item.isActive ? 'success' : 'neutral'" small>
          {{ item.isActive ? t('shared.active') : t('shared.inactive') }}
        </UiBadge>
      </UiTableCell>
    </template>
  </StorefrontListTable>
</template>
