<template>
  <UiCard data-testid="service-invoices">
    <UiCardHeader :title="$t('client.services.relatedInvoices')" :icon="FileText" />

    <UiStack v-if="invoicesLoading" gap="sm">
      <UiSkeleton v-for="i in 3" :key="i" shape="block" class="h-10" />
    </UiStack>

    <!--
      Why the table is empty, when it is empty because the read failed. "No invoices found for
      this service" must never be printed for an answer that never arrived: a failed read is
      never rendered as an absence of invoices.
    -->
    <UiAlert v-else-if="invoicesError" variant="error" data-testid="service-invoices-error">{{ invoicesError }}</UiAlert>

    <!--
      An empty list is "nothing is recorded against this service", never "nothing was charged".
      Invoices raised before the platform recorded which service a line was for carry no link,
      and the API reports how many such invoices this client holds so the difference can be said
      out loud rather than implied by a blank table.
    -->
    <UiText v-else-if="!relatedInvoices.length" size="sm" tone="subtle" class="text-center py-6" data-testid="service-invoices-empty">
      {{ $t('client.services.noInvoices') }}
    </UiText>

    <UiTable v-else scrollable>
      <UiTableHead>
        <UiTableRow :hoverable="false">
          <UiTableTh>{{ $t('client.services.invoiceId') }}</UiTableTh>
          <UiTableTh>{{ $t('client.services.invoiceDate') }}</UiTableTh>
          <UiTableTh>{{ $t('client.services.invoiceDue') }}</UiTableTh>
          <UiTableTh align="right">{{ $t('client.services.invoiceAmount') }}</UiTableTh>
          <UiTableTh align="right">{{ $t('client.services.invoiceStatus') }}</UiTableTh>
        </UiTableRow>
      </UiTableHead>
      <UiTableBody>
        <UiTableRow v-for="invoice in relatedInvoices" :key="invoice.id" :data-testid="`service-invoice-${invoice.id}`">
          <UiTableTd>
            <UiLink data-testid="service-invoice-link" :to="clientInvoicePath(invoice.id)" :underline="false" class="font-medium">#{{ invoice.id }}</UiLink>
          </UiTableTd>
          <UiTableTd>
            <UiText as="span" size="sm" tone="subtle">{{ formatDate(invoice.invoiceDate) }}</UiText>
          </UiTableTd>
          <UiTableTd>
            <UiText as="span" size="sm" tone="subtle">{{ formatDate(invoice.dueDate) }}</UiText>
          </UiTableTd>
          <UiTableTd align="right">
            <UiText as="span" size="sm" weight="medium">{{ formatCurrency(invoice.total, { code: invoice.currency }) }}</UiText>
          </UiTableTd>
          <UiTableTd align="right">
            <ClientStatusBadge :status="invoice.status" />
          </UiTableTd>
        </UiTableRow>
      </UiTableBody>
    </UiTable>

    <!--
      Shown whether or not the table has rows, and only when the read succeeded: it is a
      statement about this client's billing history, not a substitute for the list. Charges the
      platform never recorded a service for cannot be shown on any service, and saying so is the
      difference between "we have nothing" and "we cannot tell you".
    -->
    <UiAlert
      v-if="!invoicesLoading && !invoicesError && unattributedInvoices > 0"
      variant="info"
      class="mt-5"
      data-testid="service-invoices-unrecorded"
    >
      {{ $t('client.services.unrecordedNote', { count: unattributedInvoices }) }}
      <UiLink data-testid="service-invoices-all" :to="CLIENT_INVOICES_PATH" :underline="false">{{ $t('client.services.viewAllInvoices') }}</UiLink>
    </UiAlert>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * ServiceInvoicesCard: the invoices raised against one service, read through the service
 * management store, with a note about invoices the platform could not attribute to any service.
 */
import { CLIENT_INVOICES_PATH, clientInvoicePath } from '~/constants/clientRoutes'
import { FileText } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useServiceManagementStore } from '~/stores/servicemanagement'
import { formatCurrency } from '~/utils/formatCurrency'
import { formatDate } from '~/utils/formatDate'

/** The service management store that owns the list, its loading flag and its failure sentence. */
const { relatedInvoices, unattributedInvoices, invoicesLoading, invoicesError } = storeToRefs(useServiceManagementStore())
</script>
