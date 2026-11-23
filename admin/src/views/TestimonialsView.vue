<script setup lang="ts">
/**
 * Storefront testimonials list: customer quotes shown on the portal.
 *
 * Shows the testimonials ordered by sort order with drag-and-drop reordering, rating and status,
 * and navigation to the dedicated create and edit pages. Deleting asks first.
 */
import { computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { navTitleKey } from '@/lib/navigation'
import StorefrontListTable from '@/components/storefront/StorefrontListTable.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiImage from '@/components/ui/UiImage.vue'
import { UiImageFit, UiImageRounded, UiImageSize } from '@/utils/ui/image'
import UiStack from '@/components/ui/UiStack.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiTextTone, UiTextSize } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { useStorefrontList } from '@/composables/useStorefrontList'
import { storefrontAssetUrl } from '@/lib/runtimeConfig'
import { storefrontColumnCaptions } from '@/lib/storefrontList'
import { displayTranslation } from '@/lib/storefrontTranslations'
import { useTestimonialsStore } from '@/stores/testimonials'
import type { Testimonial } from '@/types/testimonial'
import { storefrontTestIds } from '@/types/storefront'
import { AppRoute, createPath, editPath } from '@/lib/routes'
import { EMPTY_VALUE } from '@/lib/format'
import schema from '@/views/TestimonialsView.schema'

const { t } = useI18n()

/** Test ids of the page. */
const testids = storefrontTestIds('testimonials')

/** Testimonials store: the list, the loading flag and the refusal sentences. */
const store = useTestimonialsStore()

/** Delete confirmation and reorder state of the page. */
const list = useStorefrontList<Testimonial>(store)

/** Column captions between the drag handle and the actions. */
const columns = computed<string[]>(() => storefrontColumnCaptions(schema.table.columns, t))

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
const quoteOf = (item: Testimonial): string => displayTranslation(item.translations)?.quote ?? EMPTY_VALUE

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
    :title="t(navTitleKey(AppRoute.StorefrontTestimonials))"
    :subtitle="t('storefront.testimonials.subtitle')"
    :add-label="t('storefront.testimonials.create')"
    :empty-label="t('storefront.testimonials.empty')"
    :delete-title="t('storefront.testimonials.deleteTitle')"
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
        <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm">
          <UiImage
            :src="item.avatarUrl ? storefrontAssetUrl(item.avatarUrl) : undefined"
            :alt="item.name"
            :fit="UiImageFit.Cover"
            :rounded="UiImageRounded.Md"
            :size="UiImageSize.Thumb"
            fallback
          />
          <UiStack :gap="UiGap.None">
            <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Lg" class="font-medium">{{ item.name }}</UiText>
            <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs">{{ authorLine(item) }}</UiText>
          </UiStack>
        </UiStack>
      </UiTableCell>
      <UiTableCell muted>{{ quoteOf(item) }}</UiTableCell>
      <UiTableCell>{{ item.rating }} / 5</UiTableCell>
      <UiTableCell>
        <UiBadge :tone="item.isActive ? UiTone.Success : UiTone.Neutral" small>
          {{ item.isActive ? t('shared.active') : t('shared.inactive') }}
        </UiBadge>
      </UiTableCell>
    </template>
  </StorefrontListTable>
</template>
