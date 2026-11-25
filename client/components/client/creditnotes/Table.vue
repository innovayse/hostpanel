<template>
  <UiTable
    scrollable
    :label="$t('client.creditNotes.title')"
    :columns="7"
    :loading="loading"
    :empty="notes.length === 0"
    :empty-text="$t('client.creditNotes.empty')"
    :empty-description="$t('client.creditNotes.emptyHint')"
    :empty-icon="FileMinus"
    data-testid="credit-notes-table"
  >
    <template #head>
      <UiTableHead>
        <UiTableRow :hoverable="false">
          <UiTableTh>{{ $t('client.creditNotes.colNumber') }}</UiTableTh>
          <UiTableTh>{{ $t('client.creditNotes.colDate') }}</UiTableTh>
          <UiTableTh>{{ $t('client.creditNotes.colInvoice') }}</UiTableTh>
          <UiTableTh>{{ $t('client.creditNotes.colMethod') }}</UiTableTh>
          <UiTableTh align="right">{{ $t('client.creditNotes.colTotal') }}</UiTableTh>
          <UiTableTh align="center">{{ $t('client.creditNotes.colStatus') }}</UiTableTh>
          <UiTableTh actions />
        </UiTableRow>
      </UiTableHead>
    </template>
    <UiTableBody>
      <UiTableRow v-for="note in notes" :key="note.id" :data-testid="`credit-note-row-${note.id}`">
        <UiTableTd>
          <UiText :as="UiTextTag.Span" size="sm" weight="medium" :data-testid="`credit-note-number-${note.id}`">{{ note.number }}</UiText>
        </UiTableTd>
        <UiTableTd>
          <UiText :as="UiTextTag.Span" size="sm" tone="subtle">{{ formatDate(note.issuedAt) }}</UiText>
        </UiTableTd>
        <UiTableTd>
          <UiLink tone="primary" :to="clientInvoicePath(note.invoiceId)" :data-testid="`credit-note-invoice-${note.id}`">#{{ note.invoiceId }}</UiLink>
        </UiTableTd>
        <UiTableTd>
          <UiText :as="UiTextTag.Span" size="sm" :data-testid="`credit-note-method-${note.id}`">{{ $t(`client.creditNotes.method.${note.method}`) }}</UiText>
        </UiTableTd>
        <UiTableTd align="right">
          <UiText :as="UiTextTag.Span" size="sm" weight="semibold" :data-testid="`credit-note-total-${note.id}`">
            {{ formatCurrency(note.total, { code: note.currency }) }}
          </UiText>
        </UiTableTd>
        <UiTableTd align="center">
          <UiBadge :variant="STATUS_TONES[note.status]" size="sm" rounded :data-testid="`credit-note-status-${note.id}`">
            {{ $t(`client.creditNotes.status.${note.status}`) }}
          </UiBadge>
        </UiTableTd>
        <UiTableTd align="right">
          <UiButton
            variant="subtle"
            size="sm"
            :loading="downloadingId === note.id"
            :hint="$t('client.invoices.downloadPdf')"
            :data-testid="`credit-note-download-${note.id}`"
            @click="emit('download', note)"
          >
            {{ $t('common.pdf') }}
          </UiButton>
        </UiTableTd>
      </UiTableRow>
    </UiTableBody>
  </UiTable>
</template>

<script setup lang="ts">
/**
 * Table. The credit notes issued to the client: number, date, the invoice credited, where the value went, the total and the
 * state, with a button that downloads each one as a PDF. Its loading and empty states are the table's own.
 *
 * A credit note is not invoice credit, so nothing here is labelled "credit" alone. The table only formats what the backend sent and
 * asks its parent to download; it holds no state of its own.
 */
import { FileMinus } from 'lucide-vue-next'
import { clientInvoicePath } from '~/constants/clientRoutes'
import { ClientCreditNoteStatus } from '~/types/clientcreditnote'
import type { ClientCreditNote } from '~/types/clientcreditnote'
import { formatCurrency } from '~/utils/formatCurrency'
import { formatDate } from '~/utils/formatDate'
import { UiTextTag, UiTone } from '~/utils/ui/enums'

/** Props of CreditNotesTable. */
interface Props {
  /** The notes to list, newest first as the backend sent them. */
  notes: ClientCreditNote[]
  /** The id of the note whose PDF is being downloaded; null when none is. */
  downloadingId: number | null
  /** Whether the notes are still being read. */
  loading: boolean
}

/** The props of the component. */
defineProps<Props>()

/** The events of the component. */
const emit = defineEmits<{
  /** The client asked for the PDF of a note. */
  download: [note: ClientCreditNote]
}>()

/** The tone of the badge of each state: issued is informational, applied is a success, voided is a warning. */
const STATUS_TONES: Record<ClientCreditNoteStatus, UiTone> = {
  [ClientCreditNoteStatus.Issued]: UiTone.Info,
  [ClientCreditNoteStatus.Applied]: UiTone.Success,
  [ClientCreditNoteStatus.Voided]: UiTone.Warning
}
</script>
