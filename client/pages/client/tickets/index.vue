<template>
  <div>
    <div class="mb-8 flex items-center justify-between gap-4 flex-wrap">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">{{ $t('client.tickets.title') }}</h1>
        <p class="text-gray-500 dark:text-gray-400 text-sm mt-1">{{ $t('client.tickets.subtitle') }}</p>
      </div>
      <NuxtLink
        to="/client/tickets/new"
        class="px-5 py-2.5 rounded-xl bg-cyan-500/10 border border-cyan-500/20 text-cyan-400 text-sm font-medium hover:bg-cyan-500/20 transition-colors flex items-center gap-2"
      >
        <Plus :size="16" :stroke-width="2" />
        {{ $t('client.tickets.openNew') }}
      </NuxtLink>
    </div>

    <!-- Filter tabs -->
    <UiTabs
      :tabs="tabs.map(t => ({ value: t.key, label: t.label }))"
      v-model="activeTab"
      class="mb-6"
    />

    <!-- Loading -->
    <div v-if="store.ticketsLoading" class="space-y-3">
      <div v-for="i in 4" :key="i" class="h-16 rounded-xl bg-white/5 border border-white/10 animate-pulse" />
    </div>

    <!-- Empty -->
    <div v-else-if="!filteredTickets.length" class="text-center py-20">
      <MessageSquare :size="48" :stroke-width="2" class="text-gray-300 dark:text-gray-600 mx-auto mb-4" />
      <p class="text-gray-400 mb-4">{{ $t('client.tickets.empty') }}</p>
      <NuxtLink
        to="/client/tickets/new"
        class="px-6 py-2.5 rounded-xl bg-cyan-500 text-white font-semibold text-sm hover:bg-cyan-400 transition-colors"
      >
        {{ $t('client.tickets.openCta') }}
      </NuxtLink>
    </div>

    <!-- Tickets list -->
    <div v-else class="space-y-3">
      <NuxtLink
        v-for="ticket in filteredTickets"
        :key="ticket.id"
        :to="`/client/tickets/${ticket.id}`"
        class="block p-4 rounded-xl bg-gray-50 dark:bg-white/5 border border-gray-200 dark:border-white/10 hover:border-cyan-500/30 hover:bg-gray-100 dark:hover:bg-white/8 transition-all duration-200 group"
      >
        <div class="flex items-start justify-between gap-4">
          <div class="flex items-start gap-3 min-w-0">
            <div class="w-8 h-8 rounded-lg bg-cyan-500/10 border border-cyan-500/20 flex items-center justify-center flex-shrink-0 mt-0.5">
              <MessageSquare :size="14" :stroke-width="2" class="text-cyan-400" />
            </div>
            <div class="min-w-0">
              <h3 class="text-gray-900 dark:text-white font-medium text-sm group-hover:text-cyan-600 dark:group-hover:text-cyan-400 transition-colors truncate">
                {{ ticket.subject }}
              </h3>
              <div class="flex items-center gap-3 mt-1 text-xs text-gray-500">
                <span>{{ ticket.deptname }}</span>
                <span>·</span>
                <span>{{ ticket.date }}</span>
                <span v-if="ticket.lastreply">· {{ $t('client.tickets.lastReply') }} {{ ticket.lastreply }}</span>
              </div>
            </div>
          </div>
          <div class="flex items-center gap-2 flex-shrink-0">
            <span class="hidden sm:block text-xs text-gray-500 capitalize">{{ ticket.urgency }}</span>
            <ClientStatusBadge :status="ticket.status" />
          </div>
        </div>
      </NuxtLink>
    </div>
  </div>
</template>

<script setup lang="ts">
import { MessageSquare, Plus } from 'lucide-vue-next'
import { useClientStore } from '~/stores/client'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

const { t } = useI18n()
const store = useClientStore()

await useAsyncData('client-tickets', () => store.fetchTickets())

const activeTab = ref('all')

const tabs = computed(() => [
  { key: 'all',    label: t('client.tickets.tabAll') },
  { key: 'open',   label: t('client.tickets.tabOpen') },
  { key: 'closed', label: t('client.tickets.tabClosed') }
])

const filteredTickets = computed(() => {
  if (activeTab.value === 'all') return store.tickets
  if (activeTab.value === 'open') return store.tickets.filter(t => t.status !== 'Closed')
  return store.tickets.filter(t => t.status === 'Closed')
})
</script>
