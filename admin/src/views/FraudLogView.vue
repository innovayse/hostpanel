<script setup lang="ts">
/**
 * Fraud log: every fraud check the API recorded (registrations and orders) with its decision,
 * risk score and the reasons behind it, searched by email or IP address, filtered by kind, decision and
 * reason code, and paged by the server.
 *
 * The search text, the filters and the page live in the address (`useListQuery`): the filter button opens the
 * kind, decision and reason fields, an applied filter shows as a removable chip, and a reload or a shared link
 * restores the same list. Changing the search or a filter returns to the first page.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiResultCount from '@/components/ui/UiResultCount.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { FRAUD_CHECK_KIND_OPTIONS, FRAUD_DECISION_OPTIONS, FRAUD_REASON_OPTIONS } from '@/lib/constants'
import { formatDateTime } from '@/lib/format'
import { clientPath, orderPath } from '@/lib/routes'
import { fraudDecisionTone, statusLabel } from '@/lib/tones'
import { useListQuery } from '@/composables/useListQuery'
import { useFraudLogStore } from '@/stores/fraudLog'
import { FraudCheckKind, FraudDecision, FraudReasonCode } from '@/types/fraud'
import type { FraudLogFilters } from '@/types/fraud'
import { UiButtonVariant } from '@/utils/ui/button'

const { t } = useI18n()

/** Fraud log store: the page, the total and the refusal sentence. */
const store = useFraudLogStore()

/** The filter fields the dialog edits; each is one query key and one `FraudLogFilters` member. */
const FilterKey = {
  Kind: 'kind',
  Decision: 'decision',
  Reason: 'reason',
} as const

/** The search text, the filters and the page of the list, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, narrowed, loadOnChange, enumOf, chipsOf }
  = useListQuery(Object.values(FilterKey))

/** Options of the kind filter: any, then every kind. */
const kindOptions = computed(() => [{ value: '', label: t('shared.any') }, ...FRAUD_CHECK_KIND_OPTIONS])

/** Options of the decision filter: any, then every decision. */
const decisionOptions = computed(() => [{ value: '', label: t('shared.any') }, ...FRAUD_DECISION_OPTIONS])

/** Options of the reason filter: any, then every reason code. */
const reasonOptions = computed(() => [{ value: '', label: t('shared.any') }, ...FRAUD_REASON_OPTIONS])

/** The kind in the address as a check kind, undefined when none is set or the value is not one. */
const kindOfAddress = enumOf(FilterKey.Kind, FraudCheckKind)

/** The decision in the address as a fraud decision, undefined when none is set or the value is not one. */
const decisionOfAddress = enumOf(FilterKey.Decision, FraudDecision)

/** The reason in the address as a reason code, undefined when none is set or the value is not one. */
const reasonOfAddress = enumOf(FilterKey.Reason, FraudReasonCode)

/** What the address narrows the log by, as the store sends it to the API. */
const filters = computed<FraudLogFilters>(() => ({
  search: appliedSearch.value,
  kind: kindOfAddress.value,
  decision: decisionOfAddress.value,
  reason: reasonOfAddress.value,
}))

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(() => [
  {
    key: FilterKey.Kind,
    label: t('fraud.log.kind'),
    value: kindOfAddress.value === undefined ? undefined : statusLabel(FRAUD_CHECK_KIND_OPTIONS, kindOfAddress.value),
  },
  {
    key: FilterKey.Decision,
    label: t('fraud.log.decision'),
    value: decisionOfAddress.value === undefined ? undefined : statusLabel(FRAUD_DECISION_OPTIONS, decisionOfAddress.value),
  },
  {
    key: FilterKey.Reason,
    label: t('fraud.log.reason'),
    value: reasonOfAddress.value === undefined ? undefined : statusLabel(FRAUD_REASON_OPTIONS, reasonOfAddress.value),
  },
])

/**
 * Reads the page the address names with the search and filters it holds.
 *
 * @returns True when read; false when refused (see `store.error`).
 */
const load = (): Promise<boolean> => store.fetchPage(page.value, filters.value)

// Every change of a filter, the search or the page loads the log the address names.
loadOnChange(load)

/** True while the first load is in flight and there are no rows yet; the toolbar then keeps its layout. */
const initialLoading = computed<boolean>(() => store.loading && store.entries.length === 0)
</script>

<template>
  <UiStack data-testid="fraud-log-view">
    <UiPageHeader :title="t('fraud.log.title')" :subtitle="t('fraud.log.subtitle')" />

    <UiListToolbar
      v-model:search="search"
      search-testid="fraud-log-search"
      :search-placeholder="t('fraud.log.searchPlaceholder')"
      :active-count="activeCount"
      :chips="chips"
      :loading="initialLoading"
      @apply="apply"
      @reset="reset"
      @cancel="cancel"
      @remove="remove"
      @clear="clearAll"
    >
      <UiField :label="t('fraud.log.kind')">
        <template #default="{ id }">
          <UiSelect
            :id="id"
            v-model="draft.kind"
            :options="kindOptions"
            :placeholder="t('shared.any')"
            data-testid="fraud-log-kind"
          />
        </template>
      </UiField>
      <UiField :label="t('fraud.log.decision')">
        <template #default="{ id }">
          <UiSelect
            :id="id"
            v-model="draft.decision"
            :options="decisionOptions"
            :placeholder="t('shared.any')"
            data-testid="fraud-log-decision"
          />
        </template>
      </UiField>
      <UiField :label="t('fraud.log.reason')">
        <template #default="{ id }">
          <UiSelect
            :id="id"
            v-model="draft.reason"
            :options="reasonOptions"
            :placeholder="t('shared.any')"
            data-testid="fraud-log-reason"
          />
        </template>
      </UiField>
    </UiListToolbar>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="fraud-log-error"
      @retry="load"
    />

    <UiResultCount :loading="initialLoading" data-testid="fraud-log-count">
      {{ t('shared.totalcountRecordsFound', { totalCount: store.totalCount }) }}
    </UiResultCount>

    <UiCard class="overflow-hidden">
      <UiTable
        :loading="initialLoading"
        :empty="!store.loading && store.entries.length === 0"
        data-testid="fraud-log-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.date') }}</UiTableHeader>
            <UiTableHeader>{{ t('fraud.log.kind') }}</UiTableHeader>
            <UiTableHeader>{{ t('fraud.log.decision') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('fraud.log.score') }}</UiTableHeader>
            <UiTableHeader>{{ t('fraud.log.reasons') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.client') }}</UiTableHeader>
            <UiTableHeader>{{ t('fraud.log.order') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.ipAddress') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.email') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="entry in store.entries" :key="entry.id" hoverable :data-testid="`fraud-log-row-${entry.id}`">
            <UiTableCell muted>{{ formatDateTime(entry.createdAt) }}</UiTableCell>
            <UiTableCell>{{ statusLabel(FRAUD_CHECK_KIND_OPTIONS, entry.kind) }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="fraudDecisionTone(entry.decision)">{{ statusLabel(FRAUD_DECISION_OPTIONS, entry.decision) }}</UiBadge>
            </UiTableCell>
            <UiTableCell numeric>{{ entry.score }}</UiTableCell>
            <UiTableCell>
              <UiStack v-if="entry.reasons.length > 0" direction="row" wrap gap="xs">
                <UiBadge v-for="code in entry.reasons" :key="code" small>{{ statusLabel(FRAUD_REASON_OPTIONS, code) }}</UiBadge>
              </UiStack>
              <UiText v-else tone="muted" size="sm">{{ t('fraud.log.noReasons') }}</UiText>
            </UiTableCell>
            <UiTableCell>
              <UiLink v-if="entry.clientId !== null" :to="clientPath(entry.clientId)" :variant="UiButtonVariant.Link">
                #{{ entry.clientId }}
              </UiLink>
              <template v-else>{{ t('shared.notAvailable') }}</template>
            </UiTableCell>
            <UiTableCell>
              <UiLink v-if="entry.orderId !== null" :to="orderPath(entry.orderId)" :variant="UiButtonVariant.Link">
                #{{ entry.orderId }}
              </UiLink>
              <template v-else>{{ t('shared.notAvailable') }}</template>
            </UiTableCell>
            <UiTableCell class="font-mono">{{ entry.ipAddress ?? t('shared.notAvailable') }}</UiTableCell>
            <UiTableCell>{{ entry.email ?? t('shared.notAvailable') }}</UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          {{ narrowed ? t('fraud.log.noMatches') : t('shared.noRecordsFound') }}
        </template>
      </UiTable>
    </UiCard>

    <UiPagination
      :page="page"
      :total="store.totalCount"
      :page-size="store.pageSize"
      testid="fraud-log-pagination"
      @update:page="setPage"
    />
  </UiStack>
</template>
