<script setup lang="ts">
/**
 * Client summary dashboard: shows aggregated billing, service, and activity
 * data across 9 information cards and 3 tables. The cards, the cards of the tables and the columns of the tables are
 * the ones of the screen's schema, drawn by `UiDetailCards`, which also draws their skeleton while the summary is read for the
 * first time.
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useClientSummaryStore } from '@/stores/clientSummary'
import { useClientsStore } from '@/stores/clients'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { BILLING_CYCLE_OPTIONS, CLIENT_STATUS_OPTIONS, CONTACT_TYPE_OPTIONS, DOMAIN_STATUS_OPTIONS, QUOTE_STAGE_OPTIONS, SERVICE_STATUS_OPTIONS } from '@/lib/constants'
import { formatDate, EMPTY_VALUE } from '@/lib/format'
import { formatPlainAmount, formatPrice } from '@/lib/money'
import { domainStatusTone, serviceStatusTone, statusLabel, clientStatusTone, contactTypeTone, quoteStageTone } from '@/lib/tones'
import ClientExternalAccountCard from '@/components/clients/ClientExternalAccountCard.vue'
import ClientVatCard from '@/components/clients/ClientVatCard.vue'
import PluginSlot from '@/components/plugins/PluginSlot.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiDetailCards from '@/components/ui/UiDetailCards.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiSectionHeader from '@/components/ui/UiSectionHeader.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusBadge from '@/components/ui/UiStatusBadge.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
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
import { UiButtonVariant, UiButtonType } from '@/utils/ui/button'
import { UiAlignItems, UiJustify, UiWidth, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { Icon } from '@/lib/icons'
import { ClientSection, clientItemPath, clientSectionPath, clientTicketNewPath, quoteAddPath, quotePath } from '@/lib/routes'
import { schemaHeader } from '@/lib/schemaHeader'
import { UiTableDensity } from '@/utils/ui/injection'
import schema, {
  CountRow,
  IncomeRow,
  InfoRow,
  InvoiceRow,
  OtherRow,
  SUMMARY_CARDS_PART,
  SUMMARY_DOMAINS_TABLE,
  SUMMARY_QUOTES_TABLE,
  SUMMARY_SERVICES_TABLE,
  SummaryCard,
} from '@/views/ClientSummaryView.schema'
import { UiActionKind } from '@/utils/ui/actions'

const { t } = useI18n()

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

/** The text and tone of one value of the "other information" card. */
interface OtherValue {
  /** Text shown for the value. */
  text: string
  /** Tone of the text: primary for a plain value, success or danger for a flag that is on or off. */
  tone: UiTextTone
}

/** The amounts of one invoice status line: how many invoices, and their sum per currency. */
interface InvoiceValue {
  /** Number of invoices. */
  count: number
  /** Sum of the invoices, one text per currency. */
  totals: string[]
}

/**
 * Computes a human-readable duration from a given ISO date to now.
 *
 * @param iso - ISO 8601 date string.
 * @returns Duration string (e.g. "2 months", "1 year, 3 months").
 */
const computeDuration = (iso: string | null | undefined): string => {
  if (!iso) {
    return EMPTY_VALUE
  }
  const start = new Date(iso)
  if (isNaN(start.getTime())) {
    return EMPTY_VALUE
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

/** Caption keys of the personal and address lines, in the order the schema lists them. */
const INFO_LABELS: string[] = Object.values(InfoRow)

/** Caption keys of the status, date and flag lines, in the order the schema lists them. */
const OTHER_LABELS: string[] = Object.values(OtherRow)

/** Caption keys of the invoice status lines, in the order the schema lists them. */
const INVOICE_LABELS: InvoiceRow[] = Object.values(InvoiceRow)

/** Caption keys of the income lines, in the order the schema lists them. */
const INCOME_LABELS: IncomeRow[] = Object.values(IncomeRow)

/** Caption keys of the record count lines, in the order the schema lists them. */
const COUNT_LABELS: CountRow[] = Object.values(CountRow)

/** Cards that read the loaded client; the others read the summary. */
const CLIENT_CARDS: readonly string[] = [
  SummaryCard.Info,
  SummaryCard.Other,
  SummaryCard.Contacts,
  SummaryCard.Notes,
  SummaryCard.ExternalAccount,
  SummaryCard.Vat,
]

/**
 * Tells whether a card has its data to show: while the summary loads every card is drawn (as its skeleton), afterwards a
 * card is drawn once what it reads has arrived.
 *
 * @param key - The key of the card in the schema.
 * @returns True when the card is drawn.
 */
const hasCardData = (key: string): boolean => {
  if (initialLoading.value) {
    return true
  }
  return CLIENT_CARDS.includes(key) ? Boolean(client.value) : Boolean(summary.value)
}

/** The columns of the schema without the cards whose data has not arrived. */
const visibleColumns = computed(() => SUMMARY_CARDS_PART.columns.map(column => column.filter(card => hasCardData(card.key))))

/** Values of the personal and address lines, by caption key. */
const infoValues = computed((): Record<string, string> => {
  const c = client.value
  if (!c) {
    return {}
  }
  return {
    [InfoRow.FirstName]: c.firstName || EMPTY_VALUE,
    [InfoRow.LastName]: c.lastName || EMPTY_VALUE,
    [InfoRow.CompanyName]: c.companyName || EMPTY_VALUE,
    [InfoRow.Email]: c.email || EMPTY_VALUE,
    [InfoRow.Phone]: c.phone || EMPTY_VALUE,
    [InfoRow.Address1]: c.address.street || EMPTY_VALUE,
    [InfoRow.Address2]: c.address.address2 || EMPTY_VALUE,
    [InfoRow.City]: c.address.city || EMPTY_VALUE,
    [InfoRow.State]: c.address.state || EMPTY_VALUE,
    [InfoRow.Postcode]: c.address.postCode || EMPTY_VALUE,
    [InfoRow.Country]: c.address.country || EMPTY_VALUE,
  }
})

/**
 * A plain value of the "other information" card.
 *
 * @param text - The text to show.
 * @returns The value in the primary tone.
 */
const plainValue = (text: string): OtherValue => ({ text, tone: UiTextTone.Primary })

/**
 * A yes/no value of the "other information" card.
 *
 * @param on - Whether the flag is on.
 * @param text - The text to show for it.
 * @returns The value, green when the flag is on and red when it is off.
 */
const flagValue = (on: boolean, text: string): OtherValue => ({ text, tone: on ? UiTextTone.Success : UiTextTone.Danger })

/**
 * Translates a boolean.
 *
 * @param on - The flag.
 * @returns "Yes" or "No".
 */
const yesNo = (on: boolean): string => (on ? t('shared.yes') : t('shared.no'))

/** Values of the "other information" lines but the status badge, by caption key. */
const otherValues = computed((): Record<string, OtherValue> => {
  const c = client.value
  if (!c) {
    return {}
  }
  return {
    [OtherRow.SignupDate]: plainValue(formatDate(c.createdAt)),
    [OtherRow.ClientFor]: plainValue(computeDuration(c.createdAt)),
    [OtherRow.TaxExempt]: flagValue(c.settings.taxExempt, yesNo(c.settings.taxExempt)),
    [OtherRow.AutoCcProcessing]: flagValue(!c.settings.disableCcProcessing, yesNo(!c.settings.disableCcProcessing)),
    [OtherRow.OverdueReminders]: flagValue(c.settings.overdueNotices, yesNo(c.settings.overdueNotices)),
    [OtherRow.TwoFactor]: flagValue(c.twoFactorEnabled, c.twoFactorEnabled ? t('shared.enabled') : t('shared.disabled')),
  }
})

/** The bucket of the summary each invoice status line reads. */
const INVOICE_STATUS: Record<InvoiceRow, keyof ClientInvoiceStatuses> = {
  [InvoiceRow.Paid]: 'paid',
  [InvoiceRow.Draft]: 'draft',
  [InvoiceRow.Unpaid]: 'unpaid',
  [InvoiceRow.Overdue]: 'overdue',
  [InvoiceRow.Cancelled]: 'cancelled',
  [InvoiceRow.PartiallyRefunded]: 'partiallyRefunded',
  [InvoiceRow.Refunded]: 'refunded',
}

/** Count and sums of the invoice status lines, by caption key. */
const invoiceValues = computed<Record<string, InvoiceValue>>(() => {
  const invoices = summary.value?.invoices
  if (!invoices) {
    return {}
  }
  return Object.fromEntries(INVOICE_LABELS.map((label) => {
    const bucket = invoices[INVOICE_STATUS[label]]
    return [label, { count: bucket.count, totals: amountTexts(bucket.totals) }]
  }))
})

/** The amounts each income line reads from the loaded summary. */
const INCOME_AMOUNTS: Record<IncomeRow, (data: ClientSummaryData) => CurrencyAmount[]> = {
  /**
   * Gross revenue per currency.
   *
   * @param data - The loaded summary.
   * @returns The gross revenue amounts.
   */
  [IncomeRow.GrossRevenue]: data => data.transactions.grossRevenue,
  /**
   * Client expenses per currency.
   *
   * @param data - The loaded summary.
   * @returns The expense amounts.
   */
  [IncomeRow.ClientExpenses]: data => data.transactions.clientExpenses,
  /**
   * Net income per currency.
   *
   * @param data - The loaded summary.
   * @returns The net income amounts.
   */
  [IncomeRow.NetIncome]: data => data.transactions.netIncome,
  /**
   * The credit balance, a single amount.
   *
   * @param data - The loaded summary.
   * @returns The credit balance as a one-element list.
   */
  [IncomeRow.CreditBalance]: data => [data.creditBalance],
}

/** Amount texts of the income lines, one per currency, by caption key. */
const incomeValues = computed<Record<string, string[]>>(() => {
  const data = summary.value
  if (!data) {
    return {}
  }
  return Object.fromEntries(INCOME_LABELS.map(label => [label, amountTexts(INCOME_AMOUNTS[label](data))]))
})

/** The value text each record count line reads from the loaded counts. */
const COUNT_TEXTS: Record<CountRow, (counts: ClientRecordCounts) => string> = {
  /**
   * Active over total services.
   *
   * @param counts - The record counts of the summary.
   * @returns The translated "active of total" text.
   */
  [CountRow.ActiveServices]: counts => t('clients.clientSummary.activeservicescountTotalservicescountTotal', {
    activeServicesCount: counts.activeServicesCount,
    totalServicesCount: counts.totalServicesCount,
  }),
  /**
   * Total domains.
   *
   * @param counts - The record counts of the summary.
   * @returns The domain count as text.
   */
  [CountRow.Domains]: counts => String(counts.totalDomainsCount),
  /**
   * Accepted over total quotes.
   *
   * @param counts - The record counts of the summary.
   * @returns The translated "accepted of total" text.
   */
  [CountRow.AcceptedQuotes]: counts => t('clients.clientSummary.acceptedquotescountTotalquotescountTotal', {
    acceptedQuotesCount: counts.acceptedQuotesCount,
    totalQuotesCount: counts.totalQuotesCount,
  }),
  /**
   * Open over total tickets.
   *
   * @param counts - The record counts of the summary.
   * @returns The translated "open of total" text.
   */
  [CountRow.OpenTickets]: counts => t('clients.clientSummary.openticketscountTotalticketscountTotal', {
    openTicketsCount: counts.openTicketsCount,
    totalTicketsCount: counts.totalTicketsCount,
  }),
}

/** Value texts of the record count lines, by caption key. */
const countValues = computed<Record<string, string>>(() => {
  const counts = summary.value?.counts
  if (!counts) {
    return {}
  }
  return Object.fromEntries(COUNT_LABELS.map(label => [label, COUNT_TEXTS[label](counts)]))
})

/** Toasts the outcome of the last notes save. */
const notesFeedback = useOutcomeToast()

/**
 * Saves admin notes for the current client and shows the outcome (success, or the API's sentence).
 *
 * @returns Promise that resolves when save completes.
 */
const saveNotes = async (): Promise<void> => {
  if (!client.value) {
    return
  }
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

/** Toasts the outcome of the last external account save or clear. */
const externalAccountNotice = useOutcomeToast()

/**
 * Sets or clears the external account link and toasts the outcome.
 *
 * @param payload - The provider and account id; both null clears the link.
 * @param doneKey - Locale key of the outcome line shown on success.
 * @returns Promise that resolves when the call finishes; a refusal is toasted with the API's sentence.
 */
const writeExternalAccount = async (payload: SetClientExternalAccountPayload, doneKey: string): Promise<void> => {
  const saved = await clientsStore.saveExternalAccount(clientId.value, payload)
  if (saved) {
    externalAccountNotice.show(t(doneKey), false)
  } else {
    externalAccountNotice.show(clientsStore.externalAccountError ?? t('errors.noAnswer'), true)
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

/** Toasts the outcome of the last VAT number check. */
const vatNotice = useOutcomeToast()

/**
 * Runs the VAT number check again and toasts the outcome: a recorded verdict is reported; a refusal (the service gave no verdict
 * included) is toasted with the API's sentence and the status stays as it was.
 *
 * @returns Promise that resolves when the call finishes.
 */
const checkVat = async (): Promise<void> => {
  const checked = await clientsStore.checkVat(clientId.value)
  if (checked) {
    vatNotice.show(t('clients.vat.checked'), false)
  } else {
    vatNotice.show(clientsStore.vatError ?? t('errors.noAnswer'), true)
  }
}

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
    icon: Icon.Edit,
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
    icon: Icon.Edit,
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
    icon: Icon.Edit,
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
  <UiStack :gap="UiGap.Lg">
    <UiPageHeader v-bind="schemaHeader(schema, t)" />

    <UiErrorState
      v-if="summaryStore.error"
      :message="summaryStore.error"
      :loading="summaryStore.loading"
      testid="client-summary-error"
      @retry="summaryStore.fetchAll(clientId)"
    />

    <template v-else>
      <UiDetailCards :columns="visibleColumns" :loading="initialLoading">
        <template #card-info>
          <UiStack :gap="UiGap.Sm">
            <UiStack v-for="label in INFO_LABELS" :key="label" :direction="UiDirection.Row" :justify="UiJustify.Between" :gap="UiGap.Sm">
              <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ t(label) }}</UiText>
              <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ infoValues[label] }}</UiText>
            </UiStack>
          </UiStack>
        </template>

        <template #card-other>
          <UiStack :gap="UiGap.Sm">
            <UiStack v-for="label in OTHER_LABELS" :key="label" :direction="UiDirection.Row" :justify="UiJustify.Between" :align="UiAlignItems.Center" :gap="UiGap.Sm">
              <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ t(label) }}</UiText>
              <UiBadge v-if="label === OtherRow.Status && client" :tone="clientStatusTone(client.status)" small>{{ statusLabel(CLIENT_STATUS_OPTIONS, client.status) }}</UiBadge>
              <UiText v-else :tone="otherValues[label]?.tone" :size="UiTextSize.Md">{{ otherValues[label]?.text }}</UiText>
            </UiStack>
          </UiStack>
        </template>

        <template #card-contacts>
          <UiStack v-if="client && client.contacts.length > 0">
            <UiStack
              v-for="contact in client.contacts"
              :key="contact.id"
              :direction="UiDirection.Row"
              :justify="UiJustify.Between"
              :align="UiAlignItems.Center"
              :gap="UiGap.Sm"
            >
              <UiStack :gap="UiGap.None">
                <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ contact.firstName }} {{ contact.lastName }}</UiText>
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ contact.email }}</UiText>
              </UiStack>
              <UiBadge :tone="contactTypeTone(contact.type)" small>{{ statusLabel(CONTACT_TYPE_OPTIONS, contact.type) }}</UiBadge>
            </UiStack>
          </UiStack>
          <UiText v-else :tone="UiTextTone.Muted">{{ t('clients.clientSummary.noAdditionalContacts') }}</UiText>
          <UiStack :direction="UiDirection.Row">
            <UiLink
              :to="clientSectionPath(clientId, ClientSection.Contacts)"
              :variant="UiButtonVariant.Link"
              data-testid="client-summary-contacts-all"
            >
              {{ t('shared.viewAll') }}
            </UiLink>
          </UiStack>
        </template>

        <template #card-billing>
          <UiStack :gap="UiGap.Sm">
            <UiStack v-for="label in INVOICE_LABELS" :key="label" :direction="UiDirection.Row" :justify="UiJustify.Between" :gap="UiGap.Sm">
              <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ t(label) }}</UiText>
              <UiStack :direction="UiDirection.Row" :gap="UiGap.Md">
                <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Md" class="w-6 text-right tabular-nums" data-testid="client-summary-billing-count">{{ invoiceValues[label]?.count }}</UiText>
                <UiStack :align="UiAlignItems.End" :gap="UiGap.Xs" :min-width="UiWidth.Xs" data-testid="client-summary-billing-amount">
                  <UiText v-for="text in invoiceValues[label]?.totals" :key="text" :tone="UiTextTone.Primary" :size="UiTextSize.Md" class="tabular-nums">{{ text }}</UiText>
                </UiStack>
              </UiStack>
            </UiStack>
          </UiStack>
          <UiStack :gap="UiGap.Sm">
            <UiStack v-for="label in INCOME_LABELS" :key="label" :direction="UiDirection.Row" :justify="UiJustify.Between" :gap="UiGap.Sm">
              <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ t(label) }}</UiText>
              <UiStack :align="UiAlignItems.End" :gap="UiGap.Xs" :min-width="UiWidth.Xs">
                <UiText v-for="text in incomeValues[label]" :key="text" :tone="UiTextTone.Primary" :size="UiTextSize.Md" class="tabular-nums">{{ text }}</UiText>
              </UiStack>
            </UiStack>
          </UiStack>
          <UiStack :direction="UiDirection.Row" wrap>
            <UiLink :to="clientSectionPath(clientId, ClientSection.Invoices)" :variant="UiButtonVariant.Link" data-testid="client-summary-create-invoice">
              {{ t('shared.createInvoice') }}
            </UiLink>
            <UiLink :to="clientSectionPath(clientId, ClientSection.BillableItems)" :variant="UiButtonVariant.Link" data-testid="client-summary-add-billable">
              {{ t('shared.addBillableItem') }}
            </UiLink>
            <UiLink :to="quoteAddPath(clientId)" :variant="UiButtonVariant.Link" data-testid="client-summary-create-quote">
              {{ t('shared.createQuote') }}
            </UiLink>
          </UiStack>
        </template>

        <template #card-products>
          <UiStack :gap="UiGap.Sm">
            <UiStack v-for="label in COUNT_LABELS" :key="label" :direction="UiDirection.Row" :justify="UiJustify.Between" :gap="UiGap.Sm">
              <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ t(label) }}</UiText>
              <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ countValues[label] }}</UiText>
            </UiStack>
          </UiStack>
          <UiStack :direction="UiDirection.Row" wrap>
            <UiLink :to="clientSectionPath(clientId, ClientSection.Tickets)" :variant="UiButtonVariant.Link" data-testid="client-summary-tickets-all">
              {{ t('clients.clientSummary.viewAllTickets') }}
            </UiLink>
            <UiLink :to="clientTicketNewPath(clientId)" :variant="UiButtonVariant.Link" data-testid="client-summary-ticket-new">
              {{ t('shared.createTicket') }}
            </UiLink>
          </UiStack>
        </template>

        <template #card-emails>
          <UiStack v-if="summary && summary.recentEmails.length > 0" :gap="UiGap.Sm">
            <UiStack v-for="email in summary.recentEmails" :key="email.id" :direction="UiDirection.Row" :gap="UiGap.Sm">
              <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ formatDate(email.sentAt) }}</UiText>
              <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ email.subject }}</UiText>
            </UiStack>
          </UiStack>
          <UiText v-else :tone="UiTextTone.Muted">{{ t('clients.clientSummary.noEmailsSent') }}</UiText>
          <UiStack :direction="UiDirection.Row">
            <UiLink
              :to="clientSectionPath(clientId, ClientSection.Emails)"
              :variant="UiButtonVariant.Link"
              data-testid="client-summary-emails-all"
            >
              {{ t('shared.viewAll') }}
            </UiLink>
          </UiStack>
        </template>

        <template #card-notes>
          <UiTextarea
            v-model="adminNotes"
            rows="6"
            :placeholder="t('clients.clientSummary.addNotesAboutThisClient')"
            data-testid="client-summary-notes-input"
          />
          <UiStack :direction="UiDirection.Row">
            <UiButton :action="UiActionKind.Save"
              :type="UiButtonType.Button"
              :variant="UiButtonVariant.Primary"
              :loading="savingNotes"
              data-testid="client-summary-notes-save"
              @click="saveNotes"
            >
              {{ t('shared.save') }}
            </UiButton>
          </UiStack>
        </template>

        <template #card-external>
          <ClientExternalAccountCard
            v-model:provider="externalProvider"
            v-model:account-id="externalAccountId"
            :linked="client?.externalAccount ?? null"
            :saving="clientsStore.externalAccountSaving"
            :field-errors="clientsStore.externalAccountFieldErrors"
            @save="saveExternalAccount"
            @clear="clearExternalAccount"
            @clear-field-error="clientsStore.clearExternalAccountFieldError"
          />
        </template>

        <template #card-vat>
          <ClientVatCard
            :tax-id="client?.settings.taxId ?? null"
            :vat="client?.vat ?? null"
            :checking="clientsStore.vatChecking"
            @check="checkVat"
          />
        </template>
      </UiDetailCards>

      <UiDataTable
        :empty-text="t('clients.clientSummary.noServicesFound')"
        :density="UiTableDensity.Comfortable"
        :loading="initialLoading"
        :empty="!initialLoading && summaryStore.services.length === 0"
        data-testid="client-summary-services"
      >
        <template #header>
          <UiSectionHeader :level="2">
            {{ t('clients.clientSummary.productsServices2') }}
            <template #actions>
              <UiLink :to="clientSectionPath(clientId, ClientSection.Services)" :variant="UiButtonVariant.Link" data-testid="client-summary-services-all">
                {{ t('shared.viewAll') }}
              </UiLink>
            </template>
          </UiSectionHeader>
        </template>
        <UiSchemaTableHead :columns="SUMMARY_SERVICES_TABLE.columns" />
        <UiTableBody>
          <UiTableRow
            v-for="service in summaryStore.services"
            :key="service.id"
            clickable
            @click="openService(service.id)"
          >
            <UiTableCell muted>{{ service.id }}</UiTableCell>
            <UiTableCell>{{ service.productName }}</UiTableCell>
            <UiTableCell muted>{{ service.domain ?? EMPTY_VALUE }}</UiTableCell>
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
      </UiDataTable>

      <UiDataTable
        :empty-text="t('clients.clientSummary.noDomainsFound')"
        :density="UiTableDensity.Comfortable"
        :loading="initialLoading"
        :empty="!initialLoading && summaryStore.domains.length === 0"
        data-testid="client-summary-domains"
      >
        <template #header>
          <UiSectionHeader :level="2">
            {{ t('shared.domains') }}
            <template #actions>
              <UiLink :to="clientSectionPath(clientId, ClientSection.Domains)" :variant="UiButtonVariant.Link" data-testid="client-summary-domains-all">
                {{ t('shared.viewAll') }}
              </UiLink>
            </template>
          </UiSectionHeader>
        </template>
        <UiSchemaTableHead :columns="SUMMARY_DOMAINS_TABLE.columns" />
        <UiTableBody>
          <UiTableRow
            v-for="domain in summaryStore.domains"
            :key="domain.id"
            clickable
            @click="openDomain(domain.id)"
          >
            <UiTableCell muted>{{ domain.id }}</UiTableCell>
            <UiTableCell>{{ domain.name }}</UiTableCell>
            <UiTableCell muted>{{ domain.registrar ?? EMPTY_VALUE }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="domainStatusTone(domain.status)" small>{{ statusLabel(DOMAIN_STATUS_OPTIONS, domain.status) }}</UiBadge>
            </UiTableCell>
            <UiTableCell muted>{{ formatDate(domain.expiresAt) }}</UiTableCell>
            <UiTableCell>
              <UiStatusBadge
                :state="domain.autoRenew"
                :on-label="t('shared.yes')"
                :off-label="t('shared.no')"
              />
            </UiTableCell>
            <UiTableCell actions @click.stop>
              <UiRowActions :actions="domainActions(domain)" :testid="`client-summary-domain-actions-${domain.id}`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiDataTable>

      <UiDataTable
        :empty-text="t('clients.clientSummary.noQuotesFound')"
        :density="UiTableDensity.Comfortable"
        :loading="initialLoading"
        :empty="!initialLoading && summaryStore.quotes.length === 0"
        data-testid="client-summary-quotes"
      >
        <template #header>
          <UiSectionHeader :level="2">
            {{ t('clients.clientSummary.currentQuotes') }}
            <template #actions>
              <UiLink :to="clientSectionPath(clientId, ClientSection.Quotes)" :variant="UiButtonVariant.Link" data-testid="client-summary-quotes-all">
                {{ t('shared.viewAll') }}
              </UiLink>
            </template>
          </UiSectionHeader>
        </template>
        <UiSchemaTableHead :columns="SUMMARY_QUOTES_TABLE.columns" />
        <UiTableBody>
          <UiTableRow
            v-for="quote in summaryStore.quotes"
            :key="quote.id"
            clickable
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
      </UiDataTable>

      <PluginSlot :key="clientId" :slot-id="PluginSlotId.AdminClientPanels" :context="{ clientId }" />
    </template>
  </UiStack>
</template>
