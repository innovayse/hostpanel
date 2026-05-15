<template>
  <div v-if="branchData">
    <!-- Hero Section -->
    <section class="relative py-12 md:py-32 bg-[#0a0a0f] overflow-hidden">
      <!-- Background -->
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-br from-primary-500/10 via-transparent to-secondary-500/10" />
        <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/20 rounded-full blur-[150px] animate-blob" />
        <div class="absolute bottom-0 right-1/4 w-[400px] h-[400px] bg-secondary-500/20 rounded-full blur-[140px] animate-blob animation-delay-2000" />

        <!-- Grid pattern -->
        <div class="absolute inset-0 opacity-[0.02]">
          <div style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 60px 60px;" class="w-full h-full" />
        </div>

        <!-- Floating particles -->
        <div class="absolute top-1/4 left-1/3 w-2 h-2 bg-primary-400/70 rounded-full animate-float" style="animation-delay: 0.5s;" />
        <div class="absolute bottom-1/3 right-1/4 w-3 h-3 bg-secondary-400/60 rounded-full animate-float" style="animation-delay: 1.5s;" />
      </div>

      <div class="container-custom relative z-10">
        <div class="max-w-4xl mx-auto">
          <!-- Breadcrumbs -->
          <UiBreadcrumbs :items="breadcrumbItems" />
          <!-- Icon and badge -->
          <div class="flex flex-col sm:flex-row items-center sm:items-start gap-4 mb-6 text-center sm:text-left">
            <div class="w-16 h-16 sm:w-20 sm:h-20 rounded-xl sm:rounded-2xl bg-gradient-to-br from-primary-500/20 to-cyan-500/10 flex items-center justify-center border-2 border-primary-500/30 shadow-2xl flex-shrink-0" style="box-shadow: 0 0 60px rgba(14, 165, 233, 0.3);">
              <Icon v-if="branchData.icon" :name="branchData.icon" class="text-4xl sm:text-5xl text-primary-400" />
            </div>
            <div class="flex-1">
              <span v-if="serviceData" class="inline-block px-3 py-1 rounded-full text-xs font-bold bg-primary-500/20 text-primary-400 mb-2">
                {{ serviceData.title }}
              </span>
              <h1 class="text-3xl sm:text-4xl md:text-5xl lg:text-6xl font-bold text-white break-words">
                {{ branchData.name }}
              </h1>
            </div>
          </div>

          <!-- Objective -->
          <div class="flex items-start gap-4 p-6 rounded-2xl bg-gradient-to-br from-secondary-500/10 to-primary-500/5 border border-secondary-500/20 mb-8">
            <Target :size="28" :stroke-width="2" class="text-secondary-400 flex-shrink-0 mt-1" />
            <div>
              <span class="text-sm text-secondary-400 uppercase tracking-wider font-bold mb-2 block">{{ $t('serviceBranch.objective') }}</span>
              <p class="text-xl text-white font-semibold mb-3">{{ branchData.objective }}</p>
              <p class="text-base text-gray-400 leading-relaxed">
                {{ getBranchDescription() }}
              </p>
            </div>
          </div>

          <!-- Key Highlights -->
          <div class="grid md:grid-cols-3 gap-4">
            <div
              v-for="(highlight, hIndex) in branchHighlights"
              :key="highlight.label"
              v-motion
              :initial="{ opacity: 0, y: 20 }"
              :enter="{ opacity: 1, y: 0, transition: { delay: 200 + hIndex * 100, duration: 400 } }"
              class="p-4 rounded-xl bg-white/5 border border-white/10 hover:border-primary-500/30 transition-all duration-300 group"
            >
              <Icon :name="highlight.icon" class="text-2xl text-primary-400 mb-2 group-hover:scale-110 transition-transform" />
              <div class="text-sm text-gray-500 mb-1">{{ highlight.label }}</div>
              <div class="text-lg font-bold text-white">{{ highlight.value }}</div>
            </div>
          </div>
        </div>
      </div>

      <!-- Corner decorations -->
      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/30 pointer-events-none" />
      <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/30 pointer-events-none" />
    </section>

    <!-- What We Deliver -->
    <section class="py-8 md:py-20 bg-[#0a0a0f] relative">
      <div class="container-custom">
        <div class="max-w-4xl mx-auto">
          <h2 class="text-3xl font-bold text-white mb-8 flex flex-col sm:flex-row items-center gap-3 break-words text-center sm:text-left">
            <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-primary-500/20 to-secondary-500/10 flex items-center justify-center">
              <Icon name="mdi:package-variant" class="text-primary-400 text-xl" />
            </div>
            {{ $t('serviceBranch.whatWeDeliver') }}
          </h2>

          <div class="grid md:grid-cols-2 gap-6">
            <div
              v-for="(item, index) in deliverables"
              :key="index"
              v-motion
              :initial="{ opacity: 0, x: -30 }"
              :visibleOnce="{ opacity: 1, x: 0, transition: { delay: index * 100, duration: 400 } }"
              class="p-5 rounded-xl bg-white/5 border border-white/10 hover:border-primary-500/30 hover:bg-white/10 transition-all duration-300 group"
            >
              <div class="flex items-start gap-3">
                <CheckCircle :size="20" :stroke-width="2" class="text-primary-400 flex-shrink-0 group-hover:scale-110 transition-transform" />
                <div>
                  <h3 class="font-semibold text-white mb-1 group-hover:text-primary-300 transition-colors">{{ item.title }}</h3>
                  <p class="text-sm text-gray-400">{{ item.description }}</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- How It Works -->
    <section class="py-8 md:py-20 bg-[#0d0d12] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-r from-secondary-500/5 via-transparent to-primary-500/5" />
      </div>

      <div class="container-custom relative z-10">
        <div class="max-w-4xl mx-auto">
          <h2 class="text-3xl font-bold text-white mb-8 flex flex-col sm:flex-row items-center gap-3 break-words text-center sm:text-left">
            <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-secondary-500/20 to-primary-500/10 flex items-center justify-center">
              <Icon name="mdi:cog" class="text-secondary-400 text-xl" />
            </div>
            {{ $t('serviceBranch.howItWorks') }}
          </h2>

          <div class="space-y-6">
            <div
              v-for="(step, index) in processSteps"
              :key="index"
              v-motion
              :initial="{ opacity: 0, x: -40 }"
              :visibleOnce="{ opacity: 1, x: 0, transition: { delay: index * 150, duration: 500 } }"
              class="flex gap-6 group"
            >
              <!-- Step number -->
              <div class="flex-shrink-0">
                <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-secondary-500 to-primary-500 flex items-center justify-center text-white font-bold shadow-lg group-hover:scale-110 group-hover:rotate-6 transition-all duration-300" style="box-shadow: 0 0 30px rgba(168, 85, 247, 0.3);">
                  {{ String(index + 1).padStart(2, '0') }}
                </div>
              </div>

              <!-- Step content -->
              <div class="flex-1 pb-6 border-b border-white/10 last:border-0">
                <h3 class="text-lg font-bold text-white mb-2 group-hover:text-secondary-300 transition-colors">{{ step.title }}</h3>
                <p class="text-gray-400 leading-relaxed">{{ step.description }}</p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- Benefits -->
    <section class="py-8 md:py-20 bg-[#0a0a0f] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute top-1/3 right-1/4 w-96 h-96 bg-primary-500/10 rounded-full blur-[150px]" />
      </div>

      <div class="container-custom relative z-10">
        <div class="max-w-4xl mx-auto">
          <h2 class="text-3xl font-bold text-white mb-8 flex flex-col sm:flex-row items-center gap-3 break-words text-center sm:text-left">
            <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-primary-500/20 to-cyan-500/10 flex items-center justify-center">
              <Trophy :size="20" :stroke-width="2" class="text-primary-400" />
            </div>
            {{ $t('serviceBranch.keyBenefits') }}
          </h2>

          <div class="grid md:grid-cols-3 gap-6">
            <div
              v-for="(benefit, index) in benefits"
              :key="index"
              v-motion
              :initial="{ opacity: 0, y: 40 }"
              :visibleOnce="{ opacity: 1, y: 0, transition: { delay: index * 100, duration: 500 } }"
              class="relative p-6 rounded-xl bg-white/5 border border-white/10 hover:border-primary-500/30 transition-all duration-300 hover:-translate-y-2 group text-center"
            >
              <div class="absolute inset-0 rounded-xl opacity-0 group-hover:opacity-100 transition-opacity duration-500 bg-gradient-to-br from-primary-500/10 to-secondary-500/5" />

              <Icon :name="benefit.icon" class="relative text-4xl mx-auto mb-3 group-hover:scale-110 transition-transform" :style="{ color: benefit.color }" />
              <h3 class="relative text-lg font-bold text-white mb-2">{{ benefit.title }}</h3>
              <p class="relative text-sm text-gray-400">{{ benefit.description }}</p>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- Pricing Packages -->
    <section class="py-8 md:py-20 bg-[#0d0d12] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-r from-secondary-500/5 via-transparent to-primary-500/5" />
      </div>

      <div class="container-custom relative z-10">
        <div class="max-w-5xl mx-auto">
          <!-- Header -->
          <div class="text-center mb-12">
            <span class="inline-block px-4 py-1.5 bg-secondary-500/20 text-secondary-400 text-sm font-medium rounded-full mb-4">
              {{ $t('serviceBranch.pricing.badge') }}
            </span>
            <h2 class="text-3xl md:text-4xl font-bold text-white mb-4">
              {{ $t('serviceBranch.pricing.title') }} <span class="text-secondary-400">{{ $t('serviceBranch.pricing.titleHighlight') }}</span>
            </h2>
            <p class="text-gray-400 max-w-2xl mx-auto">
              {{ $t('serviceBranch.pricing.subtitle') }}
            </p>
          </div>

          <!-- Pricing Grid -->
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4 md:gap-6 mb-8 w-full">
            <div
              v-for="(pkg, index) in pricingPackages"
              :key="pkg.name"
              v-motion
              :initial="{ opacity: 0, y: 30 }"
              :visibleOnce="{ opacity: 1, y: 0, transition: { delay: index * 100, duration: 500 } }"
              class="relative group min-w-0"
            >
              <div
                class="relative h-full p-4 sm:p-6 md:p-8 rounded-xl md:rounded-2xl border-2 transition-all duration-300 hover:-translate-y-2 flex flex-col overflow-hidden"
                :class="index === 1 ? 'md:scale-105 bg-gradient-to-br from-secondary-500/10 to-primary-500/5' : 'bg-white/5'"
                :style="{
                  borderColor: index === 1 ? '#a855f7' : 'rgba(255,255,255,0.1)',
                  boxShadow: index === 1 ? '0 0 60px rgba(168, 85, 247, 0.3)' : 'none'
                }"
              >
                <!-- Popular badge -->
                <div v-if="index === 1" class="absolute -top-[7px] left-1/2 -translate-x-1/2 z-10">
                  <span class="px-3 sm:px-4 py-0.5 sm:py-1 text-[10px] sm:text-xs font-bold rounded-full bg-secondary-500 text-white shadow-lg whitespace-nowrap">
                    {{ $t('serviceBranch.pricing.recommended') }}
                  </span>
                </div>

                <!-- Package name -->
                <div class="text-center mb-4 sm:mb-5 md:mb-6">
                  <h3 class="text-lg sm:text-xl font-bold text-white mb-1.5 sm:mb-2 break-words">{{ pkg.name }}</h3>
                  <p class="text-xs sm:text-sm text-gray-400 break-words">{{ pkg.description }}</p>
                </div>

                <!-- Price -->
                <div class="text-center mb-4 sm:mb-5 md:mb-6">
                  <div class="text-xs sm:text-sm text-gray-500 mb-1">{{ $t('serviceBranch.pricing.startingAt') }}</div>
                  <div class="text-2xl sm:text-3xl md:text-4xl font-bold text-white mb-1 break-words" :style="index === 1 ? { color: '#a855f7' } : {}">
                    {{ pkg.price }}
                  </div>
                  <div class="text-[10px] sm:text-xs text-gray-500 break-words">{{ pkg.billing }}</div>
                </div>

                <!-- Features -->
                <div class="flex-1 space-y-2 sm:space-y-2.5 md:space-y-3 mb-4 sm:mb-5 md:mb-6">
                  <div
                    v-for="feature in pkg.features"
                    :key="feature"
                    class="flex items-start gap-1.5 sm:gap-2"
                  >
                    <CheckCircle :size="18" :stroke-width="2" class="text-primary-400 flex-shrink-0 mt-0.5" />
                    <span class="text-xs sm:text-sm text-gray-300 break-words leading-relaxed">{{ feature }}</span>
                  </div>
                </div>

                <!-- CTA Button -->
                <button
                  class="w-full py-2 sm:py-2.5 md:py-3 rounded-lg sm:rounded-xl font-semibold transition-all duration-300 hover:scale-105 text-xs sm:text-sm md:text-base"
                  :class="index === 1 ? 'bg-secondary-500 text-white shadow-lg' : 'border-2 border-gray-700 text-white hover:bg-white/5'"
                  :style="index === 1 ? { boxShadow: '0 8px 30px rgba(168, 85, 247, 0.4)' } : {}"
                  @click="navigateTo(localePath('/contact'))"
                >
                  {{ $t('serviceBranch.pricing.getStarted') }}
                </button>
              </div>
            </div>
          </div>

          <!-- Additional info -->
          <div class="text-center text-sm text-gray-500">
            {{ $t('serviceBranch.pricing.guarantee') }}
          </div>
        </div>
      </div>
    </section>

    <!-- Case Studies / Results -->
    <section class="py-8 md:py-20 bg-[#0a0a0f] relative">
      <div class="container-custom">
        <div class="max-w-4xl mx-auto">
          <h2 class="text-3xl font-bold text-white mb-8 flex flex-col sm:flex-row items-center gap-3 break-words text-center sm:text-left">
            <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-secondary-500/20 to-primary-500/10 flex items-center justify-center">
              <Icon name="mdi:chart-bar" class="text-secondary-400 text-xl" />
            </div>
            {{ $t('serviceBranch.successMetrics') }}
          </h2>

          <div class="grid md:grid-cols-3 gap-6">
            <div
              v-for="(metric, mIndex) in successMetrics"
              :key="metric.label"
              v-motion
              :initial="{ opacity: 0, scale: 0.9 }"
              :visibleOnce="{ opacity: 1, scale: 1, transition: { delay: mIndex * 100, duration: 400 } }"
              class="p-6 rounded-xl bg-gradient-to-br from-white/5 to-white/[0.02] border border-white/10 text-center group hover:border-secondary-500/30 hover:-translate-y-1 transition-all duration-300"
            >
              <div class="text-4xl font-bold bg-gradient-to-r from-secondary-400 to-primary-400 bg-clip-text text-transparent mb-2 group-hover:scale-110 transition-transform">
                {{ metric.value }}
              </div>
              <div class="text-sm text-gray-400">{{ metric.label }}</div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- CTA -->
    <section class="py-8 md:py-20 bg-[#0d0d12] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-r from-primary-500/10 via-transparent to-secondary-500/10" />
        <div class="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[400px] bg-primary-500/15 rounded-full blur-[150px]" />
      </div>

      <div class="container-custom relative z-10">
        <div class="max-w-3xl mx-auto text-center">
          <h2 class="text-3xl md:text-4xl font-bold text-white mb-4">
            {{ $t('serviceBranch.ctaReady') }} <span class="text-primary-400">{{ branchData.name }}</span>?
          </h2>
          <p class="text-lg text-gray-400 mb-8">
            {{ $t('serviceBranch.ctaDescription') }}
          </p>

          <div class="flex flex-col sm:flex-row gap-4 justify-center">
            <UiButton
              variant="primary"
              size="lg"
              class="hover:shadow-xl hover:shadow-primary-500/50"
              @click="navigateTo(localePath('/contact'))"
            >
              <RocketIcon :size="16" :stroke-width="2" class="mr-2" />
              {{ $t('serviceBranch.cta.startProject') }}
            </UiButton>
            <UiButton
              variant="outline"
              size="lg"
              class="border-gray-700 hover:border-primary-500"
              @click="navigateTo(localePath('/services'))"
            >
              {{ $t('serviceBranch.cta.viewAllServices') }}
            </UiButton>
          </div>
        </div>
      </div>
    </section>
  </div>

  <!-- 404 State -->
  <div v-else class="min-h-screen flex items-center justify-center bg-[#0a0a0f]">
    <div class="text-center">
      <Icon name="mdi:alert-circle" class="text-6xl text-gray-600 mx-auto mb-4" />
      <h1 class="text-2xl font-bold text-white mb-2">{{ $t('serviceBranch.notFound') }}</h1>
      <p class="text-gray-400 mb-6">{{ $t('serviceBranch.notFoundDescription') }}</p>
      <UiButton @click="navigateTo(localePath('/services'))">
        <Icon name="mdi:arrow-left" class="mr-2" />
        {{ $t('common.backToServices') }}
      </UiButton>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * Dynamic service branch detail page
 * Route: /services/[service]/[branch]
 */

import { services as rawServices } from '~/lib/data'
import { Target, CheckCircle, Trophy, RocketIcon } from 'lucide-vue-next'

const { t, tm, locale } = useI18n()
const localePath = useLocalePath()
const route = useRoute()

// Map raw services with i18n translations
const services = computed(() => rawServices.map(service => ({
  ...service,
  title: t(`services.items.${service.id}.title`),
  description: t(`services.items.${service.id}.description`),
  features: tm(`services.items.${service.id}.features`) as string[],
  branches: service.branchKeys.map(key => ({
    key,
    name: t(`services.items.${service.id}.branches.${key}.name`),
    objective: t(`services.items.${service.id}.branches.${key}.objective`),
    icon: service.branchIcons[key]
  }))
})))

// Find service (computed to be reactive)
const serviceData = computed(() => {
  const serviceId = route.params.service as string
  return services.value.find(s => s.id === serviceId) || null
})

// Find branch
const branchData = computed(() => {
  const branchKey = route.params.branch as string

  if (!serviceData.value || !serviceData.value.branches) return null

  return serviceData.value.branches.find(b =>
    b.key === branchKey
  ) || null
})

// Breadcrumb items
const breadcrumbItems = computed(() => {
  const items = [
    { label: t('common.services'), to: localePath('/services') }
  ]

  if (serviceData.value) {
    items.push({
      label: serviceData.value.title,
      to: localePath(`/services#${route.params.service}`)
    })
  }

  if (branchData.value) {
    items.push({
      label: branchData.value.name,
      to: ''
    })
  }

  return items
})

// Slugify helper
const slugify = (text: string) => {
  return text
    .toLowerCase()
    .replace(/[^\w\s-]/g, '')
    .replace(/\s+/g, '-')
    .replace(/-+/g, '-')
    .trim()
}

// Dynamic content based on branch
const deliverables = computed(() => {
  if (!branchData.value) return []

  return [
    {
      title: t('serviceBranch.deliverables.strategy.title'),
      description: t('serviceBranch.deliverables.strategy.description', { branch: branchData.value.name })
    },
    {
      title: t('serviceBranch.deliverables.plan.title'),
      description: t('serviceBranch.deliverables.plan.description')
    },
    {
      title: t('serviceBranch.deliverables.tracking.title'),
      description: t('serviceBranch.deliverables.tracking.description')
    },
    {
      title: t('serviceBranch.deliverables.optimization.title'),
      description: t('serviceBranch.deliverables.optimization.description')
    }
  ]
})

const processSteps = computed(() => {
  if (!branchData.value) return []

  return [
    {
      title: t('serviceBranch.process.discovery.title'),
      description: t('serviceBranch.process.discovery.description', { branch: branchData.value.name })
    },
    {
      title: t('serviceBranch.process.strategy.title'),
      description: t('serviceBranch.process.strategy.description')
    },
    {
      title: t('serviceBranch.process.implementation.title'),
      description: t('serviceBranch.process.implementation.description')
    },
    {
      title: t('serviceBranch.process.monitor.title'),
      description: t('serviceBranch.process.monitor.description')
    }
  ]
})

const benefits = computed(() => {
  return [
    {
      icon: 'mdi:chart-line',
      color: '#0ea5e9',
      title: t('serviceBranch.benefits.results.title'),
      description: t('serviceBranch.benefits.results.description')
    },
    {
      icon: 'mdi:account-group',
      color: '#a855f7',
      title: t('serviceBranch.benefits.team.title'),
      description: t('serviceBranch.benefits.team.description')
    },
    {
      icon: 'mdi:clock-outline',
      color: '#06b6d4',
      title: t('serviceBranch.benefits.delivery.title'),
      description: t('serviceBranch.benefits.delivery.description')
    }
  ]
})

const pricingPackages = computed(() => {
  return [
    {
      name: t('serviceBranch.packages.starter.name'),
      description: t('serviceBranch.packages.starter.description'),
      price: t('serviceBranch.packages.starter.price'),
      billing: t('serviceBranch.packages.starter.billing'),
      features: tm('serviceBranch.packages.starter.features') as string[]
    },
    {
      name: t('serviceBranch.packages.professional.name'),
      description: t('serviceBranch.packages.professional.description'),
      price: t('serviceBranch.packages.professional.price'),
      billing: t('serviceBranch.packages.professional.billing'),
      features: tm('serviceBranch.packages.professional.features') as string[]
    },
    {
      name: t('serviceBranch.packages.enterprise.name'),
      description: t('serviceBranch.packages.enterprise.description'),
      price: t('serviceBranch.packages.enterprise.price'),
      billing: t('serviceBranch.packages.enterprise.billing'),
      features: tm('serviceBranch.packages.enterprise.features') as string[]
    }
  ]
})

const successMetrics = computed(() => {
  return [
    { value: '150+', label: t('serviceBranch.metrics.projects') },
    { value: '95%', label: t('serviceBranch.metrics.satisfaction') },
    { value: '3x', label: t('serviceBranch.metrics.roi') }
  ]
})

const branchHighlights = computed(() => {
  return [
    { icon: 'mdi:clock-outline', label: t('serviceBranch.highlights.timeline'), value: t('serviceBranch.highlights.timelineValue') },
    { icon: 'mdi:account-group', label: t('serviceBranch.highlights.teamSize'), value: t('serviceBranch.highlights.teamSizeValue') },
    { icon: 'mdi:chart-line', label: t('serviceBranch.highlights.successRate'), value: t('serviceBranch.highlights.successRateValue') }
  ]
})

const getBranchDescription = () => {
  if (!branchData.value) return ''

  // Use default description template with interpolation
  return t('serviceBranch.defaultDescription', {
    branch: branchData.value.name,
    objective: branchData.value.objective.toLowerCase()
  })
}

// Breadcrumb schema
const { breadcrumbSchema, serviceSchema, injectSchema } = useSchemaOrg()

watchEffect(() => {
  if (branchData.value && serviceData.value) {
    const localePrefix = locale.value === 'en' ? '' : `/${locale.value}`
    const currentUrl = `${localePrefix}/services/${route.params.service}/${route.params.branch}`
    const homeUrl = `${localePrefix}`
    const servicesUrl = `${localePrefix}/services`

    // Inject breadcrumb schema
    injectSchema([
      breadcrumbSchema([
        { name: 'Home', url: homeUrl },
        { name: 'Services', url: servicesUrl },
        { name: serviceData.value.title, url: `${servicesUrl}#${route.params.service}` },
        { name: branchData.value.name, url: currentUrl }
      ]),
      serviceSchema({
        name: branchData.value.name,
        description: getBranchDescription(),
        url: currentUrl,
        serviceType: serviceData.value.title
      })
    ])
  }
})

// Set page meta

const _branchConfig = useRuntimeConfig()
const _branchBaseUrl = _branchConfig.public.baseUrl || 'https://innovayse.com'

const _branchPath = computed(() => `/services/${route.params.service}/${route.params.branch}`)
const _branchCanonical = computed(() =>
  locale.value === 'en'
    ? `${_branchBaseUrl}${_branchPath.value}`
    : `${_branchBaseUrl}/${locale.value}${_branchPath.value}`
)

useSeoMeta({
  title: () => `${branchData.value?.name || 'Service'} - ${serviceData.value?.title || 'Services'} | Innovayse`,
  description: () => `${branchData.value?.name} service - ${branchData.value?.objective}. Professional ${serviceData.value?.title} solutions by Innovayse.`,
  keywords: () => `${branchData.value?.name}, ${serviceData.value?.title}, digital marketing, online marketing, SEO, PPC`,
  ogTitle: () => `${branchData.value?.name || 'Service'} - ${serviceData.value?.title || 'Services'} | Innovayse`,
  ogDescription: () => `${branchData.value?.name} service - ${branchData.value?.objective}. Professional ${serviceData.value?.title} solutions by Innovayse.`,
  ogType: 'website',
  ogImage: `${_branchBaseUrl}/og-image.jpg`,
  twitterCard: 'summary_large_image',
  twitterTitle: () => `${branchData.value?.name || 'Service'} | Innovayse`,
  twitterDescription: () => `${branchData.value?.objective || ''}`,
  twitterImage: `${_branchBaseUrl}/og-image.jpg`
})

useHead(() => ({
  link: [
    { rel: 'canonical', href: _branchCanonical.value },
    { rel: 'alternate', hreflang: 'en', href: `${_branchBaseUrl}${_branchPath.value}` },
    { rel: 'alternate', hreflang: 'ru', href: `${_branchBaseUrl}/ru${_branchPath.value}` },
    { rel: 'alternate', hreflang: 'hy', href: `${_branchBaseUrl}/hy${_branchPath.value}` },
    { rel: 'alternate', hreflang: 'x-default', href: `${_branchBaseUrl}${_branchPath.value}` }
  ]
}))
</script>
