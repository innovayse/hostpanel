<script setup lang="ts">
/**
 * Client summary dashboard: shows aggregated billing, service, and activity
 * data across 7 information cards and 3 mini tables.
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useClientSummaryStore } from '@/stores/clientSummary'
import { useClientsStore } from '@/stores/clients'
import { useResultBanner } from '@/composables/useResultBanner'
import { BILLING_CYCLE_OPTIONS, QUOTE_STAGE_OPTIONS, SERVICE_STATUS_OPTIONS } from '@/lib/constants'
import { formatDate } from '@/lib/format'
import { formatPlainAmount, formatPrice } from '@/lib/money'
import { domainStatusTone, serviceStatusTone, statusLabel, clientStatusTone, contactTypeTone, quoteStageTone } from '@/lib/tones'
import ClientExternalAccountCard from '@/components/clients/ClientExternalAccountCard.vue'
import PluginSlot from '@/components/plugins/PluginSlot.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiSectionHeader from '@/components/ui/UiSectionHeader.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import type { QuoteListItem } from '@/types/quote'
import type { DomainRegistration } from '@/types/domain'
import type { ServiceListItem } from '@/types/service'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import type { SetClientExternalAccountPayload } from '@/types/clientexternalaccount'
import type { ClientInvoiceStatuses, ClientRecordCounts, ClientSummaryData } from '@/types/client'
import type { CurrencyAmount } from '@/types/currency'
import { PluginSlotId } from '@/types/pluginslot'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { ClientSection, clientItemPath, clientSectionPath, clientTicketNewPath, quoteAddPath, quotePath } from '@/lib/routes'
import { UiTableDensity } from '@/utils/ui/injection'

const { t } = useI18n()

/** Empty-value dash. */
const DASH = '—'

/** Current route, source of the client id. */
const route = useRoute()

/** Router, used to open a service, domain or quote. */
const router = useRouter()

/** Summary store, the only caller of the summary endpoints. */
const summaryStore = useClientSummaryStore()

/** Clients store, owner of the loaded client. */
const clientsStore = useClientsStore()

/** Client ID from route params. */
const clientId = computed(() => Number(route.params.id))

/** Shorthand for the current client detail. */
const client = computed(() => clientsStore.current)

/** Shorthand for the summary data. */
const summary = computed(() => summaryStore.summary)

/** True while the summary is read for the first time: every card keeps its heading and captions and draws its values as placeholders. */
const initialLoading = computed<boolean>(() => summaryStore.loading && summary.value === null)

/**
 * Billing currency of the client, used only to print a zero for a figure the client has no records
 * behind; every other amount carries the currency the summary endpoint names for it.
 */
const billingCurrency = computed<string | null>(() => client.value?.preferences.currency ?? null)

/**
 * Formats the per-currency amounts of one figure, one text per currency the client has records in.
 *
 * @param amounts - The amounts the summary endpoint returned for the figure.
 * @returns One "12.50 USD" text per currency, or a single zero in the billing currency when there are none.
 */
const amountTexts = (amounts: CurrencyAmount[]): string[] =>
  amounts.length > 0
    ? amounts.map((a) => formatPlainAmount(a.amount, a.currency))
    : [formatPlainAmount(0, billingCurrency.value)]

/** Local admin notes for the textarea. */
const adminNotes = ref('')

/** True while saving admin notes. */
const savingNotes = ref(false)

/** One caption/value line of a card. */
interface InfoRow {
  /** Translated caption. */
  label: string
  /** Value text; null while it is still loading. */
  value: string | null
}

/** One yes/no line of the "other information" card. */
interface FlagRow {
  /** Translated caption. */
  label: string
  /** Whether the flag is on. */
  on: boolean
  /** Text shown for the flag; null while it is still loading. */
  text: string | null
}

/**
 * Computes a human-readable duration from a given ISO date to now.
 *
 * @param iso - ISO 8601 date string.
 * @returns Duration string (e.g. "2 months", "1 year, 3 months").
 */
const computeDuration = (iso: string | null | undefined): string => {
  if (!iso) {
    return DASH
  }
  const start = new Date(iso)
  if (isNaN(start.getTime())) {
    return DASH
  }
  const now = new Date()
  let years = now.getFullYear() - start.getFullYear()
  let months = now.getMonth() - start.getMonth()
  if (months < 0) {
    years--
    months += 12
  }
  const parts: string[] = []
  if (years > 0) {
    parts.push(t('duration.years', { n: years }))
  }
  if (months > 0) {
    parts.push(t('duration.months', { n: months }))
  }
  return parts.length > 0 ? parts.join(', ') : t('duration.lessThanMonth')
}

/** Personal and address lines of the client. */
const clientInfoRows = computed<InfoRow[]>(() => {
  const c = client.value
  return [
    { label: t('shared.firstName'), value: c ? c.firstName || DASH : null },
    { label: t('shared.lastName'), value: c ? c.lastName || DASH : null },
    { label: t('shared.companyName'), value: c ? c.companyName || DASH : null },
    { label: t('shared.emailAddress'), value: c ? c.email || DASH : null },
    { label: t('shared.phoneNumber'), value: c ? c.phone || DASH : null },
    { label: t('shared.address1'), value: c ? c.address.street || DASH : null },
    { label: t('shared.address2'), value: c ? c.address.address2 || DASH : null },
    { label: t('shared.city'), value: c ? c.address.city || DASH : null },
    { label: t('clients.clientSummary.stateRegion'), value: c ? c.address.state || DASH : null },
    { label: t('shared.postcode'), value: c ? c.address.postCode || DASH : null },
    { label: t('shared.country'), value: c ? c.address.country || DASH : null },
  ]
})

/**
 * Builds a yes/no line.
 *
 * @param label - Translated caption.
 * @param on - Whether the flag is on.
 * @param text - Text shown for the flag.
 * @returns The line.
 */
const flag = (label: string, on: boolean, text: string | null): FlagRow => ({ label, on, text })

/** Yes/no lines of the "other information" card. */
const flagRows = computed<FlagRow[]>(() => {
  const c = client.value
  if (!c) {
    return [
      flag(t('shared.taxExempt'), false, null),
      flag(t('clients.clientSummary.autoCcProcessing'), false, null),
      flag(t('clients.clientSummary.overdueReminders'), false, null),
      flag(t('clients.clientSummary.twoFactorAuth'), false, null),
    ]
  }
  /**
   * Translates a boolean.
   *
   * @param on - The flag.
   * @returns "Yes" or "No".
   */
  const yesNo = (on: boolean): string => (on ? t('shared.yes') : t('shared.no'))
  return [
    flag(t('shared.taxExempt'), c.settings.taxExempt, yesNo(c.settings.taxExempt)),
    flag(t('clients.clientSummary.autoCcProcessing'), !c.settings.disableCcProcessing, yesNo(!c.settings.disableCcProcessing)),
    flag(t('clients.clientSummary.overdueReminders'), c.settings.overdueNotices, yesNo(c.settings.overdueNotices)),
    flag(t('clients.clientSummary.twoFactorAuth'), c.twoFactorEnabled, c.twoFactorEnabled ? t('shared.enabled') : t('shared.disabled')),
  ]
})

/** One invoice-status line: caption, count, total. */
interface InvoiceRow {
  /** Translated caption. */
  label: string
  /** Number of invoices; null while it is still loading. */
  count: number | null
  /** Sum of the invoices, one text per currency; empty while it is still loading. */
  totals: string[]
}

/** One caption/amounts line of the income block: a text per currency. */
interface AmountRow {
  /** Translated caption. */
  label: string
  /** Amount texts, one per currency. */
  values: string[]
}

/** Invoice status lines of the billing card: the caption key and the status bucket each line reads. */
const INVOICE_LINES: { label: string; status: keyof ClientInvoiceStatuses }[] = [
  { label: 'clients.clientSummary.paid', status: 'paid' },
  { label: 'clients.clientSummary.draft', status: 'draft' },
  { label: 'clients.clientSummary.unpaidDue', status: 'unpaid' },
  { label: 'clients.clientSummary.overdue', status: 'overdue' },
  { label: 'clients.clientSummary.cancelled', status: 'cancelled' },
  { label: 'clients.clientSummary.partiallyRefunded', status: 'partiallyRefunded' },
  { label: 'clients.clientSummary.refunded', status: 'refunded' },
]

/** Invoice status lines of the billing card; the figures stay empty while the summary loads. */
const invoiceRows = computed<InvoiceRow[]>(() => {
  const invoices = summary.value?.invoices
  return INVOICE_LINES.map(({ label, status }) => ({
    label: t(label),
    count: invoices ? invoices[status].count : null,
    totals: invoices ? amountTexts(invoices[status].totals) : [],
  }))
})

/** Income lines of the billing card: the caption key and the amounts each line reads. */
const INCOME_LINES: { label: string; amounts: (data: ClientSummaryData) => CurrencyAmount[] }[] = [
  {
    label: 'clients.clientSummary.grossRevenue',
    /**
     * Gross revenue per currency.
     *
     * @param data - The loaded summary.
     * @returns The gross revenue amounts.
     */
    amounts: data => data.transactions.grossRevenue,
  },
  {
    label: 'clients.clientSummary.clientExpenses',
    /**
     * Client expenses per currency.
     *
     * @param data - The loaded summary.
     * @returns The expense amounts.
     */
    amounts: data => data.transactions.clientExpenses,
  },
  {
    label: 'clients.clientSummary.netIncome',
    /**
     * Net income per currency.
     *
     * @param data - The loaded summary.
     * @returns The net income amounts.
     */
    amounts: data => data.transactions.netIncome,
  },
  {
    label: 'clients.clientSummary.creditBalance',
    /**
     * The credit balance, a single amount.
     *
     * @param data - The loaded summary.
     * @returns The credit balance as a one-element list.
     */
    amounts: data => [data.creditBalance],
  },
]

/** Income lines of the billing card; the amounts stay empty while the summary loads. */
const incomeRows = computed<AmountRow[]>(() => {
  const data = summary.value
  return INCOME_LINES.map(({ label, amounts }) => ({
    label: t(label),
    values: data ? amountTexts(amounts(data)) : [],
  }))
})

/** Count lines of the products card: the caption key and the value text each line reads. */
const COUNT_LINES: { label: string; text: (counts: ClientRecordCounts) => string }[] = [
  {
    label: 'clients.clientSummary.activeServices',
    /**
     * Active over total services.
     *
     * @param counts - The record counts of the summary.
     * @returns The translated "active of total" text.
     */
    text: counts => t('clients.clientSummary.activeservicescountTotalservicescountTotal', {
      activeServicesCount: counts.activeServicesCount,
      totalServicesCount: counts.totalServicesCount,
    }),
  },
  {
    label: 'shared.domains',
    /**
     * Total domains.
     *
     * @param counts - The record counts of the summary.
     * @returns The domain count as text.
     */
    text: counts => String(counts.totalDomainsCount),
  },
  {
    label: 'clients.clientSummary.acceptedQuotes',
    /**
     * Accepted over total quotes.
     *
     * @param counts - The record counts of the summary.
     * @returns The translated "accepted of total" text.
     */
    text: counts => t('clients.clientSummary.acceptedquotescountTotalquotescountTotal', {
      acceptedQuotesCount: counts.acceptedQuotesCount,
      totalQuotesCount: counts.totalQuotesCount,
    }),
  },
  {
    label: 'clients.clientSummary.openTickets',
    /**
     * Open over total tickets.
     *
     * @param counts - The record counts of the summary.
     * @returns The translated "open of total" text.
     */
    text: counts => t('clients.clientSummary.openticketscountTotalticketscountTotal', {
      openTicketsCount: counts.openTicketsCount,
      totalTicketsCount: counts.totalTicketsCount,
    }),
  },
]

/** Count lines of the products card; the values stay empty while the summary loads. */
const countRows = computed<InfoRow[]>(() => {
  const counts = summary.value?.counts
  return COUNT_LINES.map(({ label, text }) => ({ label: t(label), value: counts ? text(counts) : null }))
})

/** The outcome line of the last notes save, shown under the Save button. */
const notesFeedback = useResultBanner()

/** Tone of the notes outcome line: danger for a refusal, success otherwise. */
const notesFeedbackTone = computed<UiTone>(() => (notesFeedback.isError.value ? UiTone.Danger : UiTone.Success))

/**
 * Saves admin notes for the current client and shows the outcome (success, or the API's sentence).
 *
 * @returns Promise that resolves when save completes.
 */
const saveNotes = async (): Promise<void> => {
  if (!client.value) {
    return
  }
  notesFeedback.clear()
  savingNotes.value = true
  const saved = await clientsStore.saveAdminNotes(clientId.value, adminNotes.value)
  savingNotes.value = false
  if (saved) {
    notesFeedback.show(t('clients.clientSummary.notesSaved'), false)
  } else {
    notesFeedback.show(clientsStore.saveError ?? t('errors.noAnswer'), true)
  }
}

/** Name of the other system in the external account card. */
const externalProvider = ref('')

/** Account id in the external account card. */
const externalAccountId = ref('')

/** The outcome line of the last external account save or clear, shown inside its card. */
const externalAccountNotice = useResultBanner()

/**
 * Sets or clears the external account link and, once the API accepted it, reports it.
 *
 * @param payload - The provider and account id; both null clears the link.
 * @param doneKey - Locale key of the outcome line shown on success.
 * @returns Promise that resolves when the call finishes; a refusal shows as the card's error.
 */
const writeExternalAccount = async (payload: SetClientExternalAccountPayload, doneKey: string): Promise<void> => {
  externalAccountNotice.clear()
  const saved = await clientsStore.saveExternalAccount(clientId.value, payload)
  if (saved) {
    externalAccountNotice.show(t(doneKey), false)
  }
}

/**
 * Saves the typed external account link.
 *
 * @param payload - The provider and account id the operator typed.
 * @returns Promise that resolves when the call finishes.
 */
const saveExternalAccount = (payload: SetClientExternalAccountPayload): Promise<void> =>
  writeExternalAccount(payload, 'clients.externalAccount.saved')

/**
 * Removes the external account link; the refreshed client detail empties the card's inputs.
 *
 * @returns Promise that resolves when the call finishes.
 */
const clearExternalAccount = (): Promise<void> =>
  writeExternalAccount({ provider: null, accountId: null }, 'clients.externalAccount.cleared')

/**
 * Opens a service of this client.
 *
 * @param id - The service id.
 * @returns Nothing.
 */
const openService = (id: number): void => {
  router.push(clientItemPath(clientId.value, ClientSection.Services, id))
}

/**
 * Opens a domain of this client.
 *
 * @param id - The domain id.
 * @returns Nothing.
 */
const openDomain = (id: number): void => {
  router.push(clientItemPath(clientId.value, ClientSection.Domains, id))
}

/**
 * Opens a quote.
 *
 * @param id - The quote id.
 * @returns Nothing.
 */
const openQuote = (id: number): void => {
  router.push(quotePath(id))
}

/**
 * The row actions of a service: one, open it.
 *
 * @param service - The service row.
 * @returns The action list passed to UiRowActions.
 */
const serviceActions = (service: ServiceListItem): UiMenuAction[] => [
  {
    label: t('shared.edit'),
    testid: `client-summary-service-open-${service.id}`,
    /**
     * Opens this row.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      openService(service.id)
    },
  },
]

/**
 * The row actions of a domain: one, open it.
 *
 * @param domain - The domain row.
 * @returns The action list passed to UiRowActions.
 */
const domainActions = (domain: DomainRegistration): UiMenuAction[] => [
  {
    label: t('shared.edit'),
    testid: `client-summary-domain-open-${domain.id}`,
    /**
     * Opens this row.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      openDomain(domain.id)
    },
  },
]

/**
 * The row actions of a quote: one, open it.
 *
 * @param quote - The quote row.
 * @returns The action list passed to UiRowActions.
 */
const quoteActions = (quote: QuoteListItem): UiMenuAction[] => [
  {
    label: t('shared.edit'),
    testid: `client-summary-quote-open-${quote.id}`,
    /**
     * Opens this row.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      openQuote(quote.id)
    },
  },
]

/**
 * Fills the external account inputs when the held link changes. It watches the two values, not the
 * object: a notes save replaces the client with a new object holding the same link, and that must
 * not wipe what the operator is typing in the card.
 *
 * @returns Nothing.
 */
watch([() => client.value?.externalAccount?.provider, () => client.value?.externalAccount?.id], ([provider, id]) => {
  externalProvider.value = provider ?? ''
  externalAccountId.value = id ?? ''
}, { immediate: true })

/** Initialize admin notes from the client detail when it loads. */
watch(() => client.value?.preferences.adminNotes, (val) => {
  adminNotes.value = val ?? ''
}, { immediate: true })

onMounted(() => {
  summaryStore.fetchAll(clientId.value)
})
</script>

<template>
  <UiStack gap="lg">
    <UiHeading :level="1" variant="card">{{ t('clients.clientSummary.summary') }}</UiHeading>

    <UiErrorState
      v-if="summaryStore.error"
      :message="summaryStore.error"
      :loading="summaryStore.loading"
      testid="client-summary-error"
      @retry="summaryStore.fetchAll(clientId)"
    />

    <template v-else>
      <UiGrid :columns="3" :aria-busy="initialLoading ? 'true' : undefined" :data-testid="initialLoading ? 'client-summary-loading' : undefined">
        <UiCard v-if="client || initialLoading" padding="md" data-testid="client-summary-info">
          <UiStack>
            <UiHeading :level="2" variant="card">{{ t('clients.clientSummary.clientInformation') }}</UiHeading>
            <UiStack gap="sm">
              <UiStack v-for="row in clientInfoRows" :key="row.label" direction="row" :justify="UiJustify.Between" gap="sm">
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ row.label }}</UiText>
                <UiSkeletonBar v-if="row.value === null" class="h-4 w-28 rounded-md" aria-hidden="true" />
                <UiText v-else :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ row.value }}</UiText>
              </UiStack>
            </UiStack>
          </UiStack>
        </UiCard>

        <UiCard v-if="client || initialLoading" padding="md" data-testid="client-summary-other">
          <UiStack>
            <UiHeading :level="2" variant="card">{{ t('clients.clientSummary.otherInformation') }}</UiHeading>
            <UiStack gap="sm">
              <UiStack direction="row" :justify="UiJustify.Between" :align="UiAlignItems.Center" gap="sm">
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ t('shared.status') }}</UiText>
                <UiSkeletonBar v-if="!client" class="h-5 w-16 rounded-full" aria-hidden="true" />
                <UiBadge v-else :tone="clientStatusTone(client.status)" small>{{ client.status }}</UiBadge>
              </UiStack>
              <UiStack direction="row" :justify="UiJustify.Between" gap="sm">
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ t('clients.clientSummary.signupDate') }}</UiText>
                <UiSkeletonBar v-if="!client" class="h-4 w-24 rounded-md" aria-hidden="true" />
                <UiText v-else :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ formatDate(client.createdAt) }}</UiText>
              </UiStack>
              <UiStack direction="row" :justify="UiJustify.Between" gap="sm">
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ t('clients.clientSummary.clientFor') }}</UiText>
                <UiSkeletonBar v-if="!client" class="h-4 w-24 rounded-md" aria-hidden="true" />
                <UiText v-else :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ computeDuration(client.createdAt) }}</UiText>
              </UiStack>
              <UiStack v-for="row in flagRows" :key="row.label" direction="row" :justify="UiJustify.Between" gap="sm">
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ row.label }}</UiText>
                <UiSkeletonBar v-if="row.text === null" class="h-4 w-12 rounded-md" aria-hidden="true" />
                <UiText v-else :tone="row.on ? UiTextTone.Success : UiTextTone.Danger" :size="UiTextSize.Md">{{ row.text }}</UiText>
              </UiStack>
            </UiStack>
          </UiStack>
        </UiCard>

        <UiCard v-if="client || initialLoading" padding="md" data-testid="client-summary-contacts">
          <UiStack>
            <UiHeading :level="2" variant="card">{{ t('clients.clientSummary.contacts') }}</UiHeading>
            <UiStack v-if="!client" gap="sm" aria-hidden="true">
              <UiSkeletonBar class="h-4 w-40 rounded-md" />
              <UiSkeletonBar class="h-4 w-32 rounded-md" />
            </UiStack>
            <UiStack v-else-if="client.contacts.length > 0">
              <UiStack
                v-for="contact in client.contacts"
                :key="contact.id"
                direction="row"
                :justify="UiJustify.Between"
                :align="UiAlignItems.Center"
                gap="sm"
              >
                <UiStack gap="none">
                  <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ contact.firstName }} {{ contact.lastName }}</UiText>
                  <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ contact.email }}</UiText>
                </UiStack>
                <UiBadge :tone="contactTypeTone(contact.type)" small>{{ contact.type }}</UiBadge>
              </UiStack>
            </UiStack>
            <UiText v-else :tone="UiTextTone.Muted">{{ t('clients.clientSummary.noAdditionalContacts') }}</UiText>
            <UiStack direction="row">
              <UiLink
                :to="clientSectionPath(clientId, ClientSection.Contacts)"
                :variant="UiButtonVariant.Link"
                data-testid="client-summary-contacts-all"
              >
                {{ t('clients.clientSummary.viewAll') }}
              </UiLink>
            </UiStack>
          </UiStack>
        </UiCard>

        <UiCard v-if="summary || initialLoading" padding="md" data-testid="client-summary-billing">
          <UiStack>
            <UiHeading :level="2" variant="card">{{ t('clients.clientSummary.invoicesBilling') }}</UiHeading>
            <UiStack gap="sm">
              <UiStack v-for="row in invoiceRows" :key="row.label" direction="row" :justify="UiJustify.Between" gap="sm">
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ row.label }}</UiText>
                <UiStack direction="row" gap="md">
                  <UiSkeletonBar v-if="row.count === null" class="h-4 w-6 rounded-md" aria-hidden="true" />
                  <UiText v-else :tone="UiTextTone.Secondary" :size="UiTextSize.Md" class="w-6 text-right tabular-nums" data-testid="client-summary-billing-count">{{ row.count }}</UiText>
                  <UiSkeletonBar v-if="row.count === null" class="h-4 w-20 rounded-md" aria-hidden="true" />
                  <UiStack v-else align="end" gap="xs" class="min-w-[5.5rem]" data-testid="client-summary-billing-amount">
                    <UiText v-for="text in row.totals" :key="text" :tone="UiTextTone.Primary" :size="UiTextSize.Md" class="tabular-nums">{{ text }}</UiText>
                  </UiStack>
                </UiStack>
              </UiStack>
            </UiStack>
            <UiStack gap="sm">
              <UiStack v-for="row in incomeRows" :key="row.label" direction="row" :justify="UiJustify.Between" gap="sm">
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ row.label }}</UiText>
                <UiSkeletonBar v-if="row.values.length === 0" class="h-4 w-20 rounded-md" aria-hidden="true" />
                <UiStack v-else align="end" gap="xs" class="min-w-[5.5rem]">
                  <UiText v-for="text in row.values" :key="text" :tone="UiTextTone.Primary" :size="UiTextSize.Md" class="tabular-nums">{{ text }}</UiText>
                </UiStack>
              </UiStack>
            </UiStack>
            <UiStack direction="row" wrap>
              <UiLink :to="clientSectionPath(clientId, ClientSection.Invoices)" :variant="UiButtonVariant.Link" data-testid="client-summary-create-invoice">
                {{ t('clients.clientSummary.createInvoice') }}
              </UiLink>
              <UiLink :to="clientSectionPath(clientId, ClientSection.BillableItems)" :variant="UiButtonVariant.Link" data-testid="client-summary-add-billable">
                {{ t('shared.addBillableItem') }}
              </UiLink>
              <UiLink :to="quoteAddPath(clientId)" :variant="UiButtonVariant.Link" data-testid="client-summary-create-quote">
                {{ t('clients.clientSummary.createNewQuote') }}
              </UiLink>
            </UiStack>
          </UiStack>
        </UiCard>

        <UiCard v-if="summary || initialLoading" padding="md" data-testid="client-summary-products">
          <UiStack>
            <UiHeading :level="2" variant="card">{{ t('clients.clientSummary.productsServices') }}</UiHeading>
            <UiStack gap="sm">
              <UiStack v-for="row in countRows" :key="row.label" direction="row" :justify="UiJustify.Between" gap="sm">
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ row.label }}</UiText>
                <UiSkeletonBar v-if="row.value === null" class="h-4 w-16 rounded-md" aria-hidden="true" />
                <UiText v-else :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ row.value }}</UiText>
              </UiStack>
            </UiStack>
            <UiStack direction="row" wrap>
              <UiLink :to="clientSectionPath(clientId, ClientSection.Tickets)" :variant="UiButtonVariant.Link" data-testid="client-summary-tickets-all">
                {{ t('clients.clientSummary.viewAllTickets') }}
              </UiLink>
              <UiLink :to="clientTicketNewPath(clientId)" :variant="UiButtonVariant.Link" data-testid="client-summary-ticket-new">
                {{ t('shared.openNewTicket') }}
              </UiLink>
            </UiStack>
          </UiStack>
        </UiCard>

        <UiCard v-if="summary || initialLoading" padding="md" data-testid="client-summary-emails">
          <UiStack>
            <UiHeading :level="2" variant="card">{{ t('clients.clientSummary.recentEmails') }}</UiHeading>
            <UiStack v-if="!summary" gap="sm" aria-hidden="true">
              <UiSkeletonBar class="h-4 w-full rounded-md" />
              <UiSkeletonBar class="h-4 w-2/3 rounded-md" />
            </UiStack>
            <UiStack v-else-if="summary.recentEmails.length > 0" gap="sm">
              <UiStack v-for="email in summary.recentEmails" :key="email.id" direction="row" gap="sm">
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ formatDate(email.sentAt) }}</UiText>
                <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ email.subject }}</UiText>
              </UiStack>
            </UiStack>
            <UiText v-else :tone="UiTextTone.Muted">{{ t('clients.clientSummary.noEmailsSent') }}</UiText>
            <UiStack direction="row">
              <UiLink
                :to="clientSectionPath(clientId, ClientSection.Emails)"
                :variant="UiButtonVariant.Link"
                data-testid="client-summary-emails-all"
              >
                {{ t('clients.clientSummary.viewAll') }}
              </UiLink>
            </UiStack>
          </UiStack>
        </UiCard>

        <UiCard v-if="client || initialLoading" padding="md" data-testid="client-summary-notes">
          <UiStack>
            <UiHeading :level="2" variant="card">{{ t('shared.adminNotes') }}</UiHeading>
            <UiSkeletonBar v-if="!client" class="h-32 w-full rounded-control" aria-hidden="true" />
            <UiTextarea
              v-else
              v-model="adminNotes"
              rows="6"
              :placeholder="t('clients.clientSummary.addNotesAboutThisClient')"
              data-testid="client-summary-notes-input"
            />
            <UiStack direction="row">
              <UiButton
                type="button"
                :variant="UiButtonVariant.Primary"
                :loading="savingNotes"
                :disabled="!client"
                data-testid="client-summary-notes-save"
                @click="saveNotes"
              >
                {{ t('shared.save') }}
              </UiButton>
            </UiStack>
            <UiStatusMessage :message="notesFeedback.message.value" :tone="notesFeedbackTone" data-testid="client-summary-notes-feedback" />
          </UiStack>
        </UiCard>

        <ClientExternalAccountCard
          v-if="client || initialLoading"
          v-model:provider="externalProvider"
          v-model:account-id="externalAccountId"
          :linked="client?.externalAccount ?? null"
          :loading="initialLoading"
          :saving="clientsStore.externalAccountSaving"
          :error="clientsStore.externalAccountError"
          :field-errors="clientsStore.externalAccountFieldErrors"
          :notice="externalAccountNotice.message.value"
          @save="saveExternalAccount"
          @clear="clearExternalAccount"
          @clear-field-error="clientsStore.clearExternalAccountFieldError"
        />
      </UiGrid>

      <UiStack>
        <UiSectionHeader :level="2" variant="card">
          {{ t('clients.clientSummary.productsServices2') }}
          <template #actions>
            <UiLink :to="clientSectionPath(clientId, ClientSection.Services)" :variant="UiButtonVariant.Link" data-testid="client-summary-services-all">
              {{ t('clients.clientSummary.viewAll') }}
            </UiLink>
          </template>
        </UiSectionHeader>
        <UiCard class="overflow-x-auto">
          <UiTable
            :density="UiTableDensity.Comfortable"
            :empty="summaryStore.services.length === 0"
            :loading="initialLoading"
            :colspan="8"
            data-testid="client-summary-services"
          >
            <UiTableHead>
              <UiTableRow>
                <UiTableHeader>{{ t('shared.id') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.productService') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.domain') }}</UiTableHeader>
                <UiTableHeader numeric>{{ t('shared.price') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.billingCycle') }}</UiTableHeader>
                <UiTableHeader>{{ t('clients.clientSummary.nextDue') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
                <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
              </UiTableRow>
            </UiTableHead>
            <UiTableBody divided>
              <UiTableRow
                v-for="service in summaryStore.services"
                :key="service.id"
                hoverable
                class="cursor-pointer"
                @click="openService(service.id)"
              >
                <UiTableCell muted>{{ service.id }}</UiTableCell>
                <UiTableCell>{{ service.productName }}</UiTableCell>
                <UiTableCell muted>{{ service.domain ?? DASH }}</UiTableCell>
                <UiTableCell muted numeric>{{ formatPrice(service.price, service.priceCurrency) }}</UiTableCell>
                <UiTableCell muted>{{ statusLabel(BILLING_CYCLE_OPTIONS, service.billingCycle) }}</UiTableCell>
                <UiTableCell muted>{{ formatDate(service.nextDueDate) }}</UiTableCell>
                <UiTableCell>
                  <UiBadge :tone="serviceStatusTone(service.status)" small>{{ statusLabel(SERVICE_STATUS_OPTIONS, service.status) }}</UiBadge>
                </UiTableCell>
                <UiTableCell actions @click.stop>
                  <UiRowActions :actions="serviceActions(service)" :testid="`client-summary-service-actions-${service.id}`" />
                </UiTableCell>
              </UiTableRow>
            </UiTableBody>
            <template #empty>
              {{ t('clients.clientSummary.noServicesFound') }}
            </template>
          </UiTable>
        </UiCard>
      </UiStack>

      <UiStack>
        <UiSectionHeader :level="2" variant="card">
          {{ t('shared.domains') }}
          <template #actions>
            <UiLink :to="clientSectionPath(clientId, ClientSection.Domains)" :variant="UiButtonVariant.Link" data-testid="client-summary-domains-all">
              {{ t('clients.clientSummary.viewAll') }}
            </UiLink>
          </template>
        </UiSectionHeader>
        <UiCard class="overflow-x-auto">
          <UiTable
            :density="UiTableDensity.Comfortable"
            :empty="summaryStore.domains.length === 0"
            :loading="initialLoading"
            :colspan="7"
            data-testid="client-summary-domains"
          >
            <UiTableHead>
              <UiTableRow>
                <UiTableHeader>{{ t('shared.id') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.domainName') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.registrar') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.expiryDate') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.autoRenew') }}</UiTableHeader>
                <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
              </UiTableRow>
            </UiTableHead>
            <UiTableBody divided>
              <UiTableRow
                v-for="domain in summaryStore.domains"
                :key="domain.id"
                hoverable
                class="cursor-pointer"
                @click="openDomain(domain.id)"
              >
                <UiTableCell muted>{{ domain.id }}</UiTableCell>
                <UiTableCell>{{ domain.name }}</UiTableCell>
                <UiTableCell muted>{{ domain.registrar ?? DASH }}</UiTableCell>
                <UiTableCell>
                  <UiBadge :tone="domainStatusTone(domain.status)" small>{{ domain.status }}</UiBadge>
                </UiTableCell>
                <UiTableCell muted>{{ formatDate(domain.expiresAt) }}</UiTableCell>
                <UiTableCell>
                  <UiText :tone="domain.autoRenew ? UiTextTone.Success : UiTextTone.Muted">
                    {{ domain.autoRenew ? t('shared.yes') : t('shared.no') }}
                  </UiText>
                </UiTableCell>
                <UiTableCell actions @click.stop>
                  <UiRowActions :actions="domainActions(domain)" :testid="`client-summary-domain-actions-${domain.id}`" />
                </UiTableCell>
              </UiTableRow>
            </UiTableBody>
            <template #empty>
              {{ t('clients.clientSummary.noDomainsFound') }}
            </template>
          </UiTable>
        </UiCard>
      </UiStack>

      <UiStack>
        <UiSectionHeader :level="2" variant="card">
          {{ t('clients.clientSummary.currentQuotes') }}
          <template #actions>
            <UiLink :to="clientSectionPath(clientId, ClientSection.Quotes)" :variant="UiButtonVariant.Link" data-testid="client-summary-quotes-all">
              {{ t('clients.clientSummary.viewAll') }}
            </UiLink>
          </template>
        </UiSectionHeader>
        <UiCard class="overflow-x-auto">
          <UiTable
            :density="UiTableDensity.Comfortable"
            :empty="summaryStore.quotes.length === 0"
            :loading="initialLoading"
            :colspan="7"
            data-testid="client-summary-quotes"
          >
            <UiTableHead>
              <UiTableRow>
                <UiTableHeader>{{ t('shared.id') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.subject') }}</UiTableHeader>
                <UiTableHeader>{{ t('clients.clientSummary.createDate') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.validUntil') }}</UiTableHeader>
                <UiTableHeader numeric>{{ t('shared.total') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.stage') }}</UiTableHeader>
                <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
              </UiTableRow>
            </UiTableHead>
            <UiTableBody divided>
              <UiTableRow
                v-for="quote in summaryStore.quotes"
                :key="quote.id"
                hoverable
                class="cursor-pointer"
                @click="openQuote(quote.id)"
              >
                <UiTableCell muted>#{{ quote.id }}</UiTableCell>
                <UiTableCell>{{ quote.subject }}</UiTableCell>
                <UiTableCell muted>{{ formatDate(quote.dateCreated) }}</UiTableCell>
                <UiTableCell muted>{{ formatDate(quote.validUntil) }}</UiTableCell>
                <UiTableCell numeric>{{ formatPlainAmount(quote.total, quote.currency) }}</UiTableCell>
                <UiTableCell>
                  <UiBadge :tone="quoteStageTone(quote.stage)" small>
                    {{ statusLabel(QUOTE_STAGE_OPTIONS, quote.stage) }}
                  </UiBadge>
                </UiTableCell>
                <UiTableCell actions @click.stop>
                  <UiRowActions :actions="quoteActions(quote)" :testid="`client-summary-quote-actions-${quote.id}`" />
                </UiTableCell>
              </UiTableRow>
            </UiTableBody>
            <template #empty>
              {{ t('clients.clientSummary.noQuotesFound') }}
            </template>
          </UiTable>
        </UiCard>
      </UiStack>

      <PluginSlot :key="clientId" :slot-id="PluginSlotId.AdminClientPanels" :context="{ clientId }" />
    </template>
  </UiStack>
</template>
