<template>
  <!--
    Mobile: an overlay anchored to the body row (the layout's positioned ancestor), so it opens
    below the demo banner and the top bar instead of under them. It is `invisible` while closed so
    its links leave the tab order. Desktop: a normal flex child that fills the height and scrolls
    internally.
  -->
  <!-- Drawer width on a phone: intrinsically small UI. -->
  <aside
    ref="asideRef"
    class="absolute inset-y-0 left-0 z-20 flex w-64 max-w-[85vw] flex-col border-r border-gray-200 bg-ui-surface transition-[transform,visibility] duration-300 dark:border-white/10 lg:relative lg:inset-y-auto lg:z-auto lg:max-w-none lg:flex-shrink-0 lg:translate-x-0 lg:visible"
    :class="open ? 'translate-x-0' : '-translate-x-full invisible'"
    data-testid="client-sidebar"
  >
    <!-- Logo: desktop only, the top bar carries the brand and the close button on mobile -->
    <div class="hidden flex-shrink-0 items-center border-b border-gray-200 px-6 py-5 dark:border-white/10 lg:flex">
      <UiLink to="/" tone="inherit" :underline="false" testid="client-sidebar-brand" class="text-lg font-bold">
        {{ $t('client.nav.brand', { site: siteName }) }}
      </UiLink>
    </div>

    <!-- Navigation: scrolls when the items overflow -->
    <nav class="flex-1 space-y-1 overflow-y-auto px-3 py-4">
      <ClientLayoutNavItem
        v-for="item in items"
        :key="item.to"
        :item="item"
        :active="isActive(item.to)"
        @navigate="emit('close')"
      />
    </nav>

    <!-- Documentation link: shown only when docsUrl is configured -->
    <div v-if="docsUrl" class="flex-shrink-0 border-t border-gray-100 px-3 py-3 dark:border-white/5">
      <a
        :href="docsUrl"
        target="_blank"
        rel="noopener"
        class="flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium text-gray-600 no-underline hover:bg-gray-100 hover:text-gray-900 dark:text-gray-400 dark:hover:bg-white/10 dark:hover:text-white"
        data-testid="client-nav-documentation"
      >
        <BookOpen :size="18" :stroke-width="2" aria-hidden="true" />
        {{ $t('client.nav.documentation') }}
      </a>
    </div>

    <!-- Sign out: always at the bottom -->
    <div class="flex-shrink-0 border-t border-gray-100 px-3 py-3 dark:border-white/5">
      <UiButton variant="ghost" full-width class="!justify-start gap-3 hover:!text-red-500" data-testid="client-sign-out" @click="emit('signOut')">
        <LogOut :size="18" :stroke-width="2" />
        {{ $t('client.nav.signOut') }}
      </UiButton>
    </div>
  </aside>
</template>

<script setup lang="ts">
/**
 * The client area's left navigation: brand, the permitted links and the sign-out button.
 *
 * On mobile it is a drawer that traps focus, closes on Escape and locks the page scroll while open.
 */
import { BookOpen, LogOut } from 'lucide-vue-next'
import { useOverlay } from '~/utils/ui/overlay'
import type { ClientNavItem } from '~/types/clientnavitem'

/** Props of ClientLayoutSidebar. */
interface Props {
  /** The links to show, already filtered by the visitor's permissions. */
  items: ClientNavItem[]
  /** Whether the mobile drawer is slid in; always visible on desktop. */
  open: boolean
}

/** Props of the sidebar; `open` also drives the focus trap and scroll lock. */
const props = defineProps<Props>()

/** Operator-set site name shown as the brand. */
const { name: siteName } = useSiteIdentity()

/** Events of ClientLayoutSidebar. */
const emit = defineEmits<{
  /** The drawer should close (a link was followed or Escape was pressed). */
  close: []
  /** The visitor asked to sign out. */
  signOut: []
}>()

/** Documentation URL from runtime config; empty when the docs site is not configured. */
const { public: { docsUrl } } = useRuntimeConfig()

/** The drawer element; while open on mobile it traps focus and holds the page scroll lock. */
const asideRef = ref<HTMLElement | null>(null)

useOverlay(() => props.open, () => emit('close'), () => asideRef.value)

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
