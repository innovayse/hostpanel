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
      <UiTableRow v-for="note in notes" :key="note.id" :to="invoicePath(note)" :data-testid="`credit-note-row-${note.id}`">
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
          <UiRowActions :actions="rowActions(note)" :testid="`credit-note-actions-${note.id}`" />
        </UiTableTd>
      </UiTableRow>
    </UiTableBody>
  </UiTable>
</template>

<script setup lang="ts">
/**
 * Table. The credit notes issued to the client: number, date, the invoice credited, where the value went, the total and the
 * state, with a menu that views the credited invoice and downloads or prints each note as a PDF. Its loading and empty states are the table's own.
 *
 * A credit note is not invoice credit, so nothing here is labelled "credit" alone. The table only formats what the backend sent and
 * asks its parent to download or print; it holds no state of its own.
 */
import { Download, Eye, FileMinus, Printer } from 'lucide-vue-next'
import { clientInvoicePath } from '~/constants/clientRoutes'
import { ClientCreditNoteStatus } from '~/types/clientcreditnote'
import type { ClientCreditNote } from '~/types/clientcreditnote'
import type { UiRowAction } from '~/types/uirowaction'
import { formatCurrency } from '~/utils/formatCurrency'
import { formatDate } from '~/utils/formatDate'
import { UiTextTag, UiTone } from '~/utils/ui/enums'

/** Props of CreditNotesTable. */
interface Props {
  /** The notes to list, newest first as the backend sent them. */
  notes: ClientCreditNote[]
  /** Whether the notes are still being read. */
  loading: boolean
}

/** The props of the component. */
defineProps<Props>()

/** Translates a key into the active locale. */
const { t } = useI18n()

/** Prefixes a path with the active locale. */
const localePath = useLocalePath()

/** The events of the component. */
const emit = defineEmits<{
  /** The client asked for the PDF of a note. */
  download: [note: ClientCreditNote]
  /** The client asked to print the PDF of a note. */
  print: [note: ClientCreditNote]
}>()

/**
 * The page a note opens: the invoice it credits, since a credit note has no page of its own.
 *
 * @param note - The note the row shows.
 * @returns The path of the credited invoice, with the active locale's prefix.
 */
const invoicePath = (note: ClientCreditNote): string => localePath(clientInvoicePath(note.invoiceId))

/**
 * The actions of one note row: view the credited invoice, download the PDF or print it.
 *
 * @param note - The note the row shows.
 * @returns The actions of the row's menu, in menu order.
 */
const rowActions = (note: ClientCreditNote): UiRowAction[] => [
  { key: 'view', label: t('common.view'), icon: Eye, to: invoicePath(note) },
  { key: 'download', label: t('client.invoices.downloadPdf'), icon: Download, onSelect: () => emit('download', note) },
  { key: 'print', label: t('common.print'), icon: Printer, onSelect: () => emit('print', note) },
]

/** The tone of the badge of each state: issued is informational, applied is a success, voided is a warning. */
const STATUS_TONES: Record<ClientCreditNoteStatus, UiTone> = {
  [ClientCreditNoteStatus.Issued]: UiTone.Info,
  [ClientCreditNoteStatus.Applied]: UiTone.Success,
  [ClientCreditNoteStatus.Voided]: UiTone.Warning
}
</script>
