<script setup lang="ts">
/**
 * Storefront partners list: the partner logos shown on the portal.
 *
 * Shows the partners ordered by sort order with drag-and-drop reordering and status, and
 * navigation to the dedicated create and edit pages. Deleting asks first.
 */
import { computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import StorefrontListTable from '@/components/storefront/StorefrontListTable.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiImage from '@/components/ui/UiImage.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiText from '@/components/ui/UiText.vue'
import { useStorefrontList } from '@/composables/useStorefrontList'
import { storefrontAssetUrl } from '@/lib/runtimeConfig'
import { displayTranslation } from '@/lib/storefrontTranslations'
import { usePartnersStore } from '@/stores/partners'
import type { Partner } from '@/types/partner'
import { storefrontTestIds } from '@/types/storefront'
import { AppRoute, createPath, editPath } from '@/lib/routes'
import { EMPTY_VALUE } from '@/lib/format'

const { t } = useI18n()

/** Test ids of the page. */
const testids = storefrontTestIds('partners')

/** Partners store: the list, the loading flag and the refusal sentences. */
const store = usePartnersStore()

/** Delete confirmation and reorder state of the page. */
const list = useStorefrontList<Partner>(store)

/** Column captions between the drag handle and the actions. */
const columns = computed<string[]>(() => [
  t('storefront.partners.logo'),
  t('storefront.partners.name'),
  t('storefront.partners.website'),
  t('shared.status'),
])

/**
 * The name to show for a partner: the first written locale, an empty string when none.
 *
 * @param item - The partner.
 * @returns Its display name.
 */
const nameOf = (item: Partner): string => displayTranslation(item.translations)?.name ?? ''

/** The question shown for the staged partner. */
const deleteMessage = computed<string>(() =>
  t('storefront.partners.deleteMessage', { name: list.deleteTarget.value ? nameOf(list.deleteTarget.value) : '' }),
)

onMounted(async () => {
  // Errors of another page must not show on this list.
  store.resetErrors()
  await store.fetchAll()
})

/** Route of the create page, opened by the Add link. */
const createRoute = createPath(AppRoute.StorefrontPartners)

/**
 * Route of the edit page of a partner, opened by its row action.
 *
 * @param item - The partner to edit.
 * @returns The path of its edit page.
 */
const editRoute = (item: Partner): string => editPath(AppRoute.StorefrontPartners, item.id)
</script>

<template>
  <StorefrontListTable
    :testids="testids"
    :title="t('storefront.partners.title')"
    :subtitle="t('storefront.partners.subtitle')"
    :add-label="t('storefront.partners.add')"
    :empty-label="t('storefront.partners.empty')"
    :delete-title="t('storefront.partners.deleteTitle')"
    :edit-label="t('storefront.partners.edit')"
    :delete-label="t('storefront.partners.delete')"
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
        <UiImage
          :src="item.logoUrl ? storefrontAssetUrl(item.logoUrl) : undefined"
          :alt="nameOf(item)"
          fit="contain"
          rounded="md"
          class="h-10 w-16 border border-border"
          fallback
        />
      </UiTableCell>
      <UiTableCell>
        <UiText tone="primary" size="lg" class="font-medium">{{ nameOf(item) }}</UiText>
      </UiTableCell>
      <UiTableCell muted>{{ item.url || EMPTY_VALUE }}</UiTableCell>
      <UiTableCell>
        <UiBadge :tone="item.isActive ? 'success' : 'neutral'" small>
          {{ item.isActive ? t('shared.active') : t('shared.inactive') }}
        </UiBadge>
      </UiTableCell>
    </template>
  </StorefrontListTable>
</template>
