<script setup lang="ts">
/**
 * Gateway transaction log: the payment-gateway interactions the API records (settled invoice
 * transactions and open payment sessions), filtered by date range, debug text, gateway and
 * result, and paged by the server.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiFilterCard from '@/components/ui/UiFilterCard.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { gatewayLogResultTone } from '@/lib/tones'
import { useGatewayLogStore } from '@/stores/gatewayLog'
import { GatewayLogResult } from '@/types/gatewaylog'
import type { GatewayLogFilters } from '@/types/gatewaylog'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'

const { t, locale } = useI18n()

const store = useGatewayLogStore()

/** Current page (1-based). */
const page = ref(1)

/** True while the filter card is open. */
const filtersOpen = ref(false)

/** Date range being edited. */
const dateRange = ref<[string, string] | null>(null)

/** Debug text being edited. */
const debugData = ref('')

/** Gateway text being edited. */
const gateway = ref('')

/** Result being edited; empty means any. */
const result = ref<GatewayLogResult | ''>('')

/** Filters the list is currently loaded with. */
const applied = ref<GatewayLogFilters>({ from: '', to: '', gateway: '', debugData: '', result: '' })

/** Options of the result select. */
const resultOptions = computed(() => [
  { value: '', label: t('shared.any') },
  { value: GatewayLogResult.Payment, label: t('billing.gatewayLog.payment') },
  { value: GatewayLogResult.Refund, label: t('billing.gatewayLog.refund') },
  { value: GatewayLogResult.Credit, label: t('billing.gatewayLog.credit') },
  { value: GatewayLogResult.Pending, label: t('billing.gatewayLog.pending') },
])

/**
 * Loads a page of the log with the applied filters.
 *
 * @param next - Page number (1-based).
 * @returns Promise that resolves when the page is loaded.
 */
const load = async (next: number): Promise<void> => {
  page.value = next
  await store.fetchPage(applied.value, next, DEFAULT_PAGE_SIZE)
}

/**
 * Applies the filter card and returns to the first page.
 *
 * @returns Promise that resolves when the first page is loaded.
 */
const applyFilters = (): Promise<void> => {
  applied.value = {
    from: dateRange.value?.[0] ?? '',
    to: dateRange.value?.[1] ?? '',
    gateway: gateway.value,
    debugData: debugData.value,
    result: result.value,
  }
  return load(1)
}

/**
 * Clears the filter card and the applied filters.
 *
 * @returns Promise that resolves when the unfiltered first page is loaded.
 */
const clearFilters = (): Promise<void> => {
  dateRange.value = null
  debugData.value = ''
  gateway.value = ''
  result.value = ''
  return applyFilters()
}

onMounted(() => load(1))
</script>

<template>
  <UiStack data-testid="billing-gateway-log-view">
    <UiPageHeader
      :title="t('billing.gatewayLog.gatewayTransactionLog')"
      :subtitle="t('billing.gatewayLog.viewPaymentGatewayTransactionLogs')"
    >
      <template #actions>
        <UiButton variant="secondary" data-testid="billing-gateway-log-filter-toggle" @click="filtersOpen = !filtersOpen">
          {{ filtersOpen ? t('shared.hideFilters') : t('shared.searchFilter') }}
        </UiButton>
      </template>
    </UiPageHeader>

    <UiFilterCard v-if="filtersOpen">
      <UiGrid :columns="2" container class="items-end">
        <UiField :label="t('shared.dateRange')">
          <UiDateRangePicker v-model="dateRange" :placeholder="t('shared.selectDateRange')" />
        </UiField>
        <UiField :label="t('billing.gatewayLog.debugData')">
          <template #default="{ id }">
            <UiInput
              :id="id"
              v-model="debugData"
              type="text"
              :placeholder="t('billing.gatewayLog.searchInDebugData')"
              data-testid="billing-gateway-log-debug"
            />
          </template>
        </UiField>
        <UiField :label="t('billing.gatewayLog.gateway')">
          <template #default="{ id }">
            <UiInput
              :id="id"
              v-model="gateway"
              type="text"
              :placeholder="t('billing.gatewayLog.gateway')"
              data-testid="billing-gateway-log-gateway"
            />
          </template>
        </UiField>
        <UiField :label="t('billing.gatewayLog.result')">
          <UiSelect v-model="result" :options="resultOptions" :placeholder="t('billing.gatewayLog.result')" />
        </UiField>
      </UiGrid>
      <template #actions>
        <UiStack direction="row" justify="center" gap="sm">
          <UiButton variant="primary" data-testid="billing-gateway-log-search" @click="applyFilters">
            <UiIcon :markup="Icon.Search" size="sm" />
            {{ t('shared.search2') }}
          </UiButton>
          <UiButton variant="ghost" data-testid="billing-gateway-log-clear" @click="clearFilters">
            {{ t('shared.clear') }}
          </UiButton>
        </UiStack>
      </template>
    </UiFilterCard>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="billing-gateway-log-error"
      @retry="load(page)"
    />

    <UiText tone="muted" size="md" data-testid="billing-gateway-log-count">
      {{ t('shared.totalcountRecordsFound', { totalCount: store.totalCount }) }}
    </UiText>

    <UiCard>
      <UiTable
        :loading="store.loading && store.entries.length === 0"
        :empty="!store.loading && store.entries.length === 0"
        :colspan="4"
        data-testid="billing-gateway-log-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.date') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.gatewayLog.gateway') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.gatewayLog.debugData') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.gatewayLog.result') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="entry in store.entries" :key="entry.id" hoverable>
            <UiTableCell muted>{{ new Date(entry.dateTime).toLocaleString(locale) }}</UiTableCell>
            <UiTableCell>{{ entry.gateway }}</UiTableCell>
            <UiTableCell>
              <UiText tone="secondary" size="sm" class="whitespace-pre-wrap break-words font-mono">{{ entry.debugData }}</UiText>
            </UiTableCell>
            <UiTableCell>
              <UiBadge :tone="gatewayLogResultTone(entry.result)">{{ t(`billing.gatewayLog.${entry.result.toLowerCase()}`) }}</UiBadge>
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          {{ t('shared.noRecordsFound') }}
        </template>
      </UiTable>
    </UiCard>

    <UiPagination
      :page="page"
      :total="store.totalCount"
      :page-size="DEFAULT_PAGE_SIZE"
      testid="billing-gateway-log-pagination"
      @update:page="load"
    />
  </UiStack>
</template>
