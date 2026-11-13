<template>
  <UiDrawer
    :model-value="modelValue"
    :title="t('nav.menu')"
    size="sm"
    @update:model-value="emit('update:modelValue', $event)"
  >
    <UiStack as="nav" gap="none" :aria-label="t('nav.primary')" data-testid="header-drawer-nav">
      <template v-for="item in items" :key="item.key">
        <!-- A plain link. -->
        <UiLink
          v-if="!item.children"
          :to="item.to"
          tone="inherit"
          :underline="false"
          :class="[ROW, item.key === activeKey ? HEADER_NAV_ACTIVE : HEADER_NAV_IDLE]"
          :aria-current="item.key === activeKey ? 'page' : undefined"
          :testid="`header-drawer-${item.key}`"
          @click="close"
        >
          {{ item.label }}
        </UiLink>

        <!-- A group: its heading, then every link, so nothing hides behind a second tap. -->
        <UiStack v-else gap="none" class="mt-2" :data-testid="`header-drawer-${item.key}`">
          <UiText
            as="span"
            size="xs"
            weight="semibold"
            tone="inherit"
            class="px-3 pb-1 pt-2 uppercase tracking-wide text-classic-muted"
          >
            {{ item.label }}
          </UiText>
          <UiLink
            v-for="child in item.children"
            :key="child.key"
            :to="child.to"
            tone="inherit"
            :underline="false"
            :class="[ROW, 'group gap-3', isHeaderPathActive(path, child.to) ? HEADER_NAV_ACTIVE : HEADER_NAV_IDLE]"
            :aria-current="isHeaderPathActive(path, child.to) ? 'page' : undefined"
            :testid="`header-drawer-child-${child.key}`"
            @click="close"
          >
            <UiIconTile
              :icon="child.icon"
              size="sm"
              :tone="isHeaderPathActive(path, child.to) ? 'inverse' : 'soft'"
              :data-testid="`header-drawer-child-${child.key}-icon`"
            />
            {{ child.label }}
          </UiLink>
        </UiStack>
      </template>
    </UiStack>

    <!-- Signed in: the client-area links. -->
    <UiStack v-if="isLoggedIn" gap="none" class="mt-4 border-t border-classic-border pt-4" data-testid="header-drawer-account">
      <UiStack direction="row" align="center" gap="sm" class="min-w-0 px-3 pb-2">
        <UiAvatar :first-name="user?.firstName ?? ''" :last-name="user?.lastName ?? ''" :email="user?.email ?? ''" />
        <UiStack gap="none" class="min-w-0">
          <UiText as="span" size="sm" weight="semibold" tone="inherit" truncate class="text-classic-ink">
            {{ accountName }}
          </UiText>
          <UiText as="span" size="xs" tone="inherit" truncate class="text-classic-muted">{{ user?.email ?? '' }}</UiText>
        </UiStack>
      </UiStack>
      <UiLink
        v-for="link in accountLinks"
        :key="link.key"
        :to="link.to"
        tone="inherit"
        :underline="false"
        :class="[ROW, 'gap-3', HEADER_NAV_IDLE]"
        :testid="`header-drawer-account-${link.key}`"
        @click="close"
      >
        <component :is="link.icon" :size="16" :stroke-width="2" class="shrink-0" aria-hidden="true" />
        {{ link.label }}
      </UiLink>
    </UiStack>

    <!-- Preferences: language and currency. -->
    <UiStack gap="sm" class="mt-4 border-t border-classic-border pt-4" data-testid="header-drawer-preferences">
      <UiText as="span" size="xs" weight="semibold" tone="inherit" class="px-3 uppercase tracking-wide text-classic-muted">
        {{ t('nav.preferences') }}
      </UiText>
      <UiStack class="px-3" gap="sm">
        <UiLanguageSwitcher layout="responsive" />
        <UiCurrencySwitcher placement="top" :trigger-class="`w-full items-center justify-between gap-2 px-3 ${HEADER_CONTROL}`" />
      </UiStack>
    </UiStack>

    <template #footer>
      <!-- Signed in: sign out. -->
      <UiButton
        v-if="isLoggedIn"
        variant="bare"
        full-width
        :class="[HEADER_CONTROL, 'gap-2 px-4 text-sm font-semibold text-red-600 dark:text-red-400']"
        data-testid="header-drawer-sign-out"
        @click="onSignOut"
      >
        <LogOut :size="16" :stroke-width="2" aria-hidden="true" />
        {{ t('client.nav.signOut') }}
      </UiButton>

      <!-- Signed out: the two entries the bar shows on a wide screen. -->
      <UiStack v-else gap="sm">
        <UiButton
          variant="bare"
          :href="signInHref"
          full-width
          :class="[HEADER_CONTROL, 'px-4 text-sm font-semibold']"
          data-testid="header-drawer-sign-in"
          @click="close"
        >
          {{ t('nav.signIn') }}
        </UiButton>
        <UiButton
          variant="bare"
          :to="getStartedTo"
          full-width
          :class="HEADER_PRIMARY"
          data-testid="header-drawer-get-started"
          @click="close"
        >
          {{ t('nav.getStarted') }}
        </UiButton>
      </UiStack>
    </template>
  </UiDrawer>
</template>

<script setup lang="ts">
/**
 * LayoutHeaderDrawer: the classic header's menu below the desktop breakpoint.
 *
 * A side drawer that holds everything the bar shows on a wide screen: every navigation entry
 * (groups open, as headed lists), the signed-in client's links or the sign-in pair, the language
 * and currency pickers, and sign-out. The drawer itself (scroll lock, Escape, focus trap) is the
 * kit's `UiDrawer`; this component only fills it.
 */
import { storeToRefs } from 'pinia'
import { LogOut } from 'lucide-vue-next'
import type { HeaderNavItem } from '~/types/headernavitem'
import { useAuthStore } from '~/stores/auth'
import { HEADER_CONTROL, HEADER_NAV_ACTIVE, HEADER_NAV_IDLE, HEADER_PRIMARY } from '~/utils/headerControl'
import { buildAccountLinks } from '~/utils/headerAccountLinks'
import { activeHeaderKey, isHeaderPathActive } from '~/utils/headerNav'

/** Props of LayoutHeaderDrawer. */
const props = defineProps<{
  /** Whether the drawer is open (v-model). */
  modelValue: boolean
  /** The navigation entries to list. */
  items: HeaderNavItem[]
  /** Path of the page being viewed, to mark the current link. */
  path: string
}>()

/** Events the drawer raises. */
const emit = defineEmits<{
  /** The drawer asked to open or close. */
  'update:modelValue': [value: boolean]
}>()

/** Key of the one top-level entry that stands for the current page. */
const activeKey = computed(() => activeHeaderKey(props.path, props.items))

/** Look of every link row: a tall tap target with the bar's radius. */
const ROW = 'flex min-h-[44px] items-center rounded-lg px-3 text-base font-medium transition-colors focus-visible:ring-classic-brand'

/** Translate function. */
const { t } = useI18n()

/** Prefixes a path with the active locale. */
const localePath = useLocalePath()

/** Where "Sign in" goes under the configured auth mode. */
const { signInHref } = useAuthMode()

/** The session store. */
const authStore = useAuthStore()

/** Whether a client is signed in, and who. */
const { isLoggedIn, user } = storeToRefs(authStore)

/** Where "Get started" goes: the hosting plans. */
const getStartedTo = computed(() => localePath('/hosting'))

/** Client-area links of the signed-in group. */
const accountLinks = computed(() => buildAccountLinks(t, localePath))

/** Name shown above the account links; the generic label until the profile loads. */
const accountName = computed(() =>
  [user.value?.firstName, user.value?.lastName].filter(Boolean).join(' ') || t('nav.clientArea'))

/** Closes the drawer. */
const close = (): void => {
  emit('update:modelValue', false)
}

/** Closes the drawer and ends the session; in SSO mode this navigates the browser away. */
const onSignOut = async (): Promise<void> => {
  close()
  await authStore.signOut()
}
</script>
