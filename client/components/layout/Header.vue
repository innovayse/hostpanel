<template>
  <header
    :class="headerClasses"
    class="fixed top-0 left-0 right-0 z-40 transition-all duration-300"
  >
    <div class="container-custom">
      <div class="flex items-center justify-between h-16 lg:h-20">
        <!-- Logo -->
        <NuxtLink :to="localePath('/')" class="flex items-center">
          <NuxtImg
            src="/logo.svg"
            alt="Innovayse"
            width="200"
            height="60"
            format="webp"
            quality="90"
            loading="eager"
            class="h-10 sm:h-12 lg:h-14 xl:h-16 w-auto transition-transform hover:scale-105"
          />
        </NuxtLink>

        <!-- Desktop Navigation (1280px+) -->
        <nav class="hidden xl:flex items-center space-x-6 lg:space-x-8">
          <template v-for="link in navLinks" :key="link.href ?? link.label">
            <!-- Regular link -->
            <NuxtLink
              v-if="!link.children"
              :to="link.href"
              class="text-sm lg:text-base text-gray-300 hover:text-primary-400 font-medium transition-colors"
            >
              {{ link.label }}
            </NuxtLink>

            <!-- Dropdown link -->
            <div v-else class="relative group">
              <button
                type="button"
                class="flex items-center gap-1 text-sm lg:text-base text-gray-300 hover:text-primary-400 font-medium transition-colors py-1"
              >
                {{ link.label }}
                <Icon name="lucide:chevron-down" size="14" class="transition-transform duration-200 group-hover:rotate-180" />
              </button>

              <!-- Dropdown panel -->
              <div class="absolute left-1/2 -translate-x-1/2 top-full pt-2 opacity-0 invisible group-hover:opacity-100 group-hover:visible transition-all duration-200">
                <div class="w-52 rounded-xl bg-gray-900/95 border border-white/10 shadow-2xl backdrop-blur-lg overflow-hidden">
                  <NuxtLink
                    v-for="child in link.children"
                    :key="child.href"
                    :to="child.href"
                    class="flex items-center gap-3 px-4 py-3 text-sm text-gray-300 hover:text-white hover:bg-white/8 transition-colors"
                  >
                    <Icon v-if="child.icon" :name="NAV_ICONS[child.icon]" size="16" class="text-primary-400 flex-shrink-0" />
                    <div>
                      <div class="font-medium">{{ child.label }}</div>
                      <div v-if="child.desc" class="text-xs text-gray-500 mt-0.5">{{ child.desc }}</div>
                    </div>
                  </NuxtLink>
                </div>
              </div>
            </div>
          </template>
        </nav>

        <!-- Language Switcher & CTA (Desktop 1280px+) -->
        <div class="hidden xl:flex items-center gap-3">
          <!-- Global App Launcher widget mount point -->
          <div id="inno-launcher-mount"></div>

          <LayoutLanguageSwitcher />

          <!-- Cart icon with badge -->
          <button
            type="button"
            class="relative flex items-center justify-center w-9 h-9 rounded-lg text-gray-300 hover:text-white border border-white/10 hover:border-white/20 transition-all duration-200"
            :aria-label="$t('cart.title')"
            @click="cartStore.open()"
          >
            <Icon name="lucide:shopping-cart" size="16" />
            <ClientOnly>
              <span
                v-if="cartStore.count > 0"
                class="absolute -top-1.5 -right-1.5 min-w-[18px] h-[18px] px-1 rounded-full bg-cyan-500 text-white text-[10px] font-bold flex items-center justify-center"
              >
                {{ cartStore.count }}
              </span>
            </ClientOnly>
          </button>

          <!-- Global Account widget mount point (App Launcher widget injects here) -->
          <div id="inno-account-mount"></div>

        </div>

        <!-- Mobile: cart + burger grouped on the right -->
        <div class="xl:hidden flex items-center gap-2">
          <button
            type="button"
            class="relative flex items-center justify-center w-9 h-9 rounded-lg text-gray-300 hover:text-white border border-white/10 hover:border-white/20 transition-all duration-200"
            :aria-label="$t('cart.title')"
            @click="cartStore.open()"
          >
            <Icon name="lucide:shopping-cart" size="16" />
            <ClientOnly>
              <span
                v-if="cartStore.count > 0"
                class="absolute -top-1.5 -right-1.5 min-w-[18px] h-[18px] px-1 rounded-full bg-cyan-500 text-white text-[10px] font-bold flex items-center justify-center"
              >
                {{ cartStore.count }}
              </span>
            </ClientOnly>
          </button>

          <button
            type="button"
            class="text-gray-300 hover:text-primary-400 transition-colors"
            @click="toggleMobileMenu"
          >
            <Icon v-if="mobileMenuOpen" name="lucide:x" size="24" class="sm:w-6 sm:h-6" />
            <Icon v-else name="lucide:menu" size="24" class="sm:w-6 sm:h-6" />
          </button>
        </div><!-- end mobile right group -->
      </div>
    </div>

    <!-- Mobile Navigation (below 1280px) -->
    <Transition name="mobile-menu">
      <div
        v-if="mobileMenuOpen"
        class="xl:hidden border-t border-gray-800 bg-gray-900/95 backdrop-blur-lg"
      >
        <nav class="container-custom py-3 sm:py-4 space-y-1.5 sm:space-y-2">
          <template v-for="link in navLinks" :key="link.href ?? link.label">
            <!-- Regular mobile link -->
            <NuxtLink
              v-if="!link.children"
              :to="link.href"
              class="block py-2.5 sm:py-3 px-3 sm:px-4 rounded-lg text-sm sm:text-base text-gray-300 hover:bg-gray-800 hover:text-primary-400 font-medium transition-colors"
              @click="closeMobileMenu"
            >
              {{ link.label }}
            </NuxtLink>

            <!-- Mobile expandable group -->
            <div v-else>
              <button
                type="button"
                class="w-full flex items-center justify-between py-2.5 sm:py-3 px-3 sm:px-4 rounded-lg text-sm sm:text-base text-gray-300 hover:bg-gray-800 hover:text-primary-400 font-medium transition-colors"
                @click="toggleMobileGroup(link.label)"
              >
                {{ link.label }}
                <Icon
                  name="lucide:chevron-down"
                  size="16"
                  class="transition-transform duration-200"
                  :class="expandedMobileGroups.includes(link.label) ? 'rotate-180' : ''"
                />
              </button>
              <!-- Sub-items -->
              <div v-if="expandedMobileGroups.includes(link.label)" class="pl-4 space-y-1 mt-1">
                <NuxtLink
                  v-for="child in link.children"
                  :key="child.href"
                  :to="child.href"
                  class="flex items-center gap-2 py-2 px-3 rounded-lg text-sm text-gray-400 hover:bg-gray-800 hover:text-primary-400 transition-colors"
                  @click="closeMobileMenu"
                >
                  <Icon v-if="child.icon" :name="NAV_ICONS[child.icon]" size="14" class="text-primary-400 flex-shrink-0" />
                  {{ child.label }}
                </NuxtLink>
              </div>
            </div>
          </template>

          <!-- Client area mobile -->
          <!-- Logged in: expandable group -->
          <div v-if="isLoggedIn">
            <button
              type="button"
              class="w-full flex items-center justify-between py-2.5 sm:py-3 px-3 sm:px-4 rounded-lg text-sm sm:text-base text-gray-300 hover:bg-gray-800 hover:text-primary-400 font-medium transition-colors"
              @click="toggleMobileGroup('clientArea')"
            >
              <div class="flex items-center gap-2">
                <span class="flex items-center justify-center w-6 h-6 rounded-full bg-primary-500/20 text-primary-400 text-xs font-bold">
                  {{ user?.firstname?.charAt(0)?.toUpperCase() ?? 'U' }}
                </span>
                {{ user?.firstname ?? $t('nav.clientArea') }}
              </div>
              <Icon
                name="lucide:chevron-down"
                size="16"
                class="transition-transform duration-200"
                :class="expandedMobileGroups.includes('clientArea') ? 'rotate-180' : ''"
              />
            </button>
            <div v-if="expandedMobileGroups.includes('clientArea')" class="pl-4 space-y-1 mt-1">
              <NuxtLink :to="localePath('/client/dashboard')" class="flex items-center gap-2 py-2 px-3 rounded-lg text-sm text-gray-400 hover:bg-gray-800 hover:text-primary-400 transition-colors" @click="closeMobileMenu">
                <Icon name="lucide:layout-dashboard" size="14" class="text-primary-400 flex-shrink-0" />
                {{ $t('client.nav.dashboard') }}
              </NuxtLink>
              <NuxtLink :to="localePath('/client/services')" class="flex items-center gap-2 py-2 px-3 rounded-lg text-sm text-gray-400 hover:bg-gray-800 hover:text-primary-400 transition-colors" @click="closeMobileMenu">
                <Icon name="lucide:server" size="14" class="text-primary-400 flex-shrink-0" />
                {{ $t('client.nav.services') }}
              </NuxtLink>
              <NuxtLink :to="localePath('/client/domains')" class="flex items-center gap-2 py-2 px-3 rounded-lg text-sm text-gray-400 hover:bg-gray-800 hover:text-primary-400 transition-colors" @click="closeMobileMenu">
                <Icon name="lucide:globe" size="14" class="text-primary-400 flex-shrink-0" />
                {{ $t('client.nav.domains') }}
              </NuxtLink>
              <NuxtLink :to="localePath('/client/invoices')" class="flex items-center gap-2 py-2 px-3 rounded-lg text-sm text-gray-400 hover:bg-gray-800 hover:text-primary-400 transition-colors" @click="closeMobileMenu">
                <Icon name="lucide:file-text" size="14" class="text-primary-400 flex-shrink-0" />
                {{ $t('client.nav.invoices') }}
              </NuxtLink>
              <NuxtLink :to="localePath('/client/tickets')" class="flex items-center gap-2 py-2 px-3 rounded-lg text-sm text-gray-400 hover:bg-gray-800 hover:text-primary-400 transition-colors" @click="closeMobileMenu">
                <Icon name="lucide:message-square" size="14" class="text-primary-400 flex-shrink-0" />
                {{ $t('client.nav.support') }}
              </NuxtLink>
              <NuxtLink :to="localePath('/client/account')" class="flex items-center gap-2 py-2 px-3 rounded-lg text-sm text-gray-400 hover:bg-gray-800 hover:text-primary-400 transition-colors" @click="closeMobileMenu">
                <Icon name="lucide:settings" size="14" class="text-primary-400 flex-shrink-0" />
                {{ $t('client.nav.account') }}
              </NuxtLink>
              <button
                type="button"
                class="w-full flex items-center gap-2 py-2 px-3 rounded-lg text-sm text-red-400 hover:bg-gray-800 hover:text-red-300 transition-colors"
                @click="handleLogout"
              >
                <Icon name="lucide:log-out" size="14" class="flex-shrink-0" />
                {{ $t('client.nav.signOut') }}
              </button>
            </div>
          </div>

          <!-- Not logged in: simple link (SSO mode goes directly to SSO) -->
          <a
            v-else
            :href="clientAreaHref"
            class="flex items-center gap-2 py-2.5 sm:py-3 px-3 sm:px-4 rounded-lg text-sm sm:text-base text-gray-300 hover:bg-gray-800 hover:text-primary-400 font-medium transition-colors"
            @click="closeMobileMenu"
          >
            <Icon name="lucide:user" size="16" />
            {{ $t('nav.clientArea') }}
          </a>

          <!-- Language Switcher in Mobile -->
          <div class="pt-2 pb-2 px-3 sm:px-4">
            <LayoutLanguageSwitcher />
          </div>

        </nav>
      </div>
    </Transition>
  </header>

  <!-- Spacer to prevent content from going under fixed header -->
  <div class="h-16 lg:h-20" />
</template>

<script setup lang="ts">
/**
 * Header component with logo, navigation, language switcher, and CTA button.
 *
 * Features:
 * - Sticky header with blur effect on scroll
 * - Dropdown submenus for grouped navigation items (e.g. Hosting)
 * - Mobile burger menu with expandable groups
 * - Client area login/dashboard button (state-aware)
 */

import { useCartStore } from '~/stores/cart'

const { t } = useI18n()
const localePath = useLocalePath()

/**
 * Mapping for dynamic icons.
 * Uses string names for @nuxt/icon component.
 */
const NAV_ICONS: Record<string, string> = {
  server: 'lucide:server',
  globe: 'lucide:globe'
}

/**
 * Navigation links.
 * Items with `children` render as a dropdown (desktop) / expandable group (mobile).
 */
const navLinks = computed(() => [
  {
    label: t('nav.hosting'),
    children: [
      {
        href: localePath('/hosting'),
        label: t('nav.hosting'),
        desc: 'Shared, VPS and dedicated plans',
        icon: 'server'
      },
      {
        href: localePath('/domains'),
        label: t('nav.domains'),
        desc: 'Search & register domain names',
        icon: 'globe'
      },
    ]
  },
  { label: t('nav.products'), href: localePath('/products') }
])

/** Authentication state for the Client Area button */
const authStore = useAuthStore()
// storeToRefs, not a plain destructure: state and getters lose their reactivity when pulled
// off a store by hand, and the template above binds them.
const { isLoggedIn, user } = storeToRefs(authStore)
const { fetchUser, logout } = authStore
const runtimeConfig = useRuntimeConfig()

/** In SSO mode, link directly to SSO authorize (skips "Continue with Innovayse" page) */
const clientAreaHref = computed(() =>
  runtimeConfig.public.authMode === 'sso'
    ? '/api/portal/auth/sso/authorize'
    : localePath('/client/login')
)

onMounted(async () => {
  if (isLoggedIn.value) {
    await fetchUser()
  }

  // Reinit the widget so it picks up any accounts saved by the callback flow
  const w = window as unknown as { InnoWidget?: { reinit: () => void } }
  w.InnoWidget?.reinit?.()
})

async function handleLogout() {
  await logout()
  closeMobileMenu()
  navigateTo(localePath('/client/login'))
}

/** Cart store — provides item count for badge */
const cartStore = useCartStore()
onMounted(() => cartStore.init())

/** Mobile menu open/close state */
const mobileMenuOpen = ref(false)

/** Which mobile nav groups are currently expanded */
const expandedMobileGroups = ref<string[]>([])

/** Toggle a specific mobile nav group open/closed */
function toggleMobileGroup(label: string) {
  const idx = expandedMobileGroups.value.indexOf(label)
  if (idx === -1) {
    expandedMobileGroups.value.push(label)
  } else {
    expandedMobileGroups.value.splice(idx, 1)
  }
}

/** Scroll Y position for header blur effect */
const scrollY = ref(0)

/** Toggle mobile menu open/closed, locking body scroll when open */
const toggleMobileMenu = () => {
  mobileMenuOpen.value = !mobileMenuOpen.value
  if (typeof document !== 'undefined') {
    document.body.style.overflow = mobileMenuOpen.value ? 'hidden' : ''
  }
}

/** Close mobile menu and re-enable body scroll */
const closeMobileMenu = () => {
  mobileMenuOpen.value = false
  expandedMobileGroups.value = []
  if (typeof document !== 'undefined') {
    document.body.style.overflow = ''
  }
}

/** Update scrollY on scroll */
const handleScroll = () => {
  scrollY.value = window.scrollY
}

/** Header background classes — blurred when scrolled down */
const headerClasses = computed(() => {
  if (scrollY.value > 50) {
    return 'bg-gray-900/80 backdrop-blur-lg shadow-md border-b border-gray-800'
  }
  return 'bg-gray-900 border-b border-gray-800'
})

onMounted(() => {
  window.addEventListener('scroll', handleScroll)
})

onUnmounted(() => {
  window.removeEventListener('scroll', handleScroll)
  if (typeof document !== 'undefined') {
    document.body.style.overflow = ''
  }
})

// Close mobile menu on route change
const route = useRoute()
watch(() => route.path, () => {
  closeMobileMenu()
})
</script>

<style scoped>
/* Mobile menu slide-down transition */
.mobile-menu-enter-active,
.mobile-menu-leave-active {
  transition: all 0.3s ease;
  overflow: hidden;
}

.mobile-menu-enter-from,
.mobile-menu-leave-to {
  max-height: 0;
  opacity: 0;
}

.mobile-menu-enter-to,
.mobile-menu-leave-from {
  max-height: 600px;
  opacity: 1;
}
</style>
