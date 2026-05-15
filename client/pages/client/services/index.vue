<template>
  <div>
    <div class="mb-8">
      <h1 class="text-2xl font-bold text-gray-900 dark:text-white">{{ $t('client.services.title') }}</h1>
      <p class="text-gray-500 dark:text-gray-400 text-sm mt-1">{{ $t('client.services.subtitle') }}</p>
    </div>

    <!-- Loading skeleton -->
    <div v-if="store.servicesLoading" class="grid md:grid-cols-2 gap-4">
      <div v-for="i in 4" :key="i" class="h-36 rounded-2xl bg-white/5 border border-white/10 animate-pulse" />
    </div>

    <!-- Empty -->
    <div v-else-if="!store.services.length" class="text-center py-20">
      <Server :size="48" :stroke-width="2" class="text-gray-300 dark:text-gray-600 mx-auto mb-4" />
      <p class="text-gray-400 mb-4">{{ $t('client.services.empty') }}</p>
      <NuxtLink
        to="/hosting"
        class="px-6 py-2.5 rounded-xl bg-cyan-500 text-white font-semibold text-sm hover:bg-cyan-400 transition-colors"
      >
        {{ $t('client.services.browseHosting') }}
      </NuxtLink>
    </div>

    <!-- Services grid -->
    <div v-else>
      <!-- Setup required alert -->
      <UiAlert
        v-if="servicesNeedingSetup.length > 0"
        variant="info"
        :title="servicesNeedingSetup.length === 1 ? $t('client.dashboard.setupAlertSingle') : $t('client.dashboard.setupAlertMultiple', { count: servicesNeedingSetup.length })"
        class="mb-6"
      >
        {{ $t('client.dashboard.setupAlertSubtitle') }}
      </UiAlert>

      <div class="grid md:grid-cols-2 gap-4">
      <NuxtLink
        v-for="service in store.services"
        :key="service.id"
        :to="service.username ? `/client/services/${service.id}` : `/client/services/${service.id}/setup`"
        class="group block p-5 rounded-2xl bg-gray-50 dark:bg-white/5 border border-gray-200 dark:border-white/10 hover:border-cyan-500/30 hover:bg-gray-100 dark:hover:bg-white/8 transition-all duration-200"
        :class="!service.username ? 'border-blue-500/20 hover:border-blue-500/40' : ''"
      >
        <div class="flex items-start justify-between gap-4 mb-3">
          <div class="flex items-center gap-3 min-w-0">
            <div class="w-10 h-10 rounded-xl bg-cyan-500/10 border border-cyan-500/20 flex items-center justify-center flex-shrink-0">
              <Server :size="18" :stroke-width="2" class="text-cyan-400" />
            </div>
            <div class="min-w-0">
              <h3 class="text-gray-900 dark:text-white font-semibold text-sm truncate group-hover:text-cyan-600 dark:group-hover:text-cyan-400 transition-colors">
                {{ service.name }}
              </h3>
              <p class="text-gray-500 text-xs truncate">{{ service.domain || service.groupname }}</p>
            </div>
          </div>
          <div class="flex items-center gap-2 flex-shrink-0">
            <span v-if="!service.username" class="px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider bg-blue-500/10 text-blue-400 border border-blue-500/20">
              {{ $t('client.dashboard.needsSetup') }}
            </span>
            <ClientStatusBadge v-else :status="service.status" />
          </div>
        </div>

        <div class="grid grid-cols-2 gap-3 mt-4">
          <div class="text-xs">
            <span class="text-gray-500">{{ $t('client.services.nextDue') }}</span>
            <div class="text-gray-700 dark:text-gray-300 font-medium mt-0.5">{{ service.nextduedate || '—' }}</div>
          </div>
          <div class="text-xs">
            <span class="text-gray-500">{{ $t('client.services.amount') }}</span>
            <div class="text-gray-700 dark:text-gray-300 font-medium mt-0.5">
              {{ formatAmount(service.recurringamount, store.user?.currency) }}
            </div>
          </div>
        </div>

        <!-- Disk usage bar (if available) -->
        <div v-if="service.disklimit && service.disklimit !== '0'" class="mt-4">
          <div class="flex justify-between text-xs text-gray-500 mb-1">
            <span>{{ $t('client.services.diskUsage') }}</span>
            <span>{{ service.diskusage }}MB / {{ service.disklimit }}MB</span>
          </div>
          <div class="h-1.5 rounded-full bg-white/10 overflow-hidden">
            <div
              class="h-full rounded-full bg-gradient-to-r from-cyan-500 to-primary-500 transition-all duration-500"
              :style="{ width: `${Math.min(100, (parseInt(service.diskusage) / parseInt(service.disklimit)) * 100)}%` }"
            />
          </div>
        </div>
      </NuxtLink>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Server } from 'lucide-vue-next'
import { useClientStore } from '~/stores/client'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

const store = useClientStore()
const { format: formatAmount } = useCurrency()

await useAsyncData('client-services', () => store.fetchServices())

const servicesNeedingSetup = computed(() => store.services.filter(s => !s.username))
</script>
