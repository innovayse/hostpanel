<script setup lang="ts">
/**
 * Admin sidebar with navigation links, grouped under the section labels of `lib/navigation.ts`,
 * which alone decides the order (the phone drawer draws this same component).
 *
 * Supports expandable submenus for items with children.
 * Responsive behaviour:
 * - lg+: full sidebar (220px), always visible
 * - Emits `navigate` on link click so the mobile drawer can close.
 * - In the drawer (`embedded`) it also carries the theme select, which the top bar has no room for on a phone.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useNavigationStore } from '@/stores/navigation'
import UiButton from '@/components/ui/UiButton.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import BrandLogo from '@/components/layout/BrandLogo.vue'
import ReleaseUpdateNotice from '@/components/layout/ReleaseUpdateNotice.vue'
import ThemeSwitch from '@/components/layout/ThemeSwitch.vue'
import VersionLabel from '@/components/layout/VersionLabel.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { getRuntimeConfig } from '@/lib/runtimeConfig'
import { AppRoute } from '@/lib/routes'
import { NAV_SECTIONS } from '@/lib/navigation'
import type { NavItem, NavSection } from '@/lib/navigation'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

/** Props of the sidebar. */
withDefaults(defineProps<{
  /** Shown inside the menu drawer: the full width of the panel, no frame and no logo (the drawer has a title). */
  embedded?: boolean
}>(), {
  embedded: false,
})

/** Emitted when a nav link is clicked (used to close mobile drawer). */
const emit = defineEmits<{
  /** Emitted after any navigation link is clicked. */
  navigate: []
}>()

/** Translation function. */
const { t } = useI18n()

/** Auth store: the sign-out action. */
const authStore = useAuthStore()

/** Router, for the redirect after sign-out. */
const router = useRouter()

/** Current route, to mark the active item. */
const route = useRoute()

/** The navigation in flight, whose target is marked while its screen downloads. */
const navigation = useNavigationStore()

/**
 * The path the menu marks as active: the target of a navigation still downloading its screen, so
 * the clicked item lights up at once, or else the current route.
 */
const activePath = computed<string>(() => navigation.pending?.path ?? route.path)

/** Set of expanded parent item routes (keyed by `to`, stable across locales). */
const expanded = ref<Set<string>>(new Set())

/** Chevron drawn after the label of an item that has a sub-menu. */
const CHEVRON_MARKUP = '<polyline points="9 18 15 12 9 6"/>'

/** Sign-out glyph. */
const SIGN_OUT_MARKUP = '<path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4M16 17l5-5-5-5M21 12H9"/>'

/** Book / documentation glyph. */
const BOOK_MARKUP = '<path d="M4 19.5A2.5 2.5 0 016.5 17H20"/><path d="M6.5 2H20v20H6.5A2.5 2.5 0 014 19.5v-15A2.5 2.5 0 016.5 2z"/>'

/** Documentation URL from runtime config; empty when the docs site is not configured. */
const { docsUrl } = getRuntimeConfig()

/** The sidebar sections, in display order; a section with no item left draws no label. */
const sections = computed<NavSection[]>(() => NAV_SECTIONS.filter((section) => section.items.length > 0))

/** First letter of the account label, for the footer tile. */
const adminInitial = computed<string>(() => t('common.admin').charAt(0).toUpperCase())

/**
 * Turns SVG path data into the shape markup `UiIcon` draws.
 *
 * @param path - The `d` attribute of a path.
 * @returns Inner SVG markup holding that single path.
 */
const iconMarkup = (path: string): string => `<path d="${path}"/>`

/**
 * Builds the `data-testid` of a nav entry from its route.
 *
 * @param to - The route path.
 * @returns A stable id such as `nav-clients-users`.
 */
const testidOf = (to: string): string => `nav-${to.replace(/^\//, '').replaceAll('/', '-')}`

/**
 * Logs out the current user and redirects to login.
 *
 * @returns Promise that resolves after logout.
 */
const handleLogout = async (): Promise<void> => {
  await authStore.logout()
  await router.push(AppRoute.Login)
}

/**
 * Checks if a nav item or any of its children is currently active.
 *
 * @param item - The navigation item to check.
 * @returns True if the current route matches.
 */
const isActive = (item: NavItem): boolean => {
  if (item.to !== AppRoute.Dashboard && activePath.value.startsWith(item.to)) {
    return true
  }
  return activePath.value === item.to
}

/**
 * Checks if a child route is the currently active route.
 *
 * @param to - The child route path.
 * @returns True if the current route matches exactly.
 */
const isChildActive = (to: string): boolean => {
  if (to === AppRoute.Clients || to === AppRoute.Settings) {
    return activePath.value === to
  }
  return activePath.value.startsWith(to)
}

/**
 * Toggles the expanded state of a parent nav item.
 *
 * @param to - The route path of the item to toggle.
 */
const toggleExpand = (to: string): void => {
  const next = new Set(expanded.value)
  if (next.has(to)) {
    next.delete(to)
  } else {
    next.add(to)
  }
  expanded.value = next
}

/**
 * Returns whether a parent item is currently expanded.
 * Auto-expands if the current route matches any child.
 *
 * @param item - The navigation item.
 * @returns True if expanded.
 */
const isExpanded = (item: NavItem): boolean => {
  if (expanded.value.has(item.to)) {
    return true
  }
  return item.children !== undefined && isActive(item)
}
</script>

<template>
  <aside
    class="flex h-full min-h-0 flex-col"
    :class="embedded ? 'w-full bg-surface-card' : 'w-[220px] shrink-0 border-r border-border bg-surface-panel'"
    data-testid="app-sidebar"
  >
    <!-- Logo: the drawer already has its own title, so the embedded menu leaves it out. -->
    <UiStack v-if="!embedded" direction="row" align="center" gap="none" class="border-b border-border px-5 py-[18px]">
      <!-- The wordmark for the active theme; the operator's upload when there is one. -->
      <BrandLogo img-class="h-7 w-auto" />
    </UiStack>

    <!-- Nav links -->
    <nav class="scroll-subtle flex-1 overflow-y-auto px-2.5 pb-2 pt-3" :aria-label="t('layout.navigation')">
      <UiStack gap="md">
        <!-- One group per section of the navigation config; its label sits over the group. -->
        <UiStack v-for="section in sections" :key="section.id" gap="xs" :data-testid="`nav-section-${section.id}`">
          <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs" class="px-3 pb-0.5 font-semibold uppercase tracking-wider" :data-testid="`nav-section-${section.id}-label`">
            {{ t(section.labelKey) }}
          </UiText>
          <template v-for="item in section.items" :key="item.to">
            <!-- Item WITH children: toggle button, then the sub-menu (one block, so the list's gap does not add a gap for a closed sub-menu) -->
            <div v-if="item.children">
              <UiButton
                type="button"
                variant="plain"
                class="flex w-full items-center gap-2.5 rounded-[9px] px-3 py-2 text-left text-[0.84rem] font-medium transition-all duration-150"
                :class="isActive(item)
                  ? 'bg-primary-500/10 text-text-primary'
                  : 'text-text-secondary hover:bg-surface-hover hover:text-text-primary'"
                :aria-expanded="isExpanded(item)"
                :data-testid="testidOf(item.to)"
                @click="toggleExpand(item.to)"
              >
                <UiIcon :markup="iconMarkup(item.icon)" size="sm" :class="isActive(item) ? 'text-primary-400' : 'opacity-60'" />
                <span class="flex-1">{{ t(item.labelKey) }}</span>
                <UiIcon :markup="CHEVRON_MARKUP" size="sm" class="text-text-muted transition-transform duration-300" :class="isExpanded(item) ? 'rotate-90' : ''" />
              </UiButton>

              <!--
                The sub-menu is drawn as a tree: each link has a short branch (`after`) to a trunk (`before`) that
                runs up the left to the parent's icon, and the trunk of the last link stops at its branch.
                The sub-menu opens and closes over 300 ms: the row grows from zero height (the grid rows go from 0fr to
                1fr, which needs no measured height) while its links fade. A closed sub-menu is `inert`, so its links
                are out of the tab order and unreadable to a screen reader, as `display: none` would make them.
              -->
              <div
                class="grid transition-[grid-template-rows,opacity] duration-300 ease-in-out motion-reduce:transition-none"
                :class="isExpanded(item) ? 'grid-rows-[1fr] opacity-100' : 'grid-rows-[0fr] opacity-0'"
                :inert="!isExpanded(item)"
                :data-testid="`${testidOf(item.to)}-submenu`"
              >
                <div class="min-h-0 overflow-hidden">
                  <UiStack gap="xs" class="py-1 pl-9">
                    <UiLink
                      v-for="child in item.children"
                      :key="child.to"
                      :to="child.to"
                      variant="plain"
                      class="relative flex items-center gap-2 rounded-[7px] px-3 py-[5px] text-[0.78rem] font-medium no-underline transition-all duration-150 before:absolute before:-left-4 before:-top-1 before:w-px before:bg-border-strong before:content-[''] before:h-[calc(100%+0.25rem)] first:before:-top-1.5 first:before:h-[calc(100%+0.5rem)] last:before:h-[calc(50%+0.25rem)] first:last:before:h-[calc(50%+0.5rem)] after:absolute after:-left-4 after:top-1/2 after:h-px after:w-3 after:transition-colors after:duration-300 after:content-['']"
                      :class="isChildActive(child.to)
                        ? 'bg-primary-500/10 text-primary-400 after:bg-primary-400'
                        : 'text-text-muted after:bg-border-strong hover:bg-surface-hover hover:text-text-secondary hover:after:bg-text-muted'"
                      :data-testid="testidOf(child.to)"
                      @click="emit('navigate')"
                    >
                      {{ t(child.labelKey) }}
                    </UiLink>
                  </UiStack>
                </div>
              </div>
            </div>

            <!-- Item WITHOUT children: direct link -->
            <UiLink
              v-else
              :to="item.to"
              variant="plain"
              class="flex items-center gap-2.5 rounded-[9px] px-3 py-2 text-[0.84rem] font-medium no-underline transition-all duration-150"
              :class="isActive(item)
                ? 'bg-primary-500/10 text-text-primary'
                : 'text-text-secondary hover:bg-surface-hover hover:text-text-primary'"
              :data-testid="testidOf(item.to)"
              @click="emit('navigate')"
            >
              <UiIcon :markup="iconMarkup(item.icon)" size="sm" :class="isActive(item) ? 'text-primary-400' : 'opacity-60'" />
              <span>{{ t(item.labelKey) }}</span>
            </UiLink>
          </template>
        </UiStack>
      </UiStack>
      <!-- Documentation link: shown only when docsUrl is configured -->
      <UiLink
        v-if="docsUrl"
        :href="docsUrl"
        external
        class="mt-2 flex items-center gap-2.5 rounded-[9px] px-3 py-2 text-[0.84rem] font-medium no-underline text-text-secondary hover:bg-surface-hover hover:text-text-primary transition-all duration-150"
        data-testid="nav-documentation"
      >
        <UiIcon :markup="BOOK_MARKUP" size="sm" class="opacity-60" />
        <span>{{ t('nav.documentation') }}</span>
      </UiLink>
    </nav>

    <!-- The top bar hides its theme picker on a phone, so the drawer is where a phone changes the theme. -->
    <UiStack v-if="embedded" gap="none" class="border-t border-border px-3 py-3">
      <ThemeSwitch testid="sidebar-theme" />
    </UiStack>

    <!-- Release update notice: shown only when an upgrade is available -->
    <ReleaseUpdateNotice />

    <!-- Footer -->
    <UiStack gap="xs" class="border-t border-border px-3 py-3">
      <UiStack direction="row" align="center" justify="between">
        <UiStack direction="row" align="center" gap="sm">
          <UiText tone="plain" class="flex h-7 w-7 shrink-0 items-center justify-center rounded-[7px] gradient-brand font-display text-[0.7rem] font-bold text-white">
            {{ adminInitial }}
          </UiText>
          <UiText tone="secondary" size="md" class="font-medium">{{ t('common.admin') }}</UiText>
        </UiStack>

        <UiIconButton :label="t('common.signOut')" size="sm" data-testid="sidebar-sign-out" @click="handleLogout">
          <UiIcon :markup="SIGN_OUT_MARKUP" size="sm" />
        </UiIconButton>
      </UiStack>

      <!-- Quiet version line; nothing is drawn until the version is known. -->
      <VersionLabel testid="sidebar-version" />
    </UiStack>
  </aside>
</template>
