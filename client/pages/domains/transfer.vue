<template>
  <div>
    <!-- Hero -->
    <section class="relative py-10 md:py-24 lg:py-32 bg-[#0a0a0f] overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-secondary-500/20 rounded-full blur-[150px] animate-blob" />
        <div class="absolute bottom-0 right-1/3 w-[400px] h-[400px] bg-primary-500/15 rounded-full blur-[130px] animate-blob animation-delay-2000" />
      </div>

      <div class="container-custom relative z-10 text-center max-w-3xl mx-auto">
        <!-- Breadcrumb -->
        <div class="flex items-center justify-center gap-2 text-sm text-gray-500 mb-6">
          <NuxtLink :to="localePath('/domains')" class="hover:text-primary-400 transition-colors">
            {{ $t('domains.card.name') }}
          </NuxtLink>
          <span>/</span>
          <span class="text-white">{{ $t('domains.tabTransfer') }}</span>
        </div>

        <div class="inline-flex items-center gap-2 px-4 py-2 mb-6 rounded-full glass border border-secondary-500/20 backdrop-blur-sm">
          <ArrowLeftRight :size="14" :stroke-width="2" class="text-secondary-400" />
          <span class="text-sm font-medium text-gray-300">{{ $t('domains.tabTransfer') }}</span>
        </div>

        <h1 class="text-4xl md:text-6xl font-bold mb-6 text-white leading-tight">
          {{ $t('domains.promoTransferTitle') }}
          <span class="block pb-2 text-transparent bg-clip-text bg-gradient-to-r from-secondary-400 to-cyan-400">
            {{ $t('domains.card.name') }}
          </span>
        </h1>

        <p class="text-lg text-gray-400 mb-10">{{ $t('domains.promoTransferDesc') }}</p>

        <!-- Tab switcher -->
        <div class="flex justify-center gap-2 mb-6">
          <UiButton variant="subtle" size="sm" :to="localePath('/domains')">
            <Globe :size="14" :stroke-width="2" class="mr-1.5" />
            {{ $t('domains.tabRegister') }}
          </UiButton>
          <UiButton variant="primary" size="sm">
            <ArrowLeftRight :size="14" :stroke-width="2" class="mr-1.5" />
            {{ $t('domains.tabTransfer') }}
          </UiButton>
        </div>

        <!-- Transfer form -->
        <div class="text-left max-w-xl mx-auto">
          <div class="rounded-2xl border border-secondary-500/30 bg-secondary-500/5 p-6 space-y-4">
            <h3 class="text-base font-semibold text-white">{{ $t('domains.transferFormTitle') }}</h3>
            <UiInput
              v-model="transferQuery"
              :label="$t('domains.transferDomainLabel')"
              placeholder="example.com"
              size="sm"
            />
            <div>
              <div class="flex items-center justify-between mb-1">
                <label class="block text-sm font-medium text-gray-300">{{ $t('domains.transferAuthLabel') }}</label>
                <UiTooltip position="left">
                  <button type="button" class="flex items-center gap-1 text-xs text-primary-400 hover:text-primary-300 transition-colors">
                    <HelpCircle :size="13" :stroke-width="2" />
                    {{ $t('domains.transferAuthHelp') }}
                  </button>
                  <template #content>
                    {{ $t('domains.transferAuthHelpText') }}
                  </template>
                </UiTooltip>
              </div>
              <UiInput
                v-model="transferEpp"
                placeholder="Epp Code / Auth Code"
                size="sm"
              />
            </div>
            <UiButton :full-width="true" @click="goTransfer">
              {{ $t('domains.transferBtn') }}
            </UiButton>
          </div>
          <p class="mt-3 text-xs text-gray-600 text-center">{{ $t('domains.transferNote') }}</p>
        </div>
      </div>
    </section>

    <!-- Promo: back to register -->
    <section class="py-10 bg-[#0a0a0f]">
      <div class="container-custom max-w-3xl mx-auto">
        <div class="rounded-2xl border border-white/10 bg-white/5 p-6 flex items-start justify-between gap-4">
          <div>
            <h3 class="text-base font-bold text-white mb-1">{{ $t('domains.title') }} {{ $t('domains.titleHighlight') }}</h3>
            <p class="text-sm text-gray-400 mb-4">{{ $t('domains.card.description') }}</p>
            <UiButton variant="outline" size="sm" :to="localePath('/domains')">
              <Globe :size="14" :stroke-width="2" class="mr-1.5" />
              {{ $t('domains.search') }} {{ $t('domains.card.name') }}
            </UiButton>
          </div>
          <Globe :size="40" :stroke-width="1.5" class="text-primary-400/40 flex-shrink-0" />
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { ArrowLeftRight, HelpCircle, Globe } from 'lucide-vue-next'
import { apiFetch } from '~/composables/useApi'

const { t: $t } = useI18n()
const localePath = useLocalePath()
const route = useRoute()

const transferQuery = ref(typeof route.query.domain === 'string' ? route.query.domain : '')
const transferEpp = ref(typeof route.query.eppcode === 'string' ? route.query.eppcode : '')

function goTransfer() {
  const domain = transferQuery.value.trim()
  if (!domain) return
  const epp = transferEpp.value.trim()
  const params = new URLSearchParams({ domain })
  if (epp) params.set('eppcode', epp)
  navigateTo(localePath(`/client/domains/transfer?${params.toString()}`))
}

useSeo({
  title: `${$t('domains.tabTransfer')} - ${$t('domains.card.name')} | Innovayse`,
  description: $t('domains.promoTransferDesc'),
  path: '/domains/transfer'
})
</script>
