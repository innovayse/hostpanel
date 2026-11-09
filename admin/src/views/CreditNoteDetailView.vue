<script setup lang="ts">
/**
 * One credit note: its number, state, invoice, client and reason, its lines and totals, the PDF download and, for a note that
 * stands, the button that voids it.
 *
 * Voiding asks first, in the app's own dialog, for a reason. A note paid out as a gateway refund cannot be voided, because the
 * refund cannot be taken back, so its button is replaced by that sentence. A voided note keeps its number and stays on record. The
 * outcome of an action shows above the note. Reads and writes `stores/creditNotes`.
 */
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import CreditNoteStatusBadge from '@/components/billing/CreditNoteStatusBadge.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
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
import { useResultBanner } from '@/composables/useResultBanner'
import { formatDateTime } from '@/lib/format'
import { formatPlainAmount } from '@/lib/money'
import { clientPath, invoicePath } from '@/lib/routes'
import { useCreditNotesStore } from '@/stores/creditNotes'
import { CreditNoteMethod, CreditNoteStatus } from '@/types/creditnote'
import { UiButtonVariant } from '@/utils/ui/button'
import { saveBlob } from '@/utils/saveBlob'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

/** Current route, source of the credit note id. */
const route = useRoute()

/** Store holding the note on screen. */
const store = useCreditNotesStore()

/** Longest reason the API accepts, mirrored here so the field stops typing at the limit. */
const REASON_MAX_LENGTH = 500

/** Credit note id from the route. */
const noteId = computed<string>(() => route.params.id as string)

/** The note on screen; null until loaded. */
const note = computed(() => store.detail?.note ?? null)

/** The lines on screen. */
const items = computed(() => store.detail?.items ?? [])

/** The sentence of the last refused load, shown with Retry; null when the note loaded. */
const loadError = ref<string | null>(null)

/** The outcome line of the last action. */
const feedback = useResultBanner()

/** Sentence of a failed PDF download, shown beside its button; null while there is none. */
const pdfError = ref<string | null>(null)

/** True while the void prompt is open. */
const voiding = ref(false)

/** The reason typed in the void prompt. */
const voidReason = ref('')

/** Sentence under the reason field when the prompt was confirmed without one. */
const voidReasonError = ref('')

/** True while the void request is in flight. */
const saving = ref(false)

/** True when a gateway refund note was paid out, which can never be voided. */
const refundPaidOut = computed<boolean>(() => note.value?.method === CreditNoteMethod.GatewayRefund && note.value.status === CreditNoteStatus.Applied)

/** True when the note stands and can be voided. */
const canVoid = computed<boolean>(() => note.value !== null && !refundPaidOut.value && (note.value.status === CreditNoteStatus.Issued || note.value.status === CreditNoteStatus.Applied))

/** Loads the note and records whether the load was refused. */
const load = async (): Promise<void> => {
  await store.fetchDetail(noteId.value)
  loadError.value = store.error
}

/** Downloads the note's PDF and saves it; a refusal is shown beside the button. */
const downloadPdf = async (): Promise<void> => {
  pdfError.value = null
  const file = await store.downloadPdf(noteId.value)
  if (file === null) {
    pdfError.value = store.error
    return
  }
  saveBlob(file.blob, file.fileName ?? `credit-note-${noteId.value}.pdf`)
}

/** Opens the void prompt with an empty reason. */
const askVoid = (): void => {
  voidReason.value = ''
  voidReasonError.value = ''
  voiding.value = true
}

/** Closes the void prompt; ignored while the request is in flight. */
const dismissVoid = (): void => {
  if (!saving.value) {
    voiding.value = false
  }
}

/** Voids the note with the typed reason; an empty reason stays in the prompt with a sentence under the field. */
const confirmVoid = async (): Promise<void> => {
  if (voidReason.value.trim() === '') {
    voidReasonError.value = t('billing.creditNotes.voidReasonRequired')
    return
  }
  feedback.clear()
  saving.value = true
  const voided = await store.voidNote(noteId.value, voidReason.value.trim())
  saving.value = false
  voiding.value = false
  feedback.show(voided ? t('billing.creditNotes.voided') : (store.error ?? t('errors.noAnswer')), !voided)
}

onMounted(load)
</script>

<template>
  <UiStack data-testid="billing-credit-note-view">
    <UiErrorState v-if="loadError !== null" :message="loadError" :loading="store.loading" testid="billing-credit-note-error" @retry="load" />

    <template v-if="note !== null">
      <UiPageHeader :title="t('billing.creditNotes.detailTitle', { number: note.number ?? `#${note.id}` })" :subtitle="note.reason">
        <template #actions>
          <UiButton :variant="UiButtonVariant.Secondary" data-testid="billing-credit-note-download" @click="downloadPdf">
            {{ t('billing.creditNotes.downloadPdf') }}
          </UiButton>
          <UiButton v-if="canVoid" :variant="UiButtonVariant.Danger" data-testid="billing-credit-note-void" @click="askVoid">
            {{ t('billing.creditNotes.voidButton') }}
          </UiButton>
        </template>
      </UiPageHeader>

      <UiStatusMessage :message="pdfError" :tone="UiTone.Danger" data-testid="billing-credit-note-download-feedback" />
      <UiStatusMessage :message="feedback.message.value" :tone="feedback.isError.value ? UiTone.Danger : UiTone.Success" data-testid="billing-credit-note-feedback" />
      <UiAlert v-if="refundPaidOut" variant="info" data-testid="billing-credit-note-refund-blocked">
        {{ t('billing.creditNotes.voidRefundBlocked') }}
      </UiAlert>

      <UiCard>
        <UiStack gap="sm">
          <UiText data-testid="billing-credit-note-status"><CreditNoteStatusBadge :status="note.status" /></UiText>
          <UiText>
            {{ t('billing.creditNotes.invoice') }}:
            <UiLink :to="invoicePath(note.invoiceId)" variant="link" data-testid="billing-credit-note-invoice">#{{ note.invoiceId }}</UiLink>
          </UiText>
          <UiText>
            {{ t('billing.creditNotes.client') }}:
            <UiLink :to="clientPath(note.clientId)" variant="link">{{ note.clientName }}</UiLink>
          </UiText>
          <UiText>{{ t('billing.creditNotes.methodLabel') }}: {{ t(`billing.creditNotes.method.${note.method}`) }}</UiText>
          <UiText v-if="note.refundReference">{{ t('billing.creditNotes.refundReference') }}: {{ note.refundReference }}</UiText>
          <UiText data-testid="billing-credit-note-reason">{{ t('billing.creditNotes.reason') }}: {{ note.reason }}</UiText>
          <UiText>{{ t('billing.creditNotes.issuedAt') }}: {{ formatDateTime(note.issuedAt) }}</UiText>
          <UiText v-if="note.voidedAt" data-testid="billing-credit-note-voided">
            {{ t('billing.creditNotes.voidedAt') }}: {{ formatDateTime(note.voidedAt) }}
            <template v-if="note.voidReason">, {{ t('billing.creditNotes.voidReason') }}: {{ note.voidReason }}</template>
          </UiText>
        </UiStack>
      </UiCard>

      <UiCard>
        <UiTable :empty="items.length === 0" :colspan="4" data-testid="billing-credit-note-items">
          <UiTableHead>
            <UiTableRow>
              <UiTableHeader>{{ t('billing.creditNotes.description') }}</UiTableHeader>
              <UiTableHeader numeric>{{ t('billing.creditNotes.quantity') }}</UiTableHeader>
              <UiTableHeader numeric>{{ t('billing.creditNotes.unitPrice') }}</UiTableHeader>
              <UiTableHeader numeric>{{ t('billing.creditNotes.amount') }}</UiTableHeader>
            </UiTableRow>
          </UiTableHead>
          <UiTableBody divided>
            <UiTableRow v-for="item in items" :key="item.id">
              <UiTableCell>{{ item.description }}</UiTableCell>
              <UiTableCell numeric>{{ item.quantity }}</UiTableCell>
              <UiTableCell numeric>{{ formatPlainAmount(item.unitPrice, note.currency) }}</UiTableCell>
              <UiTableCell numeric>{{ formatPlainAmount(item.amount, note.currency) }}</UiTableCell>
            </UiTableRow>
          </UiTableBody>
          <template #empty>
            {{ t('billing.creditNotes.empty') }}
          </template>
        </UiTable>
        <UiStack gap="xs" data-testid="billing-credit-note-totals">
          <UiText>{{ t('billing.creditNotes.subTotal') }}: {{ formatPlainAmount(note.subTotal, note.currency) }}</UiText>
          <UiText>{{ t('billing.creditNotes.taxAt', { rate: note.taxRate }) }}: {{ formatPlainAmount(note.tax, note.currency) }}</UiText>
          <UiText tone="primary" data-testid="billing-credit-note-total">
            {{ t('billing.creditNotes.total') }}: {{ formatPlainAmount(note.total, note.currency) }}
          </UiText>
        </UiStack>
      </UiCard>
    </template>

    <UiDialog
      v-if="voiding && note !== null"
      variant="confirm"
      :title="t('billing.creditNotes.voidTitle')"
      :message="t('billing.creditNotes.voidMessage', { number: note.number ?? `#${note.id}` })"
      :confirm-label="t('billing.creditNotes.voidButton')"
      :loading="saving"
      :tone="UiTone.Danger"
      data-testid="billing-credit-note-void-dialog"
      @confirm="confirmVoid"
      @close="dismissVoid"
    >
      <UiField
        :label="t('billing.creditNotes.voidReasonLabel')"
        :error="voidReasonError"
        required
        error-testid="billing-credit-note-void-reason-error"
      >
        <template #default="{ id, invalid, describedby }">
          <UiTextarea
            :id="id"
            v-model="voidReason"
            rows="3"
            :maxlength="REASON_MAX_LENGTH"
            :invalid="invalid"
            :aria-describedby="describedby"
            :placeholder="t('billing.creditNotes.voidReasonPlaceholder')"
            data-testid="billing-credit-note-void-reason"
          />
        </template>
      </UiField>
    </UiDialog>
  </UiStack>
</template>
