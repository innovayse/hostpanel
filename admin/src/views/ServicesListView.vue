<script setup lang="ts">
/**
 * Services list view: paged table of all client hosted services.
 *
 * Shows id, product, domain, client, price, billing cycle, next due date and status. Reads
 * `stores/services`; a refused load shows `store.error` with a retry.
 */
import { onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { BILLING_CYCLE_OPTIONS, SERVICE_STATUS_OPTIONS } from '@/lib/constants'
import { formatDate } from '@/lib/format'
import { formatPrice } from '@/lib/money'
import { serviceStatusTone, statusLabel } from '@/lib/tones'
import { useServicesStore } from '@/stores/services'
import { clientPath } from '@/lib/routes'

const { t } = useI18n()

const store = useServicesStore()

/**
 * Moves to a page of the list and loads it.
 *
 * @param page - Target page, 1-based.
 */
const goToPage = (page: number): void => {
  store.page = page
  void store.fetchAll()
}

onMounted(() => store.fetchAll())
</script>

<template>
  <UiStack>
    <UiPageHeader
      :title="t('services.servicesList.services')"
      :subtitle="t('services.servicesList.viewAndManageAllClient')"
      data-testid="services-header"
    />

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="services-error"
      @retry="store.fetchAll()"
    />

    <UiCard v-else-if="store.services.length > 0 || store.loading">
      <UiTable
        density="comfortable"
        :loading="store.loading && store.services.length === 0"
        :colspan="8"
        data-testid="services-table"
      >
        <UiTableHead>
          <UiTableRow bordered>
            <UiTableHeader>{{ t('shared.id') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.productService') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.domain') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.clientName') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.price') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.billingCycle') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.nextDueDate') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody>
          <UiTableRow
            v-for="service in store.services"
            :key="service.id"
            hoverable
            bordered
            :data-testid="`services-row-${service.id}`"
          >
            <UiTableCell muted>{{ service.id }}</UiTableCell>
            <UiTableCell>{{ service.productName }}</UiTableCell>
            <UiTableCell>{{ service.domain ?? '—' }}</UiTableCell>
            <UiTableCell>
              <UiLink variant="link" :to="clientPath(service.clientId)" :data-testid="`services-client-${service.id}`">
                {{ service.clientName }}
              </UiLink>
            </UiTableCell>
            <UiTableCell>{{ formatPrice(service.price, service.priceCurrency) }}</UiTableCell>
            <UiTableCell>{{ statusLabel(BILLING_CYCLE_OPTIONS, service.billingCycle) }}</UiTableCell>
            <UiTableCell muted>{{ formatDate(service.nextDueDate) }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="serviceStatusTone(service.status)" small>
                {{ statusLabel(SERVICE_STATUS_OPTIONS, service.status) }}
              </UiBadge>
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>

      <UiStack direction="row" justify="between" align="center" class="p-3">
        <UiText tone="muted" size="xs">
          {{ t('services.servicesList.totalcountServiceValue', { totalCount: store.totalCount }) }}
        </UiText>
        <UiPagination
          :page="store.page"
          :total="store.totalCount"
          :page-size="store.pageSize"
          testid="services-pagination"
          @update:page="goToPage"
        />
      </UiStack>
    </UiCard>

    <UiCard v-else>
      <UiEmptyState :title="t('services.servicesList.noServicesFound')" data-testid="services-empty">
        <template #icon>
          <UiIcon :markup="Icon.Server" size="lg" />
        </template>
      </UiEmptyState>
    </UiCard>
  </UiStack>
</template>
