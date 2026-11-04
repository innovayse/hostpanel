<template>
  <UiPopover v-model="open" panel-class="absolute right-0 top-full mt-2 w-[min(280px,calc(100vw-32px))]">
    <template #trigger="{ toggle }">
      <UiButton
        variant="ghost"
        :aria-label="displayName"
        :aria-expanded="open"
        aria-haspopup="menu"
        data-testid="client-account-trigger"
        class="h-10 w-10 !p-0"
        @click="toggle"
      >
        <UiAvatar :first-name="firstName" :last-name="lastName" :email="email" size="sm" />
      </UiButton>
    </template>

    <template #default="{ close }">
      <UiStack direction="row" align="center" gap="sm" class="min-w-0 px-3 py-2.5">
        <UiAvatar :first-name="firstName" :last-name="lastName" :email="email" />
        <UiStack gap="none" class="min-w-0">
          <UiText as="span" size="sm" weight="semibold" truncate data-testid="client-account-name">{{ displayName }}</UiText>
          <UiText as="span" size="xs" tone="muted" truncate data-testid="client-account-email">{{ email }}</UiText>
        </UiStack>
      </UiStack>

      <div class="my-1 h-px bg-gray-200 dark:bg-white/10" />

      <UiButton
        v-for="item in items"
        :key="item.to"
        variant="ghost"
        :to="item.to"
        role="menuitem"
        full-width
        class="!justify-start gap-3"
        :data-testid="`client-account-link-${item.to}`"
        @click="close"
      >
        <component :is="item.icon" :size="16" :stroke-width="2" aria-hidden="true" />
        {{ item.label }}
      </UiButton>

      <template v-if="docsUrl">
        <div class="my-1 h-px bg-gray-200 dark:bg-white/10" />

        <a
          :href="docsUrl"
          target="_blank"
          rel="noopener"
          role="menuitem"
          class="flex w-full items-center gap-3 rounded-md px-3 py-2 text-sm font-medium text-gray-700 no-underline hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-white/10"
          data-testid="client-account-help"
          @click="open = false"
        >
          <BookOpen :size="16" :stroke-width="2" aria-hidden="true" />
          {{ $t('client.nav.help') }}
        </a>
      </template>

      <div class="my-1 h-px bg-gray-200 dark:bg-white/10" />

      <UiButton
        variant="ghost"
        role="menuitem"
        full-width
        class="!justify-start gap-3 hover:!text-red-500"
        data-testid="client-account-sign-out"
        @click="onSignOut"
      >
        <LogOut :size="16" :stroke-width="2" aria-hidden="true" />
        {{ $t('client.nav.signOut') }}
      </UiButton>
    </template>
  </UiPopover>
</template>

<script setup lang="ts">
/**
 * The client area's own account menu: avatar with the person's initials that opens a panel with
 * their name, e-mail, the permitted navigation links and Sign out.
 *
 * It stands in for the platform shell's account widget on a deployment that does not load that
 * widget (standalone or demo), so the header is never left without an account control. Purely
 * presentational: the layout hands it the profile and the already-permission-filtered links.
 */
import { BookOpen, LogOut } from 'lucide-vue-next'
import type { ClientNavItem } from '~/types/clientnavitem'

/** Props of ClientLayoutAccountMenu. */
interface Props {
  /** Given name; empty while the profile loads. */
  firstName?: string
  /** Family name; empty while the profile loads. */
  lastName?: string
  /** E-mail address; empty while the profile loads. */
  email?: string
  /** The links offered in the panel, already filtered by the person's permissions. */
  items: ClientNavItem[]
}

/** Props of the menu with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  firstName: '',
  lastName: '',
  email: ''
})

/** Events of ClientLayoutAccountMenu. */
const emit = defineEmits<{
  /** The person chose Sign out. */
  signOut: []
}>()

/** Translator for the fallback name. */
const { t } = useI18n()

/** Documentation URL from runtime config; empty when the docs site is not configured. */
const { public: { docsUrl } } = useRuntimeConfig()

/** Whether the panel is open (v-model of the popover). */
const open = ref(false)

/** Name shown in the panel and used as the trigger's accessible name; the generic label until the profile arrives. */
const displayName = computed(() =>
  [props.firstName, props.lastName].filter(Boolean).join(' ') || t('nav.clientArea'))

/** Closes the panel and reports the request; the layout owns ending the session. */
const onSignOut = (): void => {
  open.value = false
  emit('signOut')
}
</script>
