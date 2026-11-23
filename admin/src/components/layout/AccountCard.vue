<script setup lang="ts">
/**
 * The signed-in account at the foot of the sidebar: an initials avatar, the name, the role, and
 * the account menu (settings, sign-out) behind a "more" button, sized and padded like a navigation entry.
 *
 * Naming the account lets an operator tell which account they are acting as, which matters on a panel
 * where several people share one screen. In the icon rail only the avatar is drawn, and it is the menu trigger.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import AccountAvatar from '@/components/layout/AccountAvatar.vue'
import UiMenu from '@/components/ui/UiMenu.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { AppRoute } from '@/lib/routes'
import { useAuthStore } from '@/stores/auth'
import { initialsOf } from '@/lib/format'
import type { UiMenuAction } from '@/utils/ui/menu'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { Icon } from '@/lib/icons'
import { FloatingAlign } from '@/utils/ui/floating'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'

/** Props of the account card. */
defineProps<{
  /** Draws only the avatar, for the folded icon rail. */
  rail?: boolean
}>()

/** Translation function. */
const { t } = useI18n()

/** Router, for sign-out. */
const router = useRouter()

/** Auth store: the signed-in account and the sign-out action. */
const auth = useAuthStore()

/** The signed-in operator's name (their address when the account holds none), or a neutral label before the profile has loaded. */
const accountLabel = computed<string>(() => auth.displayName ?? t('common.admin'))

/** The full address, shown as the tooltip of the name; empty before the profile has loaded. */
const accountEmail = computed<string>(() => auth.user?.email ?? '')

/** The most privileged role the account holds, in the language on screen; empty before the profile has loaded. */
const roleLabel = computed<string>(() => (auth.primaryRole === null ? '' : t(`common.roles.${auth.primaryRole}`)))

/** The avatar's initials, taken from the name shown. */
const initials = computed<string>(() => initialsOf(accountLabel.value))

/**
 * Ends the session and returns to the sign-in screen.
 *
 * @returns Promise that resolves once the operator is on the sign-in screen.
 */
const signOut = async (): Promise<void> => {
  await auth.logout()
  await router.push(AppRoute.Login)
}

/** The account menu: the settings page, then the sign-out action (the menu puts the destructive one last). */
const accountActions = computed<UiMenuAction[]>(() => [
  { label: t('nav.settings'), icon: Icon.Settings, to: AppRoute.Settings, testid: 'sidebar-settings' },
  { label: t('shared.signOut'), icon: Icon.SignOut, danger: true, onSelect: signOut, testid: 'sidebar-sign-out' },
])
</script>

<template>
  <UiStack
    :direction="UiDirection.Row"
    :align="UiAlignItems.Center"
    :gap="UiGap.None"
    class="border-border bg-surface-row"
    :class="rail ? 'justify-center rounded-control border p-0.5' : 'gap-3 rounded-control border px-3 py-1'"
    data-testid="account-card"
  >
    <AccountAvatar v-if="!rail" :initials="initials" />
    <UiStack v-if="!rail" :gap="UiGap.None" class="min-w-0 flex-1">
      <UiText :size="UiTextSize.Sm" class="truncate font-semibold" :title="accountEmail" data-testid="account-name">{{ accountLabel }}</UiText>
      <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs" class="truncate" :title="roleLabel" data-testid="account-role">{{ roleLabel }}</UiText>
    </UiStack>
    <!-- The kit's "more" button opens the account menu; the folded rail has no room beside the avatar, so there the avatar is the trigger. -->
    <UiMenu :actions="accountActions" :align="rail ? FloatingAlign.Start : FloatingAlign.End" :aria-label="t('layout.userMenu')" testid="sidebar-user-menu">
      <template v-if="rail" #default>
        <AccountAvatar :initials="initials" rail />
      </template>
    </UiMenu>
  </UiStack>
</template>
