<template>
  <!-- Full viewport, pinned to viewport — prevents any browser-level scroll -->
  <div class="fixed inset-0 overflow-hidden bg-gray-50 dark:bg-[#0a0a0f] flex flex-col">

    <!-- Mobile top bar — fixed at top, shrinks flex column -->
    <header class="lg:hidden flex-shrink-0 flex items-center justify-between px-4 py-3 border-b border-gray-200 dark:border-white/10 bg-white dark:bg-[#0d0d12] z-30">
      <NuxtLink to="/" class="text-gray-900 dark:text-white font-bold text-lg">Innovayse</NuxtLink>
      <div class="flex items-center gap-3">
        <span class="text-sm text-gray-500 dark:text-gray-400 truncate max-w-[120px]">{{ store.fullName }}</span>
        <button class="p-2 text-gray-500 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white" @click="sidebarOpen = !sidebarOpen">
          <Menu v-if="!sidebarOpen" :size="22" :stroke-width="2" />
          <X v-else :size="22" :stroke-width="2" />
        </button>
      </div>
    </header>

    <!-- Body row: sidebar + main — fills remaining height, no overflow -->
    <div class="flex flex-1 overflow-hidden">

      <!-- Sidebar
           Mobile: fixed overlay (slides in/out)
           Desktop: normal flex child, fills height, internal scroll -->
      <aside
        class="fixed inset-y-0 left-0 z-20 w-64 flex flex-col bg-white dark:bg-[#0d0d12] border-r border-gray-200 dark:border-white/10 transition-transform duration-300 lg:relative lg:z-auto lg:translate-x-0 lg:flex-shrink-0"
        :class="sidebarOpen ? 'translate-x-0' : '-translate-x-full'"
      >
        <!-- Logo -->
        <div class="px-6 py-5 border-b border-gray-200 dark:border-white/10 flex items-center justify-between flex-shrink-0">
          <NuxtLink to="/" class="flex items-center gap-2">
            <span class="text-gray-900 dark:text-white font-bold text-lg">Innovayse</span>
          </NuxtLink>
          <button class="lg:hidden text-gray-500 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white" @click="sidebarOpen = false">
            <X :size="18" :stroke-width="2" />
          </button>
        </div>

        <!-- User info -->
        <div class="px-6 py-4 border-b border-gray-200 dark:border-white/10 flex-shrink-0">
          <div class="flex items-center gap-3">
            <div class="w-9 h-9 rounded-full bg-gradient-to-br from-cyan-500/30 to-primary-500/20 flex items-center justify-center text-cyan-700 dark:text-white font-bold text-sm border border-cyan-500/30">
              {{ store.userInitial }}
            </div>
            <div class="flex-1 min-w-0">
              <div class="text-gray-900 dark:text-white font-medium text-sm truncate">{{ store.fullName || $t('client.nav.dashboard') }}</div>
              <div class="text-gray-500 text-xs truncate">{{ store.user?.email }}</div>
            </div>
          </div>
        </div>

        <!-- Navigation — scrollable if nav items overflow -->
        <nav class="flex-1 px-3 py-4 space-y-1 overflow-y-auto">
          <NuxtLink
            v-for="item in visibleNavItems"
            :key="item.to"
            :to="item.to"
            class="flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-all duration-200 group"
            :class="isActive(item.to)
              ? 'bg-cyan-500/10 text-cyan-600 dark:text-cyan-400 border border-cyan-500/20'
              : 'text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white hover:bg-gray-100 dark:hover:bg-white/5'"
            @click="sidebarOpen = false"
          >
            <component
              :is="item.icon"
              :size="18"
              :stroke-width="2"
              :class="isActive(item.to) ? 'text-cyan-600 dark:text-cyan-400' : 'text-gray-500 dark:text-gray-500 group-hover:text-gray-700 dark:group-hover:text-gray-300'"
            />
            {{ item.label }}
          </NuxtLink>
        </nav>

        <!-- Logout — always at bottom -->
        <div class="px-3 py-3 flex-shrink-0 border-t border-gray-100 dark:border-white/5">
          <button
            class="w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium text-gray-500 dark:text-gray-400 hover:text-red-500 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-all duration-200"
            @click="handleLogout"
          >
            <LogOut :size="18" :stroke-width="2" />
            {{ $t('client.nav.signOut') }}
          </button>
        </div>
      </aside>

      <!-- Mobile overlay backdrop -->
      <Transition name="fade">
        <div
          v-if="sidebarOpen"
          class="fixed inset-0 z-10 bg-black/50 lg:hidden"
          @click="sidebarOpen = false"
        />
      </Transition>

      <!-- Main — only this scrolls -->
      <main class="flex-1 min-w-0 overflow-y-auto flex flex-col">

        <!-- Desktop top bar — sticky inside the scrollable main -->
        <div class="hidden lg:flex flex-shrink-0 items-center justify-between px-8 py-4 border-b border-gray-200 dark:border-white/10 bg-white dark:bg-[#0d0d12] sticky top-0 z-10">
          <div class="text-gray-500 dark:text-gray-400 text-sm">
            {{ $t('client.nav.welcomeBack') }} <span class="text-gray-900 dark:text-white font-medium">{{ store.fullName }}</span>
          </div>
          <div class="flex items-center gap-3">
            <UiThemeToggle />
            <UiLanguageSwitcher />
            <button
              class="flex items-center gap-2 px-4 py-2 rounded-lg text-sm text-gray-500 dark:text-gray-400 hover:text-red-500 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 transition-all duration-200 border border-gray-200 dark:border-white/10 hover:border-red-200 dark:hover:border-red-500/20"
              @click="handleLogout"
            >
              <LogOut :size="16" :stroke-width="2" />
              {{ $t('client.nav.signOut') }}
            </button>
          </div>
        </div>

        <!-- Page content -->
        <div class="p-6 lg:p-8">
          <Suspense>
            <template #default>
              <slot />
            </template>
            <template #fallback>
              <div class="flex items-center justify-center min-h-[40vh]">
                <div class="w-10 h-10 border-4 border-cyan-500/20 border-t-cyan-500 rounded-full animate-spin" />
              </div>
            </template>
          </Suspense>
        </div>
      </main>

    </div>
  </div>
</template>

<script setup lang="ts">
import {
  LayoutDashboard,
  Server,
  Globe,
  FileText,
  MessageSquare,
  UserCog,
  Megaphone,
  BookOpen,
  LogOut,
  Menu,
  X
} from 'lucide-vue-next'
import { useClientStore } from '~/stores/client'
import { Permission } from '~/composables/usePermissions'

const route = useRoute()
const { t } = useI18n()
const { logout } = useClientAuth()
const { hasPermission } = usePermissions()
const store = useClientStore()
const { init } = useAppColorMode()

const sidebarOpen = ref(false)

// Apply saved color preference when entering client area; restore dark on leave
onMounted(async () => {
  init()
  if (!store.userLoaded) await store.fetchUser()
})
onUnmounted(() => {
  document.documentElement.classList.add('dark')
  document.documentElement.classList.remove('light')
})

const navItems = computed(() => [
  { to: '/client/dashboard', label: t('client.nav.dashboard'), icon: LayoutDashboard, permission: Permission.None },
  { to: '/client/services',  label: t('client.nav.services'),  icon: Server,          permission: Permission.ViewProductsServices },
  { to: '/client/domains',   label: t('client.nav.domains'),   icon: Globe,           permission: Permission.ViewDomains },
  { to: '/client/invoices',  label: t('client.nav.invoices'),  icon: FileText,        permission: Permission.ViewPayInvoices },
  { to: '/client/tickets',      label: t('client.nav.support'),        icon: MessageSquare, permission: Permission.ViewOpenSupportTickets },
  { to: '/announcements',       label: t('announcements.title'),       icon: Megaphone,     permission: Permission.None },
  { to: '/knowledgebase',       label: t('kb.title'),                  icon: BookOpen,      permission: Permission.None },
  { to: '/client/account',      label: t('client.nav.account'),        icon: UserCog,       permission: Permission.None }
])

/** Nav items filtered by the current user's permissions. */
const visibleNavItems = computed(() =>
  navItems.value.filter(item => hasPermission(item.permission))
)

function isActive(path: string) {
  return route.path === path || route.path.startsWith(path + '/')
}

async function handleLogout() {
  await logout()
  store.reset()
  navigateTo('/client/login')
}
</script>

<style scoped>
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
