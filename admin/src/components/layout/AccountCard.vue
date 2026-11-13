<script setup lang="ts">
/**
 * The signed-in account at the foot of the sidebar: an initials avatar, the name, the role, and
 * the account menu (settings, sign-out) behind the avatar.
 *
 * Naming the account lets an operator tell which account they are acting as, which matters on a panel
 * where several people share one screen. In the icon rail only the avatar is drawn, sized (32px) to sit inside the
 * rail's 40px tile with its border and padding.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import UiMenu from '@/components/ui/UiMenu.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { AppRoute } from '@/lib/routes'
import { useAuthStore } from '@/stores/auth'
import { initialsOf } from '@/lib/format'
import type { UiMenuAction } from '@/utils/ui/menu'
import { UiTextSize, UiTextTag, UiTextTone } from '@/utils/ui/text'

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

/** The role the account holds first, empty before the profile has loaded. */
const roleLabel = computed<string>(() => auth.user?.roles[0] ?? '')

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
  { label: t('nav.settings'), to: AppRoute.Settings, testid: 'sidebar-settings' },
  { label: t('common.signOut'), danger: true, onSelect: signOut, testid: 'sidebar-sign-out' },
])
</script>

<template>
  <UiStack
    direction="row"
    align="center"
    gap="none"
    class="gap-2.5 border-border bg-surface-row"
    :class="rail ? 'justify-center rounded-control border p-0.5' : 'rounded-2xl border p-3'"
    data-testid="account-card"
  >
    <!-- The avatar is the menu trigger (settings, sign-out), so the folded rail reaches them too. -->
    <UiMenu :actions="accountActions" :aria-label="t('layout.userMenu')" testid="sidebar-user-menu">
      <UiText
        :as="UiTextTag.Span"
        :tone="UiTextTone.Plain"
        class="grid shrink-0 place-items-center rounded-full bg-avatar-bg font-bold text-avatar-fg"
        :class="rail ? 'size-8 text-xs' : 'size-9 text-[0.8125rem]'"
        data-testid="account-avatar"
      >
        {{ initials }}
      </UiText>
    </UiMenu>
    <UiStack v-if="!rail" gap="none" class="min-w-0 flex-1">
      <UiText :size="UiTextSize.Sm" class="truncate font-semibold" :title="accountEmail" data-testid="account-name">{{ accountLabel }}</UiText>
      <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs" class="truncate" :title="roleLabel" data-testid="account-role">{{ roleLabel }}</UiText>
    </UiStack>
  </UiStack>
</template>
