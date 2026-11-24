<script setup lang="ts">
/**
 * Client services list: a search box, the filter dialog (status, product and billing cycle) with its removable chips,
 * and a paged table of the services belonging to the current client.
 *
 * Navigates to the service detail page when a row is clicked. The search text, the filters and the page live in the
 * address (`useListQuery`), so a reload, a shared link and the back button restore the same list, and the API applies
 * them before it pages.
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useServicesStore } from '@/stores/services'
import { useSettingsStore } from '@/stores/settings'
import { BILLING_CYCLE_OPTIONS, SERVICE_STATUS_OPTIONS } from '@/lib/constants'
import { serviceStatusTone, statusLabel } from '@/lib/tones'
import { formatDate, EMPTY_VALUE } from '@/lib/format'
import { formatPrice } from '@/lib/money'
import type { ServiceListItem } from '@/types/service'
import AddExistingServiceModal from '@/components/clients/AddExistingServiceModal.vue'
import ServiceFilterFields from '@/components/services/ServiceFilterFields.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import schema from '@/views/ClientServicesListView.schema'
import { UiJustify, UiDirection } from '@/utils/ui/layout'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { ClientSection, clientItemPath } from '@/lib/routes'
import { useListQuery } from '@/composables/useListQuery'
import { ServiceFilterKey, useServiceFilters } from '@/composables/useServiceFilters'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTableDensity } from '@/utils/ui/injection'
import { UiActionKind } from '@/utils/ui/actions'
import { UiSize } from '@/utils/ui/size'

const { t } = useI18n()

/** Current route, source of the client id. */
const route = useRoute()

/** Router, used to open a service. */
const router = useRouter()

/** Services store, the only caller of the service endpoints. */
const store = useServicesStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Loaded services list for the current page. */
const services = ref<ServiceListItem[]>([])

/** True while a request is in flight (owned by the store). */
const loading = computed(() => store.loading)

/** True while the first load is in flight and there are no rows yet; the table then draws its placeholder rows. */
const initialLoading = computed(() => loading.value && services.value.length === 0)

/** Error message, null when no error (owned by the store). */
const error = computed(() => store.error)

/** Settings store, for the product choice of the filter. */
const settingsStore = useSettingsStore()

/** The filters, the search text and the page of the list, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, narrowed, loadOnChange, enumOf, idOf, chipsOf }
  = useListQuery(Object.values(ServiceFilterKey))

/** The applied status, product and billing cycle, and their chips. */
const serviceFilters = useServiceFilters({ enumOf, idOf }, computed(() => settingsStore.products))

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(serviceFilters.chipSources)

/** Items per page. */
const pageSize = ref(DEFAULT_PAGE_SIZE)

/** Total number of matching items across all pages. */
const totalCount = ref(0)

/**
 * Fetches the page the address names, with the filters and search it holds, for this client from the backend.
 *
 * @returns Promise that resolves when data is loaded.
 */
const fetchServices = async (): Promise<void> => {
  const result = await store.fetchClientPage(
    clientId.value,
    page.value,
    pageSize.value,
    { ...serviceFilters.filters.value, search: appliedSearch.value },
  )
  if (result) {
    services.value = result.items
    totalCount.value = result.totalCount
  }
}

/**
 * Navigates to the service detail page for the given service.
 *
 * @param serviceId - The ID of the service to view.
 * @returns Nothing.
 */
const viewService = (serviceId: number): void => {
  router.push(clientItemPath(clientId.value, ClientSection.Services, serviceId))
}

/** Whether the add-existing-account modal is open. */
const showAddExisting = ref(false)

/**
 * Closes the modal and reloads the list after a service was added.
 *
 * @returns Nothing.
 */
const onExistingAdded = (): void => {
  showAddExisting.value = false
  fetchServices()
}

// Every change of a filter, the search or the page loads the list the address names.
loadOnChange(fetchServices)

onMounted(() => settingsStore.fetchProducts())
</script>

<template>
  <UiStack>
    <UiStack :direction="UiDirection.Row" :justify="UiJustify.End">
      <UiButton :variant="UiButtonVariant.Secondary" :action="UiActionKind.Add" :size="UiSize.Sm" data-testid="client-services-add-existing" @click="showAddExisting = true">
        {{ t('clients.addExistingService.open') }}
      </UiButton>
    </UiStack>
    <AddExistingServiceModal
      v-if="showAddExisting"
      :client-id="Number(clientId)"
      @added="onExistingAdded"
      @close="showAddExisting = false"
    />

    <UiListToolbar
      v-model:search="search"
      search-testid="client-services-search"
      :search-placeholder="t('clients.clientServicesList.searchServices')"
      filter-testid="client-services-filter"
      :active-count="activeCount"
      :chips="chips"
      :loading="initialLoading"
      @apply="apply"
      @reset="reset"
      @cancel="cancel"
      @remove="remove"
      @clear="clearAll"
    >
      <ServiceFilterFields
        v-model:status="draft.status"
        v-model:product-id="draft.productId"
        v-model:billing-cycle="draft.billingCycle"
        :products="settingsStore.products"
        testid-prefix="client-services-filter"
      />
    </UiListToolbar>

    <UiErrorState
      v-if="error"
      :message="error"
      :loading="loading"
      testid="client-services-error"
      @retry="fetchServices()"
    />

    <UiDataTable
      v-else
      :density="UiTableDensity.Comfortable"
      :loading="initialLoading"
      :empty="!initialLoading && services.length === 0"
      :page="page"
      :total="totalCount"
      :page-size="pageSize"
      pagination-testid="client-services-pagination"
      data-testid="client-services-table"
      @update:page="setPage"
    >
      <UiSchemaTableHead :columns="schema.table.columns" />
      <UiTableBody>
        <UiTableRow
          v-for="service in services"
          :key="service.id"
          hoverable
          clickable
          :data-testid="`client-services-row-${service.id}`"
          @click="viewService(service.id)"
        >
          <UiTableCell muted>{{ service.id }}</UiTableCell>
          <UiTableCell>{{ service.productName }}</UiTableCell>
          <UiTableCell muted>{{ service.domain ?? EMPTY_VALUE }}</UiTableCell>
          <UiTableCell muted numeric>{{ formatPrice(service.price, service.priceCurrency) }}</UiTableCell>
          <UiTableCell muted>{{ statusLabel(BILLING_CYCLE_OPTIONS, service.billingCycle) }}</UiTableCell>
          <UiTableCell muted>{{ formatDate(service.nextDueDate) }}</UiTableCell>
          <UiTableCell>
            <UiBadge :tone="serviceStatusTone(service.status)" small>{{ statusLabel(SERVICE_STATUS_OPTIONS, service.status) }}</UiBadge>
          </UiTableCell>
        </UiTableRow>
      </UiTableBody>
      <template #empty>
        <UiEmptyState
          :title="narrowed ? t('clients.clientServicesList.noServicesMatchYourFilters') : t('clients.clientServicesList.noServicesFoundForThis')"
          data-testid="client-services-empty"
        >
          <template v-if="activeCount > 0" #actions>
            <UiButton :variant="UiButtonVariant.Link" data-testid="client-services-clear-empty" @click="clearAll">
              {{ t('shared.clearFilters') }}
            </UiButton>
          </template>
        </UiEmptyState>
      </template>
    </UiDataTable>
  </UiStack>
</template>
