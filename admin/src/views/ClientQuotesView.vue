<script setup lang="ts">
/**
 * Client-scoped quotes list: shows the quotes of a specific client with a search box, the filter dialog (stage and
 * validity) with its removable chips, pagination, delete actions, and navigation to quote detail. The search text, the
 * filters and the page live in the address (`useListQuery`), so a reload, a shared link and the back button restore the
 * same list, and the API narrows it before it pages. A delete is confirmed first and reports its outcome (success, or
 * the API's sentence) above the list; a refused load shows the sentence with a Retry.
 */
import { ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useQuotesStore } from '@/stores/quotes'
import { useListQuery } from '@/composables/useListQuery'
import { QuoteFilterKey, useQuoteFilters } from '@/composables/useQuoteFilters'
import { useResultBanner } from '@/composables/useResultBanner'
import { QUOTE_STAGE_OPTIONS } from '@/lib/constants'
import { formatDate } from '@/lib/format'
import { statusLabel, quoteStageTone } from '@/lib/tones'
import { formatPlainAmount } from '@/lib/money'
import QuoteFilterFields from '@/components/billing/QuoteFilterFields.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiSectionHeader from '@/components/ui/UiSectionHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import schema from '@/views/ClientQuotesView.schema'
import type { QuoteListItem } from '@/types/quote'
import UiText from '@/components/ui/UiText.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { LEDGER_PAGE_SIZE } from '@/lib/limits'
import { quoteAddPath, quotePath } from '@/lib/routes'
import { UiTableDensity } from '@/utils/ui/injection'
import { Icon } from '@/lib/icons'
import { UiActionKind } from '@/utils/ui/actions'

const { t } = useI18n()

/** Current route, source of the client id. */
const route = useRoute()

/** Router, used to open the quote editor. */
const router = useRouter()

/** Quote store, the only caller of the quote endpoints. */
const store = useQuotesStore()

/** The filters, the search text and the page of the list, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, narrowed, loadOnChange, enumOf, chipsOf }
  = useListQuery(Object.values(QuoteFilterKey))

/** The applied stage and validity, and their chips. */
const quoteFilters = useQuoteFilters({ enumOf })

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(quoteFilters.chipSources)

/** True while the first load is in flight and there are no quotes yet; the table then draws its placeholder rows. */
const initialLoading = computed<boolean>(() => store.loading && store.clientQuotes.length === 0)

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Quote ID pending deletion. */
const deleteTargetId = ref<number | null>(null)

/** Whether the delete confirm modal is visible. */
const showDeleteModal = ref(false)

/** True while the delete is in flight. */
const deleting = ref(false)

/** The sentence of the last refused list load, shown with Retry; null when the list loaded. */
const loadError = ref<string | null>(null)

/** The outcome line of the last delete. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/**
 * Loads the page the address names with the filters and search it holds.
 *
 * @returns Promise that resolves when data is loaded.
 */
const load = async (): Promise<void> => {
  await store.fetchClientQuotes(clientId.value, page.value, { search: appliedSearch.value, ...quoteFilters.filters.value })
  loadError.value = store.error
}

// Every change of a filter, the search or the page loads the list the address names.
loadOnChange(load)

/**
 * Opens the delete confirmation modal for a quote.
 *
 * @param id - The quote ID to delete.
 * @returns Nothing.
 */
const confirmDelete = (id: number): void => {
  deleteTargetId.value = id
  showDeleteModal.value = true
}

/**
 * Deletes the targeted quote, shows the outcome and refreshes the list on success.
 *
 * @returns Promise that resolves when deletion completes.
 */
const executeDelete = async (): Promise<void> => {
  if (!deleteTargetId.value) {
    return
  }
  feedback.clear()
  deleting.value = true
  const ok = await store.deleteQuote(deleteTargetId.value)
  deleting.value = false
  showDeleteModal.value = false
  deleteTargetId.value = null
  if (ok) {
    feedback.show(t('clients.clientQuotes.quoteDeleted'), false)
    await load()
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDelete = (): void => {
  if (!deleting.value) {
    showDeleteModal.value = false
    deleteTargetId.value = null
  }
}

/**
 * Opens the create-quote form for this client.
 *
 * @returns Nothing.
 */
const createQuote = (): void => {
  router.push(quoteAddPath(clientId.value))
}

/**
 * Opens a quote for editing.
 *
 * @param id - The quote ID.
 * @returns Nothing.
 */
const editQuote = (id: number): void => {
  router.push(quotePath(id))
}

/**
 * The row actions of a quote: edit, and delete last (confirmed first).
 *
 * @param quote - The row.
 * @returns The action list passed to UiRowActions.
 */
const quoteActions = (quote: QuoteListItem): UiMenuAction[] => [
  {
    label: t('shared.edit'),
    icon: Icon.Edit,
    testid: `client-quotes-edit-${quote.id}`,
    /**
     * Starts editing this row.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      editQuote(quote.id)
    },
  },
  {
    label: t('shared.delete'),
    icon: Icon.Trash,
    testid: `client-quotes-delete-${quote.id}`,
    /**
     * Asks for confirmation, then deletes this row.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      confirmDelete(quote.id)
    },
    danger: true,
  },
]
</script>

<template>
  <UiStack>
    <UiSectionHeader :level="1" variant="card">
      {{ t('shared.quotes') }}
      <template #actions>
        <UiButton :action="UiActionKind.Add"
          type="button"
          :variant="UiButtonVariant.Primary"
          data-testid="client-quotes-create"
          @click="createQuote"
        >
          {{ t('clients.clientQuotes.createNewQuote') }}
        </UiButton>
      </template>
    </UiSectionHeader>

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="client-quotes-feedback" />

    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="client-quotes-error"
      @retry="load()"
    />

    <template v-else>
      <UiListToolbar
        v-model:search="search"
        search-testid="client-quotes-search"
        :search-placeholder="t('billing.quotesList.searchQuotes')"
        filter-testid="client-quotes-filter"
        :active-count="activeCount"
        :chips="chips"
        :loading="initialLoading"
        @apply="apply"
        @reset="reset"
        @cancel="cancel"
        @remove="remove"
        @clear="clearAll"
      >
        <QuoteFilterFields
          v-model:stage="draft.stage"
          v-model:validity="draft.validity"
          testid-prefix="client-quotes-filter"
        />
      </UiListToolbar>

      <UiCard class="overflow-x-auto">
        <UiTable
          :density="UiTableDensity.Comfortable"
          :loading="initialLoading"
          :empty="!initialLoading && store.clientQuotes.length === 0"
          data-testid="client-quotes-table"
        >
          <UiSchemaTableHead :columns="schema.table.columns" />
          <UiTableBody divided>
            <UiTableRow v-for="quote in store.clientQuotes" :key="quote.id" hoverable>
              <UiTableCell muted>#{{ quote.id }}</UiTableCell>
              <UiTableCell>{{ quote.subject }}</UiTableCell>
              <UiTableCell muted>{{ formatDate(quote.dateCreated) }}</UiTableCell>
              <UiTableCell muted>{{ formatDate(quote.validUntil) }}</UiTableCell>
              <UiTableCell numeric>{{ formatPlainAmount(quote.total, quote.currency) }}</UiTableCell>
              <UiTableCell>
                <UiBadge :tone="quoteStageTone(quote.stage)" small>
                  {{ statusLabel(QUOTE_STAGE_OPTIONS, quote.stage) }}
                </UiBadge>
              </UiTableCell>
              <UiTableCell actions>
                <UiRowActions :actions="quoteActions(quote)" :testid="`client-quotes-actions-${quote.id}`" />
              </UiTableCell>
            </UiTableRow>
          </UiTableBody>
          <template #empty>
            <UiEmptyState
              :title="narrowed ? t('clients.clientQuotes.noQuotesMatchYourFilters') : t('clients.clientQuotes.noQuotesFound')"
              data-testid="client-quotes-empty"
            >
              <template v-if="activeCount > 0" #actions>
                <UiButton :variant="UiButtonVariant.Link" data-testid="client-quotes-clear-empty" @click="clearAll">
                  {{ t('clients.clientQuotes.clearFilters') }}
                </UiButton>
              </template>
            </UiEmptyState>
          </template>
        </UiTable>
        <UiStack
          v-if="initialLoading || store.clientQuotesTotal > LEDGER_PAGE_SIZE"
          direction="row"
          :justify="UiJustify.Between"
          :align="UiAlignItems.Center"
        >
          <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
            {{ t('clients.clientQuotes.clientquotestotalQuoteValue', { clientQuotesTotal: store.clientQuotesTotal }) }}
          </UiText>
          <UiPagination
            :page="page"
            :total="store.clientQuotesTotal"
            :page-size="LEDGER_PAGE_SIZE"
            :loading="initialLoading"
            testid="client-quotes-pagination"
            @update:page="setPage"
          />
        </UiStack>
      </UiCard>
    </template>

    <UiDialog
      v-if="showDeleteModal"
      variant="confirm"
      :title="t('clients.clientQuotes.deleteQuote')"
      :message="t('clients.clientQuotes.confirmDeleteQuote', { id: deleteTargetId })"
      :confirm-label="t('shared.delete')"
      :loading="deleting"
      tone="danger"
      @confirm="executeDelete"
      @close="dismissDelete"
    />
  </UiStack>
</template>
