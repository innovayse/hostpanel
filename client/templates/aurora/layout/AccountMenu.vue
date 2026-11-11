<template>
  <UiPopover v-model="open" panel-class="w-[280px]">
    <template #trigger="{ toggle }">
      <UiButton
        variant="bare"
        :aria-label="menuLabel"
        :aria-expanded="open"
        aria-haspopup="menu"
        data-testid="account-menu-trigger"
        class="h-10 w-10 rounded-xl border border-line2"
        :class="open ? 'bg-acbg' : ''"
        @click="toggle"
      >
        <UiAvatar
          :first-name="firstName"
          :last-name="lastName"
          :email="email"
          size="sm"
        />
      </UiButton>
    </template>

    <template #default="{ close }">
      <UiStack direction="row" align="center" gap="sm" class="min-w-0 px-3 py-2.5">
        <UiAvatar :first-name="firstName" :last-name="lastName" :email="email" />
        <UiStack gap="none" class="min-w-0">
          <UiText
            as="span"
            size="sm"
            weight="semibold"
            tone="inherit"
            truncate
            class="text-tx"
            data-testid="account-menu-name"
          >
            {{ displayName }}
          </UiText>
          <UiText as="span" size="xs" tone="inherit" truncate class="text-mut2">{{ email }}</UiText>
        </UiStack>
      </UiStack>

      <div class="my-1 h-px bg-line2" />

      <UiButton
        v-for="item in menuItems"
        :key="item.key"
        variant="bare"
        :to="item.to"
        role="menuitem"
        :data-testid="`account-menu-${item.key}`"
        :class="ITEM_CLASSES"
        @click="close"
      >
        <!-- The row is its own flex box: the button centres its content, and a utility on the button alone loses to that. -->
        <UiStack direction="row" align="center" gap="sm" class="w-full min-w-0">
          <component :is="item.icon" :size="16" :stroke-width="2" class="shrink-0 text-mut2" aria-hidden="true" />
          <UiText as="span" size="sm" tone="inherit" truncate>{{ item.label }}</UiText>
        </UiStack>
      </UiButton>

      <div class="my-1 h-px bg-line2" />

      <UiButton
        variant="bare"
        role="menuitem"
        data-testid="account-menu-sign-out"
        :class="[ITEM_CLASSES, 'text-danger']"
        @click="onSignOut"
      >
        <UiStack direction="row" align="center" gap="sm" class="w-full min-w-0">
          <LogOut :size="16" :stroke-width="2" class="shrink-0" aria-hidden="true" />
          <UiText as="span" size="sm" tone="inherit" truncate>{{ t('client.nav.signOut') }}</UiText>
        </UiStack>
      </UiButton>
    </template>
  </UiPopover>
</template>

<script setup lang="ts">
/**
 * aurora template account menu: the signed-in half of the site header.
 *
 * It reads the session, so a signed-in visitor sees their account rather than
 * "Sign in" / "Get started": avatar, account destinations, sign out, beside the
 * apps launcher.
 *
 * The avatar comes from `UiAvatar` and the floating panel from `UiPopover`, so nothing is
 * drawn by hand twice and the outside-click / Escape handling lives in one place.
 */
import { FileText, Globe, LayoutDashboard, LogOut, Server, UserCog } from 'lucide-vue-next'
import {
  CLIENT_ACCOUNT_PATH, CLIENT_DASHBOARD_PATH, CLIENT_DOMAINS_PATH, CLIENT_INVOICES_PATH, CLIENT_SERVICES_PATH
} from '~/constants/clientRoutes'
const { t } = useI18n()

/**
 * Look shared by every row of the panel: full width, left-aligned content, and the same
 * tinted background for pointer hover and keyboard focus, both from the template tokens.
 */
const ITEM_CLASSES = 'w-full rounded-xl px-3 py-2.5 text-tx transition-colors hover:bg-acbg focus-visible:bg-acbg focus-visible:ring-inset'

/** Prefixes a path with the active locale. */
const localePath = useLocalePath()
/** The session store. */
const authStore = useAuthStore()
/** Session state of the customer. */
const { user } = storeToRefs(authStore)
/** Session actions. */
const { fetchUser, signOut } = authStore

/** Whether the panel is open (v-model of the popover). */
const open = ref(false)

/** Given name, empty until the profile request resolves. */
const firstName = computed(() => user.value?.firstName ?? '')

/** Family name, empty until the profile request resolves. */
const lastName = computed(() => user.value?.lastName ?? '')

/** E-mail address, empty until the profile request resolves. */
const email = computed(() => user.value?.email ?? '')

/**
 * Name shown in the panel header.
 *
 * Falls back to the generic client-area label rather than an empty line: the
 * menu is rendered from the `authed` cookie, which is known during server
 * rendering, whereas the profile only arrives after mount.
 */
const displayName = computed(() =>
  [firstName.value, lastName.value].filter(Boolean).join(' ') || t('nav.clientArea'))

/** Accessible name for the trigger, which is otherwise only an avatar. */
const menuLabel = computed(() => displayName.value)

/** Destinations offered in the panel, in the order the client-area nav uses. */
const menuItems = computed(() => [
  { key: 'dashboard', icon: LayoutDashboard, label: t('client.nav.dashboard'), to: localePath(CLIENT_DASHBOARD_PATH) },
  { key: 'services', icon: Server, label: t('client.nav.services'), to: localePath(CLIENT_SERVICES_PATH) },
  { key: 'domains', icon: Globe, label: t('client.nav.domains'), to: localePath(CLIENT_DOMAINS_PATH) },
  { key: 'invoices', icon: FileText, label: t('client.nav.invoices'), to: localePath(CLIENT_INVOICES_PATH) },
  { key: 'account', icon: UserCog, label: t('client.nav.account'), to: localePath(CLIENT_ACCOUNT_PATH) },
])

/** Ends the session; in SSO mode this navigates the browser away. */
const onSignOut = async (): Promise<void> => {
  open.value = false
  await signOut()
}

onMounted(() => {
  // The cookie says there is a session; the name and e-mail behind it still have
  // to be fetched, and only on the client: the panel renders without them.
  fetchUser()
})
</script>
