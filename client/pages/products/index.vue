<template>
  <div>
    <!-- Hero Section -->
    <section class="relative py-12 md:py-32 lg:py-40 bg-[#0a0a0f] overflow-hidden">
      <!-- Animated Background -->
      <div class="absolute inset-0 pointer-events-none">
        <!-- Gradient blobs -->
        <div class="absolute top-0 right-1/4 w-[600px] h-[600px] bg-secondary-500/25 rounded-full blur-[150px] animate-blob" />
        <div class="absolute bottom-0 left-1/3 w-[500px] h-[500px] bg-cyan-500/20 rounded-full blur-[140px] animate-blob animation-delay-2000" />
        <div class="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[400px] h-[400px] bg-primary-500/15 rounded-full blur-[130px] animate-blob animation-delay-4000" />

        <!-- Grid pattern -->
        <div class="absolute inset-0 opacity-[0.025]">
          <div style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 80px 80px;" class="w-full h-full" />
        </div>

        <!-- Floating elements -->
        <div class="absolute top-1/4 left-1/4 w-3 h-3 bg-secondary-400/60 rounded-full animate-float" style="animation-delay: 0.5s;" />
        <div class="absolute top-1/3 right-1/3 w-2 h-2 bg-cyan-400/70 rounded-full animate-float" style="animation-delay: 1.2s;" />
        <div class="absolute bottom-1/3 left-1/3 w-2 h-2 bg-primary-400/70 rounded-full animate-float" style="animation-delay: 2s;" />
      </div>

      <div class="container-custom relative z-10">
        <div class="max-w-4xl mx-auto text-center">
          <!-- Badge -->
          <div class="inline-flex items-center gap-2 px-4 py-2 mb-6 rounded-full glass border border-secondary-500/20 backdrop-blur-sm">
            <span class="relative flex h-2 w-2">
              <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-secondary-400 opacity-75"></span>
              <span class="relative inline-flex rounded-full h-2 w-2 bg-secondary-500"></span>
            </span>
            <span class="text-sm font-medium text-gray-300">{{ $t('products.hero.badge') }}</span>
          </div>

          <!-- Headline -->
          <h1 class="text-5xl md:text-6xl lg:text-7xl font-bold mb-8 leading-[1.1]">
            <span class="block text-primary-400">
              {{ $t('products.hero.headline1') }}
            </span>
            <span class="block text-white mt-4">
              {{ $t('products.hero.headline2') }}
            </span>
          </h1>

          <!-- Subtitle -->
          <p class="text-xl text-gray-400 mb-12 leading-relaxed max-w-3xl mx-auto">
            <i18n-t keypath="products.hero.subtitle" tag="span">
              <template #whiteLabel>
                <span class="text-secondary-400 font-semibold">{{ $t('products.hero.whiteLabelHighlight') }}</span>
              </template>
            </i18n-t>
          </p>

          <!-- CTA Buttons -->
          <div class="flex flex-col sm:flex-row items-center justify-center gap-3 sm:gap-4 mb-12 sm:mb-14 md:mb-16">
            <button
              class="w-full sm:w-auto group relative px-4 py-2.5 sm:px-6 sm:py-3 md:px-8 md:py-4 rounded-lg sm:rounded-xl font-semibold text-white overflow-hidden transition-all duration-300 hover:scale-105 hover:shadow-2xl hover:shadow-secondary-500/50 text-sm sm:text-base md:text-lg"
              @click="scrollToProducts"
            >
              <div class="absolute inset-0 bg-gradient-to-r from-secondary-600 to-cyan-600 transition-transform duration-300 group-hover:scale-110" />
              <div class="absolute inset-0 opacity-0 group-hover:opacity-100 transition-opacity duration-700">
                <div class="absolute inset-0 bg-gradient-to-r from-transparent via-white/30 to-transparent animate-shine" style="background-size: 200% 100%;" />
              </div>
              <span class="relative flex items-center justify-center gap-2">
                {{ $t('products.hero.cta.viewProducts') }}
                <ArrowDown :size="20" :stroke-width="2" class="group-hover:translate-y-1 transition-transform sm:w-5 sm:h-5 md:w-6 md:h-6" />
              </span>
            </button>

            <button
              class="w-full sm:w-auto px-4 py-2.5 sm:px-6 sm:py-3 md:px-8 md:py-4 rounded-lg sm:rounded-xl font-semibold text-white border-2 border-gray-700 hover:border-secondary-500/50 glass-dark transition-all duration-300 hover:scale-105 text-sm sm:text-base md:text-lg"
              @click="navigateTo(localePath('/contact'))"
            >
              <span class="flex items-center justify-center gap-2">
                <Calendar :size="20" :stroke-width="2" class="sm:w-5 sm:h-5 md:w-6 md:h-6" />
                {{ $t('products.hero.cta.bookDemo') }}
              </span>
            </button>
          </div>

          <!-- Trust badges -->
          <div
            v-motion
            :initial="{ opacity: 0, y: 20 }"
            :enter="{ opacity: 1, y: 0, transition: { delay: 400, duration: 500 } }"
            class="flex flex-wrap items-center justify-center gap-8 text-sm text-gray-500"
          >
            <div class="flex items-center gap-2">
              <ShieldCheck :size="20" :stroke-width="2" class="text-green-400" />
              <span>{{ $t('products.hero.trustBadges.soc2') }}</span>
            </div>
            <div class="flex items-center gap-2">
              <Lock :size="20" :stroke-width="2" class="text-cyan-400" />
              <span>{{ $t('products.hero.trustBadges.encryption') }}</span>
            </div>
            <div class="flex items-center gap-2">
              <Trophy :size="20" :stroke-width="2" class="text-secondary-400" />
              <span>{{ $t('products.hero.trustBadges.uptime') }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Corner decorations -->
      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-cyan-500/30 pointer-events-none" />
      <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-secondary-500/30 pointer-events-none" />
    </section>

    <!-- Products Showcase -->
    <section id="products-section" class="py-24 bg-[#0a0a0f] relative">
      <div class="container-custom">
        <!-- Product Navigation (Catalog) -->
        <div class="flex flex-wrap justify-center gap-2 sm:gap-3 mb-12 sm:mb-14 md:mb-16">
          <NuxtLink
            v-for="(product, index) in products"
            :key="product.id"
            v-motion
            :initial="{ opacity: 0, y: 20 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: index * 100, duration: 400 } }"
            :to="localePath(product.learnMoreUrl || `/products/${product.id}`)"
            :class="[
              'relative px-3 py-2 sm:px-4 sm:py-2.5 md:px-6 md:py-3 rounded-lg sm:rounded-xl font-semibold transition-all duration-300 overflow-hidden whitespace-nowrap text-sm sm:text-base border border-white/10 hover:border-white/20 bg-white/5 text-gray-400 hover:text-white'
            ]"
          >
            <span class="relative flex items-center gap-1.5 sm:gap-2">
              <Icon :name="`mdi:${product.icon}`" class="text-base sm:text-lg md:text-xl" />
              {{ product.name }}
            </span>
          </NuxtLink>
        </div>

        <!-- Catalog Grid -->
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 md:gap-8">
          <NuxtLink
            v-for="(product, index) in products"
            :key="product.id"
            v-motion
            :initial="{ opacity: 0, y: 30 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: index * 100, duration: 500 } }"
            :to="localePath(product.learnMoreUrl || `/products/${product.id}`)"
            class="group relative h-full flex flex-col p-6 sm:p-8 rounded-2xl border border-white/10 glass-dark hover:border-white/20 transition-all duration-300 hover:-translate-y-2"
          >
            <!-- Card Decoration (Radial Glow) -->
            <div class="absolute inset-0 bg-gradient-to-br opacity-0 group-hover:opacity-100 transition-opacity duration-500 rounded-2xl pointer-events-none" :style="{ backgroundImage: `radial-gradient(circle at top right, ${product.color}15, transparent 70%)` }" />

            <!-- Card Header -->
            <div class="relative flex items-start gap-4 mb-6">
              <div
                class="w-14 h-14 rounded-xl flex items-center justify-center border-2 shadow-lg group-hover:scale-110 transition-transform duration-500"
                :style="{
                  backgroundColor: `${product.color}15`,
                  borderColor: `${product.color}40`,
                  boxShadow: `0 0 30px ${product.color}20`
                }"
              >
                <Icon :name="`mdi:${product.icon}`" class="text-3xl" :style="{ color: product.color }" />
              </div>
              <div class="flex-1">
                <h3 class="text-xl font-bold text-white group-hover:text-primary-300 transition-colors">{{ product.name }}</h3>
                <p class="text-xs uppercase tracking-wider font-bold mb-2" :style="{ color: product.color }">{{ product.tagline }}</p>
              </div>
            </div>

            <!-- Product Summary -->
            <p class="relative text-sm text-gray-400 leading-relaxed mb-6 flex-1">
              {{ product.description }}
            </p>

            <!-- Feature Highlights -->
            <div v-if="product.features && product.features.length" class="relative space-y-2 mb-8">
              <div
                v-for="(feature, fIndex) in product.features?.slice(0, 3)"
                :key="fIndex"
                class="flex items-center gap-2"
              >
                <CheckCircle :size="16" :stroke-width="2" :style="{ color: product.color }" />
                <span class="text-xs text-gray-300 line-clamp-1">{{ feature }}</span>
              </div>
            </div>
            <div v-else class="relative space-y-2 mb-8">
              <div class="flex items-center gap-2">
                <CheckCircle :size="16" :stroke-width="2" :style="{ color: product.color }" />
                <span class="text-xs text-gray-300">{{ $t('products.products.badge') }}</span>
              </div>
            </div>

            <!-- Action Link (Visual Only) -->
            <div
              class="relative inline-flex items-center justify-center gap-2 px-6 py-3 rounded-xl font-bold text-white transition-all duration-300 overflow-hidden group/btn"
              :style="{
                backgroundColor: `${product.color}20`,
                borderColor: `${product.color}40`,
                borderWidth: '1px'
              }"
            >
              <span class="relative z-10 flex items-center gap-2 group-hover/btn:translate-x-1 transition-transform">
                {{ $t('products.pricing.cta.learnMore') }}
                <Icon name="mdi:arrow-right" :size="18" />
              </span>
              <div class="absolute inset-0 bg-gradient-to-r opacity-0 group-hover/btn:opacity-100 transition-opacity duration-300" :style="{ backgroundImage: `linear-gradient(to right, ${product.color}40, ${product.color}20)` }" />
            </div>
          </NuxtLink>
        </div>
      </div>

      <!-- Decorative element -->
      <div class="absolute bottom-0 left-0 right-0 h-px bg-gradient-to-r from-transparent via-secondary-500/30 to-transparent" />
    </section>

    <!-- Comparison Table -->
    <section class="py-8 md:py-20 bg-[#0d0d12] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-br from-primary-500/5 via-transparent to-secondary-500/5" />
        <div class="absolute top-1/2 right-1/4 w-96 h-96 bg-primary-500/10 rounded-full blur-[150px]" />
      </div>

      <div class="container-custom relative z-10">
        <div
          v-motion
          :initial="{ opacity: 0, y: 30 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 600 } }"
          class="text-center mb-12"
        >
          <span class="inline-block px-4 py-1.5 bg-primary-500/20 text-primary-400 text-sm font-medium rounded-full mb-4">
            {{ $t('products.comparison.title') }}
          </span>
          <h2 class="text-3xl md:text-5xl font-bold text-white mb-4">
            <i18n-t keypath="products.comparison.headline" tag="span">
              <template #perfectMatch>
                <span class="text-primary-400">{{ $t('products.comparison.perfectMatchHighlight') }}</span>
              </template>
            </i18n-t>
          </h2>
          <p class="text-lg text-gray-400 max-w-2xl mx-auto">
            {{ $t('products.comparison.subtitle') }}
          </p>
        </div>

        <!-- Comparison Grid -->
        <div
          v-motion
          :initial="{ opacity: 0, y: 40 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 200, duration: 600 } }"
          class="max-w-6xl mx-auto"
        >
          <UiTable scrollable>
            <!-- Header -->
            <UiTableHead>
              <tr>
                <th class="text-left p-6 text-white font-bold text-lg w-1/4">{{ $t('products.comparison.headers.product') }}</th>
                <th class="p-6 text-center">
                  <div class="text-gray-400 font-semibold text-sm mb-1">{{ $t('products.comparison.headers.starter') }}</div>
                  <div class="text-xs text-gray-500">{{ $t('products.comparison.headers.starterDesc') }}</div>
                </th>
                <th class="p-6 text-center bg-gradient-to-b from-primary-500/10 to-transparent">
                  <div class="inline-flex flex-col">
                    <div class="text-white font-bold text-sm mb-1">{{ $t('products.comparison.headers.professional') }}</div>
                    <div class="text-xs font-semibold px-2 py-0.5 rounded-full bg-primary-500 text-white inline-block">{{ $t('products.comparison.headers.professionalBadge') }}</div>
                  </div>
                </th>
                <th class="p-6 text-center">
                  <div class="text-gray-400 font-semibold text-sm mb-1">{{ $t('products.comparison.headers.enterprise') }}</div>
                  <div class="text-xs text-gray-500">{{ $t('products.comparison.headers.enterpriseDesc') }}</div>
                </th>
              </tr>
            </UiTableHead>

            <!-- Body -->
            <UiTableBody class="bg-[#12121a]">
              <UiTableRow
                v-for="(product, pIndex) in products.filter(p => p.pricing.length > 0)"
                :key="product.id"
                v-motion
                :initial="{ opacity: 0, x: -20 }"
                :visibleOnce="{ opacity: 1, x: 0, transition: { delay: 300 + pIndex * 100, duration: 400 } }"
                group
              >
                <!-- Product name -->
                <td class="p-6">
                  <NuxtLink :to="localePath(product.learnMoreUrl || `/products/${product.id}`)" class="flex items-center gap-3 group/link">
                    <div
                      class="w-12 h-12 rounded-xl flex items-center justify-center border transition-all duration-300 group-hover/link:scale-110"
                      :style="{
                        backgroundColor: product.color + '15',
                        borderColor: product.color + '30'
                      }"
                    >
                      <Icon :name="`mdi:${product.icon}`" class="text-2xl" :style="{ color: product.color }" />
                    </div>
                    <div>
                      <div class="font-bold text-white group-hover/link:text-primary-300 transition-colors">{{ product.name }}</div>
                      <div class="text-xs text-gray-500 group-hover/link:text-gray-400 transition-colors">{{ product.tagline }}</div>
                    </div>
                  </NuxtLink>
                </td>

                <!-- Pricing tiers -->
                <td class="p-6 text-center">
                  <div class="text-2xl font-bold text-white">{{ product.pricing[0]?.price }}</div>
                </td>
                <td class="p-6 text-center bg-primary-500/5">
                  <div class="text-2xl font-bold" :style="{ color: product.color }">
                    {{ product.pricing[1]?.price }}
                  </div>
                </td>
                <td class="p-6 text-center">
                  <div class="text-2xl font-bold text-white">{{ product.pricing[2]?.price }}</div>
                </td>
              </UiTableRow>
            </UiTableBody>
          </UiTable>
        </div>

        <!-- Table footer note -->
        <div class="text-center mt-8">
          <p class="text-sm text-gray-500">
            {{ $t('products.comparison.footer') }}
          </p>
        </div>
      </div>
    </section>

    <!-- Benefits Section -->
    <section class="py-8 md:py-20 bg-[#0a0a0f] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[700px] h-[500px] bg-secondary-500/15 rounded-full blur-[180px]" />

        <!-- Floating shapes -->
        <div class="absolute top-20 left-1/4 w-2 h-2 bg-secondary-400/60 rounded-full animate-float" style="animation-delay: 0.7s;" />
        <div class="absolute bottom-1/4 right-1/3 w-3 h-3 bg-cyan-400/50 rounded-full animate-float" style="animation-delay: 1.6s;" />
      </div>

      <div class="container-custom relative z-10">
        <!-- Header -->
        <div class="text-center mb-16">
          <h2 class="text-3xl md:text-5xl font-bold text-white mb-4">
            <i18n-t keypath="products.benefits.title" tag="span">
              <template #benefitsHighlight>
                <span class="bg-gradient-to-r from-secondary-400 to-cyan-400 bg-clip-text text-transparent">{{ $t('products.benefits.benefitsHighlight') }}</span>
              </template>
            </i18n-t>
          </h2>
          <p class="text-gray-400 max-w-2xl mx-auto">
            {{ $t('products.benefits.subtitle') }}
          </p>
        </div>

        <!-- Benefits Grid -->
        <div class="grid md:grid-cols-2 lg:grid-cols-4 gap-6 mb-16">
          <div
            v-for="(benefit, bIndex) in benefits"
            :key="bIndex"
            v-motion
            :initial="{ opacity: 0, y: 40 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: bIndex * 100, duration: 500 } }"
            class="relative group"
          >
            <div class="relative h-full p-8 rounded-2xl bg-gradient-to-br from-white/5 to-white/[0.02] border border-white/10 hover:border-secondary-500/40 transition-all duration-300 hover:-translate-y-2 text-center">
              <!-- Animated background -->
              <div class="absolute inset-0 rounded-2xl opacity-0 group-hover:opacity-100 transition-opacity duration-500 bg-gradient-to-br from-secondary-500/10 to-cyan-500/5" />

              <!-- Icon -->
              <div class="relative inline-flex w-16 h-16 rounded-2xl bg-gradient-to-br from-secondary-500/25 to-cyan-500/15 items-center justify-center mb-4 group-hover:scale-110 group-hover:rotate-6 transition-all duration-300 border-2 border-secondary-500/30 shadow-xl" :style="{ boxShadow: `0 0 30px ${['#a855f7', '#06b6d4', '#8b5cf6', '#22d3ee'][bIndex]}20` }">
                <Icon :name="benefit.icon" class="text-3xl text-secondary-400" />
              </div>

              <!-- Content -->
              <h3 class="relative text-lg font-bold text-white mb-3">{{ benefit.title }}</h3>
              <p class="relative text-sm text-gray-400 leading-relaxed">{{ benefit.description }}</p>

              <!-- Bottom accent -->
              <div class="absolute bottom-0 left-1/2 -translate-x-1/2 h-1 w-0 group-hover:w-24 bg-gradient-to-r from-secondary-500 to-cyan-500 transition-all duration-500 rounded-full" />
            </div>
          </div>
        </div>

        <!-- Additional Info -->
        <div class="grid md:grid-cols-3 gap-6 max-w-4xl mx-auto">
          <div
            v-motion
            :initial="{ opacity: 0, y: 30 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 100, duration: 400 } }"
            class="p-6 rounded-xl bg-white/5 border border-white/10 text-center hover:border-green-500/30 transition-colors group"
          >
            <CheckCircle :size="32" :stroke-width="2" class="text-green-400 mx-auto mb-3 group-hover:scale-110 transition-transform" />
            <div class="text-sm font-semibold text-white mb-1">{{ $t('products.benefits.additional.trial.title') }}</div>
            <div class="text-xs text-gray-500">{{ $t('products.benefits.additional.trial.description') }}</div>
          </div>

          <div
            v-motion
            :initial="{ opacity: 0, y: 30 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 200, duration: 400 } }"
            class="p-6 rounded-xl bg-white/5 border border-white/10 text-center hover:border-blue-500/30 transition-colors group"
          >
            <ShieldCheck :size="32" :stroke-width="2" class="text-blue-400 mx-auto mb-3 group-hover:scale-110 transition-transform" />
            <div class="text-sm font-semibold text-white mb-1">{{ $t('products.benefits.additional.guarantee.title') }}</div>
            <div class="text-xs text-gray-500">{{ $t('products.benefits.additional.guarantee.description') }}</div>
          </div>

          <div
            v-motion
            :initial="{ opacity: 0, y: 30 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 300, duration: 400 } }"
            class="p-6 rounded-xl bg-white/5 border border-white/10 text-center hover:border-purple-500/30 transition-colors group"
          >
            <Infinity :size="32" :stroke-width="2" class="text-purple-400 mx-auto mb-3 group-hover:scale-110 transition-transform" />
            <div class="text-sm font-semibold text-white mb-1">{{ $t('products.benefits.additional.lifetime.title') }}</div>
            <div class="text-xs text-gray-500">{{ $t('products.benefits.additional.lifetime.description') }}</div>
          </div>
        </div>
      </div>

      <!-- Corner decorations -->
      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-cyan-500/25 pointer-events-none" />
      <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-secondary-500/25 pointer-events-none" />
    </section>

    <!-- Product Testimonials -->
    <section class="py-8 md:py-20 bg-[#0a0a0f] relative">
      <div class="container-custom">
        <div class="text-center mb-12">
          <h2 class="text-3xl md:text-4xl font-bold text-white mb-4">
            <i18n-t keypath="products.testimonials.title" tag="span">
              <template #thousands>
                <span class="text-secondary-400">{{ $t('products.testimonials.thousandsHighlight') }}</span>
              </template>
            </i18n-t>
          </h2>
          <p class="text-gray-400">{{ $t('products.testimonials.subtitle') }}</p>
        </div>

        <div class="grid md:grid-cols-3 gap-6 max-w-5xl mx-auto">
          <div
            v-for="(testimonial, tIndex) in productTestimonials"
            :key="testimonial.id"
            v-motion
            :initial="{ opacity: 0, y: 30 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: tIndex * 100, duration: 400 } }"
            class="p-6 rounded-2xl bg-gradient-to-br from-white/5 to-white/[0.02] border border-white/10 hover:border-secondary-500/30 transition-all duration-300 hover:-translate-y-2 group"
          >
            <!-- Stars -->
            <div class="flex gap-1 mb-4">
              <Star v-for="i in 5" :key="i" :size="14" :stroke-width="2" fill="currentColor" class="text-secondary-400" />
            </div>

            <!-- Text -->
            <p class="text-sm text-gray-300 mb-4 italic leading-relaxed">
              "{{ testimonial.text }}"
            </p>

            <!-- Author -->
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 rounded-full bg-gradient-to-br from-secondary-500/30 to-cyan-500/20 flex items-center justify-center text-white font-bold text-sm border border-secondary-500/30">
                {{ testimonial.name.charAt(0) }}
              </div>
              <div>
                <div class="text-sm font-semibold text-white">{{ testimonial.name }}</div>
                <div class="text-xs text-gray-500">{{ testimonial.company }}</div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- CTA -->
    <SectionsCTA />
  </div>
</template>

<script setup lang="ts">
/**
 * Products page - SaaS products showcase with pricing
 */

import { testimonials } from '../../lib/data'
import { ArrowDown, Calendar, ShieldCheck, Lock, Trophy, PlayCircle, Star, DollarSign, Sparkle, CheckCircle, Infinity, Search, ArrowLeftRight } from 'lucide-vue-next'
import { useCartStore } from '~/stores/cart'

const { t, tm, locale } = useI18n()
const localePath = useLocalePath()
const cart = useCartStore()
onMounted(() => cart.init())

// productConfig, currencyByLocale, nameToKey, parseDescription — auto-imported from utils/whmcs.ts

// Fetch all product groups (hosting gid=1 + SaaS gids 3-9)
const { data: whmcsRaw } = await useApi('/api/portal/public/products', {
  query: computed(() => ({ lang: locale.value, gids: productGids.join(',') }))
})

// SEO setup with canonical, hreflang, OG, Twitter tags
const { baseUrl: productsBaseUrl } = useSeo({
  title: t('seo.products.title'),
  description: t('seo.products.description'),
  keywords: t('seo.products.keywords'),
  type: 'website',
  path: '/products'
})

// Schema.org
const { organizationSchema: productsOrgSchema, injectSchema: productsInjectSchema } = useSchemaOrg()
productsInjectSchema([
  productsOrgSchema(),
  {
    '@context': 'https://schema.org',
    '@type': 'CollectionPage',
    '@id': `${productsBaseUrl}/products#productspage`,
    url: `${productsBaseUrl}/products`,
    name: 'SaaS Products - Innovayse',
    description: t('seo.products.description'),
    inLanguage: ['en', 'ru', 'hy'],
    publisher: { '@id': `${productsBaseUrl}/#organization` }
  }
])

// productGids, productGidToKey, productConfig, currencyByLocale, parseDescription — auto-imported from utils/whmcs.ts

/** Build plan pricing helper */
function buildPricing(plans: any[]) {
  const preferred = (currencyByLocale[locale.value] ?? 'USD') as string
  return plans.map((plan: any) => {
    const currency = plan.pricing
      ? (plan.pricing[preferred] ?? Object.values(plan.pricing)[0] as any)
      : null
    const cycleKeys = ['monthly', 'quarterly', 'semiannually', 'annually', 'biennially', 'triennially'] as const
    const firstCycle = cycleKeys.find(k => currency?.[k] && currency[k] !== '-1.00' && currency[k] !== '0.00')
    const price = firstCycle && currency ? `${currency.prefix}${currency[firstCycle]}` : ''
    const { features: pTFeatures } = parseDescription(plan.translated_description || '')
    const { features: pEnFeatures } = parseDescription(plan.description || '')
    return {
      pid: plan.pid as number,
      product_url: (plan.product_url as string) || '',
      tier: plan.translated_name || plan.name,
      slug: (plan.slug as string) || '',
      price,
      prefix: currency?.prefix ?? '',
      rawPrice: firstCycle && currency ? currency[firstCycle] : '0',
      billingcycle: firstCycle ?? 'monthly',
      paytype: (plan.paytype as string) || 'recurring',
      features: pTFeatures.length > 0 ? pTFeatures : pEnFeatures
    }
  })
}

/** Group WHMCS plans by gid → one entry per product family, with hosting + domains prepended */
const products = computed(() => {
  const raw = (whmcsRaw.value as any[] ?? [])
  const items: any[] = []

  // ── Domains (static) ──
  items.push({
    id: 'domains',
    gid: null,
    icon: 'earth',
    color: '#3b82f6',
    demoUrl: '',
    learnMoreUrl: '/domains',
    name: t('domains.card.name'),
    tagline: t('domains.card.tagline'),
    description: t('domains.card.description'),
    features: [t('domains.card.f1'), t('domains.card.f2'), t('domains.card.f3')],
    pricing: []
  })

  // ── All product families (gids 1, 3-9) ──
  for (const gid of productGids) {
    const cfgKey = productGidToKey[gid] ?? ''
    const cfg = productConfig[cfgKey] ?? { name: cfgKey, icon: 'cube', color: '#6366f1', learnMoreUrl: '' }
    const allInGroup = raw.filter((p: any) => Number(p.gid) === gid)
    const parentProduct = allInGroup.find((p: any) => (p.slug as string)?.trim() === cfgKey)
    const subPlans = allInGroup.filter((p: any) => (p.slug as string)?.trim() !== cfgKey)
    // If no sub-plans, use parent product as the single plan
    const plans = subPlans.length > 0 ? subPlans : (parentProduct ? [parentProduct] : [])
    if (plans.length === 0) continue

    const firstPlan = parentProduct || plans[0]
    
    // Aggregate unique features from all plans in this group for the hub view
    const featuresSet = new Set<string>()
    allInGroup.forEach(p => {
      // 1. From group_features
      const gf = Array.isArray(p.group_features) ? p.group_features : []
      gf.forEach((f: any) => featuresSet.add(f.translated_feature || f.feature))
      
      // 2. From description parsing
      const { features: parsed } = parseDescription(p.translated_description || p.description || '')
      parsed.forEach(f => featuresSet.add(f))
    })
    const features = Array.from(featuresSet).filter(Boolean).slice(0, 6)

    const tSummary = parseDescription(firstPlan?.translated_description || '').summary
    const enSummary = parseDescription(firstPlan?.description || '').summary

    items.push({
      id: cfgKey,
      gid,
      ...cfg,
      tagline: getGt(firstPlan, 'tagline'),
      description: firstPlan?.translated_shortdescription || firstPlan?.shortdescription || tSummary || enSummary || getGt(firstPlan, 'headline'),
      features,
      pricing: buildPricing(plans)
    })
  }

  return items
})

const getProductImage = (index: number) => {
  const images = [
    '/images/categories/team.jpg',
    '/images/categories/realestate.jpg',
    '/images/categories/ecommerce.jpg',
    '/images/categories/saas.jpg'
  ]
  return images[index % images.length]
}

const scrollToProducts = () => {
  document.getElementById('products-section')?.scrollIntoView({ behavior: 'smooth' })
}

const domainSearchQuery = ref('')
const goSearchDomain = () => {
  const q = domainSearchQuery.value.trim()
  navigateTo(localePath('/domains') + (q ? `?domain=${encodeURIComponent(q)}` : ''))
}

// Use computed for benefits to support translations
const benefits = computed(() => [
  {
    icon: 'mdi:palette',
    title: t('products.benefits.whiteLabel.title'),
    description: t('products.benefits.whiteLabel.description')
  },
  {
    icon: 'mdi:headset',
    title: t('products.benefits.support.title'),
    description: t('products.benefits.support.description')
  },
  {
    icon: 'mdi:refresh',
    title: t('products.benefits.updates.title'),
    description: t('products.benefits.updates.description')
  },
  {
    icon: 'mdi:code-tags',
    title: t('products.benefits.api.title'),
    description: t('products.benefits.api.description')
  }
])

// Get product-related testimonials with i18n translations
const productTestimonials = computed(() => testimonials.slice(0, 3).map(testimonial => {
  const text = t(`testimonials.items.${testimonial.id}.text`)
  return {
    id: testimonial.id,
    name: testimonial.name,
    company: testimonial.company,
    text: text.length > 120 ? text.slice(0, 120) + '...' : text
  }
}))
</script>

<style scoped>
.product-transition-enter-active,
.product-transition-leave-active {
  transition: all 0.4s ease;
}

.product-transition-enter-from {
  opacity: 0;
  transform: translateX(30px);
}

.product-transition-leave-to {
  opacity: 0;
  transform: translateX(-30px);
}

@keyframes shine {
  0% {
    transform: translateX(-100%);
  }
  100% {
    transform: translateX(100%);
  }
}

.animate-shine {
  animation: shine 3s linear infinite;
}
</style>
