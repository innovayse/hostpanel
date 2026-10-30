<script setup lang="ts">
/**
 * Summary tab of the invoice screen: who is billed, the dates and amounts, the status, and the
 * actions that move the invoice along (publish, cancel, mark paid). Cancel and mark paid are
 * confirmed first; the outcome of each action (success, or the API's sentence) shows next to the
 * buttons. The line items and the transaction ledgers sit below.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useResultBanner } from '@/composables/useResultBanner'
import InvoiceItemsSection from '@/components/billing/InvoiceItemsSection.vue'
import InvoiceTransactionsSection from '@/components/billing/InvoiceTransactionsSection.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { formatDate } from '@/lib/format'
import { invoiceStatusTone } from '@/lib/tones'
import { formatUsd } from '@/lib/money'
import { useBillingStore } from '@/stores/billing'
import { InvoiceBulkAction, InvoiceStatus } from '@/types/invoice'
import type { InvoiceDetail } from '@/types/invoice'
import { UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { ClientSection, clientSectionPath } from '@/lib/routes'

const { t } = useI18n()

const props = defineProps<{
  /** The invoice to display. */
  invoice: InvoiceDetail
  /** True to hide the editing controls and the action buttons. */
  readonly?: boolean
}>()

/** The actions this tab can run; one runs at a time. */
const SummaryAction = {
  Publish: 'publish',
  Cancel: 'cancel',
  MarkPaid: 'markPaid',
} as const

/** One of the {@link SummaryAction} values. */
type SummaryAction = (typeof SummaryAction)[keyof typeof SummaryAction]

/** One caption/value line of the invoice facts. */
interface FactRow {
  /** Stable key of the row. */
  key: string
  /** Row caption. */
  label: string
  /** Row value. */
  value: string
  /** Tone of the value. */
  tone: UiTextTone
}

const store = useBillingStore()

/** The action currently in flight, null when idle. */
const running = ref<SummaryAction | null>(null)

/** The action waiting for the operator's confirmation, null when no prompt is open. */
const pending = ref<SummaryAction | null>(null)

/** The outcome line of the last action, shown next to the buttons. */
const feedback = useResultBanner()

/** The success sentence of each action. */
const SUCCESS_KEYS: Record<SummaryAction, string> = {
  publish: 'billing.invoiceSummaryTab.published',
  cancel: 'billing.invoiceSummaryTab.cancelled',
  markPaid: 'billing.invoiceSummaryTab.markedPaid',
}

/** Statuses from which the invoice can still be paid. */
const PAYABLE: string[] = [InvoiceStatus.Unpaid, InvoiceStatus.Overdue]

/** Statuses from which the invoice can still be cancelled. */
const CANCELLABLE: string[] = [InvoiceStatus.Draft, InvoiceStatus.Unpaid, InvoiceStatus.Overdue]

/** True when the invoice is a draft the client cannot see yet. */
const isDraft = computed<boolean>(() => props.invoice.status === InvoiceStatus.Draft)

/** True when the invoice can still be paid. */
const canPay = computed<boolean>(() => PAYABLE.includes(props.invoice.status))

/** True when the invoice can still be cancelled. */
const canCancel = computed<boolean>(() => CANCELLABLE.includes(props.invoice.status))

/**
 * What is still owed, exactly as the API reports it. The API owns the rule (the total less the
 * recorded payments, nothing for a Paid, Refunded or Cancelled invoice), so a settlement booked
 * without a payment row of the invoice's own still shows a zero balance here.
 */
const balance = computed<number>(() => props.invoice.amountDue)

/** The invoice facts shown beside the status. */
const facts = computed<FactRow[]>(() => [
  { key: 'invoiceDate', label: t('shared.invoiceDate'), value: formatDate(props.invoice.dates.invoiceDate), tone: UiTextTone.Secondary },
  { key: 'dueDate', label: t('shared.dueDate'), value: formatDate(props.invoice.dates.dueDate), tone: UiTextTone.Secondary },
  { key: 'amount', label: t('billing.invoiceSummaryTab.invoiceAmount'), value: formatUsd(props.invoice.amounts.total ?? 0), tone: UiTextTone.Secondary },
  { key: 'balance', label: t('shared.balance'), value: formatUsd(balance.value), tone: balance.value > 0 ? UiTextTone.Danger : UiTextTone.Success },
])

/** True when the confirmation prompt for cancelling is open. */
const confirmingCancel = computed<boolean>(() => pending.value === SummaryAction.Cancel)

/** True when the confirmation prompt for marking paid is open. */
const confirmingMarkPaid = computed<boolean>(() => pending.value === SummaryAction.MarkPaid)

/**
 * Runs one invoice action and shows its outcome next to the buttons: the success sentence, or
 * the API's own sentence when refused. The store has reloaded the invoice on success.
 *
 * @param action - Which action is running.
 * @param task - The store call; true when the change was accepted.
 */
const run = async (action: SummaryAction, task: () => Promise<boolean>): Promise<void> => {
  feedback.clear()
  running.value = action
  const ok = await task()
  running.value = null
  pending.value = null
  if (ok) {
    feedback.show(t(SUCCESS_KEYS[action]), false)
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
}

/**
 * Opens the confirmation prompt of an action.
 *
 * @param action - The action to confirm.
 */
const ask = (action: SummaryAction): void => {
  pending.value = action
}

/** Closes the confirmation prompt without running anything; ignored while the action runs. */
const dismiss = (): void => {
  if (running.value === null) {
    pending.value = null
  }
}

/**
 * Publishes the draft.
 *
 * @returns Resolves when the request has settled.
 */
const publish = (): Promise<void> => run(SummaryAction.Publish, () => store.publishInvoice(props.invoice.id))

/**
 * Cancels the invoice (after the operator confirmed).
 *
 * @returns Resolves when the request has settled.
 */
const cancel = (): Promise<void> => run(SummaryAction.Cancel, () => store.cancelInvoice(props.invoice.id))

/**
 * Marks the invoice paid by hand and reloads it (after the operator confirmed).
 *
 * @returns Resolves when the request has settled.
 */
const markPaid = (): Promise<void> =>
  run(SummaryAction.MarkPaid, async () => {
    const ok = await store.bulkAction([props.invoice.id], InvoiceBulkAction.MarkPaid)
    if (ok) {
      await store.fetchById(props.invoice.id)
    }
    return ok
  })
</script>

<template>
  <UiStack data-testid="billing-invoice-summary">
    <UiAlert v-if="isDraft && !readonly" variant="info" data-testid="billing-invoice-draft-notice">
      {{ t('billing.invoiceSummaryTab.thisIsADraftInvoice') }}
    </UiAlert>

    <UiCard padding="md">
      <UiGrid :columns="2" gap="lg">
        <UiGrid :columns="2" gap="sm" class="items-center">
          <UiText tone="muted" size="md">{{ t('shared.clientName') }}</UiText>
          <UiStack direction="row" gap="sm" align="center" wrap>
            <UiText tone="primary" size="md" data-testid="billing-invoice-client-name">{{ invoice.clientName }}</UiText>
            <UiLink
              variant="link"
              :to="clientSectionPath(invoice.clientId, ClientSection.Invoices)"
              data-testid="billing-invoice-client-invoices"
            >
              {{ t('billing.invoiceSummaryTab.viewInvoices') }}
            </UiLink>
          </UiStack>
          <template v-for="fact in facts" :key="fact.key">
            <UiText tone="muted" size="md">{{ fact.label }}</UiText>
            <UiText :tone="fact.tone" size="md" :data-testid="`billing-invoice-fact-${fact.key}`">{{ fact.value }}</UiText>
          </template>
        </UiGrid>

        <UiStack align="center" gap="sm">
          <UiBadge :tone="invoiceStatusTone(invoice.status)" data-testid="billing-invoice-status">
            {{ invoice.status }}
          </UiBadge>
          <UiText tone="muted" size="sm">
            {{ t('billing.invoiceSummaryTab.paymentMethod') }} {{ invoice.payment.method || t('shared.none') }}
          </UiText>

          <UiStack v-if="!readonly" direction="row" gap="sm" justify="center" wrap>
            <UiButton
              v-if="isDraft"
              variant="primary"
              :loading="running === SummaryAction.Publish"
              :disabled="running !== null"
              data-testid="billing-invoice-publish"
              @click="publish"
            >
              {{ t('billing.invoiceSummaryTab.publish') }}
            </UiButton>
            <UiButton
              v-if="canCancel"
              variant="secondary"
              :loading="running === SummaryAction.Cancel"
              :disabled="running !== null"
              data-testid="billing-invoice-cancel"
              @click="ask(SummaryAction.Cancel)"
            >
              {{ t('shared.markCancelled') }}
            </UiButton>
            <UiButton
              v-if="canPay"
              variant="primary"
              :loading="running === SummaryAction.MarkPaid"
              :disabled="running !== null"
              data-testid="billing-invoice-mark-paid"
              @click="ask(SummaryAction.MarkPaid)"
            >
              {{ t('billing.invoiceSummaryTab.markPaid') }}
            </UiButton>
          </UiStack>
          <UiAlert
            v-if="feedback.message.value"
            :variant="feedback.isError.value ? UiTone.Danger : UiTone.Success"
            data-testid="billing-invoice-summary-feedback"
          >
            {{ feedback.message.value }}
          </UiAlert>
        </UiStack>
      </UiGrid>
    </UiCard>

    <UiDialog
      :open="confirmingCancel"
      :variant="UiDialogVariant.Confirm"
      :title="t('billing.invoiceSummaryTab.confirmCancelTitle')"
      :message="t('billing.invoiceSummaryTab.confirmCancelMessage', { id: invoice.id })"
      :confirm-label="t('shared.markCancelled')"
      :loading="running === SummaryAction.Cancel"
      :tone="UiTone.Danger"
      @confirm="cancel"
      @close="dismiss"
    />
    <UiDialog
      :open="confirmingMarkPaid"
      :variant="UiDialogVariant.Confirm"
      :title="t('billing.invoiceSummaryTab.confirmMarkPaidTitle')"
      :message="t('billing.invoiceSummaryTab.confirmMarkPaidMessage', { id: invoice.id, amount: formatUsd(balance) })"
      :confirm-label="t('billing.invoiceSummaryTab.markPaid')"
      :loading="running === SummaryAction.MarkPaid"
      :tone="UiTone.Primary"
      @confirm="markPaid"
      @close="dismiss"
    />

    <InvoiceItemsSection :invoice="invoice" :readonly="readonly" />
    <InvoiceTransactionsSection :invoice="invoice" />
  </UiStack>
</template>
