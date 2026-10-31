<script setup lang="ts">
/**
 * Client-scoped quotes list: shows quotes for a specific client with pagination, delete actions,
 * and navigation to quote detail. A delete is confirmed first and reports its outcome (success, or
 * the API's sentence) above the list; a refused load shows the sentence with a Retry.
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useQuotesStore } from '@/stores/quotes'
import { useResultBanner } from '@/composables/useResultBanner'
import { QUOTE_STAGE_OPTIONS } from '@/lib/constants'
import { formatDate } from '@/lib/format'
import { statusLabel, quoteStageTone } from '@/lib/tones'
import { formatPlainAmount } from '@/lib/money'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { LEDGER_PAGE_SIZE } from '@/lib/limits'
import { quoteAddPath, quotePath } from '@/lib/routes'

const { t } = useI18n()

/** Current route, source of the client id. */
const route = useRoute()

/** Router, used to open the quote editor. */
const router = useRouter()

/** Quote store, the only caller of the quote endpoints. */
const store = useQuotesStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Current page (1-based). */
const page = ref(1)

/** Items per page. */
const pageSize = ref(LEDGER_PAGE_SIZE)

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
 * Fetches client quotes with current pagination.
 *
 * @returns Promise that resolves when data is loaded.
 */
const fetchQuotes = async (): Promise<void> => {
  await store.fetchClientQuotes(clientId.value, page.value, pageSize.value)
  loadError.value = store.error
}

/**
 * Navigates to a specific page.
 *
 * @param p - Target page number.
 * @returns Nothing.
 */
const goToPage = (p: number): void => {
  page.value = p
  fetchQuotes()
}

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
    await fetchQuotes()
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

/** Loads the first page. */
onMounted(() => fetchQuotes())
</script>

<template>
  <UiStack>
    <UiStack direction="row" :justify="UiJustify.Between" :align="UiAlignItems.Center">
      <UiHeading :level="1" variant="card">{{ t('shared.quotes') }}</UiHeading>
      <UiButton
        type="button"
        :variant="UiButtonVariant.Primary"
        data-testid="client-quotes-create"
        @click="createQuote"
      >
        {{ t('clients.clientQuotes.createNewQuote') }}
      </UiButton>
    </UiStack>

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="client-quotes-feedback" />

    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="client-quotes-error"
      @retry="fetchQuotes()"
    />

    <UiStack v-if="store.loading && store.clientQuotes.length === 0" data-testid="client-quotes-loading">
      <UiSkeleton :lines="3" />
      <UiText :tone="UiTextTone.Secondary">{{ t('clients.clientQuotes.loadingQuotes') }}</UiText>
    </UiStack>

    <UiCard v-else class="overflow-x-auto">
      <UiTable
        density="comfortable"
        :empty="store.clientQuotes.length === 0"
        :colspan="7"
        data-testid="client-quotes-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.id') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.subject') }}</UiTableHeader>
            <UiTableHeader>{{ t('clients.clientQuotes.createDate') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.validUntil') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.total') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.stage') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="quote in store.clientQuotes" :key="quote.id" hoverable>
            <UiTableCell muted>#{{ quote.id }}</UiTableCell>
            <UiTableCell>{{ quote.subject }}</UiTableCell>
            <UiTableCell muted>{{ formatDate(quote.dateCreated) }}</UiTableCell>
            <UiTableCell muted>{{ formatDate(quote.validUntil) }}</UiTableCell>
            <UiTableCell>{{ formatPlainAmount(quote.total, quote.currency) }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="quoteStageTone(quote.stage)" small>
                {{ statusLabel(QUOTE_STAGE_OPTIONS, quote.stage) }}
              </UiBadge>
            </UiTableCell>
            <UiTableCell>
              <UiStack direction="row" gap="sm" :align="UiAlignItems.Center">
                <UiButton
                  type="button"
                  :variant="UiButtonVariant.Secondary"
                  :size="UiSize.Sm"
                  :data-testid="`client-quotes-edit-${quote.id}`"
                  @click="editQuote(quote.id)"
                >
                  {{ t('shared.edit') }}
                </UiButton>
                <UiButton
                  type="button"
                  :variant="UiButtonVariant.Danger"
                  :size="UiSize.Sm"
                  :data-testid="`client-quotes-delete-${quote.id}`"
                  @click="confirmDelete(quote.id)"
                >
                  {{ t('shared.delete') }}
                </UiButton>
              </UiStack>
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          {{ t('clients.clientQuotes.noQuotesFound') }}
        </template>
      </UiTable>
      <UiStack
        v-if="store.clientQuotesTotal > pageSize"
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
          :page-size="pageSize"
          testid="client-quotes-pagination"
          @update:page="goToPage"
        />
      </UiStack>
    </UiCard>

    <UiDialog
      v-if="showDeleteModal"
      variant="confirm"
      :title="t('clients.clientQuotes.deleteQuote')"
      :message="t('clients.clientQuotes.confirmDeleteQuote', { id: deleteTargetId })"
      :confirm-label="t('shared.delete')"
      :loading-label="t('shared.deleting')"
      :loading="deleting"
      tone="danger"
      @confirm="executeDelete"
      @close="dismissDelete"
    />
  </UiStack>
</template>
