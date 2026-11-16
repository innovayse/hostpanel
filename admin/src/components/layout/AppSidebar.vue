<script setup lang="ts">
/**
 * Admin sidebar with navigation links, grouped under the section labels of `lib/navigation.ts`,
 * which alone decides the order (the phone drawer draws this same component).
 *
 * A full-height column against the left edge: the product mark and name on top, every entry its own icon and a label
 * (with a count where the dashboard statistics know one), the current page a filled pill, an entry with
 * children opening a sub-menu in place, and the signed-in account at the bottom.
 * Responsive behaviour:
 * - lg+: a 248px column that the operator can fold to a 72px icon rail (remembered by the sidebar store).
 *   In the rail an entry is its icon alone (the label is its tooltip and accessible name) and a click on an
 *   entry with a sub-menu opens the panel again, so a sub-menu is never out of reach.
 * - Emits `navigate` on link click so the mobile drawer can close.
 * - In the menu sheet (`embedded`) it is always drawn in full (the sheet keeps it clear of the home indicator) and
 *   also carries the theme and language menus, which the top bar has no room for on a phone.
 */
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute } from 'vue-router'
import { useNavSections } from '@/composables/useNavSections'
import { useBrandingStore } from '@/stores/branding'
import { useDashboardStore } from '@/stores/dashboard'
import { useNavigationStore } from '@/stores/navigation'
import { usePluginUiStore } from '@/stores/pluginUi'
import { useSidebarStore } from '@/stores/sidebar'
import UiButton from '@/components/ui/UiButton.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiImage from '@/components/ui/UiImage.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import AccountCard from '@/components/layout/AccountCard.vue'
import BrandLogo from '@/components/layout/BrandLogo.vue'
import ReleaseUpdateNotice from '@/components/layout/ReleaseUpdateNotice.vue'
import LocaleMenu from '@/components/layout/LocaleMenu.vue'
import ThemeMenu from '@/components/layout/ThemeMenu.vue'
import VersionLabel from '@/components/layout/VersionLabel.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { getRuntimeConfig } from '@/lib/runtimeConfig'
import { Icon } from '@/lib/icons'
import { AppRoute } from '@/lib/routes'
import type { NavItem } from '@/lib/navigation'
import { navItemTextClasses, navSubItemTextClasses } from '@/utils/ui/nav'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

/** Props of the sidebar. */
const props = withDefaults(defineProps<{
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
const { t, locale } = useI18n()

/** Current route, to mark the active item. */
const route = useRoute()

/** The navigation in flight, whose target is marked while its screen downloads. */
const navigation = useNavigationStore()

/** Plugin ui store: the menu items plugins declare, drawn as the Add-ons section. */
const pluginUi = usePluginUiStore()

/** Sidebar store: whether the desktop panel is folded to the icon rail. */
const sidebar = useSidebarStore()

/** Branding store: whether the operator uploaded a logo for the brand slot. */
const branding = useBrandingStore()

/** Dashboard store: the statistics behind the count badges. */
const dashboard = useDashboardStore()

/**
 * The path the menu marks as active: the target of a navigation still downloading its screen, so
 * the clicked item lights up at once, or else the current route.
 */
const activePath = computed<string>(() => navigation.pending?.path ?? route.path)

/** Set of expanded parent item routes (keyed by `to`, stable across locales). */
const expanded = ref<Set<string>>(new Set())

/** Size and type shared by every top-level entry: 42px tall, 12px corners, 14px type (the kit's navigation size). */
const ITEM_BASE_CLASSES = `flex h-[2.625rem] items-center rounded-control ${navItemTextClasses} transition-all duration-150`

/**
 * Shape of a sub-menu link: a fixed 2rem row (the guide's maths relies on it), 13px type (the kit's sub-menu size).
 * `before` is its elbow: a small rounded corner at the row's vertical centre joining the list's rail to the link, 16px
 * left of the row so neither the hover fill nor the active pill ever covers it.
 */
const CHILD_BASE_CLASSES = `relative flex h-8 items-center gap-2 rounded-[7px] px-3 ${navSubItemTextClasses} no-underline transition-all duration-150 before:absolute before:-left-4 before:top-[calc(50%-0.5rem)] before:h-2 before:w-2.5 before:rounded-bl-lg before:border-b-[1.5px] before:border-l-[1.5px] before:transition-colors before:duration-300 before:content-['']`

/**
 * Shape of a sub-menu list: one continuous muted rail (`before`) from the parent row down to the centre of the last row
 * (the 1.25rem under it are the list's bottom padding and half a row), and over it the lit overlay (`after`) from the top
 * to the centre of the active row, 1.25rem plus 2.25rem for every row above it, filled with the start to end gradient.
 */
const CHILD_LIST_CLASSES = "relative py-1 pl-9 before:absolute before:bottom-5 before:left-5 before:top-0 before:w-[1.5px] before:bg-border-strong before:content-[''] after:absolute after:left-5 after:top-0 after:h-[calc(1.25rem+var(--n)*2.25rem)] after:w-[1.5px] after:bg-linear-to-b after:from-(--nav-guide-start) after:to-(--nav-guide-end) after:content-['']"

/** Elbow colour of a row that is not on the lit path (below the active one, or no active one): the muted hairline. */
const CHILD_GUIDE_IDLE_CLASSES = 'before:border-border-strong'

/** Elbow colour of a row on the lit path (up to and including the active one): the overlay's colour at that row. */
const CHILD_GUIDE_LIT_CLASSES = 'nav-guide-lit before:border-(--nav-guide-from)'

/** Place of a link on the guide (`--i`), as literal classes since a class built at run time emits no CSS; as long as the longest sub-menu. */
const CHILD_STOP_CLASSES = ['[--i:0]', '[--i:1]', '[--i:2]', '[--i:3]', '[--i:4]', '[--i:5]', '[--i:6]', '[--i:7]', '[--i:8]', '[--i:9]', '[--i:10]', '[--i:11]'] as const

/** Place of the active link on the guide (`--n`), the same literals for the same reason. */
const ACTIVE_STOP_CLASSES = ['[--n:0]', '[--n:1]', '[--n:2]', '[--n:3]', '[--n:4]', '[--n:5]', '[--n:6]', '[--n:7]', '[--n:8]', '[--n:9]', '[--n:10]', '[--n:11]'] as const

/** Look of an entry that is not the current page; the transparent border keeps both looks the same size. */
const ITEM_IDLE_CLASSES = 'border border-transparent text-text-secondary hover:bg-surface-hover hover:text-text-primary'

/** Look of the current page: the filled accent pill with its hairline. */
const ITEM_ACTIVE_CLASSES = 'border border-(color:--nav-active-border) bg-(--nav-active-bg) text-text-primary'

/** Look of a sub-menu link that is not the current page. */
const CHILD_IDLE_CLASSES = 'text-text-muted hover:bg-surface-hover hover:text-text-secondary'

/** Look of the sub-menu link of the current page. */
const CHILD_ACTIVE_CLASSES = 'bg-surface-hover text-text-accent'

/** Icon colour of an entry: muted. */
const ICON_IDLE_CLASSES = 'text-text-muted'

/** Icon colour of the current page: the accent. */
const ICON_ACTIVE_CLASSES = 'text-text-accent'

/** Documentation URL from runtime config; empty when the docs site is not configured. */
const { docsUrl } = getRuntimeConfig()

/** True when the panel is drawn as the icon rail: folded, and not inside the phone drawer (which is always full). */
const rail = computed<boolean>(() => !props.embedded && sidebar.collapsed)

/** Glyph of the fold button: a left chevron while the panel is open, a right one while it is a rail. */
const foldMarkup = computed<string>(() => (rail.value ? Icon.Chevron : Icon.Back))

/** Accessible name of the fold button. */
const foldLabel = computed<string>(() => (rail.value ? t('layout.expandSidebar') : t('layout.collapseSidebar')))

/** The sidebar sections, in display order, with the Add-ons section while a plugin declares a menu item. */
const sections = useNavSections()

/** The count badge of each entry that has one, by route: the registered clients and the open tickets. */
const counts = computed<Record<string, number | undefined>>(() => ({
  [AppRoute.Clients]: dashboard.stats?.totalClients,
  [AppRoute.Support]: dashboard.stats?.openTickets,
}))

/**
 * The count to show beside an entry.
 *
 * @param item - The entry.
 * @returns The count, or undefined when the entry has none or the statistics are not loaded yet.
 */
const countOf = (item: NavItem): number | undefined => counts.value[item.to]

/**
 * The label of a top-level entry: the panel's own through its i18n key, a plugin's as the API sent it.
 *
 * @param item - The entry.
 * @returns The label to draw.
 */
const labelOf = (item: NavItem): string => (item.labelKey === undefined ? item.label : t(item.labelKey))

/**
 * Builds the `data-testid` of a nav entry from its route.
 *
 * @param to - The route path.
 * @returns A stable id such as `nav-clients-users`.
 */
const testidOf = (to: string): string => `nav-${to.replace(/^\//, '').replaceAll('/', '-')}`

/**
 * Checks if a nav item or any of its children is currently active.
 *
 * @param item - The navigation item to check.
 * @returns True if the current route matches.
 */
const isActive = (item: NavItem): boolean => {
  // A plugin's entries nest inside each other (`/apps/x` and `/apps/x/usage`): only the page on screen is marked.
  if (item.labelKey === undefined) {
    return activePath.value === item.to
  }
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
  if (to === AppRoute.Clients) {
    return activePath.value === to
  }
  return activePath.value.startsWith(to)
}

/**
 * Finds the sub-menu link of the current page.
 *
 * @param item - The entry whose sub-menu is searched.
 * @returns The index of the active child, or -1 when none is active.
 */
const activeChildIndex = (item: NavItem): number => (item.children ?? []).findIndex((child) => isChildActive(child.to))

/**
 * Picks the elbow classes of a sub-menu link from its place relative to the active one: the elbows on the path from the
 * parent to the active link are lit in the gradient's colour, the rest stay a muted hairline (no pixel measuring).
 *
 * @param index - The index of the link in its sub-menu.
 * @param activeIndex - The index of the active link, or -1.
 * @returns The guide classes of the link.
 */
const childGuideClasses = (index: number, activeIndex: number): string => {
  if (activeIndex === -1 || index > activeIndex) {
    return CHILD_GUIDE_IDLE_CLASSES
  }
  return `${CHILD_GUIDE_LIT_CLASSES} ${CHILD_STOP_CLASSES[index]}`
}

/**
 * Picks the classes of a sub-menu list: the rail and, when a link is active, the lit overlay down to it (`--n`);
 * with no active link the overlay is not drawn.
 *
 * @param activeIndex - The index of the active link, or -1.
 * @returns The list classes.
 */
const childListClasses = (activeIndex: number): string =>
  activeIndex === -1 ? `${CHILD_LIST_CLASSES} after:hidden` : `${CHILD_LIST_CLASSES} ${ACTIVE_STOP_CLASSES[activeIndex]}`

/**
 * Toggles the expanded state of a parent nav item; in the rail it first opens the panel, since a sub-menu has no room there.
 *
 * @param to - The route path of the item to toggle.
 */
const toggleExpand = (to: string): void => {
  if (rail.value) {
    sidebar.setCollapsed(false)
  }
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

/** The scrolling navigation column, searched for the current entry. */
const navElement = ref<HTMLElement | null>(null)

/**
 * Scrolls the current page's link (a sub-menu link when it has one) into view once the menu has redrawn, so a column taller than the screen never hides the page the operator is on.
 *
 * @returns Promise that resolves after the menu has redrawn.
 */
const revealActive = async (): Promise<void> => {
  await nextTick()
  navElement.value?.querySelector<HTMLElement>('[aria-current="page"]')?.scrollIntoView?.({ block: 'nearest' })
}

onMounted(() => {
  void pluginUi.loadUi()
  void revealActive()
})

// A route change opens a sub-menu or moves the mark, and the entry may sit below the fold.
watch(activePath, () => {
  void revealActive()
})

// The plugins' labels come in the operator's language, so a language switch reads the index again.
watch(locale, () => {
  void pluginUi.loadUi(true)
})
</script>

<template>
  <aside
    class="flex h-full min-h-0 flex-col"
    :class="embedded
      ? 'w-full'
      : ['shrink-0 overflow-hidden border-r border-border-shell bg-surface-sidebar transition-[width] duration-(--duration-slow) motion-reduce:transition-none', rail ? 'w-18' : 'w-62']"
    data-testid="app-sidebar"
  >
    <!-- Header: the brand (the operator's logo when one is uploaded, else the product mark and name) with the fold button beside it, or under the mark in the rail. The drawer has its own title and no fold button, so the embedded menu leaves the header out. -->
    <UiStack
      v-if="!embedded"
      :direction="rail ? 'column' : 'row'"
      align="center"
      :justify="rail ? 'center' : 'between'"
      gap="none"
      class="h-(--shell-header-h) shrink-0 gap-0.5 border-b border-border-shell"
      :class="rail ? 'px-3' : 'gap-2 px-6'"
    >
      <UiStack direction="row" align="center" gap="none" class="min-w-0 gap-2.5">
        <BrandLogo v-if="branding.hasLogo && !rail" img-class="h-8 w-auto max-w-full" />
        <template v-else>
          <UiImage src="/favicon.svg" class="size-[34px] shrink-0" data-testid="sidebar-brand-mark" />
          <UiText
            v-if="!rail"
            :tone="UiTextTone.Plain"
            class="truncate font-display text-lg font-bold"
            data-testid="sidebar-brand-name"
          >
            {{ t('shared.brandName') }}
          </UiText>
        </template>
      </UiStack>
      <UiIconButton :label="foldLabel" size="sm" data-testid="sidebar-collapse-toggle" @click="sidebar.toggle()">
        <UiIcon :markup="foldMarkup" size="sm" />
      </UiIconButton>
    </UiStack>

    <!-- Nav links -->
    <nav ref="navElement" class="min-h-0 flex-1 scroll-pt-3 overflow-y-auto overflow-x-hidden px-3 pb-2 pt-3" :aria-label="t('layout.navigation')">
      <UiStack gap="sm">
        <!-- One group per section of the navigation config; its label sits over the group, a hairline stands in for it in the rail. -->
        <UiStack v-for="section in sections" :key="section.id" gap="none" class="gap-0.5" :data-testid="`nav-section-${section.id}`">
          <UiText
            v-if="!rail && section.labelKey !== undefined"
            :tone="UiTextTone.Muted"
            :size="UiTextSize.Xs"
            class="px-3 pb-0.5 pt-1 font-display font-semibold uppercase tracking-wider"
            :data-testid="`nav-section-${section.id}-label`"
          >
            {{ t(section.labelKey) }}
          </UiText>
          <UiStack v-else-if="rail && section.labelKey !== undefined" gap="none" class="mx-2 border-t border-border" aria-hidden="true" />
          <template v-for="item in section.items" :key="item.to">
            <!-- Item WITH children: toggle button, then the sub-menu (one block, so the list's gap does not add a gap for a closed sub-menu) -->
            <div v-if="item.children">
              <UiButton
                type="button"
                variant="plain"
                class="w-full text-left"
                :class="[ITEM_BASE_CLASSES, rail ? 'justify-center px-0' : 'gap-3 px-3', isActive(item) ? ITEM_ACTIVE_CLASSES : ITEM_IDLE_CLASSES]"
                :aria-expanded="isExpanded(item)"
                :aria-label="rail ? labelOf(item) : undefined"
                :title="rail ? labelOf(item) : undefined"
                :data-testid="testidOf(item.to)"
                @click="toggleExpand(item.to)"
              >
                <UiIcon :markup="item.icon" size="sm" :class="isActive(item) ? ICON_ACTIVE_CLASSES : ICON_IDLE_CLASSES" />
                <template v-if="!rail">
                  <span class="flex-1">{{ labelOf(item) }}</span>
                  <UiText v-if="countOf(item) !== undefined" :tone="UiTextTone.Muted" :size="UiTextSize.Xs" :data-testid="`${testidOf(item.to)}-count`">{{ countOf(item) }}</UiText>
                  <UiIcon :markup="Icon.Chevron" size="sm" class="text-text-muted transition-transform duration-300" :class="isExpanded(item) ? 'rotate-90' : ''" />
                </template>
              </UiButton>

              <!--
                The sub-menu is drawn as a tree: each link has a small rounded elbow (`before`) off one rail (the list's
                `before`) that runs down from the parent's icon to the last link's elbow; an overlay (the list's `after`) lights the
                rail in a gradient down to the active link.
                The sub-menu opens and closes over 300 ms: the row grows from zero height (the grid rows go from 0fr to
                1fr, which needs no measured height) while its links fade. A closed sub-menu is `inert`, so its links
                are out of the tab order and unreadable to a screen reader, as `display: none` would make them.
                The rail has no sub-menu: opening one unfolds the panel first.
              -->
              <div
                v-if="!rail"
                class="grid transition-[grid-template-rows,opacity] duration-300 ease-in-out motion-reduce:transition-none"
                :class="isExpanded(item) ? 'grid-rows-[1fr] opacity-100' : 'grid-rows-[0fr] opacity-0'"
                :inert="!isExpanded(item)"
                :data-testid="`${testidOf(item.to)}-submenu`"
              >
                <div class="min-h-0 overflow-hidden">
                  <UiStack gap="none" class="gap-1" :class="childListClasses(activeChildIndex(item))">
                    <UiLink
                      v-for="(child, index) in item.children"
                      :key="child.to"
                      :to="child.to"
                      variant="plain"
                      :class="[CHILD_BASE_CLASSES, childGuideClasses(index, activeChildIndex(item)), isChildActive(child.to) ? CHILD_ACTIVE_CLASSES : CHILD_IDLE_CLASSES]"
                      :aria-current="isChildActive(child.to) ? 'page' : undefined"
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
              class="no-underline"
              :class="[ITEM_BASE_CLASSES, rail ? 'justify-center px-0' : 'gap-3 px-3', isActive(item) ? ITEM_ACTIVE_CLASSES : ITEM_IDLE_CLASSES]"
              :aria-current="isActive(item) ? 'page' : undefined"
              :aria-label="rail ? labelOf(item) : undefined"
              :title="rail ? labelOf(item) : undefined"
              :data-testid="testidOf(item.to)"
              @click="emit('navigate')"
            >
              <UiIcon :markup="item.icon" size="sm" :class="isActive(item) ? ICON_ACTIVE_CLASSES : ICON_IDLE_CLASSES" />
              <span v-if="!rail" class="flex-1">{{ labelOf(item) }}</span>
              <UiText v-if="!rail && countOf(item) !== undefined" :tone="UiTextTone.Muted" :size="UiTextSize.Xs" :data-testid="`${testidOf(item.to)}-count`">{{ countOf(item) }}</UiText>
            </UiLink>
          </template>
        </UiStack>
      </UiStack>
      <!-- Documentation link: shown only when docsUrl is configured -->
      <UiLink
        v-if="docsUrl"
        :href="docsUrl"
        external
        class="mt-2 no-underline"
        :class="[ITEM_BASE_CLASSES, rail ? 'justify-center px-0' : 'gap-3 px-3', ITEM_IDLE_CLASSES]"
        :aria-label="rail ? t('nav.documentation') : undefined"
        :title="rail ? t('nav.documentation') : undefined"
        data-testid="nav-documentation"
      >
        <UiIcon :markup="Icon.Book" size="sm" :class="ICON_IDLE_CLASSES" />
        <span v-if="!rail">{{ t('nav.documentation') }}</span>
      </UiLink>
    </nav>

    <!-- The top bar hides its theme and language pickers on a phone, so the drawer is where a phone changes them. -->
    <UiStack v-if="embedded" direction="row" align="center" justify="end" gap="xs" class="border-t border-border-shell px-4 py-3">
      <ThemeMenu testid="sidebar-theme" />
      <LocaleMenu testid="sidebar-locale" />
    </UiStack>

    <!-- Release update notice: shown only when an upgrade is available -->
    <ReleaseUpdateNotice v-if="!rail" />

    <!-- The signed-in account, with its menu. -->
    <UiStack gap="none" class="px-4 pt-2" :class="rail ? 'pb-4' : 'pb-2'">
      <AccountCard :rail="rail" />
    </UiStack>

    <!-- Quiet version line; nothing is drawn until the version is known. -->
    <UiStack v-if="!rail" gap="none" class="px-4 pb-4 pt-1">
      <VersionLabel testid="sidebar-version" />
    </UiStack>
  </aside>
</template>
