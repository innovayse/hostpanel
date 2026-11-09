<script setup lang="ts">
/**
 * Storefront server locations list: the data-centre locations shown on the portal.
 *
 * Shows the locations ordered by sort order with drag-and-drop reordering and status, and
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
import { useLocationsStore } from '@/stores/locations'
import type { ServerLocation } from '@/types/serverlocation'
import { storefrontTestIds } from '@/types/storefront'
import { AppRoute, createPath, editPath } from '@/lib/routes'

const { t } = useI18n()

/** Test ids of the page. */
const testids = storefrontTestIds('locations')

/** Locations store: the list, the loading flag and the refusal sentences. */
const store = useLocationsStore()

/** Delete confirmation and reorder state of the page. */
const list = useStorefrontList<ServerLocation>(store)

/** Column captions between the drag handle and the actions. */
const columns = computed<string[]>(() => [
  t('storefront.locations.country'),
  t('storefront.locations.name'),
  t('storefront.locations.city'),
  t('shared.status'),
])

/**
 * The name to show for a location: the first written locale, an empty string when none.
 *
 * @param item - The location.
 * @returns Its display name.
 */
const nameOf = (item: ServerLocation): string => displayTranslation(item.translations)?.name ?? ''

/**
 * The city to show for a location: the first written locale's, an empty string when none.
 *
 * @param item - The location.
 * @returns Its display city.
 */
const cityOf = (item: ServerLocation): string => displayTranslation(item.translations)?.city ?? ''

/** The question shown for the staged location. */
const deleteMessage = computed<string>(() =>
  t('storefront.locations.deleteMessage', { name: list.deleteTarget.value ? nameOf(list.deleteTarget.value) : '' }),
)

onMounted(async () => {
  // Errors of another page must not show on this list.
  store.resetErrors()
  await store.fetchAll()
})

/** Route of the create page, opened by the Add link. */
const createRoute = createPath(AppRoute.StorefrontLocations)

/**
 * Route of the edit page of a location, opened by its row action.
 *
 * @param item - The location to edit.
 * @returns The path of its edit page.
 */
const editRoute = (item: ServerLocation): string => editPath(AppRoute.StorefrontLocations, item.id)
</script>

<template>
  <StorefrontListTable
    :testids="testids"
    :title="t('storefront.locations.title')"
    :subtitle="t('storefront.locations.subtitle')"
    :add-label="t('storefront.locations.add')"
    :loading-label="t('storefront.locations.loading')"
    :empty-label="t('storefront.locations.empty')"
    :delete-title="t('storefront.locations.deleteTitle')"
    :edit-label="t('storefront.locations.edit')"
    :delete-label="t('storefront.locations.delete')"
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
        <UiBadge tone="neutral" small>{{ item.countryCode }}</UiBadge>
      </UiTableCell>
      <UiTableCell>
        <UiText tone="primary" size="lg" class="font-medium">{{ nameOf(item) }}</UiText>
      </UiTableCell>
      <UiTableCell muted>{{ cityOf(item) || '—' }}</UiTableCell>
      <UiTableCell>
        <UiBadge :tone="item.isActive ? 'success' : 'neutral'" small>
          {{ item.isActive ? t('shared.active') : t('shared.inactive') }}
        </UiBadge>
      </UiTableCell>
    </template>
  </StorefrontListTable>
</template>
