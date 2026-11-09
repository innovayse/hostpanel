<script setup lang="ts">
/**
 * Credit notes tab of the invoice screen: what the invoice can still be credited, the notes already issued against it, and the
 * form that issues a new one.
 *
 * A credit note is not an invoice credit: invoice credit is balance spent on an invoice, a credit note reverses part of an
 * invoice that was already paid, as account credit or as a gateway refund. The tab says so, and tells why the button is off for
 * an unpaid invoice, a credit top-up (which is refunded by hand) or an invoice with nothing left to credit. Reads and writes
 * `stores/creditNotes`.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import CreditNoteIssueDialog from '@/components/billing/CreditNoteIssueDialog.vue'
import CreditNoteStatusBadge from '@/components/billing/CreditNoteStatusBadge.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { useResultBanner } from '@/composables/useResultBanner'
import { formatDateTime } from '@/lib/format'
import { formatPlainAmount } from '@/lib/money'
import { creditNotePath } from '@/lib/routes'
import { useBillingStore } from '@/stores/billing'
import { useCreditNotesStore } from '@/stores/creditNotes'
import type { IssueCreditNotePayload } from '@/types/creditnote'
import { InvoiceStatus } from '@/types/invoice'
import type { InvoiceDetail } from '@/types/invoice'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

const props = defineProps<{
  /** The invoice whose credit notes are shown. */
  invoice: InvoiceDetail
  /** True to show the notes without the issue button. */
  readonly?: boolean
}>()

/** Notes are listed, issued and voided here. */
const store = useCreditNotesStore()

/** The invoice store, reloaded after a note so the cap and the status on screen are the new ones. */
const billing = useBillingStore()

/** Page size of the invoice's own notes; an invoice has few. */
const NOTES_PAGE_SIZE = 50

/** True while the issue form is open. */
const issuing = ref(false)

/** True while the issue request is in flight. */
const saving = ref(false)

/** The outcome line of the last issue, shown in the tab. */
const feedback = useResultBanner()

/** True for a credit top-up, which is credit already. */
const isTopUp = computed<boolean>(() => props.invoice.isCreditTopUp)

/** True when the invoice holds paid money a note can reverse. */
const isPaid = computed<boolean>(() => props.invoice.status === InvoiceStatus.Paid || props.invoice.status === InvoiceStatus.PartiallyRefunded)

/** True when the issue button can be used. */
const canIssue = computed<boolean>(() => !props.readonly && !isTopUp.value && isPaid.value && props.invoice.creditableAmount > 0)

/** The sentence that says why no note can be issued, or an empty string when one can. */
const blockedReason = computed<string>(() => {
  if (isTopUp.value) {
    return t('billing.creditNotes.blockedTopUp')
  }
  if (!isPaid.value) {
    return t('billing.creditNotes.blockedNotPaid')
  }
  if (props.invoice.creditableAmount <= 0) {
    return t('billing.creditNotes.blockedNothingLeft')
  }
  return ''
})

/** Loads the notes of this invoice. */
const load = async (): Promise<void> => {
  await store.fetchPage({ invoiceId: props.invoice.id }, 1, NOTES_PAGE_SIZE)
}

/**
 * Issues the note, then reloads the notes and the invoice. A refusal stays in the form with the API's sentence.
 *
 * @param payload - The lines, the method and the reason from the form.
 */
const issue = async (payload: IssueCreditNotePayload): Promise<void> => {
  saving.value = true
  const id = await store.issue(props.invoice.id, payload)
  saving.value = false
  if (id === null) {
    return
  }
  issuing.value = false
  feedback.show(t('billing.creditNotes.issued'), false)
  await Promise.all([load(), billing.fetchById(props.invoice.id)])
}

onMounted(load)
</script>

<template>
  <UiStack data-testid="billing-invoice-credit-notes">
    <UiAlert variant="info" data-testid="billing-invoice-credit-notes-explain">
      {{ t('billing.creditNotes.explain') }}
    </UiAlert>

    <UiStatusMessage :message="feedback.message.value" :tone="feedback.isError.value ? UiTone.Danger : UiTone.Success" data-testid="billing-invoice-credit-notes-feedback" />

    <UiCard>
      <UiStack direction="row" justify="between" align="center">
        <UiText data-testid="billing-invoice-creditable">
          {{ t('billing.creditNotes.creditable', { amount: formatPlainAmount(invoice.creditableAmount, invoice.currency) }) }}
        </UiText>
        <UiButton v-if="!readonly" :disabled="!canIssue" data-testid="billing-invoice-credit-note-issue" @click="issuing = true">
          {{ t('billing.creditNotes.issueButton') }}
        </UiButton>
      </UiStack>
      <UiText v-if="blockedReason" tone="muted" size="sm" data-testid="billing-invoice-credit-note-blocked">{{ blockedReason }}</UiText>
    </UiCard>

    <UiCard>
      <UiTable
        :loading="store.loading && store.notes.length === 0"
        :empty="!store.loading && store.notes.length === 0"
        :colspan="6"
        data-testid="billing-invoice-credit-notes-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('billing.creditNotes.number') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.creditNotes.statusLabel') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.creditNotes.methodLabel') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('billing.creditNotes.total') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.creditNotes.issuedAt') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="note in store.notes" :key="note.id" hoverable>
            <UiTableCell>
              <UiLink :to="creditNotePath(note.id)" variant="link" :data-testid="`billing-invoice-credit-note-${note.id}`">
                {{ note.number ?? `#${note.id}` }}
              </UiLink>
            </UiTableCell>
            <UiTableCell><CreditNoteStatusBadge :status="note.status" /></UiTableCell>
            <UiTableCell>{{ t(`billing.creditNotes.method.${note.method}`) }}</UiTableCell>
            <UiTableCell numeric>{{ formatPlainAmount(note.total, note.currency) }}</UiTableCell>
            <UiTableCell muted>{{ formatDateTime(note.issuedAt) }}</UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          {{ t('billing.creditNotes.emptyForInvoice') }}
        </template>
      </UiTable>
    </UiCard>

    <CreditNoteIssueDialog
      v-if="issuing"
      :invoice="invoice"
      :loading="saving"
      :error="store.error"
      :field-errors="store.fieldErrors"
      @submit="issue"
      @close="issuing = false"
    />
  </UiStack>
</template>
