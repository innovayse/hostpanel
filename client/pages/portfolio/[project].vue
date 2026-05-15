<template>
  <div v-if="projectData">
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
      </div>

      <div class="container-custom relative z-10">
        <div class="max-w-5xl mx-auto">
          <!-- Breadcrumbs -->
          <UiBreadcrumbs :items="breadcrumbItems" />
          <!-- Project header -->
          <div class="mb-8">
            <div
              v-motion
              :initial="{ opacity: 0, x: -30 }"
              :enter="{ opacity: 1, x: 0, transition: { delay: 200, duration: 500 } }"
              class="flex items-center flex-wrap gap-3 mb-6"
            >
              <span
                v-for="cat in projectData.category"
                :key="cat"
                class="inline-block px-4 py-1.5 rounded-full text-xs font-bold"
                :style="{
                  backgroundColor: getCategoryColor(cat) + '25',
                  color: getCategoryColor(cat)
                }"
              >
                {{ getCategoryLabel(cat) }}
              </span>
              <span class="text-gray-500">•</span>
              <span class="text-gray-400">{{ projectData.year }}</span>
            </div>

            <h1
              v-motion
              :initial="{ opacity: 0, y: 30 }"
              :enter="{ opacity: 1, y: 0, transition: { delay: 300, duration: 600 } }"
              class="text-4xl md:text-5xl lg:text-6xl font-bold text-white mb-6"
            >
              {{ projectData.title }}
            </h1>

            <p
              v-motion
              :initial="{ opacity: 0, y: 20 }"
              :enter="{ opacity: 1, y: 0, transition: { delay: 400, duration: 500 } }"
              class="text-xl text-gray-300 max-w-3xl leading-relaxed mb-8"
            >
              {{ projectData.overview }}
            </p>

            <a
              v-if="projectData.url"
              v-motion
              :initial="{ opacity: 0, y: 20 }"
              :enter="{ opacity: 1, y: 0, transition: { delay: 500, duration: 400 } }"
              :href="projectData.url"
              target="_blank"
              rel="noopener noreferrer"
              class="inline-flex items-center gap-2 px-6 py-3 rounded-xl bg-primary-500/10 border border-primary-500/30 text-primary-400 font-semibold hover:bg-primary-500/20 hover:border-primary-500/50 transition-all duration-200"
            >
              <ExternalLink :size="16" :stroke-width="2" />
              {{ projectData.url.replace(/^https?:\/\/(www\.)?/, '') }}
            </a>
          </div>
        </div>
      </div>

      <!-- Corner decorations -->
      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/30 pointer-events-none" />
      <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/30 pointer-events-none" />
    </section>

    <!-- Project Details -->
    <section class="py-8 md:py-20 bg-[#0a0a0f] relative">
      <div class="container-custom">
        <div class="max-w-5xl mx-auto space-y-12">
          <!-- Project Info Grid -->
          <div v-if="projectData.industry || projectData.duration || projectData.teamSize" class="grid md:grid-cols-3 gap-6">
            <div
              v-if="projectData.industry"
              v-motion
              :initial="{ opacity: 0, y: 30 }"
              :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 100, duration: 400 } }"
              class="p-5 rounded-xl bg-white/5 border border-white/10 text-center group hover:border-primary-500/30 transition-colors"
            >
              <Icon name="mdi:office-building" class="text-3xl text-primary-400 mx-auto mb-2 group-hover:scale-110 transition-transform" />
              <div class="text-xs text-gray-500 mb-1">{{ $t('common.industry') }}</div>
              <div class="font-semibold text-white">{{ projectData.industry }}</div>
            </div>

            <div
              v-if="projectData.duration"
              v-motion
              :initial="{ opacity: 0, y: 30 }"
              :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 200, duration: 400 } }"
              class="p-5 rounded-xl bg-white/5 border border-white/10 text-center group hover:border-secondary-500/30 transition-colors"
            >
              <Icon name="mdi:calendar" class="text-3xl text-secondary-400 mx-auto mb-2 group-hover:scale-110 transition-transform" />
              <div class="text-xs text-gray-500 mb-1">{{ $t('common.duration') }}</div>
              <div class="font-semibold text-white">{{ projectData.duration }}</div>
            </div>

            <div
              v-if="projectData.teamSize"
              v-motion
              :initial="{ opacity: 0, y: 30 }"
              :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 300, duration: 400 } }"
              class="p-5 rounded-xl bg-white/5 border border-white/10 text-center group hover:border-cyan-500/30 transition-colors"
            >
              <Icon name="mdi:account-group" class="text-3xl text-cyan-400 mx-auto mb-2 group-hover:scale-110 transition-transform" />
              <div class="text-xs text-gray-500 mb-1">{{ $t('common.teamSize') }}</div>
              <div class="font-semibold text-white">{{ projectData.teamSize }}</div>
            </div>
          </div>
          <!-- The Challenge -->
          <div
            v-motion
            :initial="{ opacity: 0, y: 40 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 600 } }"
          >
            <h2 class="text-3xl font-bold text-white mb-6 flex flex-col sm:flex-row items-center gap-3 break-words text-center sm:text-left">
              <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-primary-500/20 to-cyan-500/10 flex items-center justify-center">
                <Target :size="24" :stroke-width="2" class="text-primary-400" />
              </div>
              {{ $t('portfolio.project.challenge') }}
            </h2>
            <p class="text-lg text-gray-400 leading-relaxed">
              {{ projectData.task }}
            </p>
          </div>

          <!-- Our Approach -->
          <div
            v-motion
            :initial="{ opacity: 0, y: 40 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 600 } }"
          >
            <h2 class="text-3xl font-bold text-white mb-6 flex flex-col sm:flex-row items-center gap-3 break-words text-center sm:text-left">
              <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-secondary-500/20 to-primary-500/10 flex items-center justify-center">
                <Icon name="mdi:cog" class="text-secondary-400 text-2xl" />
              </div>
              {{ $t('portfolio.project.approach') }}
            </h2>
            <p class="text-lg text-gray-400 leading-relaxed">
              {{ projectData.process }}
            </p>
          </div>

          <!-- Key Metrics -->
          <div v-if="projectData.metrics && projectData.metrics.length > 0">
            <h2
              v-motion
              :initial="{ opacity: 0, x: -30 }"
              :visibleOnce="{ opacity: 1, x: 0, transition: { duration: 500 } }"
              class="text-3xl font-bold text-white mb-6 flex flex-col md:flex-row items-center gap-3 text-center md:text-left"
            >
              <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-primary-500/20 to-secondary-500/10 flex items-center justify-center">
                <Icon name="mdi:chart-bar" class="text-primary-400 text-2xl" />
              </div>
              {{ $t('portfolio.project.metrics') }}
            </h2>

            <div class="grid md:grid-cols-4 gap-6">
              <div
                v-for="(metric, mIndex) in projectData.metrics"
                :key="metric.label"
                v-motion
                :initial="{ opacity: 0, scale: 0.9 }"
                :visibleOnce="{ opacity: 1, scale: 1, transition: { delay: mIndex * 100, duration: 400 } }"
                class="p-6 rounded-xl bg-white/5 border border-white/10 hover:border-primary-500/30 transition-all duration-300 hover:-translate-y-1 group text-center"
              >
                <Icon v-if="metric.icon" :name="metric.icon" class="text-3xl text-primary-400 mx-auto mb-3 group-hover:scale-110 transition-transform" />
                <div class="text-3xl font-bold bg-gradient-to-r from-primary-400 to-secondary-400 bg-clip-text text-transparent mb-2">
                  {{ metric.value }}
                </div>
                <div class="text-xs text-gray-500 uppercase tracking-wider">{{ metric.label }}</div>
              </div>
            </div>
          </div>

          <!-- Features -->
          <div v-if="projectData.features && projectData.features.length > 0">
            <h2
              v-motion
              :initial="{ opacity: 0, x: -30 }"
              :visibleOnce="{ opacity: 1, x: 0, transition: { duration: 500 } }"
              class="text-3xl font-bold text-white mb-6 flex flex-col md:flex-row items-center gap-3 text-center md:text-left"
            >
              <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-cyan-500/20 to-blue-500/10 flex items-center justify-center">
                <Sparkle :size="24" :stroke-width="2" class="text-cyan-400" />
              </div>
              {{ $t('portfolio.project.features') }}
            </h2>

            <div class="grid md:grid-cols-2 gap-4">
              <div
                v-for="(feature, fIndex) in projectData.features"
                :key="feature"
                v-motion
                :initial="{ opacity: 0, x: -20 }"
                :visibleOnce="{ opacity: 1, x: 0, transition: { delay: fIndex * 50, duration: 300 } }"
                class="flex items-center gap-3 p-4 rounded-xl bg-white/5 border border-white/10 hover:border-cyan-500/30 hover:bg-white/10 transition-all duration-300 group"
              >
                <CheckCircle :size="20" :stroke-width="2" class="text-cyan-400 flex-shrink-0 group-hover:scale-110 transition-transform" />
                <span class="text-sm text-gray-300 group-hover:text-white transition-colors">{{ feature }}</span>
              </div>
            </div>
          </div>

          <!-- Results -->
          <div>
            <h2
              v-motion
              :initial="{ opacity: 0, x: -30 }"
              :visibleOnce="{ opacity: 1, x: 0, transition: { duration: 500 } }"
              class="text-3xl font-bold text-white mb-6 flex flex-col md:flex-row items-center gap-3 text-center md:text-left"
            >
              <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-green-500/20 to-emerald-500/10 flex items-center justify-center">
                <Icon name="mdi:chart-line" class="text-green-400 text-2xl" />
              </div>
              {{ $t('portfolio.project.results') }}
            </h2>

            <div class="grid md:grid-cols-3 gap-6">
              <div
                v-for="(result, index) in projectData.results"
                :key="index"
                v-motion
                :initial="{ opacity: 0, y: 30 }"
                :visibleOnce="{ opacity: 1, y: 0, transition: { delay: index * 100, duration: 400 } }"
                class="relative p-6 rounded-2xl bg-gradient-to-br from-green-500/10 to-emerald-500/5 border border-green-500/20 hover:border-green-500/40 transition-all duration-300 hover:-translate-y-1 group"
              >
                <div class="absolute inset-0 rounded-2xl opacity-0 group-hover:opacity-100 transition-opacity duration-500 bg-gradient-to-br from-green-500/10 to-emerald-500/5" />

                <CheckCircle :size="28" :stroke-width="2" class="relative text-green-400 mb-3 group-hover:scale-110 transition-transform" />
                <p class="relative text-base text-gray-300 leading-relaxed">{{ result }}</p>
              </div>
            </div>
          </div>

          <!-- Technologies -->
          <div>
            <h2
              v-motion
              :initial="{ opacity: 0, x: -30 }"
              :visibleOnce="{ opacity: 1, x: 0, transition: { duration: 500 } }"
              class="text-3xl font-bold text-white mb-6 flex flex-col md:flex-row items-center gap-3 text-center md:text-left"
            >
              <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-cyan-500/20 to-blue-500/10 flex items-center justify-center">
                <Icon name="mdi:code-tags" class="text-cyan-400 text-2xl" />
              </div>
              {{ $t('portfolio.project.technologies') }}
            </h2>

            <div class="flex flex-wrap gap-3">
              <span
                v-for="(tech, tIndex) in projectData.technologies"
                :key="tech"
                v-motion
                :initial="{ opacity: 0, scale: 0.8 }"
                :visibleOnce="{ opacity: 1, scale: 1, transition: { delay: tIndex * 30, duration: 300 } }"
                class="px-5 py-3 rounded-xl bg-white/5 border border-white/10 text-sm font-semibold text-gray-300 hover:border-primary-500/40 hover:bg-white/10 hover:text-white transition-all duration-300 hover:-translate-y-1 group relative overflow-hidden"
              >
                <span class="absolute inset-0 opacity-0 group-hover:opacity-100 transition-opacity duration-500 bg-gradient-to-br from-primary-500/10 to-secondary-500/5" />
                <span class="relative">{{ tech }}</span>
              </span>
            </div>

            <!-- Visit Site -->
            <div v-if="projectData.url" class="mt-6">
              <a
                :href="projectData.url"
                target="_blank"
                rel="noopener noreferrer"
                class="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-primary-500/10 border border-primary-500/30 text-primary-400 text-sm font-semibold hover:bg-primary-500/20 hover:border-primary-500/50 transition-all duration-200"
              >
                <ExternalLink :size="16" :stroke-width="2" />
                {{ projectData.url.replace(/^https?:\/\/(www\.)?/, '') }}
              </a>
            </div>
          </div>

          <!-- Testimonial -->
          <div
            v-if="projectData.testimonial"
            v-motion
            :initial="{ opacity: 0, y: 50 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 700 } }"
            class="relative p-8 md:p-10 rounded-2xl bg-gradient-to-br from-primary-500/15 to-secondary-500/10 border-2 border-primary-500/30 overflow-hidden"
          >
            <!-- Decorative background -->
            <div class="absolute top-0 right-0 w-48 h-48 bg-secondary-500/10 rounded-full blur-3xl" />
            <div class="absolute bottom-0 left-0 w-32 h-32 bg-primary-500/10 rounded-full blur-2xl" />

            <!-- Quote icon -->
            <div class="absolute top-6 right-8 text-6xl text-white/5">
              <Icon name="mdi:format-quote-close" />
            </div>

            <div class="relative">
              <!-- Stars -->
              <div class="flex gap-1 mb-6">
                <Star v-for="i in 5" :key="i" :size="20" :stroke-width="2" fill="currentColor" class="text-primary-400" />
              </div>

              <!-- Quote -->
              <p class="text-xl text-white/90 italic mb-8 leading-relaxed">
                "{{ projectData.testimonial.text }}"
              </p>

              <!-- Author -->
              <div class="flex items-center gap-4">
                <div class="w-14 h-14 rounded-full bg-gradient-to-br from-primary-500/30 to-secondary-500/20 flex items-center justify-center text-white font-bold text-lg border-2 border-primary-500/30">
                  {{ projectData.testimonial.author.charAt(0) }}
                </div>
                <div>
                  <div class="font-bold text-white text-lg">{{ projectData.testimonial.author }}</div>
                  <div class="text-gray-400">{{ projectData.testimonial.position }}</div>
                  <div class="text-secondary-400 font-semibold">{{ projectData.testimonial.company }}</div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- Navigation -->
    <section class="py-12 bg-[#0d0d12]">
      <div class="container-custom">
        <div class="max-w-5xl mx-auto space-y-4">
          <!-- Desktop Navigation: 3 columns side by side -->
          <div class="hidden md:flex items-center justify-between gap-3">
            <NuxtLink
              v-if="previousProject"
              :to="localePath(`/portfolio/${previousProject.id}`)"
              class="group flex items-center gap-2.5 px-5 py-3 rounded-xl bg-white/5 border border-white/10 hover:border-primary-500/40 hover:bg-white/10 transition-all duration-300 min-w-[180px] flex-1 max-w-[280px]"
            >
              <Icon name="mdi:arrow-left" class="text-primary-400 group-hover:-translate-x-1 transition-transform flex-shrink-0" />
              <div class="text-left min-w-0 flex-1">
                <div class="text-xs text-gray-500 mb-0.5">{{ $t('common.previous') }}</div>
                <div class="font-semibold text-sm text-white group-hover:text-primary-300 transition-colors truncate">{{ previousProject.title }}</div>
              </div>
            </NuxtLink>
            <div v-else class="min-w-[180px] flex-1 max-w-[280px]" />

            <NuxtLink
              :to="localePath('/portfolio')"
              class="flex items-center justify-center gap-2 px-6 py-3 rounded-xl bg-white/5 border border-white/10 hover:border-primary-500/40 hover:bg-white/10 transition-all duration-300 text-white font-semibold text-sm whitespace-nowrap flex-shrink-0"
            >
              <Icon name="mdi:grid" />
              <span>{{ $t('common.allProjects') }}</span>
            </NuxtLink>

            <NuxtLink
              v-if="nextProject"
              :to="localePath(`/portfolio/${nextProject.id}`)"
              class="group flex items-center gap-2.5 px-5 py-3 rounded-xl bg-white/5 border border-white/10 hover:border-primary-500/40 hover:bg-white/10 transition-all duration-300 min-w-[180px] flex-1 max-w-[280px]"
            >
              <div class="text-right min-w-0 flex-1">
                <div class="text-xs text-gray-500 mb-0.5">{{ $t('common.next') }}</div>
                <div class="font-semibold text-sm text-white group-hover:text-primary-300 transition-colors truncate">{{ nextProject.title }}</div>
              </div>
              <Icon name="mdi:arrow-right" class="text-primary-400 group-hover:translate-x-1 transition-transform flex-shrink-0" />
            </NuxtLink>
            <div v-else class="min-w-[180px] flex-1 max-w-[280px]" />
          </div>

          <!-- Mobile Navigation: Stacked layout -->
          <div class="md:hidden space-y-3">
            <!-- Previous/Next buttons in one row -->
            <div class="grid grid-cols-2 gap-3">
              <NuxtLink
                v-if="previousProject"
                :to="localePath(`/portfolio/${previousProject.id}`)"
                class="group flex items-center gap-2 px-4 py-3 rounded-xl bg-white/5 border border-white/10 hover:border-primary-500/40 hover:bg-white/10 transition-all duration-300"
              >
                <Icon name="mdi:arrow-left" class="text-primary-400 group-hover:-translate-x-1 transition-transform flex-shrink-0 text-lg" />
                <div class="text-left min-w-0 flex-1">
                  <div class="text-xs text-gray-500 mb-0.5">{{ $t('common.previous') }}</div>
                  <div class="font-semibold text-xs text-white group-hover:text-primary-300 transition-colors truncate">{{ previousProject.title }}</div>
                </div>
              </NuxtLink>
              <div v-else />

              <NuxtLink
                v-if="nextProject"
                :to="localePath(`/portfolio/${nextProject.id}`)"
                class="group flex items-center gap-2 px-4 py-3 rounded-xl bg-white/5 border border-white/10 hover:border-primary-500/40 hover:bg-white/10 transition-all duration-300"
              >
                <div class="text-right min-w-0 flex-1">
                  <div class="text-xs text-gray-500 mb-0.5">{{ $t('common.next') }}</div>
                  <div class="font-semibold text-xs text-white group-hover:text-primary-300 transition-colors truncate">{{ nextProject.title }}</div>
                </div>
                <Icon name="mdi:arrow-right" class="text-primary-400 group-hover:translate-x-1 transition-transform flex-shrink-0 text-lg" />
              </NuxtLink>
              <div v-else />
            </div>

            <!-- All Projects button full width -->
            <NuxtLink
              :to="localePath('/portfolio')"
              class="flex items-center justify-center gap-2 px-6 py-3 rounded-xl bg-white/5 border border-white/10 hover:border-primary-500/40 hover:bg-white/10 transition-all duration-300 text-white font-semibold text-sm w-full"
            >
              <Icon name="mdi:grid" />
              <span>{{ $t('common.allProjects') }}</span>
            </NuxtLink>
          </div>
        </div>
      </div>
    </section>

    <!-- CTA -->
    <SectionsCTA />
  </div>

  <!-- 404 State -->
  <div v-else class="min-h-screen flex items-center justify-center bg-[#0a0a0f]">
    <div class="text-center">
      <Icon name="mdi:folder-open" class="text-6xl text-gray-600 mx-auto mb-4" />
      <h1 class="text-2xl font-bold text-white mb-2">{{ $t('portfolio.project.notFound') }}</h1>
      <p class="text-gray-400 mb-6">{{ $t('portfolio.project.notFoundDescription') }}</p>
      <UiButton @click="navigateTo(localePath('/portfolio'))">
        <Icon name="mdi:arrow-left" class="mr-2" />
        {{ $t('common.backToPortfolio') }}
      </UiButton>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * Portfolio project detail page
 * Route: /portfolio/[project]
 */

import { projects as rawProjects } from '~/lib/data'
import type { Project } from '~/types'
import { CheckCircle, Target, Sparkle, Star, ExternalLink } from 'lucide-vue-next'

const _portfolioConfig = useRuntimeConfig()
const _portfolioBaseUrl = _portfolioConfig.public.baseUrl || 'https://innovayse.com'

const { t, tm } = useI18n()
const localePath = useLocalePath()
const route = useRoute()
const projectId = route.params.project as string
const { breadcrumbSchema, injectSchema } = useSchemaOrg()

// Find raw project data
const rawProject = computed(() => {
  return rawProjects.find(p => p.id === projectId) || null
})

// Project with i18n translations
const projectData = computed(() => {
  const raw = rawProject.value
  if (!raw) return null

  const results = tm(`portfolio.items.${raw.id}.results`) as string[]
  const features = tm(`portfolio.items.${raw.id}.features`) as string[] | undefined

  // Build metrics from locale data
  const metrics = raw.metricKeys?.map(key => ({
    label: t(`portfolio.items.${raw.id}.metrics.${key}.label`),
    value: t(`portfolio.items.${raw.id}.metrics.${key}.value`),
    icon: raw.metricIcons?.[key]
  }))

  // Build testimonial if exists
  const testimonial = raw.hasTestimonial ? {
    text: t(`portfolio.items.${raw.id}.testimonial.text`),
    author: raw.testimonialAuthor || '',
    position: t(`portfolio.items.${raw.id}.testimonial.position`),
    company: raw.testimonialCompany || ''
  } : undefined

  return {
    ...raw,
    title: t(`portfolio.items.${raw.id}.title`),
    description: t(`portfolio.items.${raw.id}.description`),
    overview: t(`portfolio.items.${raw.id}.overview`),
    industry: t(`portfolio.items.${raw.id}.industry`, ''),
    task: t(`portfolio.items.${raw.id}.task`),
    process: t(`portfolio.items.${raw.id}.process`),
    duration: t(`portfolio.items.${raw.id}.duration`, ''),
    teamSize: t(`portfolio.items.${raw.id}.teamSize`, ''),
    results: Array.isArray(results) ? results : [],
    metrics,
    features: Array.isArray(features) ? features : undefined,
    testimonial
  }
})

// Navigation - use i18n translated projects
const projects = computed(() => rawProjects.map(project => ({
  ...project,
  title: t(`portfolio.items.${project.id}.title`)
})))

const currentIndex = computed(() => {
  return rawProjects.findIndex(p => p.id === projectId)
})

const previousProject = computed(() => {
  const index = currentIndex.value
  return index > 0 ? projects.value[index - 1] : null
})

const nextProject = computed(() => {
  const index = currentIndex.value
  return index < rawProjects.length - 1 && index !== -1 ? projects.value[index + 1] : null
})

// Breadcrumb items
const breadcrumbItems = computed(() => [
  { label: t('common.portfolio'), to: localePath('/portfolio') },
  { label: projectData.value?.title || '' }
])

// Category helpers
const getCategoryColor = (category: string) => {
  const colors: Record<string, string> = {
    development: '#a78bfa',
    ecommerce: '#34d399',
    saas: '#38bdf8',
    seo: '#f472b6'
  }
  return colors[category] || '#a78bfa'
}

const getCategoryLabel = (category: string) => {
  return t(`portfolio.filters.${category}`, category)
}

// SEO
const { locale } = useI18n()
const _portfolioPath = computed(() => `/portfolio/${projectId}`)
const _portfolioCanonical = computed(() =>
  locale.value === 'en'
    ? `${_portfolioBaseUrl}${_portfolioPath.value}`
    : `${_portfolioBaseUrl}/${locale.value}${_portfolioPath.value}`
)

watchEffect(() => {
  if (projectData.value) {
    const ogImg = projectData.value.images?.[0] || `${_portfolioBaseUrl}/og-image.jpg`

    useSeoMeta({
      title: `${projectData.value.title} - Portfolio | Innovayse`,
      description: projectData.value.description,
      ogTitle: `${projectData.value.title} | Innovayse Portfolio`,
      ogDescription: projectData.value.description,
      ogImage: ogImg,
      ogType: 'article',
      ogUrl: _portfolioCanonical.value,
      twitterCard: 'summary_large_image',
      twitterTitle: `${projectData.value.title} | Innovayse`,
      twitterDescription: projectData.value.description,
      twitterImage: ogImg
    })

    injectSchema([
      breadcrumbSchema([
        { name: 'Home', url: _portfolioBaseUrl },
        { name: t('common.portfolio'), url: `${_portfolioBaseUrl}/portfolio` },
        { name: projectData.value.title, url: _portfolioCanonical.value }
      ])
    ])
  }
})

useHead(() => ({
  link: [
    { rel: 'canonical', href: _portfolioCanonical.value },
    { rel: 'alternate', hreflang: 'en', href: `${_portfolioBaseUrl}${_portfolioPath.value}` },
    { rel: 'alternate', hreflang: 'ru', href: `${_portfolioBaseUrl}/ru${_portfolioPath.value}` },
    { rel: 'alternate', hreflang: 'hy', href: `${_portfolioBaseUrl}/hy${_portfolioPath.value}` },
    { rel: 'alternate', hreflang: 'x-default', href: `${_portfolioBaseUrl}${_portfolioPath.value}` }
  ]
}))
</script>
