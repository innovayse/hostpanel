<script setup lang="ts">
/**
 * Admin top navigation bar.
 *
 * Shows current page title, search, notifications, and user menu.
 * On mobile emits toggle-sidebar to open the drawer.
 */
import { ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { SUPPORTED_LOCALES, setLocale, type SupportedLocale } from '../../i18n'
import { useAuthStore } from '../../modules/auth/stores/authStore'

/** Emitted when the hamburger button is clicked on mobile. */
const emit = defineEmits<{
  /** Emitted to toggle the sidebar drawer on mobile/tablet. */
  'toggle-sidebar': []
}>()

const route = useRoute()
const { t, locale } = useI18n()

/** Maps route paths to i18n keys for the page title. */
const titleKeys: Record<string, string> = {
  '/dashboard':   'nav.dashboard',
  '/clients':     'nav.clients',
  '/billing':     'nav.billing',
  '/services':    'nav.servicesTitle',
  '/domains':     'nav.domains',
  '/support':     'nav.support',
  '/plugins':     'nav.pluginManager',
  '/servers':     'nav.servers',
  '/integrations':'nav.integrations',
  '/settings':    'nav.settings',
  '/reports':     'nav.reports',
  '/orders':      'nav.orders',
}

/** Current page title derived from the active route. */
const pageTitle = computed(() => {
  const match = Object.entries(titleKeys).find(([k]) => route.path === k || route.path.startsWith(k + '/'))
  return match ? t(match[1]) : t('common.admin')
})

/** Controls the language menu visibility. */
const showLanguageMenu = ref(false)

/** Display labels for the language switcher button/menu. */
const localeLabels: Record<SupportedLocale, string> = { en: 'EN', hy: 'ՀՅ', ru: 'РУ' }

/**
 * Switches the active locale and closes the menu.
 *
 * @param code - The locale to switch to.
 */
function chooseLocale(code: SupportedLocale): void {
  setLocale(code)
  showLanguageMenu.value = false
}

/** Controls the notification popover visibility. */
const showNotifications = ref(false)

/** Mock notification count — will come from store later. */
const notificationCount = ref(3)

/** Toggles notification dropdown. */
function toggleNotifications(): void {
  showNotifications.value = !showNotifications.value
}

const router = useRouter()
const auth = useAuthStore()

/** Controls the account menu visibility. */
const showAccountMenu = ref(false)

/**
 * The signed-in operator's address, or a neutral label before the profile has loaded.
 *
 * The header used to read "Admin" for everyone, spelled into the markup — so an operator could
 * not tell which account they were acting as, which matters on a panel where several people
 * share one screen.
 */
const accountLabel = computed(() => auth.user?.email ?? t('common.admin'))

/** First letter of the address, for the avatar tile. Empty while there is no profile yet. */
const accountInitial = computed(() => auth.user?.email?.[0]?.toUpperCase() ?? '?')

/** Opens or closes the account menu. */
function toggleAccountMenu(): void {
  showAccountMenu.value = !showAccountMenu.value
}

/**
 * Ends the session and returns to the sign-in screen.
 *
 * Closes the menu first: the sign-out navigates away, and a menu left open is still painted
 * over the login screen for the moment the router takes to swap the view.
 */
async function signOut(): Promise<void> {
  showAccountMenu.value = false
  await auth.logout()
  await router.push('/login')
}
</script>

<template>
  <header class="flex items-center justify-between h-14 px-4 lg:px-6 border-b border-border bg-surface-panel shrink-0">

    <!-- Left: hamburger + page title -->
    <div class="flex items-center gap-3">
      <!-- Hamburger — visible on mobile/tablet -->
      <button
        class="flex lg:hidden items-center justify-center w-8 h-8 rounded-lg text-text-secondary hover:text-text-primary hover:bg-white/[0.05] transition-all"
        @click="emit('toggle-sidebar')"
        :aria-label="t('common.toggleSidebar')"
      >
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round">
          <line x1="3" y1="6" x2="21" y2="6"/>
          <line x1="3" y1="12" x2="21" y2="12"/>
          <line x1="3" y1="18" x2="21" y2="18"/>
        </svg>
      </button>

      <!-- Page title -->
      <h2 class="font-display font-semibold text-[0.95rem] text-text-primary tracking-tight">
        {{ pageTitle }}
      </h2>
    </div>

    <!-- Right: search + notifications + avatar -->
    <div class="flex items-center gap-2">

      <!-- Search -->
      <div class="hidden md:flex items-center gap-2 h-8 px-3 rounded-lg bg-white/[0.04] border border-border text-text-muted text-[0.8rem] w-52 cursor-pointer hover:border-primary-500/30 hover:bg-primary-500/[0.03] transition-all group">
        <svg class="w-3.5 h-3.5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round">
          <circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/>
        </svg>
        <span class="flex-1">{{ t('common.search') }}</span>
        <kbd class="text-[0.65rem] px-1 py-0.5 rounded bg-white/[0.06] border border-border font-mono text-text-muted group-hover:border-primary-500/20">⌘K</kbd>
      </div>

      <!-- Language switcher -->
      <div class="relative">
        <button
          class="flex items-center gap-1 h-8 px-2.5 rounded-lg text-text-secondary text-[0.8rem] font-medium hover:text-text-primary hover:bg-white/[0.05] transition-all"
          @click="showLanguageMenu = !showLanguageMenu"
        >
          {{ localeLabels[locale as SupportedLocale] }}
        </button>

        <div
          v-if="showLanguageMenu"
          class="absolute right-0 top-full mt-2 w-28 bg-surface-elevated border border-border rounded-xl shadow-2xl z-50 overflow-hidden"
        >
          <button
            v-for="code in SUPPORTED_LOCALES"
            :key="code"
            class="flex w-full items-center px-3 py-2 text-[0.8rem] text-left transition-colors"
            :class="locale === code ? 'text-primary-400 bg-primary-500/8' : 'text-text-secondary hover:text-text-primary hover:bg-white/[0.04]'"
            @click="chooseLocale(code)"
          >
            {{ localeLabels[code] }}
          </button>
        </div>

        <div v-if="showLanguageMenu" class="fixed inset-0 z-40" @click="showLanguageMenu = false" />
      </div>

      <!-- Notifications -->
      <div class="relative">
        <button
          class="relative flex items-center justify-center w-8 h-8 rounded-lg text-text-secondary hover:text-text-primary hover:bg-white/[0.05] transition-all"
          @click="toggleNotifications"
          :aria-label="t('common.notifications')"
        >
          <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round">
            <path d="M18 8A6 6 0 006 8c0 7-3 9-3 9h18s-3-2-3-9M13.73 21a2 2 0 01-3.46 0"/>
          </svg>
          <!-- Badge -->
          <span
            v-if="notificationCount > 0"
            class="absolute top-1 right-1 w-[7px] h-[7px] rounded-full gradient-brand"
          />
        </button>

        <!-- Dropdown -->
        <div
          v-if="showNotifications"
          class="absolute right-0 top-full mt-2 w-72 bg-surface-elevated border border-border rounded-xl shadow-2xl z-50 overflow-hidden"
        >
          <div class="flex items-center justify-between px-4 py-3 border-b border-border">
            <span class="text-[0.8rem] font-semibold font-display text-text-primary">{{ t('common.notifications') }}</span>
            <span class="text-[0.65rem] font-medium text-primary-400 bg-primary-500/10 border border-primary-500/20 rounded-full px-2 py-0.5">{{ t('common.newCount', { count: notificationCount }) }}</span>
          </div>
          <div class="divide-y divide-border">
            <div class="px-4 py-3 hover:bg-white/[0.03] transition-colors cursor-pointer">
              <p class="text-[0.8rem] text-text-primary mb-0.5">New client registered</p>
              <p class="text-[0.72rem] text-text-muted">2 minutes ago</p>
            </div>
            <div class="px-4 py-3 hover:bg-white/[0.03] transition-colors cursor-pointer">
              <p class="text-[0.8rem] text-text-primary mb-0.5">Invoice #1042 overdue</p>
              <p class="text-[0.72rem] text-text-muted">1 hour ago</p>
            </div>
            <div class="px-4 py-3 hover:bg-white/[0.03] transition-colors cursor-pointer">
              <p class="text-[0.8rem] text-text-primary mb-0.5">Domain expiring soon</p>
              <p class="text-[0.72rem] text-text-muted">3 hours ago</p>
            </div>
          </div>
          <div class="px-4 py-2.5 border-t border-border">
            <button class="text-[0.75rem] text-primary-400 hover:text-primary-300 transition-colors">{{ t('common.viewAllNotifications') }}</button>
          </div>
        </div>

        <!-- Backdrop to close -->
        <div v-if="showNotifications" class="fixed inset-0 z-40" @click="showNotifications = false" />
      </div>

      <!-- Divider -->
      <div class="w-px h-5 bg-border mx-1" />

      <!-- User avatar -->
      <div class="relative">
        <!--
          A button, not a div. It carried a chevron and a pointer cursor but no handler at all,
          so it looked like a menu and did nothing when pressed — and being a div, the keyboard
          could not reach it either.
        -->
        <button
          type="button"
          class="flex items-center gap-2 cursor-pointer group"
          :aria-expanded="showAccountMenu"
          aria-haspopup="menu"
          :aria-label="accountLabel"
          @click="toggleAccountMenu"
        >
          <div class="flex items-center justify-center w-7 h-7 rounded-lg gradient-brand text-white text-[0.7rem] font-bold font-display shrink-0">
            {{ accountInitial }}
          </div>
          <span class="hidden sm:block max-w-[12rem] truncate text-[0.8rem] font-medium text-text-secondary group-hover:text-text-primary transition-colors">{{ accountLabel }}</span>
          <svg class="hidden sm:block w-3.5 h-3.5 text-text-muted transition-transform" :class="{ 'rotate-180': showAccountMenu }" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <polyline points="6 9 12 15 18 9"/>
          </svg>
        </button>

        <!-- Dropdown -->
        <div
          v-if="showAccountMenu"
          role="menu"
          class="absolute right-0 top-full mt-2 w-60 bg-surface-elevated border border-border rounded-xl shadow-2xl z-50 overflow-hidden"
        >
          <div class="px-4 py-3 border-b border-border">
            <p class="text-[0.8rem] font-semibold font-display text-text-primary truncate">{{ accountLabel }}</p>
            <p v-if="auth.user?.roles?.length" class="text-[0.72rem] text-text-muted truncate">{{ auth.user.roles.join(', ') }}</p>
          </div>
          <div class="py-1">
            <RouterLink
              to="/settings"
              role="menuitem"
              class="block px-4 py-2 text-[0.8rem] text-text-secondary hover:bg-white/[0.03] hover:text-text-primary transition-colors"
              @click="showAccountMenu = false"
            >
              {{ t('nav.settings') }}
            </RouterLink>
            <button
              type="button"
              role="menuitem"
              class="w-full text-left px-4 py-2 text-[0.8rem] text-red-400 hover:bg-red-500/10 transition-colors"
              @click="signOut"
            >
              {{ t('common.signOut') }}
            </button>
          </div>
        </div>

        <!-- Backdrop to close -->
        <div v-if="showAccountMenu" class="fixed inset-0 z-40" @click="showAccountMenu = false" />
      </div>

    </div>
  </header>
</template>
