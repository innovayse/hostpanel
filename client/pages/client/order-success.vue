<template>
  <div class="min-h-[60vh] flex items-center justify-center px-4">
    <div class="text-center max-w-md w-full">
      <!-- Success icon -->
      <div class="mx-auto w-20 h-20 rounded-full bg-green-500/10 flex items-center justify-center mb-6">
        <CheckCircle :size="48" class="text-green-500" />
      </div>

      <h1 class="text-2xl font-bold text-white mb-2">{{ $t('checkout.success.title') }}</h1>
      <p class="text-gray-400 mb-8">{{ $t('checkout.success.description') }}</p>

      <!-- Order details card -->
      <div class="bg-white/5 border border-white/10 rounded-2xl p-6 mb-8 text-left">
        <div class="flex justify-between items-center mb-3">
          <span class="text-sm text-gray-400">{{ $t('checkout.success.orderNumber') }}</span>
          <span class="text-sm font-bold text-white">{{ orderNumber }}</span>
        </div>
        <div class="flex justify-between items-center mb-3">
          <span class="text-sm text-gray-400">{{ $t('checkout.success.status') }}</span>
          <span class="px-2.5 py-0.5 rounded-full text-xs font-bold bg-green-500/20 text-green-400">
            {{ $t('checkout.success.paid') }}
          </span>
        </div>
        <div class="flex justify-between items-center">
          <span class="text-sm text-gray-400">{{ $t('checkout.success.amount') }}</span>
          <span class="text-sm font-bold text-white">{{ amount }}</span>
        </div>
      </div>

      <!-- CTA buttons -->
      <div class="space-y-3">
        <NuxtLink
          :to="localePath('/client/services')"
          class="block w-full py-3 px-6 rounded-2xl bg-gradient-to-r from-cyan-600 to-primary-600 text-white font-bold text-center hover:from-cyan-500 hover:to-primary-500 transition-all"
          @click="forceRefreshServices"
        >
          {{ $t('checkout.success.setupService') }}
        </NuxtLink>
        <NuxtLink
          :to="localePath('/client/invoices')"
          class="block w-full py-3 px-6 rounded-2xl bg-white/5 border border-white/10 text-white font-semibold text-center hover:bg-white/10 transition-colors"
        >
          {{ $t('checkout.success.viewInvoices') }}
        </NuxtLink>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * Payment success page shown after a successful Stripe checkout.
 * Displays order confirmation and links to services setup.
 */
import { CheckCircle } from 'lucide-vue-next'

definePageMeta({ middleware: 'client-auth' })

const { t: $t } = useI18n()
const localePath = useLocalePath()
const route = useRoute()
const store = useClientStore()

/** Order number from query params. */
const orderNumber = computed(() => (route.query.order as string) || '—')

/** Formatted amount from query params; shows dash when zero or missing. */
const amount = computed(() => {
  const raw = (route.query.amount as string) || ''
  if (!raw || raw === '0' || raw === '0.00' || raw === '$0.00') return '—'
  return raw
})

/**
 * Forces a fresh fetch of services before navigating.
 * Clears the loaded flag so the services page re-fetches.
 */
function forceRefreshServices() {
  store.servicesLoaded = false
}
</script>
