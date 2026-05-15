<template>
  <div class="overflow-x-hidden">
    <!-- Hero -->
    <section class="relative py-12 md:py-24 lg:py-32 bg-[#0a0a0f] overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute top-0 right-1/4 w-[500px] h-[500px] bg-cyan-500/20 rounded-full blur-[140px] animate-blob" />
        <div class="absolute bottom-0 left-1/3 w-[400px] h-[400px] bg-secondary-500/15 rounded-full blur-[130px] animate-blob animation-delay-2000" />
      </div>

      <div class="container-custom relative z-10 text-center px-4">
        <div class="inline-flex items-center gap-2 px-4 py-2 mb-6 rounded-full glass border border-cyan-500/20 backdrop-blur-sm">
          <Server :size="14" :stroke-width="2" class="text-cyan-400" />
          <span class="text-xs md:text-sm font-medium text-gray-300 uppercase tracking-wider">{{ $t('hosting.badge') }}</span>
        </div>

        <h1 class="text-3xl md:text-6xl font-bold mb-6 text-white leading-tight">
          {{ $t('hosting.title') }}
          <span class="block pb-2 text-transparent bg-clip-text bg-gradient-to-r from-cyan-400 to-primary-400">{{ $t('hosting.titleHighlight') }}</span>
        </h1>
        <p class="text-base md:text-lg text-gray-400 mb-10 max-w-2xl mx-auto">
          {{ $t('hosting.subtitle') }}
        </p>
      </div>
    </section>

    <!-- Plans -->
    <section class="py-12 md:py-20 bg-[#0a0a0f] relative overflow-visible">
      <div class="container-custom relative z-10">
        <!-- Loading -->
        <div v-if="pending" class="grid sm:grid-cols-2 lg:grid-cols-3 gap-6 max-w-5xl mx-auto py-20 px-4">
          <div v-for="i in 3" :key="i" class="h-[500px] rounded-2xl bg-white/5 border border-white/10 animate-pulse" />
        </div>

        <!-- Error -->
        <div v-else-if="error" class="text-center py-20 px-4">
          <AlertCircle :size="48" :stroke-width="2" class="text-red-400 mx-auto mb-4" />
          <p class="text-gray-400">{{ $t('hosting.loadError') }}</p>
          <UiButton variant="primary" size="sm" class="mt-4" @click="refresh()">Retry</UiButton>
        </div>

        <template v-else>
          <!-- Billing Cycle Tabs -->
          <div class="sticky top-20 z-40 py-4 mb-16 md:mb-20 px-4 flex justify-center">
            <div 
              class="flex items-center gap-1 p-1 rounded-2xl bg-[#0a0a0f]/80 backdrop-blur-xl border border-white/10 shadow-[0_20px_50px_rgba(0,0,0,0.5)] max-w-full overflow-x-auto no-scrollbar scroll-smooth"
            >
              <button
                v-for="cycle in availableCycles"
                :key="cycle.key"
                class="px-5 py-2.5 rounded-xl text-xs md:text-sm font-bold transition-all duration-300 whitespace-nowrap"
                :class="selectedCycle === cycle.key 
                  ? 'bg-gradient-to-r from-cyan-500 to-primary-500 text-white shadow-lg shadow-cyan-500/20 scale-105 z-10' 
                  : 'text-gray-500 hover:text-gray-300 hover:bg-white/5'"
                @click="selectedCycle = cycle.key"
              >
                {{ cycle.label }}
              </button>
            </div>
          </div>

          <!-- Free Hosting -->
          <div v-if="freePlan" class="max-w-6xl mx-auto mb-16 px-4">
            <div
              v-motion
              :initial="{ opacity: 0, scale: 0.95 }"
              :visibleOnce="{ opacity: 1, scale: 1, transition: { duration: 600 } }"
              class="relative overflow-hidden rounded-[2.5rem] border border-cyan-500/30 bg-gradient-to-br from-cyan-500/10 via-[#0a0a0f] to-primary-500/10 backdrop-blur-md p-8 md:p-12 shadow-[0_0_80px_rgba(6,182,212,0.15)]"
            >
              <div class="relative z-10 flex flex-col lg:flex-row items-center justify-between gap-12">
                <div class="flex-1 text-center lg:text-left">
                  <div class="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-cyan-500/20 border border-cyan-500/30 text-cyan-400 text-[10px] md:text-xs font-black uppercase tracking-widest mb-6">
                    <CheckCircle :size="14" />
                    {{ $t('hosting.getStartedForFree') }}
                  </div>
                  <h2 class="text-3xl md:text-5xl font-black text-white mb-4 uppercase tracking-tighter">
                    {{ formatPlanNameHelper(freePlan.translated_name || freePlan.name) }}
                  </h2>
                  <p class="text-base md:text-lg text-gray-300 mb-8 max-w-2xl mx-auto lg:mx-0 leading-relaxed font-medium">
                    {{ freePlan.translated_tagline || freePlan.tagline }}
                    <span class="block mt-2 text-sm text-gray-500 font-normal italic">{{ freePlan.translated_shortdescription || freePlan.shortdescription }}</span>
                  </p>
                  <div class="flex flex-wrap justify-center lg:justify-start gap-3 mb-4">
                    <div v-for="feature in getPlanFeaturesHelper(freePlan).slice(0, 4)" :key="feature" class="flex items-center gap-2 text-xs md:text-sm text-gray-300 bg-white/[0.03] px-4 py-2.5 rounded-2xl border border-white/5 backdrop-blur-sm">
                      <Check :size="14" class="text-cyan-400" />
                      {{ feature }}
                    </div>
                  </div>
                </div>
                <div class="w-full lg:w-[320px] flex flex-col items-center lg:items-end gap-6 text-center lg:text-right">
                  <div>
                    <div class="text-6xl font-black text-white mb-1 uppercase tracking-tighter bg-clip-text text-transparent bg-gradient-to-b from-white to-gray-500">FREE</div>
                    <div class="text-gray-500 text-xs font-bold uppercase tracking-widest">{{ $t('hosting.foreverFree') }}</div>
                  </div>
                  <div class="flex flex-col gap-3 w-full">
                    <UiButton variant="primary" size="lg" full-width class="!rounded-2xl shadow-2xl shadow-cyan-500/20 !h-14 !text-base font-black uppercase tracking-widest" @click="addToCartHelper(freePlan)">
                      <ShoppingCart :size="18" class="mr-2" />
                      {{ cart.hasItem(freePlan.pid, selectedCycle) ? $t('hosting.inCart') : $t('hosting.getStarted') }}
                    </UiButton>
                    <NuxtLink :to="localePath(`/hosting/${planSlugHelper(freePlan)}`)" class="text-xs font-bold text-gray-500 hover:text-cyan-400 flex items-center justify-center gap-2 transition-colors uppercase tracking-widest group">
                      {{ $t('hosting.viewAllFeatures') }}
                      <ArrowRight :size="14" class="group-hover:translate-x-1 transition-transform" />
                    </NuxtLink>
                  </div>
                </div>
              </div>
            </div>

            <!-- Separator -->
            <div class="text-center mt-24 mb-16">
              <span class="text-xs md:text-sm font-black text-white/40 uppercase tracking-[0.3em] block mb-4">{{ $t('hosting.premiumSolutions') }}</span>
              <div class="w-24 h-px bg-gradient-to-r from-transparent via-cyan-500 to-transparent mx-auto" />
            </div>
          </div>

          <!-- Other Plans Grid -->
          <div class="flex flex-wrap justify-center gap-6 md:gap-8 max-w-7xl mx-auto px-4 mb-24">
            <div
              v-for="(plan, index) in otherPlans"
              :key="plan.pid"
              v-motion
              :initial="{ opacity: 0, y: 30 }"
              :visibleOnce="{ opacity: 1, y: 0, transition: { delay: index * 100, duration: 500 } }"
              class="relative w-full sm:w-[calc(50%-12px)] lg:w-[calc(33.333%-22px)]"
            >
              <div
                class="relative h-full p-8 rounded-[2.5rem] border transition-all duration-500 flex flex-col overflow-hidden group/card bg-white/[0.01] border-white/5"
                :class="{ 'border-cyan-500/40 bg-cyan-500/[0.03] shadow-[0_0_60px_rgba(6,182,212,0.1)]': plan.is_featured === 'on' }"
              >
                <div v-if="plan.is_featured === 'on'" class="absolute -top-3 left-1/2 -translate-x-1/2 z-20">
                  <span class="px-5 py-1.5 bg-gradient-to-r from-cyan-500 to-primary-500 text-white text-[10px] font-black rounded-full shadow-xl shadow-cyan-500/30 uppercase tracking-widest">
                    {{ $t('hosting.mostPopular') }}
                  </span>
                </div>

                <div class="mb-8">
                  <NuxtLink :to="localePath(`/hosting/${planSlugHelper(plan)}`)" class="block mb-2">
                    <h2 class="text-2xl md:text-3xl font-black text-white hover:text-cyan-400 transition-colors uppercase tracking-tighter leading-none">
                      {{ formatPlanNameHelper(plan.translated_name || plan.name) }}
                    </h2>
                  </NuxtLink>
                  <p v-if="plan.translated_tagline || plan.tagline" class="text-[10px] text-cyan-400 font-bold uppercase tracking-[0.2em] mb-4">
                    {{ plan.translated_tagline || plan.tagline }}
                  </p>
                  <p v-if="plan.translated_shortdescription || plan.shortdescription" class="text-sm text-gray-500 leading-relaxed line-clamp-2 min-h-[40px] italic">
                    {{ plan.translated_shortdescription || plan.shortdescription }}
                  </p>
                </div>

                <div class="mb-8 p-6 rounded-3xl bg-white/[0.03] border border-white/5">
                  <div v-if="getPlanPriceRawHelper(plan, selectedCycle)" class="flex items-baseline gap-1 mb-6">
                    <span v-if="locale !== 'hy'" class="text-xl font-bold text-gray-500">{{ activeCurrencyPrefix }}</span>
                    <span class="text-5xl font-black text-white tabular-nums tracking-tighter">
                      {{ formatAmountHelper(getPlanPriceRawHelper(plan, selectedCycle)) }}
                    </span>
                    <span v-if="locale === 'hy'" class="text-xl font-bold text-white ml-1">֏</span>
                    <span class="text-gray-500 text-xs font-bold uppercase tracking-widest">/ {{ $t(`hosting.cycles.${selectedCycle}`) }}</span>
                  </div>
                  <div v-else class="mb-6">
                    <span class="text-2xl font-black text-white uppercase tracking-tighter">{{ $t('hosting.custom') }}</span>
                    <span class="block text-[10px] text-gray-500 italic mt-1">{{ $t('hosting.customContact') }}</span>
                  </div>

                  <!-- Mini Price List -->
                  <div v-if="getPlanCyclesHelperHelper(plan).length > 1" class="space-y-1.5 pt-4 border-t border-white/5">
                    <div
                      v-for="c in getPlanCyclesHelperHelper(plan).slice(0, 4)"
                      :key="c.key"
                      class="flex justify-between items-center text-[10px] px-3 py-2 rounded-xl transition-all duration-300 cursor-pointer group/item"
                      :class="c.key === selectedCycle 
                        ? 'bg-cyan-500/20 text-cyan-300 font-black' 
                        : 'text-gray-500 hover:bg-white/5 hover:text-gray-300'"
                      @click="selectedCycle = c.key"
                    >
                      <span class="uppercase tracking-widest">{{ $t(`hosting.cycles.${c.key}`) }}</span>
                      <span class="font-mono">{{ c.price }}</span>
                    </div>
                  </div>
                </div>

                <ul class="space-y-4 mb-10 flex-1">
                  <li v-for="feature in getPlanFeaturesHelper(plan)" :key="feature" class="flex items-start gap-3 text-sm text-gray-400 group-hover/card:text-gray-300 transition-colors">
                    <div class="mt-1 w-5 h-5 rounded-full bg-cyan-500/10 flex items-center justify-center border border-cyan-500/20">
                      <CheckCircle :size="12" class="text-cyan-400 flex-shrink-0" />
                    </div>
                    {{ feature }}
                  </li>
                </ul>

                <div class="flex flex-col gap-3">
                  <UiButton 
                    variant="outline" 
                    full-width 
                    class="!rounded-2xl !h-12 font-black uppercase tracking-widest !text-xs transition-all shadow-xl hover:shadow-cyan-500/20"
                    :class="cart.hasItem(plan.pid, selectedCycle) ? '!bg-green-500/20 !text-green-400 !border-green-500/40' : '!border-white/10 !text-white hover:!border-cyan-500/50'" 
                    @click="addToCartHelper(plan)"
                  >
                    {{ cart.hasItem(plan.pid, selectedCycle) ? $t('hosting.inCart') : $t('hosting.addToCart') }}
                  </UiButton>
                  <NuxtLink :to="localePath(`/hosting/${planSlugHelper(plan)}`)" class="w-full py-3.5 rounded-2xl text-[10px] font-black text-center bg-white/[0.05] text-gray-400 hover:bg-white/[0.08] hover:text-white uppercase tracking-[0.2em] transition-all">
                    {{ $t('hosting.getStarted') }}
                  </NuxtLink>
                </div>
              </div>
            </div>
          </div>

          <!-- Agency Section -->
          <div v-if="agencyPlan" class="max-w-6xl mx-auto mb-24 px-4">
            <div class="text-center mb-16">
              <span class="text-xs md:text-sm font-black text-white/40 uppercase tracking-[0.3em] block mb-4">{{ $t('hosting.enterpriseSolutions') }}</span>
              <div class="w-24 h-px bg-gradient-to-r from-transparent via-primary-500 to-transparent mx-auto" />
            </div>

            <div
              v-motion
              :initial="{ opacity: 0, y: 40 }"
              :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 700 } }"
              class="relative rounded-3xl md:rounded-[3rem] border border-primary-500/30 bg-gradient-to-br from-primary-500/10 via-[#0a0a0f] to-cyan-500/10 backdrop-blur-md p-8 md:p-12 shadow-[0_0_100px_rgba(139,92,246,0.15)]"
            >
              <div class="relative z-10 flex flex-col lg:flex-row items-center justify-between gap-12">
                <div class="flex-1 text-center lg:text-left">
                  <div class="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-primary-500/20 border border-primary-500/30 text-primary-400 text-[10px] md:text-xs font-black uppercase tracking-widest mb-6">
                    <CheckCircle :size="14" />
                    {{ $t('hosting.agencyBadge') }}
                  </div>
                  <h2 class="text-3xl md:text-6xl font-black text-white mb-4 uppercase tracking-tighter leading-none">
                    {{ formatPlanNameHelper(agencyPlan.translated_name || agencyPlan.name) }}
                  </h2>
                  <p class="text-base md:text-xl text-gray-300 mb-8 max-w-2xl mx-auto lg:mx-0 leading-relaxed font-medium">
                    {{ agencyPlan.translated_tagline || agencyPlan.tagline }}
                    <span class="block mt-2 text-sm text-gray-500 font-normal italic leading-normal">{{ agencyPlan.translated_shortdescription || agencyPlan.shortdescription }}</span>
                  </p>
                  <div class="flex flex-wrap justify-center lg:justify-start gap-3">
                    <div v-for="feature in getPlanFeaturesHelper(agencyPlan).slice(0, 6)" :key="feature" class="flex items-center gap-2 text-xs md:text-sm text-gray-300 bg-white/[0.03] px-4 py-2.5 rounded-2xl border border-white/5 backdrop-blur-sm">
                      <Check :size="14" class="text-primary-400" />
                      {{ feature }}
                    </div>
                  </div>
                </div>
                <div class="w-full lg:w-[320px] flex flex-col items-center lg:items-end gap-8 text-center lg:text-right">
                  <div class="flex flex-col gap-1">
                    <div v-if="getPlanPriceRawHelper(agencyPlan, selectedCycle)" class="flex items-baseline justify-center lg:justify-end gap-1">
                      <span v-if="locale !== 'hy'" class="text-2xl font-bold text-gray-500">{{ activeCurrencyPrefix }}</span>
                      <span class="text-6xl md:text-7xl font-black text-white tabular-nums tracking-tighter-2 bg-clip-text text-transparent bg-gradient-to-b from-white to-primary-500 leading-none">
                        {{ formatAmountHelper(getPlanPriceRawHelper(agencyPlan, selectedCycle)) }}
                      </span>
                      <span v-if="locale === 'hy'" class="text-3xl font-bold text-white ml-2">֏</span>
                    </div>
                    <div class="text-gray-500 text-xs font-black uppercase tracking-[0.2em] mt-2">/ {{ $t(`hosting.cycles.${selectedCycle}`) }}</div>
                  </div>
                  <div class="flex flex-col gap-4 w-full">
                    <UiButton 
                      variant="primary" 
                      full-width 
                      class="!rounded-2xl !h-16 !text-lg font-black uppercase tracking-widest !bg-primary-600 hover:!bg-primary-500 shadow-2xl shadow-primary-500/30 transition-all scale-100 hover:scale-[1.02]" 
                      @click="addToCartHelper(agencyPlan)"
                    >
                      {{ cart.hasItem(agencyPlan.pid, selectedCycle) ? $t('hosting.inCart') : $t('hosting.addToCart') }}
                    </UiButton>
                    <NuxtLink :to="localePath(`/hosting/${planSlugHelper(agencyPlan)}`)" class="text-xs font-black text-gray-500 hover:text-primary-400 flex items-center justify-center gap-3 transition-colors uppercase tracking-[0.2em] group">
                      {{ $t('hosting.viewAllFeatures') }}
                      <ArrowRight :size="14" class="group-hover:translate-x-1.5 transition-transform" />
                    </NuxtLink>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Comparison Table -->
          <div id="compare-plans" class="mt-32 max-w-7xl mx-auto px-4 overflow-hidden">
            <div class="text-center mb-16">
              <h2 class="text-3xl md:text-5xl font-black text-white mb-4 uppercase tracking-tighter">{{ $t('hosting.comparison.title') }}</h2>
              <p class="text-gray-400 max-w-2xl mx-auto text-base md:text-lg font-medium">{{ $t('hosting.comparison.subtitle') }}</p>
            </div>
            
            <div class="relative rounded-3xl md:rounded-[2.5rem] border border-white/5 bg-white/[0.01] overflow-hidden backdrop-blur-3xl group/table shadow-2xl">
              <div class="overflow-x-auto no-scrollbar scroll-smooth">
                <table class="w-full text-left border-collapse min-w-[320px] md:min-w-[1100px]">
                  <thead>
                    <tr class="bg-white/[0.03]">
                      <th class="p-4 md:p-8 text-[10px] md:text-xs font-black text-gray-500 border-b border-white/10 sticky left-0 bg-[#0a0a0f] z-30 min-w-[140px] md:min-w-[280px] uppercase tracking-widest">
                        <div class="flex items-center gap-2">
                          <Server :size="16" class="text-cyan-400" />
                          {{ $t('hosting.comparison.feature') }}
                        </div>
                      </th>
                      <th v-for="plan in orderedComparisonsHelper" :key="plan.pid" class="p-4 md:p-8 border-b border-white/10 text-center relative group/col min-w-[120px]">
                        <div class="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-cyan-500 to-primary-500 scale-x-0 group-hover/col:scale-x-100 transition-transform duration-500" />
                        <div class="text-[10px] md:text-sm font-black text-white mb-2 group-hover/col:text-cyan-400 transition-colors uppercase tracking-widest leading-tight">
                          {{ formatPlanNameHelper(plan.translated_name || plan.name) }}
                        </div>
                        <div v-if="getPlanPriceRawHelper(plan, selectedCycle)" class="text-[8px] md:text-[10px] text-gray-500 font-black tracking-widest uppercase opacity-80 tabular-nums">
                          {{ activeCurrencyPrefix }}{{ formatAmountHelper(getPlanPriceRawHelper(plan, selectedCycle)) }}
                        </div>
                      </th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-white/5">
                    <tr v-for="row in comparisonRows" :key="row.key" class="hover:bg-white/[0.03] transition-colors group/row">
                      <td class="p-4 md:p-6 text-[10px] md:text-sm text-gray-400 font-bold sticky left-0 bg-[#0a0a0f]/95 backdrop-blur-xl z-20 border-r border-white/5 group-hover/row:text-white transition-colors">
                        <div class="flex items-center gap-3">
                          <div class="hidden md:flex w-8 h-8 rounded-lg bg-white/5 items-center justify-center border border-white/5 group-hover/row:border-cyan-500/30 group-hover/row:bg-cyan-500/5 transition-all">
                            <component :is="row.icon" v-if="row.icon" :size="14" class="text-cyan-400/60 group-hover/row:text-cyan-400" />
                          </div>
                          <span class="whitespace-nowrap">{{ $t(`hosting.comparison.${row.key}`) }}</span>
                        </div>
                      </td>
                      <td v-for="plan in orderedComparisonsHelper" :key="plan.pid" class="p-4 md:p-6 text-[10px] md:text-sm text-gray-500 text-center relative group-hover/row:text-gray-200">
                        <span v-if="row.valueType === 'check'">
                          <div class="flex justify-center items-center">
                            <div v-if="getComparisonCheckValueHelperFn(plan, row.key)" class="w-6 h-6 md:w-8 md:h-8 rounded-full bg-green-500/10 flex items-center justify-center border border-green-500/20">
                              <Check :size="14" class="text-green-400" />
                            </div>
                            <span v-else class="text-gray-800 opacity-20 font-mono">—</span>
                          </div>
                        </span>
                        <span v-else class="font-bold tabular-nums tracking-wide">
                          {{ getComparisonRawValueHelperFn(plan, row.key) }}
                        </span>
                      </td>
                    </tr>

                    <!-- Bottom Action Row -->
                    <tr>
                      <td class="p-4 md:p-8 sticky left-0 bg-[#0a0a0f]/95 backdrop-blur-xl z-20 border-r border-white/5" />
                      <td v-for="plan in orderedComparisonsHelper" :key="plan.pid" class="p-4 md:p-8 text-center bg-white/[0.01]">
                        <button 
                          @click="addToCartHelper(plan)"
                          class="px-4 md:px-6 py-2.5 rounded-xl text-[8px] md:text-xs font-black uppercase tracking-widest transition-all duration-300 border-2 scale-100 hover:scale-110 active:scale-95 whitespace-nowrap"
                          :class="cart.hasItem(plan.pid, selectedCycle) 
                            ? 'bg-green-500/20 border-green-500/40 text-green-400' 
                            : 'border-white/10 hover:border-cyan-500 hover:bg-cyan-500 text-white'"
                        >
                          {{ cart.hasItem(plan.pid, selectedCycle) ? $t('hosting.inCart') : $t('hosting.select') }}
                        </button>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
              <!-- Mobile Scroll Info -->
              <div class="md:hidden flex items-center justify-center gap-2 py-3 bg-white/[0.03] border-t border-white/5 text-[8px] text-gray-500 font-black uppercase tracking-[0.2em]">
                <ArrowRight :size="10" class="animate-pulse" />
                {{ $t('common.scrollHorizontal') || 'Scroll to explore plans' }}
              </div>
            </div>
          </div>
        </template>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { 
  Server, CheckCircle, AlertCircle, ShoppingCart, 
  Check, ArrowRight, HardDrive, Zap, Globe, 
  Mail, Database, ShieldCheck, History, Headphones 
} from 'lucide-vue-next'
import { useCartStore } from '~/stores/cart'

interface WhmcsCurrency {
  prefix: string; suffix: string; monthly: string; quarterly: string; 
  semiannually: string; annually: string; biennially: string; triennially: string;
}

interface WhmcsPlan {
  pid: number; name: string; translated_name?: string; tagline?: string; translated_tagline?: string;
  shortdescription?: string; translated_shortdescription?: string; description: string; translated_description?: string;
  is_featured?: string; pricing: Record<string, WhmcsCurrency>;
}

const { t: $t, locale } = useI18n()
const localePath = useLocalePath()
const cart = useCartStore()
onMounted(() => cart.init())

const { data: _plansRaw, pending, error, refresh } = await useApi<WhmcsPlan[]>('/api/portal/public/products', {
  query: computed(() => ({ lang: locale.value, gid: 1 })),
  key: `hosting-plans-res-${locale.value}`
})

const plans = computed<WhmcsPlan[]>(() => (_plansRaw.value as WhmcsPlan[]) ?? [])
const freePlan = computed(() => plans.value.find(p => p.name.toLowerCase().includes('free')))
const agencyPlan = computed(() => plans.value.find(p => p.name.toLowerCase().includes('agency')))
const otherPlans = computed(() => plans.value.filter(p => !p.name.toLowerCase().includes('free') && !p.name.toLowerCase().includes('agency')))

const orderedComparisonsHelper = computed(() => {
  const order = ['free', 'starter', 'webmaster', 'business', 'pro', 'agency']
  return [...plans.value].sort((a, b) => {
    const clean = (s: string) => s.toLowerCase().replace(/[^a-z]/g, '').replace(/businnes|busines/g, 'business')
    const nameA = clean(a.name)
    const nameB = clean(b.name)
    const ai = order.findIndex(o => nameA.includes(o))
    const bi = order.findIndex(o => nameB.includes(o))
    return (ai === -1 ? 99 : ai) - (bi === -1 ? 99 : bi)
  })
})

const comparisonRows = [
  { key: 'storage', icon: HardDrive, valueType: 'dynamic' },
  { key: 'bandwidth', icon: Zap, valueType: 'dynamic' },
  { key: 'websites', icon: Globe, valueType: 'dynamic' },
  { key: 'email', icon: Mail, valueType: 'dynamic' },
  { key: 'databases', icon: Database, valueType: 'dynamic' },
  { key: 'freeSsl', icon: ShieldCheck, valueType: 'check' },
  { key: 'backups', icon: History, valueType: 'dynamic' },
  { key: 'support', icon: Headphones, valueType: 'check' }
]

const selectedCycle = ref<'monthly'|'quarterly'|'semiannually'|'annually'|'biennially'|'triennially'>('monthly')
const allCycleKeys = ['monthly', 'quarterly', 'semiannually', 'annually', 'biennially', 'triennially'] as const
type CycleKey = typeof allCycleKeys[number]

function getCurrencyHelper(plan: WhmcsPlan): WhmcsCurrency | undefined {
  const currencyByLocale: Record<string, string> = { en: 'USD', hy: 'AMD', ru: 'RUB' }
  const preferred = currencyByLocale[locale.value] ?? 'USD'
  return plan.pricing[preferred] ?? Object.values(plan.pricing)[0]
}

const availableCycles = computed(() =>
  allCycleKeys.filter(key => plans.value.some(p => {
    const c = getCurrencyHelper(p); return c?.[key] && c[key] !== '-1.00' && c[key] !== '0.00'
  })).map(key => ({ key, label: $t(`hosting.cycles.${key}`) }))
)

const activeCurrencyPrefix = computed(() => {
  const first = plans.value?.[0]; return first ? (getCurrencyHelper(first)?.prefix ?? '') : ''
})

function getComparisonCheckValueHelperFn(plan: WhmcsPlan, key: string): boolean {
  const desc = (plan.translated_description || plan.description || '').toLowerCase()
  if (key === 'freeSsl') return desc.includes('ssl') || desc.includes('security') || desc.includes('սերտիֆիկատ')
  if (key === 'support') return true
  return false
}

const planDataMap: Record<string, any> = {
  free: { websites: '1', databases: '1', email: '2', bandwidth: '5 GB', storage: '600 MB', backups: false },
  starter: { websites: '1', databases: '5', email: '10', bandwidth: '50 GB', storage: '5 GB', backups: true },
  webmaster: { websites: '3', databases: '10', email: '100', bandwidth: '400 GB', storage: '20 GB', backups: true },
  business: { websites: '10', databases: '20', email: 'Unlimited', bandwidth: 'Unlimited', storage: '20 GB', backups: true },
  pro: { websites: 'Unlimited', databases: 'Unlimited', email: 'Unlimited', bandwidth: 'Unlimited', storage: '50 GB', backups: true },
  agency: { websites: 'Unlimited', databases: 'Unlimited', email: 'Unlimited', bandwidth: 'Unlimited', storage: 'Unlimited', backups: true }
}

function getComparisonRawValueHelperFn(plan: WhmcsPlan, key: string): string {
  const nameClean = plan.name.toLowerCase().replace(/[^a-z]/g, '').replace(/businnes|busines/g, 'business')
  const planKey = Object.keys(planDataMap).find(k => nameClean.includes(k))
  if (planKey && planDataMap[planKey][key] !== undefined) {
    const val = planDataMap[planKey][key]
    if (key === 'backups') return val ? $t('hosting.comparison.daily') : '—'
    return val
  }
  return '—'
}

function formatAmountHelper(amount: string): string {
  if (!amount) return ''
  let formatted = amount.replace(/\.00$/, '').replace(/\.0$/, '')
  if (!isNaN(Number(formatted))) {
    formatted = Number(formatted).toLocaleString(locale.value === 'hy' ? 'hy-AM' : 'en-US').replace(/,/g, locale.value === 'hy' ? ' ' : ',')
  }
  return formatted
}

function getPlanPriceRawHelper(plan: WhmcsPlan, cycle: CycleKey): string {
  const c = getCurrencyHelper(plan); return (c?.[cycle] && c[cycle] !== '-1.00' && c[cycle] !== '0.00') ? c[cycle] : ''
}

function getPlanCyclesHelperHelper(plan: WhmcsPlan): Array<{ key: CycleKey; price: string }> {
  const c = getCurrencyHelper(plan); if (!c) return []
  return allCycleKeys.filter(key => c[key] && c[key] !== '-1.00' && c[key] !== '0.00').map(key => {
      const amt = formatAmountHelper(c[key]); let price = locale.value === 'hy' && c.prefix === '֏' ? `${amt} ${c.prefix}` : `${c.prefix}${amt}`
      return { key, price }
    })
}

function getPlanFeaturesHelper(plan: WhmcsPlan): string[] {
  let desc = plan.translated_description || plan.description || ''
  if (locale.value === 'hy') desc = desc.replace(/վեբ-մաստեր/g, 'վեբ-վարպետ')
  return desc.split(/\r?\n/).filter(line => line.trim())
}

function formatPlanNameHelper(name: string): string {
  if (!name) return ''
  return name.replace(/Businnes/g, 'Business').replace(/businnes/g, 'business')
}

function planSlugHelper(plan: WhmcsPlan): string {
  return formatPlanNameHelper(plan.name || '').toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, '')
}

function addToCartHelper(plan: WhmcsPlan) {
  const c = getCurrencyHelper(plan); const amt = getPlanPriceRawHelper(plan, selectedCycle.value)
  const priceLabel = amt ? (locale.value === 'hy' && c?.prefix === '֏' ? `${formatAmountHelper(amt)} ${c.prefix}` : `${c?.prefix}${formatAmountHelper(amt)}`) : $t('hosting.custom')
  cart.addItem({
    pid: plan.pid, name: plan.translated_name || plan.name, billingcycle: selectedCycle.value,
    cycleLabel: $t(`hosting.cycles.${selectedCycle.value}`), price: priceLabel, prefix: c?.prefix ?? '', rawPrice: amt || '0'
  })
}
</script>

<style scoped>
.no-scrollbar::-webkit-scrollbar { display: none; }
.no-scrollbar { -ms-overflow-style: none; scrollbar-width: none; }
.tracking-tighter-2 { tracking-spacing: -0.05em; }
</style>
