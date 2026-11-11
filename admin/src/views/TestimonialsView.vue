<script setup lang="ts">
/**
 * Storefront testimonials list: customer quotes shown on the portal.
 *
 * Shows the testimonials ordered by sort order with drag-and-drop reordering, rating and status,
 * and navigation to the dedicated create and edit pages. Deleting asks first.
 */
import { computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import StorefrontListTable from '@/components/storefront/StorefrontListTable.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiImage from '@/components/ui/UiImage.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiText from '@/components/ui/UiText.vue'
import { useStorefrontList } from '@/composables/useStorefrontList'
import { Icon } from '@/lib/icons'
import { storefrontAssetUrl } from '@/lib/runtimeConfig'
import { displayTranslation } from '@/lib/storefrontTranslations'
import { useTestimonialsStore } from '@/stores/testimonials'
import type { Testimonial } from '@/types/testimonial'
import { storefrontTestIds } from '@/types/storefront'
import { AppRoute, createPath, editPath } from '@/lib/routes'

const { t } = useI18n()

/** Test ids of the page. */
const testids = storefrontTestIds('testimonials')

/** Testimonials store: the list, the loading flag and the refusal sentences. */
const store = useTestimonialsStore()

/** Delete confirmation and reorder state of the page. */
const list = useStorefrontList<Testimonial>(store)

/** Column captions between the drag handle and the actions. */
const columns = computed<string[]>(() => [
  t('storefront.testimonials.author'),
  t('storefront.testimonials.quote'),
  t('storefront.testimonials.rating'),
  t('shared.status'),
])

/** The question shown for the staged testimonial. */
const deleteMessage = computed<string>(() =>
  t('storefront.testimonials.deleteMessage', { name: list.deleteTarget.value?.name ?? '' }),
)

onMounted(async () => {
  // Errors of another page must not show on this list.
  store.resetErrors()
  await store.fetchAll()
})

/**
 * The customer's role and company, joined for the author cell.
 *
 * @param item - The testimonial shown in the row.
 * @returns Role and company separated by a comma; empty when neither is given.
 */
const authorLine = (item: Testimonial): string =>
  [displayTranslation(item.translations)?.role, item.company].filter(Boolean).join(', ')

/**
 * The quote in the display language, for the quote cell.
 *
 * @param item - The testimonial shown in the row.
 * @returns The quote, or a dash when no translation is stored.
 */
const quoteOf = (item: Testimonial): string => displayTranslation(item.translations)?.quote ?? '—'

/** Route of the create page, opened by the Add link. */
const createRoute = createPath(AppRoute.StorefrontTestimonials)

/**
 * Route of the edit page of a testimonial, opened by its row action.
 *
 * @param item - The testimonial to edit.
 * @returns The path of its edit page.
 */
const editRoute = (item: Testimonial): string => editPath(AppRoute.StorefrontTestimonials, item.id)
</script>

<template>
  <StorefrontListTable
    :testids="testids"
    :title="t('storefront.testimonials.title')"
    :subtitle="t('storefront.testimonials.subtitle')"
    :add-label="t('storefront.testimonials.add')"
    :loading-label="t('storefront.testimonials.loading')"
    :empty-label="t('storefront.testimonials.empty')"
    :delete-title="t('storefront.testimonials.deleteTitle')"
    :edit-label="t('storefront.testimonials.edit')"
    :delete-label="t('storefront.testimonials.delete')"
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
        <UiStack direction="row" align="center" gap="sm">
          <UiImage
            v-if="item.avatarUrl"
            :src="storefrontAssetUrl(item.avatarUrl)"
            :alt="item.name"
            fit="cover"
            rounded="md"
            class="h-10 w-10 border border-border"
          />
          <UiStack v-else align="center" justify="center" gap="none" class="h-10 w-10 rounded-lg border border-border bg-surface-field text-text-muted">
            <UiIcon :markup="Icon.Image" size="sm" />
          </UiStack>
          <UiStack gap="none">
            <UiText tone="primary" size="lg" class="font-medium">{{ item.name }}</UiText>
            <UiText tone="muted" size="xs">{{ authorLine(item) }}</UiText>
          </UiStack>
        </UiStack>
      </UiTableCell>
      <UiTableCell muted>{{ quoteOf(item) }}</UiTableCell>
      <UiTableCell>{{ item.rating }} / 5</UiTableCell>
      <UiTableCell>
        <UiBadge :tone="item.isActive ? 'success' : 'neutral'" small>
          {{ item.isActive ? t('shared.active') : t('shared.inactive') }}
        </UiBadge>
      </UiTableCell>
    </template>
  </StorefrontListTable>
</template>
