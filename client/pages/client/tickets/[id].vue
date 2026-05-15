<template>
  <div>
    <NuxtLink to="/client/tickets" class="inline-flex items-center gap-2 text-gray-500 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white text-sm transition-colors mb-8">
      <ArrowLeft :size="16" :stroke-width="2" />
      {{ $t('client.tickets.backTo') }}
    </NuxtLink>

    <!-- Loading -->
    <div v-if="pending" class="space-y-4">
      <div class="h-32 rounded-2xl bg-white/5 border border-white/10 animate-pulse" />
      <div class="h-48 rounded-2xl bg-white/5 border border-white/10 animate-pulse" />
    </div>

    <!-- Error -->
    <div v-else-if="error || !ticket" class="text-center py-20">
      <AlertCircle :size="48" :stroke-width="2" class="text-red-400 mx-auto mb-4" />
      <p class="text-gray-400">{{ $t('client.tickets.notFound') }}</p>
    </div>

    <div v-else>
      <!-- Ticket header -->
      <UiCard class="mb-6">
        <div class="flex items-start justify-between gap-4 flex-wrap">
          <div>
            <h1 class="text-xl font-bold text-gray-900 dark:text-white">{{ ticket.subject }}</h1>
            <div class="flex items-center gap-3 mt-2 text-xs text-gray-500">
              <span>{{ ticket.deptname }}</span>
              <span>·</span>
              <span>{{ ticket.date }}</span>
              <span>·</span>
              <span class="capitalize">{{ ticket.priority }}</span>
            </div>
          </div>
          <ClientStatusBadge :status="ticket.status" />
        </div>
      </UiCard>

      <!-- Thread -->
      <div class="space-y-4 mb-6">
        <!-- Original message -->
        <UiCard padding="md">
          <div class="flex items-center justify-between mb-4">
            <div class="flex items-center gap-3">
              <div class="w-8 h-8 rounded-full bg-cyan-500/20 border border-cyan-500/30 flex items-center justify-center text-xs font-bold text-cyan-400">
                {{ user?.name?.charAt(0)?.toUpperCase() }}
              </div>
              <div>
                <div class="text-gray-900 dark:text-white text-sm font-medium">{{ user?.name }}</div>
                <div class="text-gray-500 text-xs">{{ ticket.date }}</div>
              </div>
            </div>
            <span class="text-xs px-2 py-0.5 rounded-full bg-cyan-500/10 text-cyan-400 border border-cyan-500/20">{{ $t('client.tickets.you') }}</span>
          </div>
          <!-- Render message as pre-wrapped text to preserve formatting -->
          <p class="text-gray-600 dark:text-gray-300 text-sm leading-relaxed whitespace-pre-wrap">{{ ticket.message }}</p>
        </UiCard>

        <!-- Replies -->
        <div
          v-for="reply in ticket.replies?.reply ?? []"
          :key="reply.replyid"
          class="p-5 rounded-2xl border transition-all"
          :class="reply.admin
            ? 'bg-primary-500/5 border-primary-500/20'
            : 'bg-gray-50 dark:bg-white/5 border-gray-200 dark:border-white/10'"
        >
          <div class="flex items-center justify-between mb-4">
            <div class="flex items-center gap-3">
              <div
                class="w-8 h-8 rounded-full flex items-center justify-center text-xs font-bold border"
                :class="reply.admin
                  ? 'bg-primary-500/20 border-primary-500/30 text-primary-400'
                  : 'bg-gray-500/20 border-gray-500/30 text-gray-400'"
              >
                {{ (reply.admin || reply.name || '?').charAt(0).toUpperCase() }}
              </div>
              <div>
                <div class="text-gray-900 dark:text-white text-sm font-medium">{{ reply.admin ? `${reply.admin} (${$t('client.tickets.support')})` : reply.name }}</div>
                <div class="text-gray-500 text-xs">{{ reply.date }}</div>
              </div>
            </div>
            <span v-if="reply.admin" class="text-xs px-2 py-0.5 rounded-full bg-primary-500/10 text-primary-400 border border-primary-500/20">
              {{ $t('client.tickets.staff') }}
            </span>
          </div>
          <p class="text-gray-600 dark:text-gray-300 text-sm leading-relaxed whitespace-pre-wrap">{{ reply.message }}</p>
        </div>
      </div>

      <!-- Reply form (only for open tickets) -->
      <UiCard v-if="ticket.status !== 'Closed'">
        <h2 class="text-base font-bold text-gray-900 dark:text-white mb-4">{{ $t('client.tickets.addReply') }}</h2>
        <UiForm
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
            required
          />
          <template #actions>
            <UiButton type="submit" :loading="replying">
              <Send v-if="!replying" :size="16" :stroke-width="2" class="mr-2" />
              {{ replying ? $t('client.tickets.sending') : $t('client.tickets.sendReply') }}
            </UiButton>
          </template>
        </UiForm>
      </UiCard>

      <!-- Closed ticket notice -->
      <div v-else class="p-4 rounded-xl bg-gray-50 dark:bg-white/5 border border-gray-200 dark:border-white/10 text-center text-gray-500 dark:text-gray-500 text-sm">
        {{ $t('client.tickets.closedNotice') }}
        <NuxtLink to="/client/tickets/new" class="text-cyan-600 dark:text-cyan-400 hover:text-cyan-700 dark:hover:text-cyan-300 transition-colors">
          {{ $t('client.tickets.openNewLink') }}
        </NuxtLink>
        {{ $t('client.tickets.closedSuffix') }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ArrowLeft, AlertCircle, Send } from 'lucide-vue-next'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

const { t } = useI18n()
const route = useRoute()
const { user } = useClientAuth()

const { data: ticket, pending, error, refresh } = await useApi(`/api/portal/client/tickets/${route.params.id}`)

// Reply form state
const replyMessage = ref('')
const replying = ref(false)
const replyError = ref('')
const replySuccess = ref(false)

async function submitReply() {
  if (!replyMessage.value.trim()) return
  replying.value = true
  replyError.value = ''
  replySuccess.value = false
  try {
    await apiFetch(`/api/portal/client/tickets/${route.params.id}/reply`, {
      method: 'POST',
      body: { message: replyMessage.value }
    })
    replyMessage.value = ''
    replySuccess.value = true
    // Refresh the ticket thread to show the new reply
    await refresh()
  } catch (err: any) {
    replyError.value = err?.data?.statusMessage || t('client.tickets.replyError')
  } finally {
    replying.value = false
  }
}
</script>
