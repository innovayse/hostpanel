<template>
  <div ref="rootEl" class="relative">
    <button
      type="button"
      :aria-label="menuLabel"
      :aria-expanded="open"
      aria-haspopup="menu"
      class="grid h-10 w-10 place-items-center rounded-[11px] border border-line2"
      :class="open ? 'bg-[rgba(125,227,255,0.12)]' : 'bg-transparent'"
      @click="open = !open"
    >
      <UiAvatar
        :first-name="firstName"
        :last-name="lastName"
        :email="email"
        size="sm"
      />
    </button>

    <div
      v-if="open"
      role="menu"
      class="fixed right-[clamp(16px,4vw,48px)] top-[74px] z-40 flex w-[min(280px,calc(100vw-32px))] flex-col rounded-[18px] border border-line2 bg-panel p-2.5 shadow-panel"
    >
      <div class="flex min-w-0 items-center gap-3 px-3 py-2.5">
        <UiAvatar :first-name="firstName" :last-name="lastName" :email="email" />
        <span class="flex min-w-0 flex-col">
          <span class="truncate text-sm font-semibold text-tx">{{ displayName }}</span>
          <span class="truncate text-xs text-mut2">{{ email }}</span>
        </span>
      </div>

      <span class="my-1 h-px bg-line2" />

      <NuxtLink
        v-for="item in menuItems"
        :key="item.key"
        :to="item.to"
        role="menuitem"
        class="rounded-[11px] px-3 py-2.5 text-sm text-tx hover:bg-[rgba(125,227,255,0.09)]"
        @click="open = false"
      >{{ item.label }}</NuxtLink>

      <span class="my-1 h-px bg-line2" />

      <button
        type="button"
        role="menuitem"
        class="rounded-[11px] px-3 py-2.5 text-left text-sm text-danger hover:bg-[rgba(125,227,255,0.09)]"
        @click="onSignOut"
      >{{ t('client.nav.signOut') }}</button>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * aurora template account menu — the signed-in half of the site header.
 *
 * The public header used to render "Sign in" / "Get started" to everyone,
 * because it never consulted the session at all: the auth store was wired into
 * the client-area shell and into the legacy `components/layout/Header.vue`, but
 * the active template's header read neither. This is the treatment that belongs
 * beside the apps launcher — avatar, account destinations, sign out.
 *
 * The avatar comes from `UiAvatar` rather than being drawn here, per
 * `ui-components.md`: "Nothing is drawn by hand twice."
 */
const { t } = useI18n()
const localePath = useLocalePath()
const authStore = useAuthStore()
const { user } = storeToRefs(authStore)
const { fetchUser, signOut } = authStore

const open = ref(false)
const rootEl = ref<HTMLElement | null>(null)

/** Given name, empty until the profile request resolves. */
const firstName = computed(() => user.value?.firstname ?? '')

/** Family name, empty until the profile request resolves. */
const lastName = computed(() => user.value?.lastname ?? '')

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
  { key: 'dashboard', label: t('client.nav.dashboard'), to: localePath('/client/dashboard') },
  { key: 'services', label: t('client.nav.services'), to: localePath('/client/services') },
  { key: 'domains', label: t('client.nav.domains'), to: localePath('/client/domains') },
  { key: 'invoices', label: t('client.nav.invoices'), to: localePath('/client/invoices') },
  { key: 'account', label: t('client.nav.account'), to: localePath('/client/account') },
])

/** Ends the session; in SSO mode this navigates the browser away. */
const onSignOut = async () => {
  open.value = false
  await signOut()
}

/** Closes the panel when the visitor clicks anywhere outside it. */
const onClickOutside = (event: MouseEvent) => {
  if (open.value && rootEl.value && !rootEl.value.contains(event.target as Node)) open.value = false
}

const onEscape = (event: KeyboardEvent) => {
  if (event.key === 'Escape') open.value = false
}

onMounted(() => {
  // The cookie says there is a session; the name and e-mail behind it still have
  // to be fetched, and only on the client — the panel renders without them.
  fetchUser()
  document.addEventListener('click', onClickOutside)
  document.addEventListener('keydown', onEscape)
})

onBeforeUnmount(() => {
  document.removeEventListener('click', onClickOutside)
  document.removeEventListener('keydown', onEscape)
})
</script>
