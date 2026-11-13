<script setup lang="ts">
/**
 * Pending bank transfers: the invoices whose client chose to pay by manual bank transfer and that wait for a decision.
 *
 * Each row names the invoice, the client, the amount, the reference the client was told to write on the transfer and
 * when they chose it. Staff compare it with the bank statement and either confirm it (the invoice is paid through the
 * normal payment path) or reject it with a note the client reads (the invoice is open again). Both ask first, in the
 * app's own dialog; the outcome shows above the table and the list reloads, so a transfer someone else decided a moment
 * ago leaves it. Reads and writes `stores/bankTransfers`.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useResultBanner } from '@/composables/useResultBanner'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { formatDateTime } from '@/lib/format'
import { formatCurrencyCode } from '@/lib/money'
import { clientPath, invoicePath } from '@/lib/routes'
import { useBankTransfersStore } from '@/stores/bankTransfers'
import type { PendingBankTransfer } from '@/types/pendingbanktransfer'
import { UiTone } from '@/utils/ui/tone'

/** Longest note the API accepts, mirrored here so the field stops typing at the limit. */
const NOTE_MAX_LENGTH = 1000

/** Translation function. */
const { t } = useI18n()

/** Store holding the waiting transfers, their total and the load state. */
const store = useBankTransfersStore()

/** Current page (1-based). */
const page = ref(1)

/** The transfer staged for confirmation, null when no confirm prompt is open. */
const confirmTarget = ref<PendingBankTransfer | null>(null)

/** The transfer staged for rejection, null when no reject prompt is open. */
const rejectTarget = ref<PendingBankTransfer | null>(null)

/** The note typed in the reject prompt. */
const note = ref('')

/** The sentence under the note field when the prompt was confirmed without a note. */
const noteError = ref('')

/** True while a decision is being sent. */
const deciding = ref(false)

/** The outcome line of the last decision. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/** The sentence of the last refused list load, shown with Retry; null when the list loaded. */
const loadError = ref<string | null>(null)

/** Whether the current page has any transfer to list. */
const hasRows = computed<boolean>(() => store.transfers.length > 0)

/**
 * Loads a page of waiting transfers and records whether the load was refused.
 *
 * @param next - Page number (1-based).
 * @returns Promise that resolves when the page has settled.
 */
const load = async (next: number): Promise<void> => {
  page.value = next
  await store.fetchPage(next, DEFAULT_PAGE_SIZE)
  loadError.value = store.error
}

/**
 * Opens the confirm prompt for a transfer.
 *
 * @param transfer - The transfer whose money arrived.
 */
const askConfirm = (transfer: PendingBankTransfer): void => {
  confirmTarget.value = transfer
}

/**
 * Opens the reject prompt for a transfer with an empty note.
 *
 * @param transfer - The transfer whose money did not arrive.
 */
const askReject = (transfer: PendingBankTransfer): void => {
  note.value = ''
  noteError.value = ''
  rejectTarget.value = transfer
}

/** Closes the confirm prompt; ignored while the decision is being sent. */
const dismissConfirm = (): void => {
  if (!deciding.value) {
    confirmTarget.value = null
  }
}

/** Closes the reject prompt; ignored while the decision is being sent. */
const dismissReject = (): void => {
  if (!deciding.value) {
    rejectTarget.value = null
  }
}

/**
 * Shows the outcome of a decision above the table and reloads the list, whichever way it went: a refusal such as "nothing
 * is waiting any more" means the list is stale.
 *
 * @param succeeded - Whether the API accepted the decision.
 * @param successText - The sentence for a success.
 * @returns Promise that resolves when the list has been reloaded.
 */
const settle = async (succeeded: boolean, successText: string): Promise<void> => {
  if (succeeded) {
    feedback.show(successText, false)
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
  await load(page.value)
}

/**
 * Confirms the staged transfer.
 *
 * @returns Promise that resolves when the decision and the reload have finished.
 */
const handleConfirm = async (): Promise<void> => {
  const target = confirmTarget.value
  if (target === null) {
    return
  }
  feedback.clear()
  deciding.value = true
  const confirmed = await store.confirm(target.invoiceId)
  deciding.value = false
  confirmTarget.value = null
  await settle(confirmed, t('billing.bankTransfers.confirmed', { id: target.invoiceId }))
}

/**
 * Rejects the staged transfer with the typed note; an empty note stays in the prompt with a sentence under the field.
 *
 * @returns Promise that resolves when the decision and the reload have finished.
 */
const handleReject = async (): Promise<void> => {
  const target = rejectTarget.value
  if (target === null) {
    return
  }
  if (note.value.trim() === '') {
    noteError.value = t('billing.bankTransfers.noteRequired')
    return
  }
  feedback.clear()
  deciding.value = true
  const rejected = await store.reject(target.invoiceId, note.value.trim())
  deciding.value = false
  rejectTarget.value = null
  await settle(rejected, t('billing.bankTransfers.rejected', { id: target.invoiceId }))
}

/**
 * The row actions of a pending transfer: confirm, and reject last (both confirmed in a dialog first).
 *
 * @param transfer - The row.
 * @returns The action list passed to UiRowActions.
 */
const transferActions = (transfer: PendingBankTransfer): UiMenuAction[] => [
  {
    label: t('billing.bankTransfers.confirm'),
    testid: `billing-bank-transfer-confirm-${transfer.invoiceId}`,
    /**
     * Opens the confirmation dialog for this transfer.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      askConfirm(transfer)
    },
  },
  {
    label: t('billing.bankTransfers.reject'),
    testid: `billing-bank-transfer-reject-${transfer.invoiceId}`,
    danger: true,
    /**
     * Opens the rejection dialog for this transfer.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      askReject(transfer)
    },
  },
]

onMounted(() => load(1))
</script>

<template>
  <UiStack data-testid="billing-bank-transfers-view">
    <UiPageHeader
      :title="t('billing.bankTransfers.heading')"
      :subtitle="t('billing.bankTransfers.subtitle')"
    />

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="billing-bank-transfers-feedback" />

    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="billing-bank-transfers-error"
      @retry="load(page)"
    />

    <UiText v-if="loadError === null" tone="muted" size="md" data-testid="billing-bank-transfers-count">
      {{ t('shared.totalcountRecordsFound', { totalCount: store.totalCount }) }}
    </UiText>

    <UiCard>
      <UiTable
        :loading="store.loading && !hasRows"
        :empty="!store.loading && loadError === null && !hasRows"
        data-testid="billing-bank-transfers-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.invoice') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.client') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('shared.amount') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.bankTransfers.reference') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.bankTransfers.requested') }}</UiTableHeader>
            <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="transfer in store.transfers" :key="transfer.invoiceId" hoverable>
            <UiTableCell>
              <UiLink
                :to="invoicePath(transfer.invoiceId)"
                variant="link"
                :data-testid="`billing-bank-transfer-invoice-${transfer.invoiceId}`"
              >
                #{{ transfer.invoiceId }}
              </UiLink>
            </UiTableCell>
            <UiTableCell>
              <UiLink
                variant="link"
                :to="clientPath(transfer.clientId)"
                :data-testid="`billing-bank-transfer-client-${transfer.invoiceId}`"
              >
                {{ transfer.clientName }}
              </UiLink>
            </UiTableCell>
            <UiTableCell numeric>{{ formatCurrencyCode(transfer.amountDue, transfer.currency) }}</UiTableCell>
            <UiTableCell>{{ transfer.reference }}</UiTableCell>
            <UiTableCell muted>{{ formatDateTime(transfer.requestedAt) }}</UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="transferActions(transfer)" :testid="`billing-bank-transfer-actions-${transfer.invoiceId}`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          {{ t('billing.bankTransfers.empty') }}
        </template>
      </UiTable>
    </UiCard>

    <UiPagination
      :page="page"
      :total="store.totalCount"
      :page-size="DEFAULT_PAGE_SIZE"
      testid="billing-bank-transfers-pagination"
      @update:page="load"
    />

    <UiDialog
      v-if="confirmTarget !== null"
      variant="confirm"
      :title="t('billing.bankTransfers.confirmTitle')"
      :message="t('billing.bankTransfers.confirmMessage', {
        amount: formatCurrencyCode(confirmTarget.amountDue, confirmTarget.currency),
        id: confirmTarget.invoiceId,
        reference: confirmTarget.reference,
      })"
      :confirm-label="t('billing.bankTransfers.confirm')"
      :loading="deciding"
      data-testid="billing-bank-transfers-confirm-dialog"
      @confirm="handleConfirm"
      @close="dismissConfirm"
    />

    <UiDialog
      v-if="rejectTarget !== null"
      variant="confirm"
      :title="t('billing.bankTransfers.rejectTitle')"
      :message="t('billing.bankTransfers.rejectMessage', { id: rejectTarget.invoiceId, reference: rejectTarget.reference })"
      :confirm-label="t('billing.bankTransfers.reject')"
      :loading="deciding"
      :tone="UiTone.Danger"
      data-testid="billing-bank-transfers-reject-dialog"
      @confirm="handleReject"
      @close="dismissReject"
    >
      <UiField
        :label="t('billing.bankTransfers.note')"
        :hint="t('billing.bankTransfers.noteHint')"
        :error="noteError"
        required
        error-testid="billing-bank-transfers-note-error"
      >
        <template #default="{ id, invalid, describedby }">
          <UiTextarea
            :id="id"
            v-model="note"
            rows="3"
            :maxlength="NOTE_MAX_LENGTH"
            :invalid="invalid"
            :aria-describedby="describedby"
            :placeholder="t('billing.bankTransfers.notePlaceholder')"
            data-testid="billing-bank-transfers-note"
          />
        </template>
      </UiField>
    </UiDialog>
  </UiStack>
</template>
