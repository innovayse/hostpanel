<script setup lang="ts">
/**
 * Admin sidebar with navigation links.
 *
 * Supports expandable submenus for items with children.
 * Responsive behaviour:
 * - lg+: full sidebar (220px), always visible
 * - Emits `navigate` on link click so the mobile drawer can close.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useNavigationStore } from '@/stores/navigation'
import { useReleaseStatusStore } from '@/stores/releaseStatus'
import UiButton from '@/components/ui/UiButton.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import BrandLogo from '@/components/layout/BrandLogo.vue'
import ReleaseUpdateNotice from '@/components/layout/ReleaseUpdateNotice.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { AppRoute } from '@/lib/routes'

/** A sub-menu child item. */
interface NavChild {
  /** Route path. */
  to: string
  /** i18n key for the display label. */
  labelKey: string
}

/** A top-level navigation item, optionally with children. */
interface NavItem {
  /** Route path (used when no children). */
  to: string
  /** i18n key for the display label. */
  labelKey: string
  /** SVG path data for the icon. */
  icon: string
  /** Optional sub-menu items. */
  children?: NavChild[]
}

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

/** Release status store: fetched once on mount to show the upgrade notice when available. */
const releaseStatusStore = useReleaseStatusStore()

/** Fetches the release status once the sidebar is mounted. */
onMounted(() => {
  void releaseStatusStore.fetch()
})

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

/** Top-level navigation items with SVG path icons. */
const navItems: NavItem[] = [
  {
    to: AppRoute.Dashboard, labelKey: 'nav.dashboard',
    icon: 'M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6',
  },
  {
    to: AppRoute.Clients, labelKey: 'nav.clients',
    icon: 'M17 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2M9 11a4 4 0 100-8 4 4 0 000 8zm12 2v2m0 0v2m0-2h2m-2 0h-2',
    children: [
      { to: AppRoute.Clients, labelKey: 'nav.clientsChildren.view' },
      { to: AppRoute.ClientsUsers, labelKey: 'nav.clientsChildren.users' },
      { to: AppRoute.ClientsSharedHostings, labelKey: 'nav.clientsChildren.sharedHostings' },
      { to: AppRoute.ClientsAddons, labelKey: 'nav.clientsChildren.addons' },
      { to: AppRoute.ClientsDomainRegistrations, labelKey: 'nav.clientsChildren.domainRegistrations' },
      { to: AppRoute.ClientsCancellations, labelKey: 'nav.clientsChildren.cancellations' },
      { to: AppRoute.ClientsAffiliates, labelKey: 'nav.clientsChildren.affiliates' },
    ],
  },
  {
    to: AppRoute.Billing, labelKey: 'nav.billing',
    icon: 'M9 14l6-6m-5.5.5h.01m4.99 5h.01M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16l3.5-2 3.5 2 3.5-2 3.5 2z',
    children: [
      { to: AppRoute.BillingTransactions, labelKey: 'nav.billingChildren.transactions' },
      { to: AppRoute.BillingInvoices, labelKey: 'nav.billingChildren.invoices' },
      { to: AppRoute.BillingBillableItems, labelKey: 'nav.billingChildren.billableItems' },
      { to: AppRoute.BillingQuotes, labelKey: 'nav.billingChildren.quotes' },
      { to: AppRoute.BillingOfflineCc, labelKey: 'nav.billingChildren.offlineCc' },
      { to: AppRoute.BillingDisputes, labelKey: 'nav.billingChildren.disputes' },
      { to: AppRoute.BillingGatewayLog, labelKey: 'nav.billingChildren.gatewayLog' },
    ],
  },
  {
    to: AppRoute.Reports, labelKey: 'nav.reports',
    icon: 'M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z',
  },
  {
    to: AppRoute.Orders, labelKey: 'nav.orders',
    icon: 'M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 7h6m-6 4h4',
  },
  {
    to: AppRoute.Domains, labelKey: 'nav.domains',
    icon: 'M21 12a9 9 0 01-9 9m9-9a9 9 0 00-9-9m9 9H3m9 9a9 9 0 01-9-9m9 9c1.657 0 3-4.03 3-9s-1.343-9-3-9m0 18c-1.657 0-3-4.03-3-9s1.343-9 3-9',
  },
  {
    to: AppRoute.Support, labelKey: 'nav.support',
    icon: 'M18.364 5.636l-3.536 3.536m0 5.656l3.536 3.536M9.172 9.172L5.636 5.636m3.536 9.192l-3.536 3.536M21 12a9 9 0 11-18 0 9 9 0 0118 0zm-5 0a4 4 0 11-8 0 4 4 0 018 0z',
    children: [
      { to: AppRoute.SupportOverview, labelKey: 'nav.supportChildren.overview' },
      { to: AppRoute.SupportTickets, labelKey: 'nav.supportChildren.tickets' },
      { to: AppRoute.SupportTicketsNew, labelKey: 'nav.supportChildren.newTicket' },
      { to: AppRoute.SupportPredefinedReplies, labelKey: 'nav.supportChildren.predefinedReplies' },
      { to: AppRoute.SupportAnnouncements, labelKey: 'nav.supportChildren.announcements' },
      { to: AppRoute.SupportDownloads, labelKey: 'nav.supportChildren.downloads' },
      { to: AppRoute.SupportKnowledgebase, labelKey: 'nav.supportChildren.knowledgebase' },
      { to: AppRoute.SupportNetworkIssues, labelKey: 'nav.supportChildren.networkIssues' },
    ],
  },
  {
    to: AppRoute.Plugins, labelKey: 'nav.plugins',
    icon: 'M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4',
  },
  {
    to: AppRoute.Servers, labelKey: 'nav.servers',
    icon: 'M5 12H3a2 2 0 00-2 2v4a2 2 0 002 2h18a2 2 0 002-2v-4a2 2 0 00-2-2h-2M5 12V8a2 2 0 012-2h10a2 2 0 012 2v4M5 12h14M8 20h.01M12 20h.01M16 20h.01',
  },
  {
    to: AppRoute.Integrations, labelKey: 'nav.integrations',
    icon: 'M13 10V3L4 14h7v7l9-11h-7z',
  },
  {
    to: AppRoute.Migration, labelKey: 'nav.migration',
    icon: 'M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M17 8l-5-5-5 5M12 3v12',
  },
  {
    to: AppRoute.Storefront, labelKey: 'nav.storefront',
    icon: 'M3 9l1-5h16l1 5M3 9v11h18V9M3 9h18M9 20v-6h6v6',
    children: [
      { to: AppRoute.StorefrontTestimonials, labelKey: 'nav.storefrontChildren.testimonials' },
      { to: AppRoute.StorefrontPartners, labelKey: 'nav.storefrontChildren.partners' },
      { to: AppRoute.StorefrontProjects, labelKey: 'nav.storefrontChildren.projects' },
      { to: AppRoute.StorefrontFaqs, labelKey: 'nav.storefrontChildren.faqs' },
      { to: AppRoute.StorefrontLocations, labelKey: 'nav.storefrontChildren.locations' },
    ],
  },
  {
    to: AppRoute.Settings, labelKey: 'nav.settings',
    icon: 'M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z M15 12a3 3 0 11-6 0 3 3 0 016 0z',
    children: [
      { to: AppRoute.Settings, labelKey: 'nav.settingsChildren.general' },
      { to: AppRoute.SettingsProducts, labelKey: 'nav.settingsChildren.products' },
      { to: AppRoute.SettingsTldPricing, labelKey: 'nav.settingsChildren.tldPricing' },
      { to: AppRoute.SettingsSlides, labelKey: 'nav.settingsChildren.slides' },
      { to: AppRoute.SettingsEmailTemplates, labelKey: 'nav.settingsChildren.emailTemplates' },
      { to: AppRoute.IntegrationsPayments, labelKey: 'nav.settingsChildren.gateways' },
      { to: AppRoute.SettingsCurrencies, labelKey: 'nav.settingsChildren.currencies' },
    ],
  },
]

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
      <UiStack gap="xs">
        <template v-for="item in navItems" :key="item.to">
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
    </nav>

    <!-- Release update notice: shown only when an upgrade is available -->
    <ReleaseUpdateNotice />

    <!-- Footer -->
    <UiStack direction="row" align="center" justify="between" class="border-t border-border px-3 py-3">
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
  </aside>
</template>
