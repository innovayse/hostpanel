<script setup lang="ts">
/**
 * Admin sidebar with navigation links, grouped under the section labels of `lib/navigation.ts`,
 * which alone decides the order (the menu drawer draws this same component).
 *
 * A full-height column against the left edge: the product mark and name on top, every entry its own icon and a label
 * (a count badge sits only on the child that needs attention, such as the open tickets, and a closed or folded parent shows a dot with the same tooltip), the current page a filled pill, an entry with
 * children opening a sub-menu in place, and the signed-in account at the bottom.
 * Responsive behaviour:
 * - lg+: a 248px column that the operator can fold to a 72px icon rail (remembered by the sidebar store).
 *   In the rail an entry is its icon alone (the label is its tooltip and accessible name) and a click on an
 *   entry with a sub-menu opens the panel again, so a sub-menu is never out of reach.
 * - md up to lg (tablet): always the rail, the fold button is gone (there is no room to unfold it); an entry with a
 *   sub-menu emits `open-menu` so the layout opens the full menu drawer instead.
 * - Emits `navigate` on link click so the mobile drawer can close.
 * - In the menu drawer (`embedded`) it is always drawn in full and
 *   also carries the theme and language menus, which the top bar has no room for on a phone.
 */
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute } from 'vue-router'
import { useMediaQuery } from '@/composables/useMediaQuery'
import { useNavSections } from '@/composables/useNavSections'
import { useBrandingStore } from '@/stores/branding'
import { useDashboardStore } from '@/stores/dashboard'
import { useNavigationStore } from '@/stores/navigation'
import { usePluginUiStore } from '@/stores/pluginUi'
import { useSidebarStore } from '@/stores/sidebar'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiImage from '@/components/ui/UiImage.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import AccountCard from '@/components/layout/AccountCard.vue'
import BrandLogo from '@/components/layout/BrandLogo.vue'
import ReleaseUpdateNotice from '@/components/layout/ReleaseUpdateNotice.vue'
import LocaleMenu from '@/components/layout/LocaleMenu.vue'
import ThemeToggle from '@/components/layout/ThemeToggle.vue'
import VersionLabel from '@/components/layout/VersionLabel.vue'
import UiCollapse from '@/components/ui/UiCollapse.vue'
import UiNavItem from '@/components/ui/UiNavItem.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { getRuntimeConfig } from '@/lib/runtimeConfig'
import { Icon } from '@/lib/icons'
import { AppRoute } from '@/lib/routes'
import { NavAttention } from '@/lib/navigation'
import type { NavChild, NavItem } from '@/lib/navigation'
import { UI_BELOW_LG_QUERY } from '@/utils/ui/breakpoints'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiSize } from '@/utils/ui/size'
import { navListClasses, UiNavItemLevel } from '@/utils/ui/nav'
import { UiAlignItems, UiDirection, UiGap, UiJustify, UiStackTag } from '@/utils/ui/layout'
import { UiTone } from '@/utils/ui/tone'
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
  /** Emitted when an entry with a sub-menu is chosen in the tablet rail, which has no room to open it. */
  'open-menu': []
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

/** Locale key of the wording of each kind of attention. */
const ATTENTION_KEYS: Record<NavAttention, string> = {
  [NavAttention.OpenTickets]: 'nav.openTickets',
}

/** What an attention badge shows and says. */
interface NavAttentionState {
  /** How many things wait. */
  count: number
  /** The count in words ("2 open tickets"), the tooltip and accessible name of the badge. */
  text: string
}

/** Documentation URL from runtime config; empty when the docs site is not configured. */
const { docsUrl } = getRuntimeConfig()

/** Whether the screen is narrower than the desktop sidebar: a tablet, where the panel is always the rail. */
const compact = useMediaQuery(UI_BELOW_LG_QUERY)

/** True when the panel is drawn as the icon rail: folded or on a tablet, and not inside the menu drawer (which is always full). */
const rail = computed<boolean>(() => !props.embedded && (sidebar.collapsed || compact.value))

/** Glyph of the fold button: a left chevron while the panel is open, a right one while it is a rail. */
const foldMarkup = computed<string>(() => (rail.value ? Icon.Chevron : Icon.Back))

/** Accessible name of the fold button. */
const foldLabel = computed<string>(() => (rail.value ? t('layout.expandSidebar') : t('layout.collapseSidebar')))

/** The sidebar sections, in display order, with the Add-ons section while a plugin declares a menu item. */
const sections = useNavSections()

/** What needs the operator's attention, by kind, from the dashboard statistics; zero while they are not loaded. */
const attentionCounts = computed<Record<NavAttention, number>>(() => ({
  [NavAttention.OpenTickets]: dashboard.stats?.openTickets ?? 0,
}))

/**
 * The sentence naming what a child's attention count counts, which is the badge's tooltip and accessible name.
 *
 * @param kind - What needs attention.
 * @param count - How many.
 * @returns The wording in the active language, such as "2 open tickets".
 */
const attentionText = (kind: NavAttention, count: number): string => t(ATTENTION_KEYS[kind], { count }, count)

/**
 * The attention a sub-menu link carries: its kind's count, only when something waits.
 *
 * @param child - The sub-menu link.
 * @returns The count and its wording, or null when the link carries none or nothing waits.
 */
const attentionOfChild = (child: NavChild): NavAttentionState | null => {
  if (child.attention === undefined) {
    return null
  }
  const count = attentionCounts.value[child.attention]
  return count > 0 ? { count, text: attentionText(child.attention, count) } : null
}

/**
 * The attention behind a parent entry: that of its first child with something waiting, which the parent shows as a dot
 * while its sub-menu is closed or folded away.
 *
 * @param item - The entry.
 * @returns The attention state, or null when no child has anything waiting.
 */
const attentionOfParent = (item: NavItem): NavAttentionState | null =>
  (item.children ?? []).map(attentionOfChild).find((state) => state !== null) ?? null

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
 * Toggles the expanded state of a parent nav item; in the rail it first opens the panel, since a sub-menu has no room there
 * (on a tablet the panel cannot unfold, so the menu drawer is asked for instead).
 *
 * @param to - The route path of the item to toggle.
 */
const toggleExpand = (to: string): void => {
  if (rail.value && compact.value) {
    emit('open-menu')
    return
  }
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

/**
 * The accessible name and tooltip of a parent button while the panel is a rail (no text is drawn there): its label, with the
 * waiting count appended.
 *
 * @param item - The entry.
 * @returns The name, or undefined when the drawn label already names the button.
 */
const parentName = (item: NavItem): string | undefined => {
  if (!rail.value) {
    return undefined
  }
  const attention = attentionOfParent(item)
  return attention === null ? labelOf(item) : `${labelOf(item)}, ${attention.text}`
}

/** The scrolling navigation column, searched for the current entry. */
const navElement = ref<InstanceType<typeof UiStack> | null>(null)

/**
 * Scrolls the current page's link (a sub-menu link when it has one) into view once the menu has redrawn, so a column taller than the screen never hides the page the operator is on.
 *
 * @returns Promise that resolves after the menu has redrawn.
 */
const revealActive = async (): Promise<void> => {
  await nextTick()
  const root: unknown = navElement.value?.$el
  if (root instanceof HTMLElement) {
    root.querySelector<HTMLElement>('[aria-current="page"]')?.scrollIntoView?.({ block: 'nearest' })
  }
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
  <UiStack
    :as="UiStackTag.Aside"
    :gap="UiGap.None"
    class="h-full min-h-0"
    :class="embedded
      ? 'w-full'
      : ['shrink-0 overflow-hidden border-r border-border-shell bg-surface-sidebar transition-[width] duration-(--duration-slow) motion-reduce:transition-none', rail ? 'w-18' : 'w-62']"
    data-testid="sidebar"
  >
    <!-- Header: the brand (the operator's logo when one is uploaded, else the product mark and name) with the fold button beside it, or under the mark in the rail. The drawer has its own title and no fold button, so the embedded menu leaves the header out. -->
    <UiStack
      v-if="!embedded"
      :direction="rail ? UiDirection.Column : UiDirection.Row"
      :align="UiAlignItems.Center"
      :justify="rail ? UiJustify.Center : UiJustify.Between"
      :gap="UiGap.None"
      class="h-(--shell-header-h) shrink-0 gap-0.5 border-b border-border-shell"
      :class="rail ? 'px-3' : 'gap-2 px-6'"
    >
      <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.None" class="min-w-0 gap-2.5">
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
      <UiIconButton v-if="!compact" :label="foldLabel" :variant="UiButtonVariant.Ghost" :size="UiSize.Sm" data-testid="sidebar-collapse-toggle" @click="sidebar.toggle()">
        <UiIcon :markup="foldMarkup" :size="UiSize.Sm" />
      </UiIconButton>
    </UiStack>

    <!-- Nav links -->
    <UiStack :as="UiStackTag.Nav" ref="navElement" :gap="UiGap.None" class="min-h-0 flex-1 scroll-pt-3 overflow-y-auto overflow-x-hidden px-3 pb-2 pt-3" :aria-label="t('layout.navigation')">
      <UiStack :gap="UiGap.Sm">
        <!-- One group per section of the navigation config; its label sits over the group, a hairline stands in for it in the rail. -->
        <UiStack v-for="section in sections" :key="section.id" :gap="UiGap.None" class="gap-0.5" :data-testid="`nav-section-${section.id}`">
          <UiText
            v-if="!rail && section.labelKey !== undefined"
            :tone="UiTextTone.Muted"
            :size="UiTextSize.Xs"
            class="px-3 pb-0.5 pt-1 font-display font-semibold uppercase tracking-wider"
            :data-testid="`nav-section-${section.id}-label`"
          >
            {{ t(section.labelKey) }}
          </UiText>
          <UiStack v-else-if="rail && section.labelKey !== undefined" :gap="UiGap.None" class="mx-2 border-t border-border" aria-hidden="true" />
          <template v-for="item in section.items" :key="item.to">
            <!-- Item WITH children: toggle button, then the sub-menu (one block, so the list's gap does not add a gap for a closed sub-menu) -->
            <UiStack v-if="item.children" :gap="UiGap.None">
              <UiNavItem
                :label="labelOf(item)"
                :icon="item.icon"
                :active="isActive(item)"
                :rail="rail"
                :expanded="isExpanded(item)"
                :hint="parentName(item)"
                :data-testid="testidOf(item.to)"
                @click="toggleExpand(item.to)"
              >
                <template #badge>
                  <!-- The folded rail has no sub-menu to hold the count: the dot on the button says something waits inside. -->
                  <UiBadge v-if="rail && attentionOfParent(item)" :tone="UiTone.Primary" marker class="absolute right-1.5 top-1.5 ring-2 ring-surface-sidebar" aria-hidden="true" :data-testid="`${testidOf(item.to)}-attention`" />
                  <!-- A closed sub-menu hides its count, so the parent carries a dot until it is opened. -->
                  <UiBadge v-else-if="!rail && !isExpanded(item) && attentionOfParent(item)" :tone="UiTone.Primary" marker :title="attentionOfParent(item)?.text" :aria-label="attentionOfParent(item)?.text" :data-testid="`${testidOf(item.to)}-attention`" />
                </template>
              </UiNavItem>

              <!--
                The sub-menu is drawn as a tree: every link draws its own piece of the guide (`nav-guide-row`, a trunk through the whole row and
                a curve to the label), so a label that wraps to two lines just makes a longer trunk. The pieces down to the
                active link are lit in a gradient, the others muted.
                The sub-menu opens and closes over 300 ms (`UiCollapse`: the row grows from zero height while its links fade). A closed
                sub-menu stays mounted but hidden and inert, so its links are out of the tab order and unreadable to a screen reader.
                The rail has no sub-menu: opening one unfolds the panel first.
              -->
              <UiCollapse v-if="!rail" :open="isExpanded(item)" keep-mounted>
                <UiStack :gap="UiGap.None" :class="navListClasses(activeChildIndex(item))" :data-testid="`${testidOf(item.to)}-submenu`">
                  <UiNavItem
                    v-for="(child, index) in item.children"
                    :key="child.to"
                    :level="UiNavItemLevel.Sub"
                    :label="t(child.labelKey)"
                    :to="child.to"
                    :active="isChildActive(child.to)"
                    :stop="index"
                    :active-stop="activeChildIndex(item)"
                    :data-testid="testidOf(child.to)"
                    @click="emit('navigate')"
                  >
                    <template #badge>
                      <UiBadge v-if="attentionOfChild(child)" :tone="UiTone.Primary" small :title="attentionOfChild(child)?.text" :aria-label="attentionOfChild(child)?.text" :data-testid="`${testidOf(child.to)}-count`">{{ attentionOfChild(child)?.count }}</UiBadge>
                    </template>
                  </UiNavItem>
                </UiStack>
              </UiCollapse>
            </UiStack>

            <!-- Item WITHOUT children: direct link -->
            <UiNavItem
              v-else
              :label="labelOf(item)"
              :icon="item.icon"
              :to="item.to"
              :active="isActive(item)"
              :rail="rail"
              :data-testid="testidOf(item.to)"
              @click="emit('navigate')"
            />
          </template>
        </UiStack>
      </UiStack>
      <!-- Documentation link: shown only when docsUrl is configured -->
      <UiNavItem
        v-if="docsUrl"
        :label="t('nav.documentation')"
        :icon="Icon.Book"
        :href="docsUrl"
        external
        :rail="rail"
        class="mt-2"
        data-testid="nav-documentation"
      />
    </UiStack>

    <!-- The top bar hides its theme and language pickers on a phone, so the drawer is where a phone changes them. -->
    <UiStack v-if="embedded" :direction="UiDirection.Row" :align="UiAlignItems.Center" :justify="UiJustify.End" :gap="UiGap.Xs" class="border-t border-border-shell px-4 py-3">
      <ThemeToggle testid="sidebar-theme" />
      <LocaleMenu testid="sidebar-locale" />
    </UiStack>

    <!-- Release update notice: shown only when an upgrade is available -->
    <ReleaseUpdateNotice v-if="!rail" />

    <!-- The signed-in account, with its menu. -->
    <UiStack :gap="UiGap.None" class="shrink-0 px-3 pt-2" :class="rail ? 'pb-4' : 'pb-1'">
      <AccountCard :rail="rail" />
    </UiStack>

    <!-- Quiet version line; nothing is drawn until the version is known. -->
    <UiStack v-if="!rail" :gap="UiGap.None" class="shrink-0 px-6 pb-3 pt-1">
      <VersionLabel testid="sidebar-version" />
    </UiStack>
  </UiStack>
</template>
