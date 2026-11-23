<script setup lang="ts">
/**
 * Services list view: a search box, the filter dialog (status, product and billing cycle) with its removable chips,
 * and a paged table of all client hosted services.
 *
 * The search text, the filters and the page live in the address (`useListQuery`), so a reload, a shared link and the
 * back button restore the same list, and the API applies them before it pages. Shows id, product, domain, client,
 * price, billing cycle, next due date and status. Reads `stores/services`; a refused load shows `store.error` with a
 * retry.
 */
import { computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import ServiceFilterFields from '@/components/services/ServiceFilterFields.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import schema from '@/views/ServicesListView.schema'
import { Icon } from '@/lib/icons'
import { BILLING_CYCLE_OPTIONS, SERVICE_STATUS_OPTIONS } from '@/lib/constants'
import { formatDate, EMPTY_VALUE } from '@/lib/format'
import { formatPrice } from '@/lib/money'
import { serviceStatusTone, statusLabel } from '@/lib/tones'
import { useServicesStore } from '@/stores/services'
import { useSettingsStore } from '@/stores/settings'
import { clientPath } from '@/lib/routes'
import { schemaHeader } from '@/lib/schemaHeader'
import { useListQuery } from '@/composables/useListQuery'
import { ServiceFilterKey, useServiceFilters } from '@/composables/useServiceFilters'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiSize } from '@/utils/ui/size'
import { UiTableDensity } from '@/utils/ui/injection'

const { t } = useI18n()

/** Services store, the only caller of the service endpoints. */
const store = useServicesStore()

/** Settings store, for the product choice of the filter. */
const settingsStore = useSettingsStore()

/** The filters, the search text and the page of the list, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, narrowed, loadOnChange, enumOf, idOf, chipsOf }
  = useListQuery(Object.values(ServiceFilterKey))

/** The applied status, product and billing cycle, and their chips. */
const serviceFilters = useServiceFilters({ enumOf, idOf }, computed(() => settingsStore.products))

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(serviceFilters.chipSources)

/**
 * Loads the page the address names with the filters and search it holds.
 *
 * @returns True when loaded; false when refused (see `store.error`).
 */
const load = (): Promise<boolean> => store.fetchAll(page.value, { ...serviceFilters.filters.value, search: appliedSearch.value })

// Every change of a filter, the search or the page loads the list the address names.
loadOnChange(load)

/** True while the first load is in flight and there are no rows yet; the toolbar and the table then keep their layout. */
const initialLoading = computed<boolean>(() => store.loading && store.services.length === 0)

onMounted(() => settingsStore.fetchProducts())
</script>

<template>
  <UiStack>
    <UiPageHeader
      v-bind="schemaHeader(schema, t)"
      data-testid="services-header"
    />

    <UiListToolbar
      v-model:search="search"
      search-testid="services-search"
      :search-placeholder="t('services.servicesList.searchServices')"
      filter-testid="services-filter"
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
        testid-prefix="services-filter"
      />
    </UiListToolbar>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="services-error"
      @retry="load"
    />

    <UiDataTable
      v-else
      :density="UiTableDensity.Comfortable"
      :loading="initialLoading"
      :empty="!store.loading && store.services.length === 0"
      :page="page"
      :total="store.totalCount"
      :page-size="store.pageSize"
      pagination-testid="services-pagination"
      data-testid="services-table"
      @update:page="setPage"
    >
      <UiSchemaTableHead :columns="schema.table.columns" />
      <UiTableBody>
        <UiTableRow
          v-for="service in store.services"
          :key="service.id"
          hoverable
          :data-testid="`services-row-${service.id}`"
        >
          <UiTableCell muted>{{ service.id }}</UiTableCell>
          <UiTableCell>{{ service.productName }}</UiTableCell>
          <UiTableCell>{{ service.domain ?? EMPTY_VALUE }}</UiTableCell>
          <UiTableCell>
            <UiLink :variant="UiButtonVariant.Link" :to="clientPath(service.clientId)" :data-testid="`services-client-${service.id}`">
              {{ service.clientName }}
            </UiLink>
          </UiTableCell>
          <UiTableCell numeric>{{ formatPrice(service.price, service.priceCurrency) }}</UiTableCell>
          <UiTableCell>{{ statusLabel(BILLING_CYCLE_OPTIONS, service.billingCycle) }}</UiTableCell>
          <UiTableCell muted>{{ formatDate(service.nextDueDate) }}</UiTableCell>
          <UiTableCell>
            <UiBadge :tone="serviceStatusTone(service.status)" small>
              {{ statusLabel(SERVICE_STATUS_OPTIONS, service.status) }}
            </UiBadge>
          </UiTableCell>
        </UiTableRow>
      </UiTableBody>
      <template #empty>
        <UiEmptyState
          :title="narrowed ? t('services.servicesList.noServicesMatchYourFilters') : t('services.servicesList.noServicesFound')"
          data-testid="services-empty"
        >
          <template #icon>
            <UiIcon :markup="Icon.Server" :size="UiSize.Lg" />
          </template>
          <template v-if="activeCount > 0" #actions>
            <UiButton :variant="UiButtonVariant.Link" data-testid="services-clear-empty" @click="clearAll">
              {{ t('shared.clearFilters') }}
            </UiButton>
          </template>
        </UiEmptyState>
      </template>
    </UiDataTable>
  </UiStack>
</template>
