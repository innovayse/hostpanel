<template>
  <div>
    <!-- Hero Section -->
    <section class="relative py-12 md:py-32 bg-[#0a0a0f] overflow-hidden">
      <!-- Background -->
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-br from-primary-500/10 via-transparent to-secondary-500/10" />
        <div class="absolute top-0 left-1/4 w-[600px] h-[600px] bg-primary-500/20 rounded-full blur-[150px] animate-blob" />
        <div class="absolute bottom-0 right-1/4 w-[500px] h-[500px] bg-secondary-500/20 rounded-full blur-[140px] animate-blob animation-delay-2000" />

        <!-- Grid pattern -->
        <div class="absolute inset-0 opacity-[0.02]">
          <div style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 60px 60px;" class="w-full h-full" />
        </div>

        <!-- Floating particles -->
        <div class="absolute top-1/4 left-1/3 w-2 h-2 bg-primary-400/70 rounded-full animate-float" style="animation-delay: 0.5s;" />
        <div class="absolute bottom-1/3 right-1/4 w-3 h-3 bg-secondary-400/60 rounded-full animate-float" style="animation-delay: 1.5s;" />
      </div>

      <div class="container-custom relative z-10 text-center">
        <span class="inline-block px-4 py-1.5 bg-primary-500/20 text-primary-400 text-sm font-medium rounded-full mb-6">
          {{ $t('portfolio.badge') }}
        </span>
        <h1 class="text-4xl md:text-6xl lg:text-7xl font-bold text-white mb-6 leading-tight">
          {{ $t('portfolio.title') }}
        </h1>
        <p class="text-lg md:text-xl text-gray-400 max-w-3xl mx-auto mb-8">
          {{ $t('portfolio.subtitle') }}
        </p>

        <!-- Quick stats -->
        <div class="grid grid-cols-3 gap-3 sm:gap-4 md:gap-6 max-w-2xl mx-auto">
          <div
            v-motion
            :initial="{ opacity: 0, y: 20 }"
            :enter="{ opacity: 1, y: 0, transition: { delay: 200, duration: 500 } }"
            class="text-center min-w-0"
          >
            <div class="text-2xl sm:text-3xl md:text-4xl font-bold bg-gradient-to-r from-primary-400 to-cyan-400 bg-clip-text text-transparent mb-1 break-words">{{ projects.length }}+</div>
            <div class="text-xs sm:text-sm text-gray-500 break-words leading-tight">{{ $t('portfolio.stats.projects') }}</div>
          </div>
          <div
            v-motion
            :initial="{ opacity: 0, y: 20 }"
            :enter="{ opacity: 1, y: 0, transition: { delay: 300, duration: 500 } }"
            class="text-center min-w-0"
          >
            <div class="text-2xl sm:text-3xl md:text-4xl font-bold bg-gradient-to-r from-secondary-400 to-primary-400 bg-clip-text text-transparent mb-1 break-words">15+</div>
            <div class="text-xs sm:text-sm text-gray-500 break-words leading-tight">{{ $t('portfolio.stats.industries') }}</div>
          </div>
          <div
            v-motion
            :initial="{ opacity: 0, y: 20 }"
            :enter="{ opacity: 1, y: 0, transition: { delay: 400, duration: 500 } }"
            class="text-center min-w-0"
          >
            <div class="text-2xl sm:text-3xl md:text-4xl font-bold bg-gradient-to-r from-cyan-400 to-secondary-400 bg-clip-text text-transparent mb-1 break-words">98%</div>
            <div class="text-xs sm:text-sm text-gray-500 break-words leading-tight">{{ $t('portfolio.stats.successRate') }}</div>
          </div>
        </div>
      </div>

      <!-- Corner decorations -->
      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/30 pointer-events-none" />
      <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/30 pointer-events-none" />
    </section>

    <!-- Portfolio Grid -->
    <section class="py-8 md:py-20 bg-[#0a0a0f] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute top-1/3 right-1/4 w-96 h-96 bg-primary-500/10 rounded-full blur-[150px] animate-blob animation-delay-4000" />
        <div class="absolute bottom-1/4 left-1/3 w-80 h-80 bg-secondary-500/12 rounded-full blur-[130px] animate-blob" />
      </div>

      <div class="container-custom relative z-10">
        <!-- Filter tabs -->
        <div class="flex flex-wrap justify-center gap-3 mb-12">
          <button
            v-for="tab in filterTabs"
            :key="tab.value"
            type="button"
            :class="[
              'px-6 py-3 rounded-xl font-semibold transition-all duration-300 relative overflow-hidden',
              activeFilter === tab.value
                ? 'bg-primary-500 text-white shadow-xl shadow-primary-500/30'
                : 'bg-white/5 text-gray-400 hover:text-white border border-white/10 hover:border-primary-500/50'
            ]"
            @click="setFilter(tab.value)"
          >
            <span v-if="activeFilter === tab.value" class="absolute inset-0 bg-gradient-to-r from-transparent via-white/20 to-transparent animate-shine" style="background-size: 200% 100%;" />
            <span class="relative flex items-center gap-2">
              <Icon :name="tab.icon" class="text-xl" />
              {{ tab.label }}
            </span>
          </button>
        </div>

        <!-- Projects Grid -->
        <TransitionGroup name="project-list" tag="div" class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          <NuxtLink
            v-for="project in filteredProjects"
            :key="project.id"
            :to="localePath(`/portfolio/${project.id}`)"
            class="group block"
          >
            <!-- Project Card -->
            <div class="relative rounded-2xl overflow-hidden bg-gray-900 border border-gray-800 hover:border-primary-500/50 transition-all duration-500 hover:-translate-y-2 hover:shadow-2xl hover:shadow-primary-500/20">
              <!-- Image -->
              <div class="relative h-64 overflow-hidden">
                <NuxtImg
                  :src="getProjectImage(project.category[0] || 'development')"
                  :alt="project.title"
                  width="600"
                  height="400"
                  loading="lazy"
                  class="w-full h-full object-cover transition-transform duration-700 group-hover:scale-110"
                />

                <!-- Overlay -->
                <div class="absolute inset-0 bg-gradient-to-t from-gray-900 via-gray-900/60 to-transparent" />

                <!-- Category badges -->
                <div class="absolute top-4 left-4 flex flex-wrap gap-2">
                  <span
                    v-for="cat in project.category"
                    :key="cat"
                    class="inline-flex items-center gap-2 px-3 py-1.5 rounded-full text-xs font-semibold backdrop-blur-sm transition-all duration-300"
                    :style="{
                      backgroundColor: getCategoryColor(cat) + '25',
                      color: getCategoryColor(cat),
                      borderWidth: '1px',
                      borderColor: getCategoryColor(cat) + '40'
                    }"
                  >
                    <span class="w-1.5 h-1.5 rounded-full animate-pulse" :style="{ backgroundColor: getCategoryColor(cat) }" />
                    {{ getCategoryLabel(cat) }}
                  </span>
                </div>

                <!-- Year badge -->
                <div class="absolute top-4 right-4">
                  <div class="w-12 h-12 rounded-full bg-white/10 backdrop-blur-sm flex items-center justify-center border border-white/20 group-hover:scale-110 group-hover:rotate-12 transition-all duration-300">
                    <span class="text-xs font-bold text-white">{{ project.year }}</span>
                  </div>
                </div>

                <!-- Hover overlay -->
                <div class="absolute inset-0 opacity-0 group-hover:opacity-100 transition-opacity duration-500" :style="{ background: `linear-gradient(135deg, ${getCategoryColor(project.category[0] || 'development')}20 0%, transparent 60%)` }" />
              </div>

              <!-- Content -->
              <div class="p-6">
                <h3 class="text-xl font-bold text-white mb-2 group-hover:text-primary-300 transition-colors">
                  {{ project.title }}
                </h3>
                <p class="text-gray-400 text-sm mb-4 line-clamp-2">
                  {{ project.description }}
                </p>

                <!-- Tech stack -->
                <div class="flex flex-wrap gap-2">
                  <span
                    v-for="tech in project.technologies.slice(0, 5)"
                    :key="tech"
                    class="px-2 py-1 text-xs bg-gray-800 text-gray-400 rounded group-hover:bg-gray-700 group-hover:text-gray-300 transition-colors"
                  >
                    {{ tech }}
                  </span>
                  <span v-if="project.technologies.length > 5" class="px-2 py-1 text-xs bg-gray-800 text-gray-400 rounded">
                    +{{ project.technologies.length - 5 }}
                  </span>
                </div>
              </div>

              <!-- View details button -->
              <div class="absolute bottom-6 right-6 w-12 h-12 rounded-full bg-primary-500 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-all duration-300 transform translate-y-2 group-hover:translate-y-0 shadow-lg shadow-primary-500/50">
                <ArrowRight :size="20" :stroke-width="2" class="text-white" />
              </div>

              <!-- Corner accent -->
              <div class="absolute bottom-0 left-0 w-16 h-16 border-l-2 border-b-2 border-primary-500/50 rounded-bl-2xl opacity-0 group-hover:opacity-100 transition-opacity duration-300" />
            </div>
          </NuxtLink>
        </TransitionGroup>

        <!-- Empty state -->
        <div v-if="filteredProjects.length === 0" class="text-center py-20">
          <FolderOpen :size="64" :stroke-width="2" class="text-gray-600 mx-auto mb-4" />
          <p class="text-gray-400">{{ $t('portfolio.emptyState') }}</p>
        </div>
      </div>

      <!-- Section decorations -->
      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/25 pointer-events-none" />
      <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/25 pointer-events-none" />
    </section>

    <!-- CTA -->
    <SectionsCTA />
  </div>
</template>

<script setup lang="ts">
/**
 * Portfolio page with project showcase and detail modal
 */

import { ArrowRight, FolderOpen } from 'lucide-vue-next'
import { projects as rawProjects } from '~/lib/data'
import type { Project } from '~/types'

const { t } = useI18n()
const localePath = useLocalePath()

// SEO setup with canonical, hreflang, OG, Twitter tags
const { baseUrl: portfolioBaseUrl } = useSeo({
  title: t('seo.portfolio.title'),
  description: t('seo.portfolio.description'),
  keywords: t('seo.portfolio.keywords'),
  type: 'website',
  path: '/portfolio'
})

// Schema.org
const { organizationSchema: portfolioOrgSchema, injectSchema: portfolioInjectSchema } = useSchemaOrg()
portfolioInjectSchema([
  portfolioOrgSchema(),
  {
    '@context': 'https://schema.org',
    '@type': 'CollectionPage',
    '@id': `${portfolioBaseUrl}/portfolio#portfoliopage`,
    url: `${portfolioBaseUrl}/portfolio`,
    name: 'Portfolio - Innovayse Projects',
    description: t('seo.portfolio.description'),
    inLanguage: ['en', 'ru', 'hy'],
    publisher: { '@id': `${portfolioBaseUrl}/#organization` }
  }
])

/** Projects with i18n translations */
const projects = computed(() => rawProjects.map(project => ({
  ...project,
  title: t(`portfolio.items.${project.id}.title`),
  description: t(`portfolio.items.${project.id}.description`)
})))

const filterTabs = computed(() => [
  { label: t('portfolio.filters.all'), value: 'all', icon: 'mdi:view-grid' },
  { label: t('portfolio.filters.development'), value: 'development', icon: 'mdi:code-tags' },
  { label: t('portfolio.filters.ecommerce'), value: 'ecommerce', icon: 'mdi:cart' },
  { label: t('portfolio.filters.saas'), value: 'saas', icon: 'mdi:cloud' },
  { label: t('portfolio.filters.seo'), value: 'seo', icon: 'mdi:chart-line' }
])

// Default filter
const activeFilter = ref('all')

// Support hash-based navigation on page load (e.g., /portfolio#development)
onMounted(() => {
  if (typeof window !== 'undefined') {
    const hash = window.location.hash.slice(1)
    if (hash && filterTabs.value.some(tab => tab.value === hash)) {
      activeFilter.value = hash
    }
  }
})

// Filter switch with hash update
const setFilter = (value: string) => {
  activeFilter.value = value
  // Update URL hash for shareable links
  if (typeof window !== 'undefined') {
    if (value === 'all') {
      window.history.replaceState(history.state, '', window.location.pathname)
    } else {
      window.history.replaceState(history.state, '', `#${value}`)
    }
  }
}

const filteredProjects = computed(() => {
  if (activeFilter.value === 'all') {
    return projects.value
  }
  return projects.value.filter(p => p.category.includes(activeFilter.value as any))
})

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
  const labels: Record<string, string> = {
    development: 'Development',
    ecommerce: 'E-commerce',
    saas: 'SaaS',
    seo: 'SEO'
  }
  return labels[category] || category
}

const getProjectImage = (category: string) => {
  const images: Record<string, string> = {
    development: '/images/categories/development.jpg',
    ecommerce: '/images/categories/ecommerce.jpg',
    saas: '/images/categories/saas.jpg',
    seo: '/images/categories/seo.jpg'
  }
  return images[category] || images.development
}
</script>

<style scoped>
.line-clamp-2 {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

/* Project list transition */
.project-list-move,
.project-list-enter-active,
.project-list-leave-active {
  transition: all 0.5s ease;
}

.project-list-enter-from {
  opacity: 0;
  transform: translateY(30px);
}

.project-list-leave-to {
  opacity: 0;
  transform: scale(0.9);
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
