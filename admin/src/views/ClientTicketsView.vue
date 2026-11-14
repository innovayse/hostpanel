<script setup lang="ts">
/**
 * Client tickets list page: displays support tickets with summary cards,
 * search, table, and pagination. Follows the same layout as ClientTransactionsView.
 */
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useClientTicketsStore } from '@/stores/clientTickets'
import { formatDate, EMPTY_VALUE } from '@/lib/format'
import { ticketStatusLabel, ticketStatusTone } from '@/lib/tones'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiSectionHeader from '@/components/ui/UiSectionHeader.vue'
import UiResultCount from '@/components/ui/UiResultCount.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatTile from '@/components/ui/UiStatTile.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiTextSize } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { DEFAULT_PAGE_SIZE, SEARCH_DEBOUNCE_MS } from '@/lib/limits'
import { ClientSection, clientItemPath, clientTicketNewPath } from '@/lib/routes'
import { UiTableDensity } from '@/utils/ui/injection'

const { t } = useI18n()

/** Current route, source of the client id. */
const route = useRoute()

/** Client ticket store, the only caller of the ticket endpoints. */
const store = useClientTicketsStore()

/** True while the first load is in flight and there are no tickets yet; the tiles and the table then draw their placeholders. */
const initialLoading = computed<boolean>(() => store.loading && store.tickets.length === 0)

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Current page (1-based). */
const page = ref(1)

/** Items per page. */
const pageSize = ref(DEFAULT_PAGE_SIZE)

/** Search term for subject filtering. */
const search = ref('')

/** Debounce timer for search input. */
let searchTimer: ReturnType<typeof setTimeout> | undefined

/**
 * Fetches tickets for the current client with current pagination and search.
 *
 * @returns Promise that resolves when data is loaded.
 */
const fetchTickets = async (): Promise<void> => {
  await store.fetchClientTickets(clientId.value, page.value, pageSize.value, search.value || undefined)
}

/**
 * Handles debounced search input.
 *
 * @returns Nothing.
 */
const onSearchInput = (): void => {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    page.value = 1
    fetchTickets()
  }, SEARCH_DEBOUNCE_MS)
}

/**
 * Navigates to a specific page and re-fetches.
 *
 * @param p - Target page number.
 * @returns Nothing.
 */
const goToPage = (p: number): void => {
  page.value = p
  fetchTickets()
}

onMounted(() => {
  fetchTickets()
  store.fetchStats(clientId.value)
})

onBeforeUnmount(() => {
  clearTimeout(searchTimer)
})
</script>

<template>
  <UiStack>
    <UiSectionHeader :level="1" variant="card">
      {{ t('clients.clientTickets.tickets') }}
      <template #actions>
        <UiLink
          :to="clientTicketNewPath(clientId)"
          :variant="UiButtonVariant.Primary"
          data-testid="client-tickets-new"
        >
          {{ t('clients.clientTickets.openNewTicket') }}
        </UiLink>
      </template>
    </UiSectionHeader>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="client-tickets-error"
      @retry="fetchTickets()"
    />

    <template v-else>
      <UiGrid :columns="4">
        <UiStatTile
          :label="t('clients.clientTickets.openedThisMonth')"
          :value="store.stats.openedThisMonth"
          :tone="UiTone.Primary"
          :loading="initialLoading"
        />
        <UiStatTile
          :label="t('clients.clientTickets.openedLastMonth')"
          :value="store.stats.openedLastMonth"
          :tone="UiTone.Info"
          :loading="initialLoading"
        />
        <UiStatTile
          :label="t('clients.clientTickets.openedThisYear')"
          :value="store.stats.openedThisYear"
          :tone="UiTone.Success"
          :loading="initialLoading"
        />
        <UiStatTile
          :label="t('clients.clientTickets.openedLastYear')"
          :value="store.stats.openedLastYear"
          :tone="UiTone.Neutral"
          :loading="initialLoading"
        />
      </UiGrid>

      <UiStack direction="row" :align="UiAlignItems.Center">
        <UiLabel>{{ t('clients.clientTickets.searchSubject') }}</UiLabel>
        <UiInput
          v-model="search"
          type="text"
          :placeholder="t('shared.search')"
          data-testid="client-tickets-search"
          @input="onSearchInput"
        />
      </UiStack>

      <UiCard class="overflow-x-auto">
        <UiTable
          :density="UiTableDensity.Comfortable"
          :loading="initialLoading"
          :empty="!initialLoading && store.tickets.length === 0"
          data-testid="client-tickets-table"
        >
          <UiTableHead>
            <UiTableRow>
              <UiTableHeader>{{ t('clients.clientTickets.dateOpened') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.department') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.subject') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.lastReply') }}</UiTableHeader>
            </UiTableRow>
          </UiTableHead>
          <UiTableBody divided>
            <UiTableRow v-for="ticket in store.tickets" :key="ticket.id" hoverable>
              <UiTableCell muted>{{ formatDate(ticket.createdAt) }}</UiTableCell>
              <UiTableCell muted>{{ ticket.departmentName ?? EMPTY_VALUE }}</UiTableCell>
              <UiTableCell>
                <UiLink
                  :to="clientItemPath(clientId, ClientSection.Tickets, ticket.id)"
                  :variant="UiButtonVariant.Link"
                  :data-testid="`client-tickets-open-${ticket.id}`"
                >
                  {{ ticket.subject }}
                </UiLink>
              </UiTableCell>
              <UiTableCell>
                <UiBadge :tone="ticketStatusTone[ticket.status]" small>{{ ticketStatusLabel(ticket.status) }}</UiBadge>
              </UiTableCell>
              <UiTableCell muted>{{ formatDate(ticket.lastReplyAt) }}</UiTableCell>
            </UiTableRow>
          </UiTableBody>
          <template #empty>
            {{ t('clients.clientTickets.noDataAvailableInTable') }}
          </template>
        </UiTable>
        <UiStack
          v-if="initialLoading || store.totalCount > pageSize"
          direction="row"
          :justify="UiJustify.Between"
          :align="UiAlignItems.Center"
        >
          <UiResultCount :loading="initialLoading" :size="UiTextSize.Sm">
            {{ t('clients.clientTickets.totalcountTicketValue', { totalCount: store.totalCount }) }}
          </UiResultCount>
          <UiPagination
            :page="page"
            :total="store.totalCount"
            :page-size="pageSize"
            :loading="initialLoading"
            testid="client-tickets-pagination"
            @update:page="goToPage"
          />
        </UiStack>
      </UiCard>
    </template>
  </UiStack>
</template>
