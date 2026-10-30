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
import { QUOTE_STAGE_OPTIONS, SERVICE_STATUS_OPTIONS } from '@/lib/constants'
import { capitalize, formatDate } from '@/lib/format'
import { formatMoney, formatPrice } from '@/lib/money'
import { domainStatusTone, serviceStatusTone, statusLabel, clientStatusTone, contactTypeTone, quoteStageTone } from '@/lib/tones'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { ClientSection, clientItemPath, clientSectionPath, clientTicketNewPath, quoteAddPath, quotePath } from '@/lib/routes'

const { t } = useI18n()

/** Empty-value dash. */
const DASH = '—'

/** Icon markup (shape tags) of the edit button. */
const EDIT_ICON = '<path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7" /><path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z" />'

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

/** Local admin notes for the textarea. */
const adminNotes = ref('')

/** True while saving admin notes. */
const savingNotes = ref(false)

/** One caption/value line of a card. */
interface InfoRow {
  /** Translated caption. */
  label: string
  /** Value text. */
  value: string
}

/** One yes/no line of the "other information" card. */
interface FlagRow {
  /** Translated caption. */
  label: string
  /** Whether the flag is on. */
  on: boolean
  /** Text shown for the flag. */
  text: string
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
  if (!c) {
    return []
  }
  return [
    { label: t('shared.firstName'), value: c.firstName || DASH },
    { label: t('shared.lastName'), value: c.lastName || DASH },
    { label: t('shared.companyName'), value: c.companyName || DASH },
    { label: t('shared.emailAddress'), value: c.email || DASH },
    { label: t('shared.phoneNumber'), value: c.phone || DASH },
    { label: t('shared.address1'), value: c.address.street || DASH },
    { label: t('shared.address2'), value: c.address.address2 || DASH },
    { label: t('shared.city'), value: c.address.city || DASH },
    { label: t('clients.clientSummary.stateRegion'), value: c.address.state || DASH },
    { label: t('shared.postcode'), value: c.address.postCode || DASH },
    { label: t('shared.country'), value: c.address.country || DASH },
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
const flag = (label: string, on: boolean, text: string): FlagRow => ({ label, on, text })

/** Yes/no lines of the "other information" card. */
const flagRows = computed<FlagRow[]>(() => {
  const c = client.value
  if (!c) {
    return []
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
  /** Number of invoices. */
  count: number
  /** Sum of the invoices. */
  total: number
}

/** Invoice status lines of the billing card. */
const invoiceRows = computed<InvoiceRow[]>(() => {
  const s = summary.value
  if (!s) {
    return []
  }
  return [
    { label: t('clients.clientSummary.paid'), count: s.invoices.paid.count, total: s.invoices.paid.total },
    { label: t('clients.clientSummary.draft'), count: s.invoices.draft.count, total: s.invoices.draft.total },
    { label: t('clients.clientSummary.unpaidDue'), count: s.invoices.unpaid.count, total: s.invoices.unpaid.total },
    { label: t('clients.clientSummary.overdue'), count: s.invoices.overdue.count, total: s.invoices.overdue.total },
    { label: t('clients.clientSummary.cancelled'), count: s.invoices.cancelled.count, total: s.invoices.cancelled.total },
    { label: t('clients.clientSummary.partiallyRefunded'), count: s.invoices.partiallyRefunded.count, total: s.invoices.partiallyRefunded.total },
    { label: t('clients.clientSummary.refunded'), count: s.invoices.refunded.count, total: s.invoices.refunded.total },
  ]
})

/** Income lines of the billing card. */
const incomeRows = computed<InfoRow[]>(() => {
  const s = summary.value
  if (!s) {
    return []
  }
  return [
    { label: t('clients.clientSummary.grossRevenue'), value: formatMoney(s.transactions.grossRevenue) },
    { label: t('clients.clientSummary.clientExpenses'), value: formatMoney(s.transactions.clientExpenses) },
    { label: t('clients.clientSummary.netIncome'), value: formatMoney(s.transactions.netIncome) },
    { label: t('clients.clientSummary.creditBalance'), value: formatMoney(s.creditBalance) },
  ]
})

/** Count lines of the products card. */
const countRows = computed<InfoRow[]>(() => {
  const s = summary.value
  if (!s) {
    return []
  }
  return [
    {
      label: t('clients.clientSummary.activeServices'),
      value: t('clients.clientSummary.activeservicescountTotalservicescountTotal', {
        activeServicesCount: s.counts.activeServicesCount,
        totalServicesCount: s.counts.totalServicesCount,
      }),
    },
    { label: t('shared.domains'), value: String(s.counts.totalDomainsCount) },
    {
      label: t('clients.clientSummary.acceptedQuotes'),
      value: t('clients.clientSummary.acceptedquotescountTotalquotescountTotal', {
        acceptedQuotesCount: s.counts.acceptedQuotesCount,
        totalQuotesCount: s.counts.totalQuotesCount,
      }),
    },
    {
      label: t('clients.clientSummary.openTickets'),
      value: t('clients.clientSummary.openticketscountTotalticketscountTotal', {
        openTicketsCount: s.counts.openTicketsCount,
        totalTicketsCount: s.counts.totalTicketsCount,
      }),
    },
  ]
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

    <UiStack v-if="summaryStore.loading && !summary" data-testid="client-summary-loading">
      <UiSkeleton :lines="3" />
      <UiText :tone="UiTextTone.Secondary">{{ t('clients.clientSummary.loadingSummary') }}</UiText>
    </UiStack>

    <template v-else>
      <UiGrid :columns="3">
        <UiCard v-if="client" padding="md" data-testid="client-summary-info">
          <UiStack>
            <UiHeading :level="2" variant="card">{{ t('clients.clientSummary.clientInformation') }}</UiHeading>
            <UiStack gap="sm">
              <UiStack v-for="row in clientInfoRows" :key="row.label" direction="row" :justify="UiJustify.Between" gap="sm">
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ row.label }}</UiText>
                <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ row.value }}</UiText>
              </UiStack>
            </UiStack>
          </UiStack>
        </UiCard>

        <UiCard v-if="client" padding="md" data-testid="client-summary-other">
          <UiStack>
            <UiHeading :level="2" variant="card">{{ t('clients.clientSummary.otherInformation') }}</UiHeading>
            <UiStack gap="sm">
              <UiStack direction="row" :justify="UiJustify.Between" :align="UiAlignItems.Center" gap="sm">
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ t('shared.status') }}</UiText>
                <UiBadge :tone="clientStatusTone(client.status)" small>{{ client.status }}</UiBadge>
              </UiStack>
              <UiStack direction="row" :justify="UiJustify.Between" gap="sm">
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ t('clients.clientSummary.signupDate') }}</UiText>
                <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ formatDate(client.createdAt) }}</UiText>
              </UiStack>
              <UiStack direction="row" :justify="UiJustify.Between" gap="sm">
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ t('clients.clientSummary.clientFor') }}</UiText>
                <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ computeDuration(client.createdAt) }}</UiText>
              </UiStack>
              <UiStack v-for="row in flagRows" :key="row.label" direction="row" :justify="UiJustify.Between" gap="sm">
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ row.label }}</UiText>
                <UiText :tone="row.on ? UiTextTone.Success : UiTextTone.Danger" :size="UiTextSize.Md">{{ row.text }}</UiText>
              </UiStack>
            </UiStack>
          </UiStack>
        </UiCard>

        <UiCard v-if="client" padding="md" data-testid="client-summary-contacts">
          <UiStack>
            <UiHeading :level="2" variant="card">{{ t('clients.clientSummary.contacts') }}</UiHeading>
            <UiStack v-if="client.contacts.length > 0">
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

        <UiCard v-if="summary" padding="md" data-testid="client-summary-billing">
          <UiStack>
            <UiHeading :level="2" variant="card">{{ t('clients.clientSummary.invoicesBilling') }}</UiHeading>
            <UiStack gap="sm">
              <UiStack v-for="row in invoiceRows" :key="row.label" direction="row" :justify="UiJustify.Between" gap="sm">
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ row.label }}</UiText>
                <UiStack direction="row" gap="md">
                  <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Md">{{ row.count }}</UiText>
                  <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ formatMoney(row.total) }}</UiText>
                </UiStack>
              </UiStack>
            </UiStack>
            <UiStack gap="sm">
              <UiStack v-for="row in incomeRows" :key="row.label" direction="row" :justify="UiJustify.Between" gap="sm">
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ row.label }}</UiText>
                <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ row.value }}</UiText>
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

        <UiCard v-if="summary" padding="md" data-testid="client-summary-products">
          <UiStack>
            <UiHeading :level="2" variant="card">{{ t('clients.clientSummary.productsServices') }}</UiHeading>
            <UiStack gap="sm">
              <UiStack v-for="row in countRows" :key="row.label" direction="row" :justify="UiJustify.Between" gap="sm">
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ row.label }}</UiText>
                <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ row.value }}</UiText>
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

        <UiCard v-if="summary" padding="md" data-testid="client-summary-emails">
          <UiStack>
            <UiHeading :level="2" variant="card">{{ t('clients.clientSummary.recentEmails') }}</UiHeading>
            <UiStack v-if="summary.recentEmails.length > 0" gap="sm">
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

        <UiCard v-if="client" padding="md" data-testid="client-summary-notes">
          <UiStack>
            <UiHeading :level="2" variant="card">{{ t('shared.adminNotes') }}</UiHeading>
            <UiTextarea
              v-model="adminNotes"
              rows="6"
              :placeholder="t('clients.clientSummary.addNotesAboutThisClient')"
              data-testid="client-summary-notes-input"
            />
            <UiStack direction="row">
              <UiButton
                type="button"
                :variant="UiButtonVariant.Primary"
                :disabled="savingNotes"
                data-testid="client-summary-notes-save"
                @click="saveNotes"
              >
                {{ savingNotes ? t('shared.saving') : t('shared.save') }}
              </UiButton>
            </UiStack>
            <UiStatusMessage :message="notesFeedback.message.value" :tone="notesFeedbackTone" data-testid="client-summary-notes-feedback" />
          </UiStack>
        </UiCard>
      </UiGrid>

      <UiStack>
        <UiStack direction="row" :justify="UiJustify.Between" :align="UiAlignItems.Center">
          <UiHeading :level="2" variant="card">{{ t('clients.clientSummary.productsServices2') }}</UiHeading>
          <UiLink :to="clientSectionPath(clientId, ClientSection.Services)" :variant="UiButtonVariant.Link" data-testid="client-summary-services-all">
            {{ t('clients.clientSummary.viewAll') }}
          </UiLink>
        </UiStack>
        <UiCard>
          <UiTable
            density="comfortable"
            :empty="summaryStore.services.length === 0"
            :colspan="8"
            data-testid="client-summary-services"
          >
            <UiTableHead>
              <UiTableRow>
                <UiTableHeader>{{ t('shared.id') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.productService') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.domain') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.price') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.billingCycle') }}</UiTableHeader>
                <UiTableHeader>{{ t('clients.clientSummary.nextDue') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.actions') }}</UiTableHeader>
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
                <UiTableCell muted>{{ formatPrice(service.price, service.priceCurrency) }}</UiTableCell>
                <UiTableCell muted>{{ capitalize(service.billingCycle) }}</UiTableCell>
                <UiTableCell muted>{{ formatDate(service.nextDueDate) }}</UiTableCell>
                <UiTableCell>
                  <UiBadge :tone="serviceStatusTone(service.status)" small>{{ statusLabel(SERVICE_STATUS_OPTIONS, service.status) }}</UiBadge>
                </UiTableCell>
                <UiTableCell>
                  <UiIconButton
                    :label="t('shared.edit')"
                    :size="UiSize.Sm"
                    :data-testid="`client-summary-service-open-${service.id}`"
                    @click.stop="openService(service.id)"
                  >
                    <UiIcon :markup="EDIT_ICON" />
                  </UiIconButton>
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
        <UiStack direction="row" :justify="UiJustify.Between" :align="UiAlignItems.Center">
          <UiHeading :level="2" variant="card">{{ t('shared.domains') }}</UiHeading>
          <UiLink :to="clientSectionPath(clientId, ClientSection.Domains)" :variant="UiButtonVariant.Link" data-testid="client-summary-domains-all">
            {{ t('clients.clientSummary.viewAll') }}
          </UiLink>
        </UiStack>
        <UiCard>
          <UiTable
            density="comfortable"
            :empty="summaryStore.domains.length === 0"
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
                <UiTableHeader>{{ t('shared.actions') }}</UiTableHeader>
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
                <UiTableCell>
                  <UiIconButton
                    :label="t('shared.edit')"
                    :size="UiSize.Sm"
                    :data-testid="`client-summary-domain-open-${domain.id}`"
                    @click.stop="openDomain(domain.id)"
                  >
                    <UiIcon :markup="EDIT_ICON" />
                  </UiIconButton>
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
        <UiStack direction="row" :justify="UiJustify.Between" :align="UiAlignItems.Center">
          <UiHeading :level="2" variant="card">{{ t('clients.clientSummary.currentQuotes') }}</UiHeading>
          <UiLink :to="clientSectionPath(clientId, ClientSection.Quotes)" :variant="UiButtonVariant.Link" data-testid="client-summary-quotes-all">
            {{ t('clients.clientSummary.viewAll') }}
          </UiLink>
        </UiStack>
        <UiCard>
          <UiTable
            density="comfortable"
            :empty="summaryStore.quotes.length === 0"
            :colspan="7"
            data-testid="client-summary-quotes"
          >
            <UiTableHead>
              <UiTableRow>
                <UiTableHeader>{{ t('shared.id') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.subject') }}</UiTableHeader>
                <UiTableHeader>{{ t('clients.clientSummary.createDate') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.validUntil') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.total') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.stage') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.actions') }}</UiTableHeader>
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
                <UiTableCell>{{ formatMoney(quote.total) }}</UiTableCell>
                <UiTableCell>
                  <UiBadge :tone="quoteStageTone(quote.stage)" small>
                    {{ statusLabel(QUOTE_STAGE_OPTIONS, quote.stage) }}
                  </UiBadge>
                </UiTableCell>
                <UiTableCell>
                  <UiIconButton
                    :label="t('shared.edit')"
                    :size="UiSize.Sm"
                    :data-testid="`client-summary-quote-open-${quote.id}`"
                    @click.stop="openQuote(quote.id)"
                  >
                    <UiIcon :markup="EDIT_ICON" />
                  </UiIconButton>
                </UiTableCell>
              </UiTableRow>
            </UiTableBody>
            <template #empty>
              {{ t('clients.clientSummary.noQuotesFound') }}
            </template>
          </UiTable>
        </UiCard>
      </UiStack>
    </template>
  </UiStack>
</template>
