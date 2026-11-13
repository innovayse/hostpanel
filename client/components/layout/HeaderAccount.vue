<template>
  <UiStack ref="rootRef" gap="none" class="relative" data-testid="header-account">
    <UiButton
      variant="bare"
      :class="[HEADER_CONTROL, 'gap-2 px-2', open ? 'border-classic-brand bg-classic-brand-soft' : '']"
      :aria-label="displayName"
      :aria-expanded="open"
      aria-haspopup="menu"
      :aria-controls="PANEL_ID"
      data-testid="header-account-trigger"
      @click="toggle"
      @keydown.esc="close"
    >
      <UiAvatar :first-name="firstName" :last-name="lastName" :email="email" size="sm" />
      <ChevronDown
        :size="14"
        :stroke-width="2"
        class="transition-transform duration-200"
        :class="open ? 'rotate-180' : ''"
        aria-hidden="true"
      />
    </UiButton>

    <UiStack
      v-if="open"
      :id="PANEL_ID"
      gap="none"
      role="menu"
      class="absolute right-0 top-full z-50 mt-2 w-72 rounded-lg border border-classic-border bg-classic-surface p-1.5 shadow-lg"
      data-testid="header-account-panel"
      @keydown.esc="closeAndFocus"
    >
      <UiStack direction="row" align="center" gap="sm" class="min-w-0 px-3 py-2.5">
        <UiAvatar :first-name="firstName" :last-name="lastName" :email="email" />
        <UiStack gap="none" class="min-w-0">
          <UiText
            as="span"
            size="sm"
            weight="semibold"
            tone="inherit"
            truncate
            class="text-classic-ink"
            data-testid="header-account-name"
          >
            {{ displayName }}
          </UiText>
          <UiText as="span" size="xs" tone="inherit" truncate class="text-classic-muted">{{ email }}</UiText>
        </UiStack>
      </UiStack>

      <UiDivider class="my-1" />

      <UiLink
        v-for="link in links"
        :key="link.key"
        :to="link.to"
        tone="inherit"
        :underline="false"
        role="menuitem"
        :class="[HEADER_MENU_ROW, 'items-center']"
        :testid="`header-account-${link.key}`"
        @click="close"
      >
        <component :is="link.icon" :size="16" :stroke-width="2" class="shrink-0 text-classic-muted" aria-hidden="true" />
        <UiText as="span" size="sm" tone="inherit" truncate>{{ link.label }}</UiText>
      </UiLink>

      <UiDivider class="my-1" />

      <UiButton
        variant="bare"
        role="menuitem"
        :class="[HEADER_MENU_ROW, 'w-full items-center justify-start text-red-600 dark:text-red-400']"
        data-testid="header-account-sign-out"
        @click="onSignOut"
      >
        <LogOut :size="16" :stroke-width="2" class="shrink-0" aria-hidden="true" />
        <UiText as="span" size="sm" tone="inherit" truncate>{{ t('client.nav.signOut') }}</UiText>
      </UiButton>
    </UiStack>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * LayoutHeaderAccount. The signed-in half of the classic header: an avatar button that opens
 * the client-area links and a sign-out row.
 *
 * Reads the session from the auth store and loads the profile after mount, since the signed-in
 * cookie is known during server rendering but the name and e-mail only arrive from the profile
 * request; until then the avatar shows a person glyph and the panel the generic client-area
 * label. Sign-out goes through the store, which also ends the platform session in SSO mode.
 */
import { storeToRefs } from 'pinia'
import { ChevronDown, LogOut } from 'lucide-vue-next'
import type { ComponentPublicInstance } from 'vue'
import { useAuthStore } from '~/stores/auth'
import { HEADER_CONTROL, HEADER_MENU_ROW } from '~/utils/headerControl'
import { buildAccountLinks } from '~/utils/headerAccountLinks'

/** Id of the panel, referenced by the trigger's `aria-controls`. */
const PANEL_ID = 'header-account-panel'

/** Translate function. */
const { t } = useI18n()

/** Prefixes a path with the active locale. */
const localePath = useLocalePath()

/** The session store. */
const authStore = useAuthStore()

/** The signed-in client, null until the profile request resolves. */
const { user } = storeToRefs(authStore)

/** Whether the panel is showing. */
const open = ref(false)

/** The root element, used to detect outside clicks and to return focus to the trigger. */
const rootRef = ref<ComponentPublicInstance | null>(null)

/** Given name, empty until the profile request resolves. */
const firstName = computed(() => user.value?.firstName ?? '')

/** Family name, empty until the profile request resolves. */
const lastName = computed(() => user.value?.lastName ?? '')

/** E-mail address, empty until the profile request resolves. */
const email = computed(() => user.value?.email ?? '')

/** Name shown in the panel and read by screen readers; the generic label until the profile loads. */
const displayName = computed(() =>
  [firstName.value, lastName.value].filter(Boolean).join(' ') || t('nav.clientArea'))

/** Client-area destinations of the panel. */
const links = computed(() => buildAccountLinks(t, localePath))

/** Opens a closed panel and closes an open one. */
const toggle = (): void => {
  open.value = !open.value
}

/** Closes the panel. */
const close = (): void => {
  open.value = false
}

/** Closes the panel and puts focus back on the trigger. */
const closeAndFocus = (): void => {
  open.value = false
  const root = rootRef.value?.$el as HTMLElement | undefined
  root?.querySelector<HTMLElement>('button')?.focus()
}

/** Ends the session; in SSO mode this navigates the browser away. */
const onSignOut = async (): Promise<void> => {
  open.value = false
  await authStore.signOut()
}

/**
 * Closes the panel when a click lands outside the component (the same rule as the kit's popover).
 *
 * @param event - The document click.
 */
const onDocumentClick = (event: MouseEvent): void => {
  const root = rootRef.value?.$el as HTMLElement | undefined
  if (open.value && root && !root.contains(event.target as Node)) { // a document click's target is always a Node
    close()
  }
}

onMounted(() => {
  document.addEventListener('click', onDocumentClick)
})

onBeforeUnmount(() => {
  document.removeEventListener('click', onDocumentClick)
})

onMounted(() => {
  // The cookie says there is a session; the name and e-mail behind it still have to be fetched.
  void authStore.fetchUser()
})
</script>
