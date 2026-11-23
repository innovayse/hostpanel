<template>
  <!--
    Mobile: an overlay anchored to the body row (the layout's positioned ancestor), so it opens
    below the demo banner and the top bar instead of under them. It is `invisible` while closed so
    its links leave the tab order. Desktop: a normal flex child that fills the height and scrolls
    internally.
  -->
  <!-- Drawer width on a phone: intrinsically small UI. -->
  <UiStack
    ref="asideRef"
    :as="UiStackTag.Aside"
    gap="none"
    class="absolute inset-y-0 left-0 z-20 w-64 max-w-[85vw] border-r border-gray-200 bg-ui-surface transition-[transform,visibility] duration-300 dark:border-white/10 lg:relative lg:inset-y-auto lg:z-auto lg:max-w-none lg:flex-shrink-0 lg:translate-x-0 lg:visible"
    :class="open ? 'translate-x-0' : '-translate-x-full invisible'"
    data-testid="client-sidebar"
  >
    <!-- Navigation: scrolls when the items overflow -->
    <UiStack :as="UiStackTag.Nav" gap="none" class="flex-1 space-y-1 overflow-y-auto px-3 py-4">
      <ClientLayoutNavItem
        v-for="item in items"
        :key="item.to"
        :item="item"
        :active="isActive(item.to)"
        @navigate="emit('close')"
      />
    </UiStack>

    <!-- Documentation link: shown only when docsUrl is configured -->
    <UiStack v-if="docsUrl" gap="none" class="flex-shrink-0 border-t border-gray-100 px-3 py-3 dark:border-white/5">
      <UiButton
        variant="ghost"
        :href="docsUrl"
        target="_blank"
        rel="noopener noreferrer"
        full-width
        class="!justify-start gap-3"
        data-testid="client-nav-documentation"
      >
        <BookOpen :size="18" :stroke-width="2" aria-hidden="true" />
        {{ $t('client.nav.documentation') }}
      </UiButton>
    </UiStack>

    <!-- Theme and language: the top bar carries them from `lg` up, so on a phone they live here -->
    <UiStack gap="sm" class="flex-shrink-0 border-t border-gray-100 px-3 py-3 dark:border-white/5 lg:hidden" data-testid="client-sidebar-preferences">
      <UiThemeToggle class="self-start" />
      <UiLanguageSwitcher layout="responsive" />
    </UiStack>

    <!-- Sign out: always at the bottom -->
    <UiStack gap="none" class="flex-shrink-0 border-t border-gray-100 px-3 py-3 dark:border-white/5">
      <UiButton variant="ghost" full-width class="!justify-start gap-3 hover:!text-red-500" data-testid="client-sign-out" @click="emit('signOut')">
        <LogOut :size="18" :stroke-width="2" />
        {{ $t('common.signOut') }}
      </UiButton>
    </UiStack>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * The client area's left navigation: the permitted links and the sign-out button.
 *
 * On mobile it is a drawer that traps focus, closes on Escape and locks the page scroll while open.
 */
import type { ComponentPublicInstance } from 'vue'
import { BookOpen, LogOut } from 'lucide-vue-next'
import { useOverlay } from '~/utils/ui/overlay'
import type { ClientNavItem } from '~/types/clientnavitem'
import { UiStackTag } from '~/utils/ui/enums'

/** Props of ClientLayoutSidebar. */
interface Props {
  /** The links to show, already filtered by the visitor's permissions. */
  items: ClientNavItem[]
  /** Whether the mobile drawer is slid in; always visible on desktop. */
  open: boolean
}

/** Props of the sidebar; `open` also drives the focus trap and scroll lock. */
const props = defineProps<Props>()

/** Events of ClientLayoutSidebar. */
const emit = defineEmits<{
  /** The drawer should close (a link was followed or Escape was pressed). */
  close: []
  /** The visitor asked to sign out. */
  signOut: []
}>()

/** Documentation URL from runtime config; empty when the docs site is not configured. */
const { public: { docsUrl } } = useRuntimeConfig()

/** The drawer component; its root element traps focus and holds the page scroll lock while open on mobile. */
const asideRef = ref<ComponentPublicInstance | null>(null)

useOverlay(() => props.open, () => emit('close'), () => (asideRef.value?.$el as HTMLElement | undefined) ?? null)

/** The current route, read for its query and path. */
const route = useRoute()

/**
 * Whether a link's section contains the current route.
 *
 * @param path - The link target.
 * @returns True for the page itself and everything below it.
 */
const isActive = (path: string): boolean => route.path === path || route.path.startsWith(`${path}/`)
</script>
