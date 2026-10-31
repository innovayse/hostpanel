<script setup lang="ts">
/**
 * Section navigation of the client detail pages.
 *
 * From the medium breakpoint up it is the collapsible icon rail (the kit's `UiSidebar`: icons only,
 * captions on hover). Below it the rail would take a column of a phone's width, so the same entries
 * are a horizontal, scrollable tab strip (the kit's `UiTabs`) that the layout puts above the content.
 */
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import UiSidebar from '@/components/ui/UiSidebar.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import type { UiSidebarItem } from '@/utils/ui/sidebar'
import type { UiTabItem } from '@/utils/ui/tabs'
import { ClientSection, clientSectionPath } from '@/lib/routes'

const { t } = useI18n()

/** Props for ClientInnerSidebar. */
const props = defineProps<{
  /** Client ID for constructing sub-page route paths. */
  clientId: string
}>()

/** Current route, used to mark the active entry. */
const route = useRoute()

/** Router, used to open the section a tab names. */
const router = useRouter()

/** Keys of the sidebar entries; also the suffix of each entry's `data-testid`. */
const ClientNavKey = {
  Summary: 'summary',
  Profile: 'profile',
  Services: 'services',
  Domains: 'domains',
  Users: 'users',
  Contacts: 'contacts',
  Billable: 'billable',
  Invoices: 'invoices',
  Quotes: 'quotes',
  Transactions: 'transactions',
  Tickets: 'tickets',
  Emails: 'emails',
  Log: 'log',
} as const

/** Icon markup (shape tags) per entry key. */
const ICONS: Record<(typeof ClientNavKey)[keyof typeof ClientNavKey], string> = {
  summary: '<rect x="3" y="3" width="7" height="7" rx="1" /><rect x="14" y="3" width="7" height="4" rx="1" /><rect x="14" y="11" width="7" height="10" rx="1" /><rect x="3" y="14" width="7" height="7" rx="1" />',
  profile: '<path d="M20 21v-2a4 4 0 00-4-4H8a4 4 0 00-4 4v2" /><circle cx="12" cy="7" r="4" />',
  services: '<path d="M21 16V8a2 2 0 00-1-1.73l-7-4a2 2 0 00-2 0l-7 4A2 2 0 003 8v8a2 2 0 001 1.73l7 4a2 2 0 002 0l7-4A2 2 0 0021 16z" /><polyline points="3.27 6.96 12 12.01 20.73 6.96" /><line x1="12" y1="22.08" x2="12" y2="12" />',
  domains: '<circle cx="12" cy="12" r="10" /><line x1="2" y1="12" x2="22" y2="12" /><path d="M12 2a15.3 15.3 0 014 10 15.3 15.3 0 01-4 10 15.3 15.3 0 01-4-10 15.3 15.3 0 014-10z" />',
  users: '<path d="M17 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2" /><circle cx="9" cy="7" r="4" /><path d="M23 21v-2a4 4 0 00-3-3.87" /><path d="M16 3.13a4 4 0 010 7.75" />',
  contacts: '<path d="M2 3h6a4 4 0 014 4v14a3 3 0 00-3-3H2z" /><path d="M22 3h-6a4 4 0 00-4 4v14a3 3 0 013-3h7z" />',
  billable: '<line x1="12" y1="1" x2="12" y2="23" /><path d="M17 5H9.5a3.5 3.5 0 000 7h5a3.5 3.5 0 010 7H6" />',
  invoices: '<path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" /><polyline points="14 2 14 8 20 8" /><line x1="16" y1="13" x2="8" y2="13" /><line x1="16" y1="17" x2="8" y2="17" />',
  quotes: '<path d="M16 4h2a2 2 0 012 2v14a2 2 0 01-2 2H6a2 2 0 01-2-2V6a2 2 0 012-2h2" /><rect x="8" y="2" width="8" height="4" rx="1" /><line x1="8" y1="11" x2="16" y2="11" /><line x1="8" y1="15" x2="12" y2="15" />',
  transactions: '<rect x="1" y="4" width="22" height="16" rx="2" ry="2" /><line x1="1" y1="10" x2="23" y2="10" />',
  tickets: '<path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />',
  emails: '<path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z" /><polyline points="22,6 12,13 2,6" />',
  log: '<path d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2" /><rect x="9" y="3" width="6" height="4" rx="1" /><line x1="9" y1="12" x2="15" y2="12" /><line x1="9" y1="16" x2="13" y2="16" />',
}

/** Client section per entry key (the route segment under `/clients/:id/`). */
const SEGMENTS: Record<(typeof ClientNavKey)[keyof typeof ClientNavKey], ClientSection> = {
  summary: ClientSection.Summary,
  profile: ClientSection.Profile,
  services: ClientSection.Services,
  domains: ClientSection.Domains,
  users: ClientSection.Users,
  contacts: ClientSection.Contacts,
  billable: ClientSection.BillableItems,
  invoices: ClientSection.Invoices,
  quotes: ClientSection.Quotes,
  transactions: ClientSection.Transactions,
  tickets: ClientSection.Tickets,
  emails: ClientSection.Emails,
  log: ClientSection.Log,
}

/**
 * Builds one sidebar entry.
 *
 * @param key - Entry key.
 * @param label - Translated caption.
 * @returns The entry for `UiSidebar`.
 */
const entry = (key: (typeof ClientNavKey)[keyof typeof ClientNavKey], label: string): UiSidebarItem => ({
  key,
  label,
  to: clientSectionPath(props.clientId, SEGMENTS[key]),
  icon: ICONS[key],
})

/** Inner sidebar navigation items. */
const navItems = computed<UiSidebarItem[]>(() => [
  entry(ClientNavKey.Summary, t('clients.clientSummary.summary')),
  entry(ClientNavKey.Profile, t('clients.contactForm.profile')),
  entry(ClientNavKey.Services, t('clients.clientSummary.productsServices2')),
  entry(ClientNavKey.Domains, t('shared.domains')),
  entry(ClientNavKey.Users, t('clients.clientInnerSidebar.users')),
  entry(ClientNavKey.Contacts, t('clients.clientSummary.contacts')),
  entry(ClientNavKey.Billable, t('nav.billingChildren.billableItems')),
  entry(ClientNavKey.Invoices, t('shared.invoices')),
  entry(ClientNavKey.Quotes, t('shared.quotes')),
  entry(ClientNavKey.Transactions, t('shared.transactions')),
  entry(ClientNavKey.Tickets, t('clients.clientTickets.tickets')),
  entry(ClientNavKey.Emails, t('clients.clientEmails.emails')),
  entry(ClientNavKey.Log, t('clients.clientInnerSidebar.log')),
])

/** Key of the entry whose path the current route starts with. */
const activeKey = computed<string | undefined>(() => {
  const active = navItems.value.find(item => route.path.startsWith(String(item.to)))
  return active?.key
})

/** The same entries as tabs for the strip shown on small screens. */
const tabItems = computed<UiTabItem[]>(() =>
  navItems.value.map(item => ({ value: item.key, label: item.label, testid: `client-tab-${item.key}` })),
)

/** The tab strip; its element is searched for the selected tab so the strip can bring it into view. */
const strip = ref<{ $el: HTMLElement } | null>(null)

/**
 * Scrolls the strip so the selected tab is visible: a section far to the right is out of sight on load and
 * after a route change otherwise. Only the strip scrolls (`inline`), never the page (`block: 'nearest'`).
 *
 * @returns Resolves once the strip has been updated and scrolled.
 */
const revealActiveTab = async (): Promise<void> => {
  await nextTick()
  const tab = strip.value?.$el.querySelector<HTMLElement>('[role="tab"][aria-selected="true"]')
  tab?.scrollIntoView?.({ inline: 'center', block: 'nearest' })
}

onMounted(revealActiveTab)
watch(activeKey, revealActiveTab)

/**
 * Opens the section of the tab the visitor picked.
 *
 * @param key - Key of the picked entry.
 * @returns Resolves when the navigation is over.
 */
const openSection = async (key: string): Promise<void> => {
  const target = navItems.value.find(item => item.key === key)
  if (target) {
    await router.push(String(target.to))
  }
}
</script>

<template>
  <UiTabs
    ref="strip"
    class="min-w-0 md:hidden"
    :items="tabItems"
    :model-value="activeKey ?? ''"
    :aria-label="t('clients.clientNav.strip')"
    data-testid="client-nav-strip"
    @update:model-value="openSection"
  />
  <div class="hidden shrink-0 md:flex" data-testid="client-nav-rail">
    <UiSidebar :items="navItems" :active-key="activeKey" :aria-label="t('clients.clientNav.rail')" testid="client-nav" />
  </div>
</template>
