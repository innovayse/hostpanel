<script setup lang="ts">
/**
 * Credit notes: every credit note issued against a paid invoice, newest first, with a status filter.
 *
 * A credit note is not an invoice credit: it is a numbered document that reverses part of an invoice that was already paid, as
 * account credit or as a gateway refund. Each row names the note, its invoice and client, how its value went back and its
 * total, and opens the note's page. Notes are issued from the Credit notes tab of the invoice. Reads `stores/creditNotes`.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import CreditNoteStatusBadge from '@/components/billing/CreditNoteStatusBadge.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
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
import { formatDateTime } from '@/lib/format'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { formatPlainAmount } from '@/lib/money'
import { clientPath, creditNotePath, invoicePath } from '@/lib/routes'
import { useCreditNotesStore } from '@/stores/creditNotes'
import { CreditNoteStatus } from '@/types/creditnote'

const { t } = useI18n()

/** Store holding the notes of the current page and the load state. */
const store = useCreditNotesStore()

/** Current page (1-based). */
const page = ref(1)

/** Value of the status filter: a status name, or an empty string for every status. */
const statusFilter = ref<CreditNoteStatus | ''>('')

/** The sentence of the last refused load, shown with Retry; null when the list loaded. */
const loadError = ref<string | null>(null)

/** Options of the status filter. */
const statusOptions = computed(() => [
  { value: '' as CreditNoteStatus | '', label: t('billing.creditNotes.allStatuses') },
  ...Object.values(CreditNoteStatus).map((status) => ({ value: status as CreditNoteStatus | '', label: t(`billing.creditNotes.status.${status}`) })),
])

/**
 * Loads a page of notes for the chosen status.
 *
 * @param next - Page number (1-based).
 * @returns Promise that resolves when the page has settled.
 */
const load = async (next: number): Promise<void> => {
  page.value = next
  await store.fetchPage(statusFilter.value === '' ? {} : { status: statusFilter.value }, next, DEFAULT_PAGE_SIZE)
  loadError.value = store.error
}

/**
 * Reloads from the first page when the filter changes.
 *
 * @returns Promise that resolves when the first page has settled.
 */
const applyFilter = (): Promise<void> => load(1)

onMounted(() => load(1))
</script>

<template>
  <UiStack data-testid="billing-credit-notes-view">
    <UiPageHeader :title="t('billing.creditNotes.heading')" :subtitle="t('billing.creditNotes.subtitle')" />

    <UiAlert variant="info" data-testid="billing-credit-notes-explain">{{ t('billing.creditNotes.explain') }}</UiAlert>

    <UiErrorState v-if="loadError !== null" :message="loadError" :loading="store.loading" testid="billing-credit-notes-error" @retry="load(page)" />

    <UiCard>
      <UiField :label="t('billing.creditNotes.statusFilter')">
        <template #default="{ id }">
          <UiSelect
            :id="id"
            v-model="statusFilter"
            :options="statusOptions"
            :placeholder="t('billing.creditNotes.statusFilterPlaceholder')"
            data-testid="billing-credit-notes-status-filter"
            @update:model-value="applyFilter"
          />
        </template>
      </UiField>
    </UiCard>

    <UiText v-if="loadError === null" tone="muted" size="md" data-testid="billing-credit-notes-count">
      {{ t('shared.totalcountRecordsFound', { totalCount: store.totalCount }) }}
    </UiText>

    <UiCard>
      <UiTable
        :loading="store.loading && store.notes.length === 0"
        :empty="!store.loading && loadError === null && store.notes.length === 0"
        data-testid="billing-credit-notes-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('billing.creditNotes.number') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.creditNotes.invoice') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.creditNotes.client') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.creditNotes.statusLabel') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.creditNotes.methodLabel') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('billing.creditNotes.total') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.creditNotes.issuedAt') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="note in store.notes" :key="note.id" hoverable>
            <UiTableCell>
              <UiLink :to="creditNotePath(note.id)" variant="link" :data-testid="`billing-credit-note-${note.id}`">
                {{ note.number ?? `#${note.id}` }}
              </UiLink>
            </UiTableCell>
            <UiTableCell>
              <UiLink :to="invoicePath(note.invoiceId)" variant="link">#{{ note.invoiceId }}</UiLink>
            </UiTableCell>
            <UiTableCell>
              <UiLink :to="clientPath(note.clientId)" variant="link">{{ note.clientName }}</UiLink>
            </UiTableCell>
            <UiTableCell><CreditNoteStatusBadge :status="note.status" /></UiTableCell>
            <UiTableCell>{{ t(`billing.creditNotes.method.${note.method}`) }}</UiTableCell>
            <UiTableCell numeric>{{ formatPlainAmount(note.total, note.currency) }}</UiTableCell>
            <UiTableCell muted>{{ formatDateTime(note.issuedAt) }}</UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          {{ t('billing.creditNotes.empty') }}
        </template>
      </UiTable>
    </UiCard>

    <UiPagination
      :page="page"
      :total="store.totalCount"
      :page-size="DEFAULT_PAGE_SIZE"
      testid="billing-credit-notes-pagination"
      @update:page="load"
    />
  </UiStack>
</template>
