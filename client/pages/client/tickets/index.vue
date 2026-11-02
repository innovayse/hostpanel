<template>
  <UiStack gap="lg">
    <UiPageHeader :title="$t('client.tickets.title')" :description="$t('client.tickets.subtitle')" :icon="MessageSquare" class="!mb-0">
      <template #actions>
        <UiButton :to="CLIENT_TICKET_NEW_PATH" variant="outline" data-testid="tickets-open-new">
          <Plus :size="16" :stroke-width="2" class="mr-2" />
          {{ $t('client.tickets.openNew') }}
        </UiButton>
      </template>
    </UiPageHeader>

    <UiTabs v-model="activeTab" :tabs="tabs" />

    <UiStack v-if="store.ticketsLoading" gap="sm">
      <UiSkeleton v-for="i in 4" :key="i" shape="block" class="h-16" />
    </UiStack>

    <!-- Not a customer account. Checked before the failure branch: this is a state the
         API reported, not a fault, and it carries its own explanation and a way out. -->
    <ClientNoProfileNotice v-else-if="store.clientProfileMissing" />

    <!-- Failed. Checked before the empty state, which a failure is otherwise
         indistinguishable from: the list is empty either way. -->
    <UiAlert v-else-if="store.ticketsError" variant="error" data-testid="tickets-error">{{ store.ticketsError }}</UiAlert>

    <UiEmptyState v-else-if="!filteredTickets.length" :icon="MessageSquare" :title="$t('client.tickets.empty')">
      <UiButton :to="CLIENT_TICKET_NEW_PATH" data-testid="tickets-open-cta">{{ $t('client.tickets.openCta') }}</UiButton>
    </UiEmptyState>

    <UiStack v-else gap="sm" data-testid="tickets-list">
      <ClientSupportTicketRow v-for="ticket in filteredTickets" :key="ticket.id" :ticket="ticket" />
    </UiStack>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Support ticket list: filter tabs (all / open / closed) over the client's tickets, each row a
 * link to the ticket. Loading, missing-profile and failure states come from the client store.
 */
import { CLIENT_TICKET_NEW_PATH } from '~/constants/clientRoutes'
import { MessageSquare, Plus } from 'lucide-vue-next'
import { useClientStore } from '~/stores/client'
import { ListFilter } from '~/types/listfilter'
import { isTicketOpen } from '~/utils/ticketOpen'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

/** Translates a key into the active locale. */
const { t } = useI18n()

/** The client store that owns the ticket list and its loading and failure state. */
const store = useClientStore()

await useAsyncData('client-tickets', () => store.fetchTickets(true))

/** The active filter tab, a `ListFilter` value (a string so the tab bar can v-model it). */
const activeTab = ref<string>(ListFilter.All)

/** The filter tabs. */
const tabs = computed(() => [
  { value: ListFilter.All, label: t('client.tickets.tabAll') },
  { value: ListFilter.Current, label: t('client.tickets.tabOpen') },
  { value: ListFilter.Finished, label: t('client.tickets.tabClosed') },
])

/** The tickets the active tab shows: open means anything not closed. */
const filteredTickets = computed(() => {
  if (activeTab.value === ListFilter.Current) {
    return store.tickets.filter(isTicketOpen)
  }
  if (activeTab.value === ListFilter.Finished) {
    return store.tickets.filter((ticket) => !isTicketOpen(ticket))
  }
  return store.tickets
})
</script>
