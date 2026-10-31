<script setup lang="ts">
/**
 * Fulfilment tab of the invoice screen: what the fulfilment plugins did, or still have to do, for
 * the invoice, oldest first. Each row names the plugin, what it does, where it stands, how often
 * it was tried, the last error (for staff) and the next attempt or completion time. A record the
 * API allows to retry carries a Retry button (hidden while the invoice is read-only), confirmed
 * first; the outcome (success, or the API's own sentence) shows in the tab. The list loads when
 * the tab opens and when the invoice changes; a failed load shows the API's sentence with Retry
 * instead of an empty list.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { useResultBanner } from '@/composables/useResultBanner'
import { formatDateTime } from '@/lib/format'
import { fulfilmentStatusTone } from '@/lib/tones'
import { useBillingStore } from '@/stores/billing'
import { FulfilmentKind, FulfilmentStatus } from '@/types/invoice'
import type { FulfilmentRecord, InvoiceDetail } from '@/types/invoice'
import { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTableDensity } from '@/utils/ui/injection'
import { UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

/** Props of the tab. */
const props = defineProps<{
  /** The invoice whose fulfilment records are listed. */
  invoice: InvoiceDetail
  /** True to list the records without the Retry buttons (the read-only view of the invoice). */
  readonly?: boolean
}>()

/** The invoices store: owns the records, the load state and the retry. */
const store = useBillingStore()

/** The record whose retry the operator is asked to confirm; null while no prompt is open. */
const pending = ref<FulfilmentRecord | null>(null)

/** True while a retry is being sent. */
const retrying = ref(false)

/** The outcome line of the last retry, shown in the tab. */
const feedback = useResultBanner()

/** The records of this invoice; a list left over from another invoice is never shown. */
const records = computed<FulfilmentRecord[]>(() =>
  store.fulfilment.filter((record) => record.invoiceId === props.invoice.id),
)

/** True while the first load of this invoice's records is running, so the table shows a skeleton. */
const loading = computed<boolean>(() => store.fulfilmentLoading && records.value.length === 0)

/** The sentence of a failed load, null when the load worked. */
const loadError = computed<string | null>(() => store.fulfilmentError)

/** The sentence a refused retry is reported with; the store keeps it apart from the page error. */
const retryError = computed<string | null>(() => store.fulfilmentRetryError)

/** Labels of the record statuses. */
const statusLabels = computed<Record<FulfilmentStatus, string>>(() => ({
  [FulfilmentStatus.Pending]: t('billing.invoiceFulfilmentTab.statusPending'),
  [FulfilmentStatus.Processing]: t('billing.invoiceFulfilmentTab.statusProcessing'),
  [FulfilmentStatus.Done]: t('billing.invoiceFulfilmentTab.statusDone'),
  [FulfilmentStatus.Failed]: t('billing.invoiceFulfilmentTab.statusFailed'),
}))

/** Labels of the record kinds. */
const kindLabels = computed<Record<FulfilmentKind, string>>(() => ({
  [FulfilmentKind.Paid]: t('billing.invoiceFulfilmentTab.kindPaid'),
  [FulfilmentKind.Refunded]: t('billing.invoiceFulfilmentTab.kindRefunded'),
  [FulfilmentKind.Lapsed]: t('billing.invoiceFulfilmentTab.kindLapsed'),
}))

/** The confirmation sentence, naming the plugin whose record is retried. */
const confirmMessage = computed<string>(() =>
  t('billing.invoiceFulfilmentTab.confirmRetryMessage', { plugin: pending.value?.pluginId ?? '' }),
)

/**
 * Loads the records of the open invoice.
 *
 * @returns Resolves when the request has settled.
 */
const load = (): Promise<void> => store.fetchFulfilment(props.invoice.id)

/**
 * Gives the time that matters for a record: when a Done record finished, when a Pending or
 * Processing record is next attempted. A Failed record is final until staff retry it, so it has
 * no time.
 *
 * @param record - The record.
 * @returns The formatted time, or a dash when the status has none.
 */
const whenOf = (record: FulfilmentRecord): string => {
  switch (record.status) {
    case FulfilmentStatus.Done:
      return formatDateTime(record.completedAt)
    case FulfilmentStatus.Pending:
    case FulfilmentStatus.Processing:
      return formatDateTime(record.nextAttemptAt)
    case FulfilmentStatus.Failed:
      return formatDateTime(null)
  }
}

/**
 * Opens the confirmation prompt of a retry.
 *
 * @param record - The record to retry.
 */
const askRetry = (record: FulfilmentRecord): void => {
  pending.value = record
}

/** Closes the confirmation prompt; ignored while the retry is being sent. */
const dismiss = (): void => {
  if (!retrying.value) {
    pending.value = null
  }
}

/**
 * Sends the confirmed retry and shows the outcome in the tab.
 *
 * @returns Resolves when the request has settled.
 */
const retry = async (): Promise<void> => {
  /** The record the operator confirmed. */
  const record = pending.value
  if (record === null) {
    return
  }
  feedback.clear()
  retrying.value = true
  /** The invoice the retry was asked for. */
  const invoiceId = props.invoice.id
  /** True when the API accepted the retry. */
  const ok = await store.retryFulfilment(invoiceId, record.id)
  retrying.value = false
  pending.value = null
  // The outcome belongs to the invoice it was asked for; another one is open now.
  if (props.invoice.id !== invoiceId) {
    return
  }
  if (!ok) {
    feedback.show(retryError.value ?? t('errors.noAnswer'), true)
    return
  }
  feedback.show(t('billing.invoiceFulfilmentTab.retried'), false)
}

onMounted(load)
watch(
  () => props.invoice.id,
  () => {
    feedback.clear()
    pending.value = null
    void load()
  },
)
</script>

<template>
  <UiStack data-testid="billing-invoice-fulfilment">
    <UiCard padding="md">
      <UiStack gap="sm">
        <UiHeading :level="3" variant="card">
          {{ t('billing.invoiceFulfilmentTab.title') }}
        </UiHeading>

        <UiAlert
          v-if="feedback.message.value"
          :variant="feedback.isError.value ? UiTone.Danger : UiTone.Success"
          data-testid="billing-invoice-fulfilment-feedback"
        >
          {{ feedback.message.value }}
        </UiAlert>

        <UiErrorState
          v-if="loadError !== null"
          :message="loadError"
          :loading="store.fulfilmentLoading"
          testid="billing-invoice-fulfilment-error"
          @retry="load"
        />

        <UiTable
          v-else
          :density="UiTableDensity.Compact"
          :loading="loading"
          :empty="!loading && records.length === 0"
          :colspan="7"
          data-testid="billing-invoice-fulfilment-table"
        >
          <UiTableHead>
            <UiTableRow>
              <UiTableHeader>{{ t('billing.invoiceFulfilmentTab.plugin') }}</UiTableHeader>
              <UiTableHeader>{{ t('billing.invoiceFulfilmentTab.kind') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
              <UiTableHeader>{{ t('billing.invoiceFulfilmentTab.attempts') }}</UiTableHeader>
              <UiTableHeader>{{ t('billing.invoiceFulfilmentTab.lastError') }}</UiTableHeader>
              <UiTableHeader>{{ t('billing.invoiceFulfilmentTab.when') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.actions') }}</UiTableHeader>
            </UiTableRow>
          </UiTableHead>
          <UiTableBody divided>
            <UiTableRow v-for="record in records" :key="record.id" hoverable>
              <UiTableCell>{{ record.pluginId }}</UiTableCell>
              <UiTableCell muted>{{ kindLabels[record.kind] ?? record.kind }}</UiTableCell>
              <UiTableCell>
                <UiBadge :tone="fulfilmentStatusTone(record.status)">
                  {{ statusLabels[record.status] ?? record.status }}
                </UiBadge>
              </UiTableCell>
              <UiTableCell muted>{{ record.attempts }}</UiTableCell>
              <UiTableCell muted>
                <UiText v-if="record.lastError" :tone="UiTextTone.Danger">{{ record.lastError }}</UiText>
                <template v-else>—</template>
              </UiTableCell>
              <UiTableCell muted>{{ whenOf(record) }}</UiTableCell>
              <UiTableCell>
                <UiButton
                  v-if="record.canRetry && !readonly"
                  :type="UiButtonType.Button"
                  :variant="UiButtonVariant.Secondary"
                  :data-testid="`billing-invoice-fulfilment-retry-${record.id}`"
                  @click="askRetry(record)"
                >
                  {{ t('billing.invoiceFulfilmentTab.retry') }}
                </UiButton>
              </UiTableCell>
            </UiTableRow>
          </UiTableBody>
          <template #empty>
            <UiText data-testid="billing-invoice-fulfilment-empty">{{ t('billing.invoiceFulfilmentTab.empty') }}</UiText>
          </template>
        </UiTable>
      </UiStack>
    </UiCard>

    <UiDialog
      :open="pending !== null"
      :variant="UiDialogVariant.Confirm"
      :title="t('billing.invoiceFulfilmentTab.confirmRetryTitle')"
      :message="confirmMessage"
      :confirm-label="t('billing.invoiceFulfilmentTab.retry')"
      :loading="retrying"
      @confirm="retry"
      @close="dismiss"
    />
  </UiStack>
</template>
