<template>
  <UiStack gap="lg">
    <UiBackLink :to="CLIENT_TICKETS_PATH" :label="$t('client.tickets.backTo')" testid="ticket-back" />

    <UiStack v-if="pending">
      <UiSkeleton shape="block" class="h-32" />
      <UiSkeleton shape="block" class="h-48" />
    </UiStack>

    <UiEmptyState v-else-if="error || !ticket" :icon="AlertCircle" :title="$t('client.tickets.notFound')" />

    <template v-else>
      <UiCard data-testid="ticket-header">
        <UiStack direction="row" justify="between" align="start" wrap>
          <UiStack gap="xs">
            <UiHeading :level="1" size="subsection">{{ ticket.subject }}</UiHeading>
            <UiStack direction="row" align="center" gap="sm" wrap>
              <UiText as="span" size="xs" tone="subtle">{{ ticket.departmentName }}</UiText>
              <UiText as="span" size="xs" tone="subtle">·</UiText>
              <UiText as="span" size="xs" tone="subtle">{{ formatDateTime(ticket.createdAt) }}</UiText>
              <UiText as="span" size="xs" tone="subtle">·</UiText>
              <UiText as="span" size="xs" tone="subtle" class="capitalize">{{ ticket.priority }}</UiText>
            </UiStack>
          </UiStack>
          <ClientStatusBadge :status="ticket.status" />
        </UiStack>
      </UiCard>

      <UiStack data-testid="ticket-thread">
        <ClientSupportTicketMessage
          :author="authorName"
          :date="formatDateTime(ticket.createdAt)"
          :message="ticket.message"
          :badge="$t('client.tickets.you')"
        />
        <ClientSupportTicketMessage
          v-for="reply in replies"
          :key="reply.id"
          :author="replyAuthor(reply)"
          :date="formatDateTime(reply.createdAt)"
          :message="reply.message"
          :badge="reply.isStaffReply ? $t('client.tickets.staff') : undefined"
          :staff="reply.isStaffReply"
        />
      </UiStack>

      <UiCard v-if="ticket.status !== TicketStatus.Closed">
        <UiHeading :level="2" size="subsection" class="mb-4">{{ $t('client.tickets.addReply') }}</UiHeading>
        <UiForm
data-testid="ticket-reply-form"
          :error="replyError"
          :success="replySuccess ? $t('client.tickets.replySent') : undefined"
          spacing="md"
          @submit="submitReply"
        >
          <UiTextarea
            v-model="replyMessage"
            :placeholder="$t('client.tickets.replyPlaceholder')"
            :rows="4"
            :resize="false"
            testid="ticket-reply-message"
            required
          />
          <template #actions>
            <UiButton type="submit" :loading="replyLoading" data-testid="ticket-reply-submit">
              <Send v-if="!replyLoading" :size="16" :stroke-width="2" class="mr-2" />
              {{ $t('client.tickets.sendReply') }}
            </UiButton>
          </template>
        </UiForm>
      </UiCard>

      <UiCard v-else padding="sm" data-testid="ticket-closed-notice">
        <UiText size="sm" tone="subtle" class="text-center">
          {{ $t('client.tickets.closedNotice') }}
          <UiLink data-testid="ticket-new-link" :to="CLIENT_TICKET_NEW_PATH" :underline="false">{{ $t('client.tickets.openNewLink') }}</UiLink>
          {{ $t('client.tickets.closedSuffix') }}
        </UiText>
      </UiCard>
    </template>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * One support ticket: its header, the conversation and, while the ticket is open, a reply form.
 * The ticket is read through the support store; the reply flags and refusal sentence are the
 * store's too.
 */
import { CLIENT_TICKETS_PATH, CLIENT_TICKET_NEW_PATH } from '~/constants/clientRoutes'
import { AlertCircle, Send } from 'lucide-vue-next'
import { useSupportStore } from '~/stores/support'
import type { TicketReply } from '~/types/ticketreply'
import { SubmitOutcome } from '~/types/submitoutcome'
import { TicketStatus } from '~/types/ticketstatus'
import { nameOrUnknown } from '~/utils/displayFallback'
import { formatDateTime } from '~/utils/formatDate'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

/** Translates a key into the active locale. */
const { t } = useI18n()
/** The current route; its params name the record shown. */
const route = useRoute()
/** The signed-in person. */
const { user } = storeToRefs(useAuthStore())

/**
 * The signed-in person's display name, for the author line on their own ticket message.
 *
 * `ClientUser` carries `firstName` and `lastName` rather than a `name`, so the line is composed
 * from those and falls back to the email so the avatar is never an empty circle.
 */
const authorName = computed(() => {
  const person = user.value
  if (!person) {
    return ''
  }

  return `${person.firstName} ${person.lastName}`.trim() || person.email
})

/** The support store: the ticket, the reply flags and the refusal sentences. */
const supportStore = useSupportStore()
/** Loading flag and reload function of the data this page reads. */
const { pending, refresh } = await useAsyncData(
  () => `client-ticket-${String(route.params.id)}`,
  () => supportStore.loadTicket(String(route.params.id))
)

/** The ticket being read; null until loaded or after a failed read. */
const ticket = computed(() => supportStore.ticket)

/** The sentence for a failed read. */
const error = computed(() => supportStore.ticketReadError)

/** The replies of the thread, oldest first. */
const replies = computed(() => ticket.value?.replies ?? [])

/** Submission state of the reply form; the store owns the loading flag and the refusal. */
const { replyLoading, replyError } = storeToRefs(supportStore)
/** Action that posts a reply to the ticket. */
const { replyToTicket } = supportStore

/** The reply being typed. */
const replyMessage = ref('')

/** True after a reply was accepted, until the next attempt starts. */
const replySuccess = ref(false)

/**
 * The author line of a reply: a staff member's name followed by the "support" tag, or the
 * name the client wrote it under.
 *
 * @param reply - The reply to name the author of.
 * @returns The text shown above the reply.
 */
const replyAuthor = (reply: TicketReply): string => {
  const name = nameOrUnknown(t, reply.authorName)

  return reply.isStaffReply ? `${name} (${t('client.tickets.support')})` : name
}

/**
 * Sends the typed reply through the store and reloads the thread when it was accepted.
 *
 * @returns Resolves when the attempt has ended; a refusal is rendered from `replyError`.
 */
const submitReply = async (): Promise<void> => {
  if (!replyMessage.value.trim()) {
    return
  }
  replySuccess.value = false
  const outcome = await replyToTicket(String(route.params.id), replyMessage.value)
  if (outcome === SubmitOutcome.Sent) {
    replyMessage.value = ''
    replySuccess.value = true
    // Reload the thread so the new reply shows.
    await refresh()
  }
}
</script>
