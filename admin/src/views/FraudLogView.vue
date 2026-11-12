<script setup lang="ts">
/**
 * Fraud log: every fraud check the API recorded (registrations and orders) with its decision,
 * risk score and the reasons behind it, filtered by decision and by reason code and paged by the
 * server. Changing a filter reloads the first page.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiFilterCard from '@/components/ui/UiFilterCard.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiLink from '@/components/ui/UiLink.vue'
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
import { FRAUD_CHECK_KIND_OPTIONS, FRAUD_DECISION_OPTIONS, FRAUD_REASON_OPTIONS } from '@/lib/constants'
import { formatDateTime } from '@/lib/format'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { clientPath, orderPath } from '@/lib/routes'
import { fraudDecisionTone, statusLabel } from '@/lib/tones'
import { useFraudLogStore } from '@/stores/fraudLog'
import type { FraudDecision, FraudReasonCode } from '@/types/fraud'
import { UiButtonVariant } from '@/utils/ui/button'

const { t } = useI18n()

/** Fraud log store: the page, the total and the refusal sentence. */
const store = useFraudLogStore()

/** Current page (1-based). */
const page = ref(1)

/** Decision filter; empty means any. */
const decision = ref<FraudDecision | ''>('')

/** Reason filter; empty means any. */
const reason = ref<FraudReasonCode | ''>('')

/** Options of the decision filter: any, then every decision. */
const decisionOptions = computed<{ value: FraudDecision | ''; label: string }[]>(() => [
  { value: '', label: t('shared.any') },
  ...FRAUD_DECISION_OPTIONS,
])

/** Options of the reason filter: any, then every reason code. */
const reasonOptions = computed<{ value: FraudReasonCode | ''; label: string }[]>(() => [
  { value: '', label: t('shared.any') },
  ...FRAUD_REASON_OPTIONS,
])

/**
 * Reads a page of the log with the filters on show.
 *
 * @param next - Page number (1-based).
 * @returns Promise that resolves when the page is read.
 */
const load = async (next: number): Promise<void> => {
  page.value = next
  await store.fetchPage({ decision: decision.value, reason: reason.value }, next, DEFAULT_PAGE_SIZE)
}

watch([decision, reason], () => load(1))

onMounted(() => load(1))
</script>

<template>
  <UiStack data-testid="fraud-log-view">
    <UiPageHeader :title="t('fraud.log.title')" :subtitle="t('fraud.log.subtitle')" />

    <UiFilterCard>
      <UiGrid :columns="2">
        <UiField :label="t('fraud.log.decision')">
          <UiSelect
            v-model="decision"
            :options="decisionOptions"
            :placeholder="t('fraud.log.decision')"
            data-testid="fraud-log-decision"
          />
        </UiField>
        <UiField :label="t('fraud.log.reason')">
          <UiSelect
            v-model="reason"
            :options="reasonOptions"
            :placeholder="t('fraud.log.reason')"
            data-testid="fraud-log-reason"
          />
        </UiField>
      </UiGrid>
    </UiFilterCard>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="fraud-log-error"
      @retry="load(page)"
    />

    <UiText tone="muted" size="md" data-testid="fraud-log-count">
      {{ t('shared.totalcountRecordsFound', { totalCount: store.totalCount }) }}
    </UiText>

    <UiCard class="overflow-hidden">
      <UiTable
        :loading="store.loading && store.entries.length === 0"
        :empty="!store.loading && store.entries.length === 0"
        :colspan="9"
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
          {{ t('shared.noRecordsFound') }}
        </template>
      </UiTable>
    </UiCard>

    <UiPagination
      :page="page"
      :total="store.totalCount"
      :page-size="DEFAULT_PAGE_SIZE"
      testid="fraud-log-pagination"
      @update:page="load"
    />
  </UiStack>
</template>
