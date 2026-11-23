<template>
  <UiCard data-testid="dashboard-tickets">
    <UiCardHeader :title="$t('client.dashboard.recentTickets')">
      <UiButton size="sm" variant="primary" :to="CLIENT_TICKET_NEW_PATH" data-testid="dashboard-ticket-new">
        <Plus :size="12" :stroke-width="2" class="mr-1" />
        {{ $t('client.tickets.create') }}
      </UiButton>
    </UiCardHeader>

    <UiStack v-if="store.ticketsLoading" gap="sm">
      <UiSkeleton v-for="i in 3" :key="i" shape="block" class="h-12" />
    </UiStack>

    <UiEmptyState v-else-if="!store.tickets.length" :icon="MessageSquare" :title="$t('client.dashboard.noTickets')" class="!py-6" data-testid="dashboard-tickets-empty">
      <UiButton size="sm" variant="subtle" :to="CLIENT_TICKET_NEW_PATH" data-testid="dashboard-ticket-open">
        {{ $t('client.tickets.create') }}
      </UiButton>
    </UiEmptyState>

    <UiStack v-else gap="sm">
      <ClientDashboardListRow
        v-for="ticket in store.tickets.slice(0, 4)"
        :key="ticket.id"
        :to="clientTicketPath(ticket.id)"
        :testid="`dashboard-ticket-${ticket.id}`"
      >
        <UiStack gap="none" class="min-w-0 flex-1">
          <UiText size="sm" weight="medium" truncate>{{ ticket.subject }}</UiText>
          <UiText size="xs" tone="subtle">{{ ticket.departmentName }}</UiText>
        </UiStack>
        <ClientStatusBadge :status="ticket.status" class="flex-shrink-0" />
      </ClientDashboardListRow>
    </UiStack>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * The dashboard's "Recent Tickets" card: the latest four support tickets with their status, or
 * an invitation to open the first one.
 */
import { CLIENT_TICKET_NEW_PATH, clientTicketPath } from '~/constants/clientRoutes'
import { MessageSquare, Plus } from 'lucide-vue-next'
import { useClientStore } from '~/stores/client'

/** Client store: the signed-in person and their services, invoices, domains and tickets. */
const store = useClientStore()
</script>
