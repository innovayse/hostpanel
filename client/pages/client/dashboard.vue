<template>
  <div>
    <!-- Page header -->
    <div class="mb-8 flex items-start justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">{{ $t('client.dashboard.title') }}</h1>
        <p class="text-gray-500 dark:text-gray-400 text-sm mt-1">{{ $t('client.dashboard.subtitle') }}</p>
      </div>
      <UiButton size="sm" variant="subtle" :loading="refreshing" class="mt-1 flex-shrink-0" @click="refresh">
        <RefreshCw :size="13" :stroke-width="2" class="mr-1.5" :class="refreshing ? 'animate-spin' : ''" />
        {{ $t('client.dashboard.refresh') }}
      </UiButton>
    </div>

    <!-- Stats cards -->
    <div class="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-10">
      <NuxtLink v-for="stat in stats" :key="stat.label" :to="stat.to" class="block">
        <UiCard padding="md" :hover="true" class="h-full">
          <div class="flex items-center justify-between mb-3">
            <span class="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">{{ stat.label }}</span>
            <component :is="stat.icon" :size="18" :stroke-width="2" :class="stat.iconColor" />
          </div>
          <div class="text-3xl font-bold text-gray-900 dark:text-white">
            <span v-if="store.servicesLoading || store.invoicesLoading || store.domainsLoading || store.ticketsLoading">—</span>
            <span v-else>{{ stat.value }}</span>
          </div>
          <div v-if="stat.sub" class="text-xs text-gray-500 mt-1">{{ stat.sub }}</div>
        </UiCard>
      </NuxtLink>
    </div>

    <!-- Alerts + expiring card -->
    <div v-if="servicesNeedingSetup.length > 0 || unpaidInvoices.length > 0 || expiringDomains.length > 0" class="space-y-4 mb-8">
      <!-- Services requiring setup alert -->
      <UiAlert v-if="servicesNeedingSetup.length > 0" variant="info" :title="setupAlertMessage">
        {{ $t('client.dashboard.setupAlertSubtitle') }}
        <template #action>
          <UiButton size="sm" variant="subtle" to="/client/services">
            <ArrowRight :size="13" :stroke-width="2" class="mr-1" />
            {{ $t('client.dashboard.setupAlertAction') }}
          </UiButton>
        </template>
      </UiAlert>

      <!-- Unpaid invoices alert -->
      <UiAlert v-if="unpaidInvoices.length > 0" :title="unpaidAlertMessage">
        {{ $t('client.dashboard.unpaidSubtitle') }}
        <template #action>
          <UiButton size="sm" variant="subtle" to="/client/invoices">
            <ArrowRight :size="13" :stroke-width="2" class="mr-1" />
            {{ $t('client.dashboard.unpaidView') }}
          </UiButton>
        </template>
      </UiAlert>

      <!-- Domains expiring soon — card matching WHMCS style -->
      <UiCard v-if="!store.domainsLoading && expiringDomains.length > 0" class="border-l-4 border-l-yellow-400 dark:border-l-yellow-500">
        <UiCardHeader :title="$t('client.dashboard.expiringSoonTitle')" :icon="Globe">
          <UiButton size="sm" variant="subtle" to="/client/domains">
            <RefreshCw :size="12" :stroke-width="2" class="mr-1" />
            {{ $t('client.dashboard.expiringSoonRenew') }}
          </UiButton>
        </UiCardHeader>
        <p class="text-gray-600 dark:text-gray-300 text-sm">
          {{ expiringDomains.length === 1
            ? $t('client.dashboard.expiringSoonSingle')
            : $t('client.dashboard.expiringSoonMultiple', { count: expiringDomains.length }) }}
        </p>
      </UiCard>
    </div>

    <!-- Row 1: Services + Invoices -->
    <div class="grid lg:grid-cols-2 gap-6 mb-6">
      <!-- Active Services -->
      <UiCard>
        <UiCardHeader :title="$t('client.dashboard.activeServices')">
          <UiButton size="sm" variant="subtle" to="/client/services">
            {{ $t('client.dashboard.myServices') }}
            <ArrowRight :size="12" :stroke-width="2" class="ml-1.5" />
          </UiButton>
        </UiCardHeader>

        <div v-if="store.servicesLoading" class="space-y-3">
          <div v-for="i in 3" :key="i" class="h-12 rounded-xl bg-gray-100 dark:bg-white/5 animate-pulse" />
        </div>

        <p v-else-if="!store.services.length" class="text-gray-500 dark:text-gray-400 text-sm py-4">
          {{ $t('client.dashboard.noActiveServices') }}
          <NuxtLink to="/hosting" class="text-cyan-600 dark:text-cyan-400 hover:underline">{{ $t('client.dashboard.placeOrder') }}</NuxtLink>
        </p>

        <div v-else class="space-y-3">
          <NuxtLink
            v-for="service in store.services.slice(0, 4)"
            :key="service.id"
            :to="service.username ? `/client/services/${service.id}` : `/client/services/${service.id}/setup`"
            class="flex items-center justify-between p-3 rounded-xl bg-gray-50 dark:bg-white/5 hover:bg-gray-100 dark:hover:bg-white/10 transition-colors"
          >
            <div class="flex items-center gap-3 min-w-0">
              <Server :size="16" :stroke-width="2" class="text-cyan-400 flex-shrink-0" />
              <div class="min-w-0">
                <div class="text-gray-900 dark:text-white text-sm font-medium truncate">{{ service.name }}</div>
                <div class="text-gray-500 text-xs truncate">{{ service.domain || $t('client.dashboard.noDomain') }}</div>
              </div>
            </div>
            <div class="flex items-center gap-3">
              <button
                v-if="service.serverhostname && service.status === 'Active'"
                :disabled="ssoLoading === service.id"
                class="p-2 rounded-lg text-gray-400 hover:text-cyan-500 hover:bg-cyan-500/10 transition-all disabled:opacity-50"
                :title="$t('client.services.actionLoginCpanel')"
                @click.stop.prevent="loginToCpanel(service)"
              >
                <Loader v-if="ssoLoading === service.id" :size="14" class="animate-spin" />
                <Monitor v-else :size="14" />
              </button>
              <ClientStatusBadge v-if="service.username" :status="service.status" />
              <div v-else class="px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider bg-blue-500/10 text-blue-400 border border-blue-500/20">
                {{ $t('client.dashboard.needsSetup') }}
              </div>
            </div>
          </NuxtLink>
        </div>
      </UiCard>

      <!-- Recent Invoices -->
      <UiCard>
        <UiCardHeader :title="$t('client.dashboard.recentInvoices')">
          <UiButton size="sm" variant="subtle" to="/client/invoices">
            {{ $t('client.dashboard.viewAll') }}
            <ArrowRight :size="12" :stroke-width="2" class="ml-1.5" />
          </UiButton>
        </UiCardHeader>

        <div v-if="store.invoicesLoading" class="space-y-3">
          <div v-for="i in 3" :key="i" class="h-12 rounded-xl bg-gray-100 dark:bg-white/5 animate-pulse" />
        </div>

        <div v-else-if="!store.invoices.length" class="text-gray-500 text-sm py-4 text-center">{{ $t('client.dashboard.noInvoices') }}</div>

        <div v-else class="space-y-3">
          <NuxtLink
            v-for="invoice in store.invoices.slice(0, 4)"
            :key="invoice.id"
            :to="`/client/invoices/${invoice.id}`"
            class="flex items-center justify-between p-3 rounded-xl bg-gray-50 dark:bg-white/5 hover:bg-gray-100 dark:hover:bg-white/10 transition-colors"
          >
            <div>
              <div class="text-gray-900 dark:text-white text-sm font-medium">#{{ invoice.id }}</div>
              <div class="text-gray-500 text-xs">{{ $t('client.dashboard.due') }} {{ invoice.duedate }}</div>
            </div>
            <div class="flex items-center gap-3">
              <span class="text-gray-900 dark:text-white font-semibold text-sm">{{ invoice.currencyprefix }}{{ invoice.total }}</span>
              <ClientStatusBadge :status="invoice.status" />
            </div>
          </NuxtLink>
        </div>
      </UiCard>
    </div>

    <!-- Row 2: Domains + Tickets -->
    <div class="grid lg:grid-cols-2 gap-6 mb-6">
      <!-- My Domains -->
      <UiCard>
        <UiCardHeader :title="$t('client.dashboard.myDomains')">
          <UiButton size="sm" variant="subtle" to="/client/domains">
            {{ $t('client.dashboard.viewAll') }}
            <ArrowRight :size="12" :stroke-width="2" class="ml-1.5" />
          </UiButton>
        </UiCardHeader>

        <div v-if="store.domainsLoading" class="space-y-3">
          <div v-for="i in 2" :key="i" class="h-12 rounded-xl bg-gray-100 dark:bg-white/5 animate-pulse" />
        </div>

        <div v-else-if="!store.domains.length" class="text-gray-500 text-sm py-4 text-center">{{ $t('client.dashboard.noDomains') }}</div>

        <div v-else class="space-y-3">
          <NuxtLink
            v-for="domain in store.domains.slice(0, 4)"
            :key="domain.id"
            :to="`/client/domains/${domain.id}`"
            class="flex items-center justify-between p-3 rounded-xl bg-gray-50 dark:bg-white/5 hover:bg-gray-100 dark:hover:bg-white/10 transition-colors"
          >
            <div class="flex items-center gap-3 min-w-0">
              <Globe :size="16" :stroke-width="2" class="text-primary-400 flex-shrink-0" />
              <span class="text-gray-900 dark:text-white text-sm font-medium truncate">{{ domain.domainname }}</span>
            </div>
            <div class="flex items-center gap-2 flex-shrink-0">
              <span class="text-gray-500 text-xs">
                {{
                  domain.expirydate && !domain.expirydate.startsWith('0000')
                    ? $t('client.dashboard.exp') + ' ' + domain.expirydate
                    : domain.nextduedate && !domain.nextduedate.startsWith('0000')
                      ? $t('client.dashboard.due') + ' ' + domain.nextduedate
                      : domain.regdate && !domain.regdate.startsWith('0000')
                        ? $t('client.dashboard.reg') + ' ' + domain.regdate
                        : '—'
                }}
              </span>
              <ClientStatusBadge :status="domain.status" />
            </div>
          </NuxtLink>
        </div>
      </UiCard>

      <!-- Recent Tickets -->
      <UiCard>
        <UiCardHeader :title="$t('client.dashboard.recentTickets')">
          <UiButton size="sm" variant="primary" to="/client/tickets/new">
            <Plus :size="12" :stroke-width="2" class="mr-1" />
            {{ $t('client.dashboard.openNewTicket') }}
          </UiButton>
        </UiCardHeader>

        <div v-if="store.ticketsLoading" class="space-y-3">
          <div v-for="i in 3" :key="i" class="h-12 rounded-xl bg-gray-100 dark:bg-white/5 animate-pulse" />
        </div>

        <div v-else-if="!store.tickets.length" class="py-6 text-center">
          <p class="text-gray-500 text-sm">{{ $t('client.dashboard.noTickets') }}</p>
          <UiButton size="sm" variant="ghost" to="/client/tickets/new" class="mt-2">
            {{ $t('client.dashboard.openTicketLink') }}
          </UiButton>
        </div>

        <div v-else class="space-y-3">
          <NuxtLink
            v-for="ticket in store.tickets.slice(0, 4)"
            :key="ticket.id"
            :to="`/client/tickets/${ticket.id}`"
            class="flex items-center justify-between p-3 rounded-xl bg-gray-50 dark:bg-white/5 hover:bg-gray-100 dark:hover:bg-white/10 transition-colors"
          >
            <div class="min-w-0 flex-1">
              <div class="text-gray-900 dark:text-white text-sm font-medium truncate">{{ ticket.subject }}</div>
              <div class="text-gray-500 text-xs">{{ ticket.deptname }}</div>
            </div>
            <ClientStatusBadge :status="ticket.status" class="ml-3 flex-shrink-0" />
          </NuxtLink>
        </div>
      </UiCard>
    </div>

    <!-- Row 3: Announcements + Register Domain -->
    <div class="grid lg:grid-cols-2 gap-6">
      <!-- Recent News -->
      <UiCard>
        <UiCardHeader :title="$t('client.dashboard.announcements')">
          <UiButton v-if="whmcsUrl" size="sm" variant="subtle" :href="`${whmcsUrl}/announcements.php`" target="_blank">
            {{ $t('client.dashboard.viewAll') }}
            <ArrowRight :size="12" :stroke-width="2" class="ml-1.5" />
          </UiButton>
        </UiCardHeader>

        <div v-if="!announcements.length" class="text-gray-500 text-sm py-4 text-center">{{ $t('client.dashboard.noAnnouncements') }}</div>

        <div v-else class="space-y-3">
          <div
            v-for="ann in announcements.slice(0, 3)"
            :key="ann.id"
            class="p-3 rounded-xl bg-gray-50 dark:bg-white/5"
          >
            <div class="text-gray-900 dark:text-white text-sm font-medium">{{ ann.title }}</div>
            <div class="text-gray-500 text-xs mt-0.5">{{ ann.date }}</div>
          </div>
        </div>
      </UiCard>

      <!-- Register a New Domain -->
      <UiCard>
        <UiCardHeader :title="$t('client.dashboard.registerDomain')" />
        <UiInput
          v-model="domainQuery"
          :placeholder="$t('client.dashboard.domainPlaceholder')"
          @keydown.enter="registerDomain"
        />
        <div class="flex gap-2 mt-3">
          <UiButton :full-width="true" @click="registerDomain">{{ $t('client.dashboard.registerBtn') }}</UiButton>
          <UiButton variant="subtle" :full-width="true" @click="transferDomain">{{ $t('client.dashboard.transferBtn') }}</UiButton>
        </div>
      </UiCard>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Server, Globe, FileText, MessageSquare, RefreshCw, ArrowRight, Plus, Monitor, Loader } from 'lucide-vue-next'
import { useClientStore } from '~/stores/client'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

const { t } = useI18n()
const config = useRuntimeConfig()
const whmcsUrl = config.public.whmcsUrl

const store = useClientStore()
await useAsyncData('client-dashboard', () => store.fetchAll())

/** Announcements from WHMCS (public endpoint) */
const { data: announcementsRaw } = await useFetch<Array<{ id: number; date: string; title: string }>>(
  '/api/portal/public/announcements'
)
const announcements = computed(() => announcementsRaw.value ?? [])

/** Refresh all dashboard data */
const refreshing = ref(false)
async function refresh() {
  refreshing.value = true
  await store.fetchAll(true)
  refreshing.value = false
}

/** Domains expiring within the next 45 days */
const expiringDomains = computed(() => {
  const cutoff = Date.now() + 45 * 86_400_000
  return store.domains.filter(d => {
    if (!d.expirydate || d.expirydate.startsWith('0000')) return false
    return new Date(d.expirydate).getTime() <= cutoff
  })
})

/** Services that are pending and have no username (need setup) */
const servicesNeedingSetup = computed(() =>
  store.services.filter(s => !s.username)
)

const setupAlertMessage = computed(() => {
  const count = servicesNeedingSetup.value.length
  return count === 1
    ? t('client.dashboard.setupAlertSingle')
    : t('client.dashboard.setupAlertMultiple', { count })
})

/** Invoices that are unpaid or overdue */
const unpaidInvoices = computed(() =>
  store.invoices.filter(i => i.status === 'Unpaid' || (i.status as string) === 'Overdue')
)

const unpaidAlertMessage = computed(() => {
  const count = unpaidInvoices.value.length
  return count === 1
    ? t('client.dashboard.unpaidSingle')
    : t('client.dashboard.unpaidMultiple', { count })
})

const stats = computed(() => [
  {
    label: t('client.dashboard.statActiveServices'),
    value: store.activeServiceCount,
    icon: Server,
    iconColor: 'text-cyan-400',
    sub: null,
    to: '/client/services'
  },
  {
    label: t('client.dashboard.statDomains'),
    value: store.domains.length,
    icon: Globe,
    iconColor: 'text-primary-400',
    sub: null,
    to: '/client/domains'
  },
  {
    label: t('client.dashboard.statUnpaidInvoices'),
    value: store.unpaidCount,
    icon: FileText,
    iconColor: store.unpaidCount > 0 ? 'text-yellow-400' : 'text-gray-400',
    sub: store.unpaidCount > 0 ? t('client.dashboard.statActionRequired') : null,
    to: '/client/invoices'
  },
  {
    label: t('client.dashboard.statOpenTickets'),
    value: store.openTicketCount,
    icon: MessageSquare,
    iconColor: 'text-secondary-400',
    sub: null,
    to: '/client/tickets'
  }
])

/** Register / Transfer domain */
const domainQuery = ref('')

function registerDomain() {
  const q = domainQuery.value.trim()
  navigateTo(q ? `/domains?search=${encodeURIComponent(q)}` : '/domains')
}

function transferDomain() {
  const q = domainQuery.value.trim()
  if (q && whmcsUrl) {
    window.open(`${whmcsUrl}/cart.php?a=add&domain=transfer&query=${encodeURIComponent(q)}`, '_blank')
  } else {
    navigateTo('/domains')
  }
}

/** cPanel SSO from Dashboard */
const ssoLoading = ref<number | null>(null)
async function loginToCpanel(service: any) {
  if (ssoLoading.value) return
  ssoLoading.value = service.id
  try {
    const { url } = await apiFetch<{ url: string }>(`/api/portal/client/services/${service.id}/cpanel-sso`)
    window.open(url, '_blank', 'noopener')
  } catch (err: any) {
    // Fallback: open plain cPanel login page if SSO fails
    if (service.serverhostname) {
      window.open(`https://${service.serverhostname}:2083`, '_blank', 'noopener')
    }
  } finally {
    ssoLoading.value = null
  }
}
</script>
