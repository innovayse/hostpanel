<script setup lang="ts">
/**
 * Client services list: shows all services belonging to the current client.
 * Navigates to the service detail page when a row is clicked.
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useServicesStore } from '@/stores/services'
import { SERVICE_STATUS_OPTIONS } from '@/lib/constants'
import { serviceStatusTone, statusLabel } from '@/lib/tones'
import { capitalize, formatDate } from '@/lib/format'
import { formatPrice } from '@/lib/money'
import type { ServiceListItem } from '@/types/service'
import AddExistingServiceModal from '@/components/clients/AddExistingServiceModal.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { ClientSection, clientItemPath } from '@/lib/routes'

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

/** Error message, null when no error (owned by the store). */
const error = computed(() => store.error)

/** Current page (1-based). */
const page = ref(1)

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
 * Navigate to a specific page and re-fetch.
 *
 * @param p - Target page number.
 * @returns Nothing.
 */
const goToPage = (p: number): void => {
  page.value = p
  fetchServices()
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

    <UiStack v-if="loading && services.length === 0" data-testid="client-services-loading">
      <UiSkeleton :lines="3" />
      <UiText :tone="UiTextTone.Secondary">{{ t('clients.clientServicesList.loadingServices') }}</UiText>
    </UiStack>

    <UiErrorState
      v-else-if="error"
      :message="error"
      :loading="loading"
      testid="client-services-error"
      @retry="fetchServices()"
    />

    <UiCard v-else-if="services.length > 0">
      <UiTable density="comfortable" data-testid="client-services-table">
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.id') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.productService') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.domain') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.price') }}</UiTableHeader>
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
            <UiTableCell muted>{{ service.domain ?? '—' }}</UiTableCell>
            <UiTableCell muted>{{ formatPrice(service.price, service.priceCurrency) }}</UiTableCell>
            <UiTableCell muted>{{ capitalize(service.billingCycle) }}</UiTableCell>
            <UiTableCell muted>{{ formatDate(service.nextDueDate) }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="serviceStatusTone(service.status)" small>{{ statusLabel(SERVICE_STATUS_OPTIONS, service.status) }}</UiBadge>
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>
      <UiStack
        v-if="totalCount > pageSize"
        direction="row"
        :justify="UiJustify.Between"
        :align="UiAlignItems.Center"
      >
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
          {{ t('clients.clientServicesList.totalcountServiceValue', { totalCount }) }}
        </UiText>
        <UiPagination
          :page="page"
          :total="totalCount"
          :page-size="pageSize"
          testid="client-services-pagination"
          @update:page="goToPage"
        />
      </UiStack>
    </UiCard>

    <UiCard v-else>
      <UiEmptyState :title="t('clients.clientServicesList.noServicesFoundForThis')" />
    </UiCard>
  </UiStack>
</template>
