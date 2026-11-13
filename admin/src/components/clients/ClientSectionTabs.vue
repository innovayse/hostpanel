<script setup lang="ts">
/**
 * Section navigation of the client detail pages: one tab per section (the kit's `UiTabs`), a horizontal strip
 * that scrolls by itself when the sections do not fit, at every screen width. The layout puts it under the
 * client's header card and above the content.
 */
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import UiTabs from '@/components/ui/UiTabs.vue'
import type { UiTabItem } from '@/utils/ui/tabs'
import { ClientSection, clientSectionPath } from '@/lib/routes'

const { t } = useI18n()

/** Props for ClientSectionTabs. */
const props = defineProps<{
  /** Client ID for constructing sub-page route paths. */
  clientId: string
}>()

/** Current route, used to mark the active entry. */
const route = useRoute()

/** Router, used to open the section a tab names. */
const router = useRouter()

/** Keys of the sections; also the suffix of each tab's `data-testid`. */
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
  Credit: 'credit',
  Tickets: 'tickets',
  Emails: 'emails',
  Log: 'log',
} as const

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
  credit: ClientSection.Credit,
  tickets: ClientSection.Tickets,
  emails: ClientSection.Emails,
  log: ClientSection.Log,
}

/** One section of the client: its key, caption and route path. */
interface SectionEntry {
  /** Key of the section; also the tab value. */
  key: string
  /** Translated caption. */
  label: string
  /** Route path of the section for this client. */
  path: string
}

/**
 * Builds one section entry.
 *
 * @param key - Section key.
 * @param label - Translated caption.
 * @returns The entry the tabs and the router read.
 */
const entry = (key: (typeof ClientNavKey)[keyof typeof ClientNavKey], label: string): SectionEntry => ({
  key,
  label,
  path: clientSectionPath(props.clientId, SEGMENTS[key]),
})

/** The sections in tab order. */
const sections = computed<SectionEntry[]>(() => [
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
  entry(ClientNavKey.Credit, t('clients.clientCredit.credit')),
  entry(ClientNavKey.Tickets, t('clients.clientTickets.tickets')),
  entry(ClientNavKey.Emails, t('clients.clientEmails.emails')),
  entry(ClientNavKey.Log, t('clients.clientInnerSidebar.log')),
])

/** Key of the section whose path the current route starts with. */
const activeKey = computed<string | undefined>(() => {
  const active = sections.value.find(section => route.path.startsWith(section.path))
  return active?.key
})

/** The sections as tabs. */
const tabItems = computed<UiTabItem[]>(() =>
  sections.value.map(section => ({ value: section.key, label: section.label, testid: `client-tab-${section.key}` })),
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

/**
 * Watches the strip's size and the size of every tab. The strip narrows after the first scroll when the page
 * below it grows a scroll bar (the content loads after the strip), and a tab changes width when the web font
 * replaces the fallback one after that scroll. The list keeps its box in the second case (it scrolls, so its
 * width does not follow its content), so the tabs are watched too. A selected tab scrolled into view before
 * either change then sits past the edge, so every change brings it back.
 */
let stripResize: ResizeObserver | null = null

/** Brings the selected tab into view, then keeps it there while the strip or its tabs change size. */
onMounted(() => {
  void revealActiveTab()
  const list = strip.value?.$el.querySelector<HTMLElement>('[role="tablist"]')
  if (list) {
    stripResize = new ResizeObserver(() => {
      void revealActiveTab()
    })
    stripResize.observe(list)
    list.querySelectorAll<HTMLElement>('[role="tab"]').forEach((tab) => {
      stripResize?.observe(tab)
    })
  }
})

/** Stops watching the strip, so a left page holds no observer. */
onBeforeUnmount(() => {
  stripResize?.disconnect()
  stripResize = null
})

watch(activeKey, revealActiveTab)

/**
 * Opens the section of the tab the visitor picked.
 *
 * @param key - Key of the picked entry.
 * @returns Resolves when the navigation is over.
 */
const openSection = async (key: string): Promise<void> => {
  const target = sections.value.find(section => section.key === key)
  if (target) {
    await router.push(target.path)
  }
}
</script>

<template>
  <UiTabs
    ref="strip"
    class="min-w-0"
    :items="tabItems"
    :model-value="activeKey ?? ''"
    :aria-label="t('clients.clientNav.strip')"
    hide-scrollbar
    data-testid="client-nav-strip"
    @update:model-value="openSection"
  />
</template>
