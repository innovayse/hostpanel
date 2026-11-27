<template>
  <UiStack gap="lg" class="max-w-lg mx-auto">
    <UiBackLink :to="CLIENT_DOMAINS_PATH" :label="$t('client.domains.backTo')" testid="transfer-back" />

    <UiCard v-if="success" data-testid="transfer-success">
      <UiEmptyState :icon="CheckCircle" :title="$t('client.domainTransfer.successTitle')" :description="$t('client.domainTransfer.successDesc')">
        <UiStack direction="row" gap="sm" justify="center" wrap>
          <UiButton variant="primary" :to="clientInvoicePath(success.invoiceId)" data-testid="transfer-pay-invoice">
            <CreditCard :size="16" :stroke-width="2" class="mr-2" />
            {{ $t('common.pay') }}
          </UiButton>
          <UiButton :to="CLIENT_DOMAINS_PATH" variant="subtle" data-testid="transfer-view-domains">{{ $t('client.domainTransfer.viewDomains') }}</UiButton>
        </UiStack>
      </UiEmptyState>
    </UiCard>

    <UiCard v-else>
      <UiCardHeader :title="$t('client.domainTransfer.title')" :icon="ArrowLeftRight" />
      <UiText size="sm" tone="muted" class="mb-6">{{ $t('client.domainTransfer.subtitle') }}</UiText>

      <UiAlert variant="info" :icon-size="16" class="mb-5">{{ $t('client.domainTransfer.infoNote') }}</UiAlert>

      <UiForm data-testid="domain-transfer-form" :error="formError" spacing="sm" @submit="submit">
        <UiInput
          v-model="form.domain"
          :label="$t('client.domainTransfer.domainLabel')"
          :placeholder="$t('client.domainTransfer.domainPlaceholder')"
          :disabled="submitting"
          size="sm"
          testid="transfer-domain"
        />

        <UiInput
          v-model="form.eppcode"
          :label="$t('client.domainTransfer.eppLabel')"
          :placeholder="$t('client.domainTransfer.eppPlaceholder')"
          :disabled="submitting"
          size="sm"
          testid="transfer-epp"
        />

        <DomainFieldsForm
          v-model="form.extendedAttributes"
          :fields="fields"
          :errors="domainsStore.transferFieldErrors"
          :disabled="submitting"
          testid="transfer-fields"
        />

        <UiSelect
          :model-value="form.years"
          :label="$t('client.domainTransfer.periodLabel')"
          :placeholder="$t('client.domainTransfer.periodPlaceholder')"
          :options="periodOptions"
          size="sm"
          testid="transfer-period"
          @update:model-value="form.years = Number($event)"
        />

        <UiSkeleton v-if="pmLoading" shape="block" class="h-10" />
        <UiSelect
          v-else-if="paymentOptions.length"
          v-model="form.paymentmethod"
          :label="$t('client.domainTransfer.paymentLabel')"
          :placeholder="$t('client.domainTransfer.paymentPlaceholder')"
          :options="paymentOptions"
          size="sm"
          testid="transfer-payment"
        />
        <UiText v-else size="xs" tone="subtle" data-testid="transfer-no-payment">{{ $t('client.domainTransfer.noPaymentMethods') }}</UiText>

        <template #actions>
          <UiButton variant="primary" type="submit" :loading="submitting" :disabled="!form.domain || !form.paymentmethod" data-testid="transfer-submit">
            <ArrowLeftRight v-if="!submitting" :size="14" :stroke-width="2" class="mr-1.5" />
            {{ $t('client.domainTransfer.submit') }}
          </UiButton>
        </template>
      </UiForm>

      <UiText size="xs" tone="subtle" class="mt-4 text-center">{{ $t('client.domainTransfer.note') }}</UiText>
    </UiCard>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Domain transfer: the form that orders the transfer of a domain in (name, EPP code, period,
 * payment method) and, once the order is placed, a link to its invoice. The order, its in-flight
 * flag and the API's refusal belong to the domains store.
 *
 * The form keeps a short column (a width cap, an allowed exception: form fields are intrinsically
 * narrow, so this is not page layout; the page width itself comes from the shared container).
 */
import { watchDebounced } from '@vueuse/core'
import DomainFieldsForm from '~/components/domains/DomainFieldsForm.vue'
import { CLIENT_DOMAINS_PATH, clientInvoicePath } from '~/constants/clientRoutes'
import { ArrowLeftRight, CheckCircle, CreditCard } from 'lucide-vue-next'
import { useAuthStore } from '~/stores/auth'
import { useDomainsStore } from '~/stores/domains'
import type { DomainField } from '~/types/domainfield'
import { DomainFieldOperation } from '~/types/domainfieldoperation'
import { attributesFor, holderOf } from '~/utils/domainFields'
import { toPaymentMethodOptions } from '~/utils/paymentMethodOptions'
import { buildYearOptions } from '~/utils/yearOptions'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

/** Translates a key into the active locale. */
const { t } = useI18n()
/** The current route; its params name the record shown. */
const route = useRoute()

/** The form fields; the domain and EPP code may arrive in the query from the domain search. */
const form = reactive({
  domain: (route.query.domain as string) || '',
  eppcode: (route.query.eppcode as string) || '',
  years: 1,
  paymentmethod: '',
  /** The registry's extra values by field key, as typed. */
  extendedAttributes: {} as Record<string, string>,
})

/** The signed-in account, whose company decides the kind of registrant. */
const { user } = storeToRefs(useAuthStore())

/** The extra fields the registrar plugin asks for the domain typed so far; empty until they are read. */
const fields = ref<DomainField[]>([])

/** How long typing in the domain field settles before the fields are read, in milliseconds. */
const FIELDS_DEBOUNCE_MS = 400

/**
 * Reads the extra fields for the typed domain and keeps the values of the fields that still apply.
 *
 * @returns Resolves when the fields are read.
 */
const loadFields = async (): Promise<void> => {
  const name = form.domain.trim()
  fields.value = name.includes('.')
    ? await domainsStore.loadDomainFields(name, DomainFieldOperation.Transfer, holderOf(user.value?.companyName))
    : []
  form.extendedAttributes = attributesFor(fields.value, form.extendedAttributes)
}

/** The domains store; reset so a previous order's result does not linger. */
const domainsStore = useDomainsStore()
domainsStore.reset()

watchDebounced(() => form.domain, loadFields, { debounce: FIELDS_DEBOUNCE_MS, immediate: true })

/** Whether the order request is in flight. */
const submitting = computed(() => domainsStore.transferSaving)

/** The order a successful transfer raised; null until then. */
const success = computed(() => domainsStore.transferResult)

/** The sentence for a refused order (owned by the store). */
const formError = computed(() => domainsStore.transferError)

/** The period choices. */
const periodOptions = computed(() => buildYearOptions(t('client.domainTransfer.year'), t('client.domainTransfer.years')))

// Payment methods, read through the domains store.
/** Loading flag of the data this page reads. */
const { pending: pmLoading } = await useAsyncData('domain-transfer-payment-methods', () => domainsStore.loadPaymentMethods())

/** The saved payment methods as select options. */
const paymentOptions = computed(() => toPaymentMethodOptions(domainsStore.paymentMethods))

// Pre-select the first payment method.
watch(paymentOptions, (options) => {
  const [first] = options
  if (first && !form.paymentmethod) {
    form.paymentmethod = first.value
  }
}, { immediate: true })

/**
 * Places the transfer order through the store. The API validates the values and explains a
 * refusal, which is rendered from the store's `transferError`.
 *
 * @returns Resolves when the attempt has ended.
 */
const submit = async (): Promise<void> => {
  await domainsStore.placeTransfer({ ...form, extendedAttributes: attributesFor(fields.value, form.extendedAttributes) })
}
</script>
