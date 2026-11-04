<template>
  <!-- One header for mobile and desktop, so the widget mount ids exist once -->
  <header
    class="z-30 flex flex-shrink-0 items-center justify-between border-b border-gray-200 bg-ui-surface px-4 py-3 dark:border-white/10 lg:px-8 lg:py-4"
    data-testid="client-topbar"
  >
    <!-- Left: the brand, the same lockup the storefront header draws, at every width -->
    <div class="mr-3 min-w-0">
      <UiLink to="/" tone="inherit" :underline="false" testid="client-topbar-brand" class="flex items-center gap-2">
        <LogoLockup img-class="h-8 w-auto max-w-[160px] object-contain" mark-class="h-8 w-8" name-class="text-lg font-bold" />
      </UiLink>
    </div>

    <!-- Right: welcome text on desktop, then the controls -->
    <div class="flex min-w-0 flex-shrink-0 items-center gap-2 lg:gap-3">
      <UiText as="span" size="sm" tone="muted" truncate class="hidden max-w-[16rem] lg:block">
        {{ $t('client.nav.welcomeBack') }}
        <UiText as="span" size="sm" weight="medium" data-testid="client-topbar-name">{{ fullName }}</UiText>
      </UiText>
      <!-- Desktop only: theme toggle, language switcher, app launcher -->
      <div class="hidden items-center gap-3 lg:flex">
        <UiThemeToggle />
        <UiLanguageSwitcher />
        <!-- data-user hands the signed-in person to the widget so its launcher appends
             ?authuser= to every tile from the first paint, instead of only after its own
             profile round-trip. The widget reads it from either mount; this is the one
             present at every width. -->
        <div v-if="widgetEnabled" id="inno-launcher-mount" :data-user="widgetUser" />
      </div>
      <!-- Account control: always visible, mobile needs account switching too. The platform widget
           fills the mount where the shell serves it; everywhere else the local menu is drawn, so
           the header is never left without an avatar. -->
      <div v-if="widgetEnabled" id="inno-account-mount" :data-user="widgetUser" />
      <ClientLayoutAccountMenu
        v-else
        :first-name="firstName"
        :last-name="lastName"
        :email="email"
        :items="navItems"
        @sign-out="$emit('signOut')"
      />
      <!-- Hamburger: mobile only -->
      <UiIconButton
        :icon="sidebarOpen ? X : Menu"
        :label="$t('client.nav.toggleMenu')"
        variant="ghost"
        class="lg:hidden"
        testid="client-menu-toggle"
        @click="$emit('toggleSidebar')"
      />
    </div>
  </header>
</template>

<script setup lang="ts">
/**
 * The client area's top bar: the brand lockup on the left, then the welcome text, theme and language
 * controls, the platform widget mounts and the mobile menu button on the right.
 */
import { Menu, X } from 'lucide-vue-next'
import type { ClientNavItem } from '~/types/clientnavitem'
import LogoLockup from '~/components/LogoLockup.vue'
import { useHeaderWidget } from '~/composables/useHeaderWidget'

/** Props of ClientLayoutTopBar. */
interface Props {
  /** Full name of the signed-in person. */
  fullName: string
  /** Given name, for the local account menu; empty while the profile loads. */
  firstName?: string
  /** Family name, for the local account menu; empty while the profile loads. */
  lastName?: string
  /** E-mail address, for the local account menu; empty while the profile loads. */
  email?: string
  /** Links of the local account menu, already filtered by the person's permissions. */
  navItems?: ClientNavItem[]
  /** The person in the widget's own JSON shape, or undefined while the profile loads. */
  widgetUser?: string
  /** Whether the mobile sidebar is open, so the button shows a cross. */
  sidebarOpen: boolean
}

/** Whether the platform widget fills the mounts here, or the local account menu is drawn instead. */
const { enabled: widgetEnabled } = useHeaderWidget()

withDefaults(defineProps<Props>(), {
  firstName: '',
  lastName: '',
  email: '',
  navItems: () => [],
  widgetUser: undefined
})

/** Events of ClientLayoutTopBar. */
defineEmits<{
  /** The mobile menu button was pressed. */
  toggleSidebar: []
  /** The person chose Sign out in the local account menu. */
  signOut: []
}>()
</script>
