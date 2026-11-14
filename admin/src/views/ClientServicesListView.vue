<script setup lang="ts">
/**
 * Client services list: shows all services belonging to the current client.
 * Navigates to the service detail page when a row is clicked. The page lives in the address (`useFilterQuery`),
 * so a reload or a shared link opens the same page.
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useServicesStore } from '@/stores/services'
import { BILLING_CYCLE_OPTIONS, SERVICE_STATUS_OPTIONS } from '@/lib/constants'
import { serviceStatusTone, statusLabel } from '@/lib/tones'
import { formatDate, EMPTY_VALUE } from '@/lib/format'
import { formatPrice } from '@/lib/money'
import type { ServiceListItem } from '@/types/service'
import AddExistingServiceModal from '@/components/clients/AddExistingServiceModal.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiResultCount from '@/components/ui/UiResultCount.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiTextSize } from '@/utils/ui/text'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { ClientSection, clientItemPath } from '@/lib/routes'
import { useFilterQuery } from '@/composables/useFilterQuery'
import { UiTableDensity } from '@/utils/ui/injection'

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

/** The page of the list, kept in the address. */
const { page, setPage } = useFilterQuery([])

/** Items per page. */
const pageSize = ref(DEFAULT_PAGE_SIZE)

/** Total number of matching items across all pages. */
const totalCount = ref(0)

/**
 * Fetches paginated services for this client from the backend.
 *
 * @returns Promise that resolves when data is loaded.
 */
const fetchServices = async (): Promise<void> => {
  const result = await store.fetchClientPage(clientId.value, page.value, pageSize.value)
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

// A page chosen, or the back button, loads the page the address names.
watch(page, fetchServices)

onMounted(() => fetchServices())
</script>

<template>
  <UiStack>
    <UiStack direction="row" :justify="UiJustify.End">
      <UiButton size="sm" data-testid="client-services-add-existing" @click="showAddExisting = true">
        {{ t('clients.addExistingService.open') }}
      </UiButton>
    </UiStack>
    <AddExistingServiceModal
      v-if="showAddExisting"
      :client-id="Number(clientId)"
      @added="onExistingAdded"
      @close="showAddExisting = false"
    />

    <UiErrorState
      v-if="error"
      :message="error"
      :loading="loading"
      testid="client-services-error"
      @retry="fetchServices()"
    />

    <UiCard v-else-if="initialLoading || services.length > 0">
      <UiTable
        :density="UiTableDensity.Comfortable"
        :loading="initialLoading"
        data-testid="client-services-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.id') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.productService') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.domain') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('shared.price') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.billingCycle') }}</UiTableHeader>
            <UiTableHeader>{{ t('clients.clientServicesList.nextDue') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow
            v-for="service in services"
            :key="service.id"
            hoverable
            class="cursor-pointer"
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
      </UiTable>
      <template v-if="initialLoading || totalCount > pageSize" #footer>
        <UiStack
          direction="row"
          wrap
          :justify="UiJustify.Between"
          :align="UiAlignItems.Center"
        >
          <UiResultCount :loading="initialLoading" :size="UiTextSize.Sm">
            {{ t('clients.clientServicesList.totalcountServiceValue', { totalCount }) }}
          </UiResultCount>
          <UiPagination
            :page="page"
            :total="totalCount"
            :page-size="pageSize"
            :loading="initialLoading"
            testid="client-services-pagination"
            @update:page="setPage"
          />
        </UiStack>
      </template>
    </UiCard>

    <UiCard v-else>
      <UiEmptyState :title="t('clients.clientServicesList.noServicesFoundForThis')" />
    </UiCard>
  </UiStack>
</template>
