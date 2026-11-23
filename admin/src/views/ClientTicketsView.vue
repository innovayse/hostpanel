<script setup lang="ts">
/**
 * Client tickets list page: summary tiles, a search box, the filter dialog (status, priority and department) with its
 * removable chips, a table and pagination.
 *
 * The search text, the filters and the page live in the address (`useListQuery`), so a reload, a shared link and the
 * back button restore the same list, and the API applies them before it pages. Follows the same layout as
 * ClientTransactionsView. A refused load offers a retry.
 */
import { computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useClientTicketsStore } from '@/stores/clientTickets'
import { useListQuery } from '@/composables/useListQuery'
import { TicketFilterKey, useTicketFilters } from '@/composables/useTicketFilters'
import { formatDate, EMPTY_VALUE } from '@/lib/format'
import { ticketStatusLabel, ticketStatusTone } from '@/lib/tones'
import SupportTicketFilterFields from '@/components/support/SupportTicketFilterFields.vue'
import SupportTicketPriorityBadge from '@/components/support/SupportTicketPriorityBadge.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiSectionHeader from '@/components/ui/UiSectionHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatRow from '@/components/ui/UiStatRow.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import schema, { CLIENT_TICKETS_STATS } from '@/views/ClientTicketsView.schema'
import { UiButtonVariant } from '@/utils/ui/button'
import type { UiStatFigure } from '@/utils/ui/statRow'
import { UiHeadingVariant } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { ClientSection, clientItemPath, clientTicketNewPath } from '@/lib/routes'
import { UiTableDensity } from '@/utils/ui/injection'
import { UiActionKind } from '@/utils/ui/actions'

const { t } = useI18n()

/** Current route, source of the client id. */
const route = useRoute()

/** Client ticket store, the only caller of the ticket endpoints. */
const store = useClientTicketsStore()

/** The figure and colour of each opened-tickets tile by its key; the captions come from the schema. */
const figures = computed<Record<string, UiStatFigure>>(() => ({
  openedThisMonth: { value: store.stats.openedThisMonth, tone: UiTone.Primary },
  openedLastMonth: { value: store.stats.openedLastMonth, tone: UiTone.Info },
  openedThisYear: { value: store.stats.openedThisYear, tone: UiTone.Success },
  openedLastYear: { value: store.stats.openedLastYear, tone: UiTone.Neutral },
}))

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** The filters, the search text and the page of the list, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, narrowed, loadOnChange, enumOf, idOf, chipsOf }
  = useListQuery(Object.values(TicketFilterKey))

/** The applied status, priority and department, and their chips. */
const ticketFilters = useTicketFilters({ enumOf, idOf }, computed(() => store.departments))

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(ticketFilters.chipSources)

/** True while the first load is in flight and there are no tickets yet; the tiles and the table then draw their placeholders. */
const initialLoading = computed<boolean>(() => store.loading && store.tickets.length === 0)

/**
 * Loads the page the address names with the filters and search it holds.
 *
 * @returns True when loaded; false when refused (see `store.error`).
 */
const load = (): Promise<boolean> =>
  store.fetchClientTickets(clientId.value, page.value, { ...ticketFilters.filters.value, search: appliedSearch.value })

// Every change of a filter, the search or the page loads the list the address names.
loadOnChange(load)

onMounted(() => {
  void store.fetchStats(clientId.value)
  void store.fetchDepartments()
})
</script>

<template>
  <UiStack>
    <UiSectionHeader :level="1" :variant="UiHeadingVariant.Card">
      {{ t('clients.clientTickets.tickets') }}
      <template #actions>
        <UiLink :action="UiActionKind.Add"
          :to="clientTicketNewPath(clientId)"
          :variant="UiButtonVariant.Primary"
          data-testid="client-tickets-new"
        >
          {{ t('shared.createTicket') }}
        </UiLink>
      </template>
    </UiSectionHeader>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="client-tickets-error"
      @retry="load"
    />

    <template v-else>
      <UiStatRow :stats="CLIENT_TICKETS_STATS" :figures="figures" :loading="initialLoading" testid-prefix="client-tickets-stat" />

      <UiListToolbar
        v-model:search="search"
        search-testid="client-tickets-search"
        :search-placeholder="t('support.supportTickets.searchTicketsBySubject')"
        filter-testid="client-tickets-filter"
        :active-count="activeCount"
        :chips="chips"
        :loading="initialLoading"
        @apply="apply"
        @reset="reset"
        @cancel="cancel"
        @remove="remove"
        @clear="clearAll"
      >
        <SupportTicketFilterFields
          v-model:status="draft.status"
          v-model:priority="draft.priority"
          v-model:department-id="draft.departmentId"
          :departments="store.departments"
          testid-prefix="client-tickets-filter"
        />
      </UiListToolbar>

      <UiDataTable
        :density="UiTableDensity.Comfortable"
        :loading="initialLoading"
        :empty="!initialLoading && store.tickets.length === 0"
        :page="page"
        :total="store.totalCount"
        :page-size="store.pageSize"
        pagination-testid="client-tickets-pagination"
        data-testid="client-tickets-table"
        @update:page="setPage"
      >
        <UiSchemaTableHead :columns="schema.table.columns" />
        <UiTableBody>
          <UiTableRow v-for="ticket in store.tickets" :key="ticket.id" hoverable :data-testid="`client-tickets-row-${ticket.id}`">
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
            <UiTableCell><SupportTicketPriorityBadge :priority="ticket.priority" /></UiTableCell>
            <UiTableCell>
              <UiBadge :tone="ticketStatusTone[ticket.status]" small>{{ ticketStatusLabel(ticket.status) }}</UiBadge>
            </UiTableCell>
            <UiTableCell muted>{{ formatDate(ticket.lastReplyAt) }}</UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          <UiEmptyState
            :title="narrowed ? t('clients.clientTickets.noTicketsMatchYourFilters') : t('clients.clientTickets.noDataAvailableInTable')"
            data-testid="client-tickets-empty"
          >
            <template v-if="activeCount > 0" #actions>
              <UiButton :variant="UiButtonVariant.Link" data-testid="client-tickets-clear-empty" @click="clearAll">
                {{ t('shared.clearFilters') }}
              </UiButton>
            </template>
          </UiEmptyState>
        </template>
      </UiDataTable>
    </template>
  </UiStack>
</template>
