<template>
  <div>
    <!-- Hero Section -->
    <section class="relative py-12 md:py-32 lg:py-40 bg-[#0a0a0f] overflow-hidden">
      <!-- Background -->
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-br from-primary-500/10 via-transparent to-secondary-500/10" />
        <div class="absolute top-0 left-1/4 w-[600px] h-[600px] bg-primary-500/20 rounded-full blur-[150px] animate-blob" />
        <div class="absolute bottom-0 right-1/4 w-[500px] h-[500px] bg-secondary-500/20 rounded-full blur-[140px] animate-blob animation-delay-2000" />

        <!-- Grid pattern -->
        <div class="absolute inset-0 opacity-[0.02]">
          <div style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 80px 80px;" class="w-full h-full" />
        </div>

        <!-- Floating particles -->
        <div class="absolute top-1/4 left-1/3 w-3 h-3 bg-primary-400/60 rounded-full animate-float" style="animation-delay: 0.5s;" />
        <div class="absolute bottom-1/3 right-1/4 w-2 h-2 bg-secondary-400/70 rounded-full animate-float" style="animation-delay: 1.5s;" />
      </div>

      <div class="container-custom relative z-10">
        <div class="max-w-4xl mx-auto text-center">
          <span
            v-motion
            :initial="{ opacity: 0, y: 20 }"
            :enter="{ opacity: 1, y: 0, transition: { duration: 500 } }"
            class="inline-block px-4 py-1.5 bg-primary-500/20 text-primary-400 text-sm font-medium rounded-full mb-6"
          >
            {{ $t('about.hero.badge') }}
          </span>

          <h1
            v-motion
            :initial="{ opacity: 0, y: 30 }"
            :enter="{ opacity: 1, y: 0, transition: { delay: 150, duration: 600 } }"
            class="text-5xl md:text-6xl lg:text-7xl font-bold text-white mb-6 leading-tight"
          >
            {{ $t('about.hero.title') }} <span class="bg-gradient-to-r from-primary-400 to-secondary-400 bg-clip-text text-transparent animate-gradient bg-300">{{ $t('about.hero.titleHighlight') }}</span>
          </h1>

          <p
            v-motion
            :initial="{ opacity: 0, y: 20 }"
            :enter="{ opacity: 1, y: 0, transition: { delay: 300, duration: 500 } }"
            class="text-xl text-gray-400 mb-8 leading-relaxed"
          >
            {{ $t('about.hero.description') }}
          </p>

          <!-- Quick stats -->
          <div class="grid grid-cols-2 md:grid-cols-4 gap-4 sm:gap-5 md:gap-6 max-w-3xl mx-auto">
            <div
              v-motion
              :initial="{ opacity: 0, y: 20 }"
              :enter="{ opacity: 1, y: 0, transition: { delay: 400, duration: 500 } }"
              class="text-center min-w-0"
            >
              <div class="text-2xl sm:text-3xl md:text-4xl font-bold bg-gradient-to-r from-primary-400 to-cyan-400 bg-clip-text text-transparent mb-1 break-words">{{ teamStats.years }}</div>
              <div class="text-xs sm:text-sm text-gray-500 break-words leading-tight">{{ $t('about.hero.stats.years') }}</div>
            </div>
            <div
              v-motion
              :initial="{ opacity: 0, y: 20 }"
              :enter="{ opacity: 1, y: 0, transition: { delay: 500, duration: 500 } }"
              class="text-center min-w-0"
            >
              <div class="text-2xl sm:text-3xl md:text-4xl font-bold bg-gradient-to-r from-secondary-400 to-primary-400 bg-clip-text text-transparent mb-1 break-words">{{ teamStats.projects }}+</div>
              <div class="text-xs sm:text-sm text-gray-500 break-words leading-tight">{{ $t('about.hero.stats.projects') }}</div>
            </div>
            <div
              v-motion
              :initial="{ opacity: 0, y: 20 }"
              :enter="{ opacity: 1, y: 0, transition: { delay: 600, duration: 500 } }"
              class="text-center min-w-0"
            >
              <div class="text-2xl sm:text-3xl md:text-4xl font-bold bg-gradient-to-r from-cyan-400 to-secondary-400 bg-clip-text text-transparent mb-1 break-words">{{ teamStats.teamSize }}+</div>
              <div class="text-xs sm:text-sm text-gray-500 break-words leading-tight">{{ $t('about.hero.stats.teamMembers') }}</div>
            </div>
            <div
              v-motion
              :initial="{ opacity: 0, y: 20 }"
              :enter="{ opacity: 1, y: 0, transition: { delay: 700, duration: 500 } }"
              class="text-center min-w-0"
            >
              <div class="text-2xl sm:text-3xl md:text-4xl font-bold bg-gradient-to-r from-primary-400 to-secondary-400 bg-clip-text text-transparent mb-1 break-words">{{ teamStats.clients }}+</div>
              <div class="text-xs sm:text-sm text-gray-500 break-words leading-tight">{{ $t('about.hero.stats.clients') }}</div>
            </div>
          </div>
        </div>
      </div>

      <!-- Corner decorations -->
      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-primary-500/30 pointer-events-none" />
      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/30 pointer-events-none" />
    </section>

    <!-- Core Values -->
    <section class="py-8 md:py-20 bg-[#0a0a0f] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-r from-primary-500/5 via-transparent to-secondary-500/5" />
        <div class="absolute top-1/4 right-1/4 w-[500px] h-[500px] bg-secondary-500/15 rounded-full blur-[150px] animate-blob" />
        <div class="absolute bottom-1/3 left-1/3 w-[400px] h-[400px] bg-primary-500/12 rounded-full blur-[140px] animate-blob animation-delay-2000" />

        <!-- Floating particles -->
        <div class="absolute top-20 left-1/4 w-2 h-2 bg-secondary-400/60 rounded-full animate-float" style="animation-delay: 0.6s;" />
        <div class="absolute bottom-1/4 right-1/3 w-3 h-3 bg-primary-400/50 rounded-full animate-float" style="animation-delay: 1.4s;" />

        <!-- Grid overlay -->
        <div class="absolute inset-0 opacity-[0.015]">
          <div style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 60px 60px;" class="w-full h-full" />
        </div>
      </div>

      <div class="container-custom relative z-10">
        <div
          v-motion
          :initial="{ opacity: 0, y: 30 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 600 } }"
          class="text-center mb-16"
        >
          <span class="inline-block px-4 py-1.5 bg-secondary-500/20 text-secondary-400 text-sm font-medium rounded-full mb-4">
            {{ $t('about.values.badge') }}
          </span>
          <h2 class="text-3xl md:text-5xl font-bold text-white mb-4">
            {{ $t('about.values.title') }} <span class="text-secondary-400">{{ $t('about.values.titleHighlight') }}</span>
          </h2>
          <p class="text-lg text-gray-400 max-w-2xl mx-auto">
            {{ $t('about.values.description') }}
          </p>
        </div>

        <div class="grid md:grid-cols-2 lg:grid-cols-4 gap-6">
          <div
            v-for="(value, index) in coreValues"
            :key="value.title"
            v-motion
            :initial="{ opacity: 0, y: 40 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: index * 100, duration: 500 } }"
            class="relative group"
          >
            <div class="relative h-full p-8 rounded-2xl bg-gradient-to-br from-white/5 to-white/[0.02] border border-white/10 hover:border-secondary-500/40 transition-all duration-300 hover:-translate-y-2 text-center">
              <!-- Glow background -->
              <div class="absolute inset-0 rounded-2xl opacity-0 group-hover:opacity-100 transition-opacity duration-500 bg-gradient-to-br from-secondary-500/10 to-primary-500/5" />

              <!-- Number badge -->
              <div class="absolute top-4 right-4 w-8 h-8 rounded-full bg-white/5 border border-white/10 flex items-center justify-center text-xs text-gray-500 font-mono group-hover:border-secondary-500/30 group-hover:text-secondary-400 transition-colors">
                0{{ index + 1 }}
              </div>

              <!-- Icon -->
              <div class="relative inline-flex w-16 h-16 rounded-2xl bg-gradient-to-br from-secondary-500/20 to-primary-500/10 items-center justify-center mb-6 group-hover:scale-110 group-hover:rotate-6 transition-all duration-300 border border-secondary-500/20">
                <Icon :name="value.icon" class="text-4xl text-secondary-400" />
              </div>

              <!-- Content -->
              <h3 class="relative text-xl font-bold text-white mb-3">{{ value.title }}</h3>
              <p class="relative text-sm text-gray-400 leading-relaxed">{{ value.description }}</p>

              <!-- Bottom accent -->
              <div class="absolute bottom-0 left-1/2 -translate-x-1/2 h-1 w-0 group-hover:w-24 bg-gradient-to-r from-secondary-500 to-primary-500 transition-all duration-500 rounded-full" />
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- Company Timeline -->
    <section class="py-8 md:py-20 bg-[#0d0d12] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-br from-secondary-500/5 via-transparent to-primary-500/5" />

        <!-- Animated blobs -->
        <div class="absolute top-1/3 left-1/4 w-[400px] h-[400px] bg-primary-500/10 rounded-full blur-[130px] animate-blob animation-delay-4000" />
        <div class="absolute bottom-1/4 right-1/4 w-[350px] h-[350px] bg-secondary-500/12 rounded-full blur-[120px] animate-blob" />

        <!-- Floating shapes -->
        <div class="absolute top-1/4 right-1/3 w-2 h-2 bg-primary-400/60 rounded-full animate-float" style="animation-delay: 0.8s;" />
        <div class="absolute bottom-1/3 left-1/4 w-3 h-3 bg-secondary-400/50 rounded-full animate-float" style="animation-delay: 1.8s;" />

        <!-- Decorative lines -->
        <div class="absolute top-0 left-1/3 w-px h-full bg-gradient-to-b from-transparent via-primary-500/10 to-transparent" />
        <div class="absolute top-0 right-1/3 w-px h-full bg-gradient-to-b from-transparent via-secondary-500/10 to-transparent" />
      </div>

      <div class="container-custom relative z-10">
        <div
          v-motion
          :initial="{ opacity: 0, y: 30 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 600 } }"
          class="text-center mb-16"
        >
          <span class="inline-block px-4 py-1.5 bg-primary-500/20 text-primary-400 text-sm font-medium rounded-full mb-4">
            {{ $t('about.timeline.badge') }}
          </span>
          <h2 class="text-3xl md:text-5xl font-bold text-white mb-4">
            {{ $t('about.timeline.title') }} <span class="text-primary-400">{{ $t('about.timeline.titleHighlight') }}</span>
          </h2>
          <p class="text-lg text-gray-400 max-w-2xl mx-auto">
            {{ $t('about.timeline.description') }}
          </p>
        </div>

        <div class="max-w-4xl mx-auto">
          <div class="relative">
            <!-- Timeline line -->
            <div class="absolute left-1/2 top-0 bottom-0 w-0.5 bg-gradient-to-b from-primary-500/50 via-secondary-500/50 to-primary-500/50 hidden md:block" />

            <div class="space-y-12">
              <div
                v-for="(event, index) in timeline"
                :key="event.year"
                v-motion
                :initial="{ opacity: 0, x: index % 2 === 0 ? -40 : 40 }"
                :visibleOnce="{ opacity: 1, x: 0, transition: { delay: index * 100, duration: 500 } }"
                :class="['grid md:grid-cols-2 gap-8 items-center', index % 2 === 0 ? '' : 'md:flex-row-reverse']"
              >
                <!-- Year badge -->
                <div :class="['flex justify-end', index % 2 === 0 ? 'md:justify-end' : 'md:justify-start md:order-2']">
                  <div class="relative group">
                    <div class="w-24 h-24 rounded-2xl bg-gradient-to-br from-primary-500/20 to-secondary-500/10 flex items-center justify-center border-2 border-primary-500/30 group-hover:scale-110 transition-transform duration-300 shadow-xl" style="box-shadow: 0 0 40px rgba(14, 165, 233, 0.2);">
                      <span class="text-2xl font-bold bg-gradient-to-br from-primary-400 to-secondary-400 bg-clip-text text-transparent">{{ event.year }}</span>
                    </div>
                    <!-- Connector dot -->
                    <div class="absolute top-1/2 -translate-y-1/2 w-4 h-4 rounded-full bg-primary-500 border-4 border-[#0d0d12] hidden md:block" :class="index % 2 === 0 ? '-right-14' : '-left-14'" />
                  </div>
                </div>

                <!-- Content -->
                <div :class="index % 2 === 0 ? '' : 'md:order-1'">
                  <div class="p-6 rounded-2xl bg-white/5 border border-white/10 hover:border-primary-500/30 transition-all duration-300 group hover:-translate-y-1">
                    <h3 class="text-xl font-bold text-white mb-2 group-hover:text-primary-300 transition-colors">{{ event.title }}</h3>
                    <p class="text-gray-400 leading-relaxed">{{ event.description }}</p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- Tech Stack -->
    <section class="py-8 md:py-20 bg-[#0a0a0f] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-bl from-primary-500/5 via-transparent to-secondary-500/5" />
        <div class="absolute bottom-0 left-1/4 w-[500px] h-[500px] bg-secondary-500/15 rounded-full blur-[150px] animate-blob animation-delay-2000" />
        <div class="absolute top-1/3 right-1/4 w-[450px] h-[450px] bg-primary-500/12 rounded-full blur-[140px] animate-blob" />

        <!-- Floating tech icons simulation -->
        <div class="absolute top-1/4 left-1/4 w-2 h-2 bg-primary-400/60 rounded-full animate-float" style="animation-delay: 0.7s;" />
        <div class="absolute bottom-1/3 right-1/3 w-3 h-3 bg-secondary-400/50 rounded-full animate-float" style="animation-delay: 1.6s;" />
        <div class="absolute top-2/3 left-1/2 w-2 h-2 bg-cyan-400/60 rounded-full animate-float" style="animation-delay: 2.3s;" />

        <!-- Grid pattern -->
        <div class="absolute inset-0 opacity-[0.015]">
          <div style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 50px 50px;" class="w-full h-full" />
        </div>
      </div>

      <div class="container-custom relative z-10">
        <div
          v-motion
          :initial="{ opacity: 0, y: 30 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 600 } }"
          class="text-center mb-16"
        >
          <span class="inline-block px-4 py-1.5 bg-secondary-500/20 text-secondary-400 text-sm font-medium rounded-full mb-4">
            {{ $t('about.techStack.badge') }}
          </span>
          <h2 class="text-3xl md:text-5xl font-bold text-white mb-4">
            {{ $t('about.techStack.title') }} <span class="text-secondary-400">{{ $t('about.techStack.titleHighlight') }}</span>
          </h2>
          <p class="text-lg text-gray-400 max-w-2xl mx-auto">
            {{ $t('about.techStack.description') }}
          </p>
        </div>

        <!-- Tech categories -->
        <div class="space-y-12 max-w-5xl mx-auto">
          <div
            v-for="(category, index) in techStack"
            :key="category.name"
            v-motion
            :initial="{ opacity: 0, y: 30 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: index * 100, duration: 400 } }"
            class="group"
          >
            <h3 class="text-lg font-bold text-white mb-6 flex flex-col md:flex-row items-center gap-3 text-center md:text-left">
              <div class="w-8 h-8 rounded-lg bg-gradient-to-br from-primary-500/20 to-secondary-500/10 flex items-center justify-center">
                <Icon :name="category.icon" class="text-primary-400" />
              </div>
              {{ category.name }}
            </h3>

            <div class="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-6 gap-4">
              <div
                v-for="tech in category.technologies"
                :key="tech"
                class="relative p-4 rounded-xl bg-white/5 border border-white/10 hover:border-primary-500/30 hover:bg-white/10 transition-all duration-300 text-center group hover:-translate-y-1 overflow-hidden"
              >
                <!-- Hover glow -->
                <div class="absolute inset-0 opacity-0 group-hover:opacity-100 transition-opacity duration-500 bg-gradient-to-br from-primary-500/10 to-secondary-500/5" />

                <span class="relative text-sm font-semibold text-gray-300 group-hover:text-white transition-colors">{{ tech }}</span>

                <!-- Bottom accent -->
                <div class="absolute bottom-0 left-1/2 -translate-x-1/2 h-0.5 w-0 group-hover:w-full bg-gradient-to-r from-primary-500 to-secondary-500 transition-all duration-500" />
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Section corner decorations -->
      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/25 pointer-events-none" />
      <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/25 pointer-events-none" />
    </section>

    <!-- CTA Section -->
    <section class="py-10 md:py-24 bg-[#0d0d12] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-r from-primary-500/10 via-transparent to-secondary-500/10" />
        <div class="absolute top-0 left-1/4 w-[600px] h-[600px] bg-secondary-500/20 rounded-full blur-[150px] animate-blob" />
        <div class="absolute bottom-0 right-1/4 w-[500px] h-[500px] bg-primary-500/18 rounded-full blur-[140px] animate-blob animation-delay-2000" />

        <!-- Floating elements -->
        <div class="absolute top-1/4 right-1/4 w-2 h-2 bg-secondary-400/70 rounded-full animate-float" style="animation-delay: 0.9s;" />
        <div class="absolute bottom-1/3 left-1/3 w-3 h-3 bg-primary-400/60 rounded-full animate-float" style="animation-delay: 1.7s;" />

        <!-- Grid overlay -->
        <div class="absolute inset-0 opacity-[0.015]">
          <div style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 70px 70px;" class="w-full h-full" />
        </div>
      </div>

      <div class="container-custom relative z-10">
        <div
          v-motion
          :initial="{ opacity: 0, y: 30 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 600 } }"
          class="max-w-4xl mx-auto text-center mb-12"
        >
          <h2 class="text-3xl md:text-5xl font-bold text-white mb-6">
            {{ $t('about.cta.title') }} <span class="text-secondary-400">{{ $t('about.cta.titleHighlight') }}</span>?
          </h2>
          <p class="text-lg text-gray-400 mb-8">
            {{ $t('about.cta.description') }}
          </p>
        </div>

        <!-- CTA Cards -->
        <div class="max-w-lg mx-auto">
          <!-- Contact CTA -->
          <div class="relative group cursor-pointer" @click="navigateTo(localePath('/contact'))">
            <div class="relative p-8 rounded-2xl bg-gradient-to-br from-primary-500/20 to-cyan-500/10 border-2 border-primary-500/30 hover:border-primary-500/50 transition-all duration-300 hover:-translate-y-2 overflow-hidden">
              <!-- Glow background -->
              <div class="absolute inset-0 opacity-0 group-hover:opacity-100 transition-opacity duration-500 bg-gradient-to-br from-primary-500/20 to-cyan-500/10" />

              <MessageSquare :size="48" :stroke-width="2" class="relative text-primary-400 mb-4 group-hover:scale-110 group-hover:rotate-6 transition-all duration-300" />
              <h3 class="relative text-2xl font-bold text-white mb-3">{{ $t('about.cta.contact.title') }}</h3>
              <p class="relative text-gray-400 mb-6">
                {{ $t('about.cta.contact.description') }}
              </p>

              <div class="relative inline-flex items-center gap-2 text-primary-400 font-semibold group-hover:gap-3 transition-all">
                <span>{{ $t('about.cta.contact.button') }}</span>
                <ArrowRight :size="18" :stroke-width="2" class="group-hover:translate-x-1 transition-transform" />
              </div>

              <!-- Corner accent -->
              <div class="absolute bottom-0 right-0 w-16 h-16 border-r-2 border-b-2 border-primary-500/50 rounded-br-2xl" />
            </div>
          </div>
        </div>
      </div>

      <!-- Section decorations -->
      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/25 pointer-events-none" />
      <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/25 pointer-events-none" />
    </section>
  </div>
</template>

<script setup lang="ts">
/**
 * About page - company story, values, and team
 */

import { MessageSquare, ArrowRight } from 'lucide-vue-next'
import { teamStats, timeline as rawTimeline } from '~/lib/data'

const { t } = useI18n()
const localePath = useLocalePath()

// SEO setup with canonical, hreflang, OG, Twitter tags
const { baseUrl } = useSeo({
  title: t('seo.about.title'),
  description: t('seo.about.description'),
  keywords: t('seo.about.keywords'),
  type: 'website',
  path: '/about'
})

// Schema.org structured data
const { organizationSchema, injectSchema } = useSchemaOrg()

injectSchema([
  organizationSchema(),
  {
    '@context': 'https://schema.org',
    '@type': 'AboutPage',
    '@id': `${baseUrl}/about#about`,
    mainEntity: {
      '@id': `${baseUrl}/#organization`
    },
    description: 'About Innovayse - Learn about our story, values, team, and 8+ years of delivering digital solutions',
    inLanguage: ['en', 'ru', 'hy']
  }
])

/** Map timeline with i18n translations */
const timeline = computed(() => rawTimeline.map(event => ({
  ...event,
  title: t(`about.timeline.items.${event.year}.title`),
  description: t(`about.timeline.items.${event.year}.description`)
})))

const coreValues = computed(() => [
  {
    icon: 'mdi:target',
    title: t('about.values.items.resultsDriven.title'),
    description: t('about.values.items.resultsDriven.description')
  },
  {
    icon: 'mdi:handshake',
    title: t('about.values.items.partnership.title'),
    description: t('about.values.items.partnership.description')
  },
  {
    icon: 'mdi:lightbulb-on',
    title: t('about.values.items.innovation.title'),
    description: t('about.values.items.innovation.description')
  },
  {
    icon: 'mdi:chart-line',
    title: t('about.values.items.dataInformed.title'),
    description: t('about.values.items.dataInformed.description')
  }
])

const techStack = computed(() => [
  {
    name: t('about.techStack.categories.frontend.name'),
    icon: 'mdi:web',
    technologies: [
      t('about.techStack.categories.frontend.technologies.vue'),
      t('about.techStack.categories.frontend.technologies.react'),
      t('about.techStack.categories.frontend.technologies.nextjs'),
      t('about.techStack.categories.frontend.technologies.nuxt'),
      t('about.techStack.categories.frontend.technologies.typescript'),
      t('about.techStack.categories.frontend.technologies.tailwind')
    ]
  },
  {
    name: t('about.techStack.categories.backend.name'),
    icon: 'mdi:database',
    technologies: [
      t('about.techStack.categories.backend.technologies.nodejs'),
      t('about.techStack.categories.backend.technologies.python'),
      t('about.techStack.categories.backend.technologies.laravel'),
      t('about.techStack.categories.backend.technologies.fastapi'),
      t('about.techStack.categories.backend.technologies.postgresql'),
      t('about.techStack.categories.backend.technologies.mongodb')
    ]
  },
  {
    name: t('about.techStack.categories.mobile.name'),
    icon: 'mdi:cellphone',
    technologies: [
      t('about.techStack.categories.mobile.technologies.reactNative'),
      t('about.techStack.categories.mobile.technologies.flutter'),
      t('about.techStack.categories.mobile.technologies.swift'),
      t('about.techStack.categories.mobile.technologies.kotlin')
    ]
  },
  {
    name: t('about.techStack.categories.cloudDevops.name'),
    icon: 'mdi:cloud',
    technologies: [
      t('about.techStack.categories.cloudDevops.technologies.aws'),
      t('about.techStack.categories.cloudDevops.technologies.googleCloud'),
      t('about.techStack.categories.cloudDevops.technologies.vercel'),
      t('about.techStack.categories.cloudDevops.technologies.docker'),
      t('about.techStack.categories.cloudDevops.technologies.cicd'),
      t('about.techStack.categories.cloudDevops.technologies.kubernetes')
    ]
  }
])
</script>
