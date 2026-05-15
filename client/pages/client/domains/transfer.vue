<template>
  <div class="max-w-lg mx-auto">
    <NuxtLink
      to="/client/domains"
      class="inline-flex items-center gap-2 text-gray-500 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white text-sm transition-colors mb-6"
    >
      <ArrowLeft :size="16" :stroke-width="2" />
      {{ $t('client.domains.backTo') }}
    </NuxtLink>

    <!-- Success state -->
    <UiCard v-if="success">
      <div class="text-center py-6">
        <div class="w-14 h-14 rounded-full bg-green-500/10 border border-green-500/20 flex items-center justify-center mx-auto mb-4">
          <CheckCircle :size="28" :stroke-width="2" class="text-green-400" />
        </div>
        <h1 class="text-xl font-bold text-gray-900 dark:text-white mb-2">{{ $t('client.domainTransfer.successTitle') }}</h1>
        <p class="text-sm text-gray-500 dark:text-gray-400 mb-6">{{ $t('client.domainTransfer.successDesc') }}</p>
        <div class="flex flex-col sm:flex-row gap-3 justify-center">
          <NuxtLink
            :to="`/client/invoices/${success.invoiceId}`"
            class="inline-flex items-center justify-center gap-2 px-5 py-2.5 rounded-xl bg-gradient-to-r from-cyan-600 to-primary-600 text-white font-semibold text-sm hover:opacity-90 transition-opacity"
          >
            <CreditCard :size="16" :stroke-width="2" />
            {{ $t('client.domainTransfer.payInvoice') }}
          </NuxtLink>
          <NuxtLink
            to="/client/domains"
            class="inline-flex items-center justify-center gap-2 px-5 py-2.5 rounded-xl border border-white/10 text-gray-300 text-sm hover:bg-white/5 transition-colors"
          >
            {{ $t('client.domainTransfer.viewDomains') }}
          </NuxtLink>
        </div>
      </div>
    </UiCard>

    <!-- Transfer form -->
    <UiCard v-else>
      <h1 class="text-lg font-bold text-gray-900 dark:text-white mb-1 flex items-center gap-2">
        <ArrowLeftRight :size="18" :stroke-width="2" class="text-cyan-500 dark:text-cyan-400" />
        {{ $t('client.domainTransfer.title') }}
      </h1>
      <p class="text-sm text-gray-500 dark:text-gray-400 mb-6">{{ $t('client.domainTransfer.subtitle') }}</p>

      <!-- Info note -->
      <div class="mb-5 flex gap-3 rounded-xl border border-blue-200 dark:border-blue-500/30 bg-blue-50 dark:bg-blue-500/10 p-3.5">
        <Info :size="16" :stroke-width="2" class="text-blue-500 dark:text-blue-400 flex-shrink-0 mt-0.5" />
        <p class="text-xs text-blue-700 dark:text-blue-300 leading-relaxed">
          {{ $t('client.domainTransfer.infoNote') }}
        </p>
      </div>

      <UiForm :error="formError" :success="''" spacing="sm" @submit="submit">
        <!-- Domain name -->
        <UiInput
          v-model="form.domain"
          :label="$t('client.domainTransfer.domainLabel')"
          placeholder="example.com"
          :disabled="submitting"
          size="sm"
        />

        <!-- EPP / Auth Code -->
        <UiInput
          v-model="form.eppcode"
          :label="$t('client.domainTransfer.eppLabel')"
          placeholder="Epp Code / Auth Code"
          :disabled="submitting"
          size="sm"
        />

        <!-- Renewal period -->
        <UiSelect
          v-model="form.years"
          :label="$t('client.domainTransfer.periodLabel')"
          :options="periodOptions"
          size="sm"
        />

        <!-- Payment method -->
        <div>
          <p v-if="pmLoading" class="h-10 rounded-xl bg-gray-100 dark:bg-white/5 animate-pulse" />
          <UiSelect
            v-else-if="paymentOptions.length"
            v-model="form.paymentmethod"
            :label="$t('client.domainTransfer.paymentLabel')"
            :options="paymentOptions"
            size="sm"
          />
          <p v-else class="text-xs text-gray-500 dark:text-gray-400">
            {{ $t('client.domainTransfer.noPaymentMethods') }}
          </p>
        </div>

        <template #actions>
          <UiButton type="submit" :loading="submitting" :disabled="!form.domain || !form.paymentmethod">
            <ArrowLeftRight v-if="!submitting" :size="14" :stroke-width="2" class="mr-1.5" />
            {{ submitting ? $t('client.domainTransfer.submitting') : $t('client.domainTransfer.submit') }}
          </UiButton>
        </template>
      </UiForm>

      <p class="text-xs text-gray-600 dark:text-gray-500 mt-4 text-center">
        {{ $t('client.domainTransfer.note') }}
      </p>
    </UiCard>
  </div>
</template>

<script setup lang="ts">
import { ArrowLeft, ArrowLeftRight, Info, CheckCircle, CreditCard } from 'lucide-vue-next'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

const { t } = useI18n()
const route = useRoute()

const form = reactive({
  domain:        (route.query.domain as string) || '',
  eppcode:       (route.query.eppcode as string) || '',
  years:         1,
  paymentmethod: '',
})

const submitting = ref(false)
const formError  = ref('')
const success    = ref<{ orderId: number; invoiceId: number } | null>(null)

// Period options
const periodOptions = computed(() => [
  { label: `1 ${t('client.domainTransfer.year')}`,  value: 1 },
  { label: `2 ${t('client.domainTransfer.years')}`, value: 2 },
  { label: `3 ${t('client.domainTransfer.years')}`, value: 3 },
])

// Payment methods
const { data: payMethods, pending: pmLoading } = await useApi<Array<{
  id: number; description: string; gateway_name: string; card_last_four?: string
}>>('/api/portal/client/payment-methods')

const paymentOptions = computed(() =>
  (payMethods.value ?? []).map(m => ({
    label: m.card_last_four ? `${m.description} •••• ${m.card_last_four}` : m.description,
    value: m.gateway_name,
  }))
)

// Pre-select first payment method
watch(paymentOptions, (opts) => {
  if (opts.length && !form.paymentmethod) form.paymentmethod = opts[0]!.value as string
}, { immediate: true })

async function submit() {
  if (!form.domain.trim()) { formError.value = t('client.domainTransfer.errorDomain'); return }
  submitting.value = true
  formError.value  = ''
  try {
    const res = await apiFetch<{ orderId: number; invoiceId: number }>(
      '/api/portal/client/domains/transfer-order',
      { method: 'POST', body: form }
    )
    success.value = res
  } catch (err: any) {
    formError.value = err?.data?.statusMessage || t('client.domainTransfer.errorDefault')
  } finally {
    submitting.value = false
  }
}
</script>
