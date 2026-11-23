<template>
  <!-- One header for mobile and desktop, so the widget mount ids exist once -->
  <UiStack
    :as="UiStackTag.Header"
    direction="row"
    align="center"
    justify="between"
    gap="none"
    class="z-30 flex-shrink-0 border-b border-gray-200 bg-ui-surface px-4 py-3 dark:border-white/10 lg:px-8 lg:py-4"
    data-testid="client-topbar"
  >
    <!-- Left: the brand, the same lockup the storefront header draws, at every width -->
    <UiStack gap="none" class="mr-3 min-w-0">
      <UiLink :to="SITE_HOME_PATH" tone="inherit" :underline="false" testid="client-topbar-brand" class="flex items-center gap-2">
        <LogoLockup img-class="h-8 w-auto object-contain" mark-class="h-8 w-8" name-class="text-lg font-bold" />
      </UiLink>
    </UiStack>

    <!-- Right: welcome text on desktop, then the controls -->
    <UiStack direction="row" align="center" gap="xs" class="min-w-0 flex-shrink-0 lg:gap-3">
      <UiText :as="UiTextTag.Span" size="sm" tone="muted" truncate class="hidden lg:block">
        {{ $t('client.nav.welcomeBack') }}
        <UiText :as="UiTextTag.Span" size="sm" weight="medium" data-testid="client-topbar-name">{{ fullName }}</UiText>
      </UiText>
      <!-- Desktop only: theme toggle, language switcher, app launcher -->
      <UiStack direction="row" align="center" gap="sm" class="hidden lg:flex">
        <UiThemeToggle />
        <UiLanguageSwitcher />
        <!-- data-user hands the signed-in person to the widget so its launcher appends
             ?authuser= to every tile from the first paint, instead of only after its own
             profile round-trip. The widget reads it from either mount; this is the one
             present at every width. -->
        <UiStack v-if="widgetEnabled" id="inno-launcher-mount" gap="none" :data-user="widgetUser" />
      </UiStack>
      <!-- Account control: always visible, mobile needs account switching too. The platform widget
           fills the mount where the shell serves it; everywhere else the local menu is drawn, so
           the header is never left without an avatar. -->
      <UiStack v-if="widgetEnabled" id="inno-account-mount" gap="none" :data-user="widgetUser" />
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
    </UiStack>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * The client area's top bar: the brand lockup on the left, then the welcome text, theme and language
 * controls, the platform widget mounts and the mobile menu button on the right.
 */
import { Menu, X } from 'lucide-vue-next'
import { SITE_HOME_PATH } from '~/constants/siteRoutes'
import type { ClientNavItem } from '~/types/clientnavitem'
import LogoLockup from '~/components/LogoLockup.vue'
import { useHeaderWidget } from '~/composables/useHeaderWidget'
import { UiStackTag, UiTextTag } from '~/utils/ui/enums'

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
