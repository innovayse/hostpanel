<template>
  <UiLinkCard :to="clientTicketPath(ticket.id)" :testid="`ticket-row-${ticket.id}`">
    <UiStack direction="row" justify="between" align="start">
      <UiStack direction="row" align="start" gap="sm" class="min-w-0">
        <UiTile :icon="MessageSquare" tone="primary" size="md" />
        <UiStack gap="xs" class="min-w-0">
          <UiText :as="UiTextTag.H3" size="sm" weight="medium" truncate class="group-hover:text-primary-600 dark:group-hover:text-primary-400 transition-colors">
            {{ ticket.subject }}
          </UiText>
          <UiStack direction="row" align="center" gap="sm" wrap>
            <UiText :as="UiTextTag.Span" size="xs" tone="subtle">{{ ticket.departmentName }}</UiText>
            <UiText :as="UiTextTag.Span" size="xs" tone="subtle">·</UiText>
            <UiText :as="UiTextTag.Span" size="xs" tone="subtle">{{ formatDateTime(ticket.createdAt) }}</UiText>
            <UiText v-if="ticket.lastReplyAt" :as="UiTextTag.Span" size="xs" tone="subtle">
              · {{ $t('client.tickets.lastReply') }} {{ formatDateTime(ticket.lastReplyAt) }}
            </UiText>
          </UiStack>
        </UiStack>
      </UiStack>
      <UiStack direction="row" align="center" gap="sm" class="flex-shrink-0">
        <UiText :as="UiTextTag.Span" size="xs" tone="subtle" class="hidden sm:block capitalize">{{ ticket.priority }}</UiText>
        <ClientStatusBadge :status="ticket.status" />
      </UiStack>
    </UiStack>
  </UiLinkCard>
</template>

<script setup lang="ts">
/**
 * TicketRow. One row of the ticket list: subject, department, dates, priority and status, the
 * whole row being a link to the ticket.
 */
import { clientTicketPath } from '~/constants/clientRoutes'
import { MessageSquare } from 'lucide-vue-next'
import type { ClientTicket } from '~/types/clientticket'
import { formatDateTime } from '~/utils/formatDate'
import { UiTextTag } from '~/utils/ui/enums'

/** Props of TicketRow. */
interface Props {
  /** The ticket the row shows. */
  ticket: ClientTicket
}

defineProps<Props>()
</script>
