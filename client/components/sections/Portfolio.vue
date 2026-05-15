<template>
  <section class="py-24 bg-[#0a0a0f] relative overflow-hidden">
    <!-- Background -->
    <div class="absolute inset-0 pointer-events-none">
      <!-- Gradient overlay -->
      <div class="absolute inset-0 bg-gradient-to-br from-primary-500/5 via-transparent to-secondary-500/5" />

      <!-- Glowing orbs -->
      <div class="absolute top-20 left-10 w-[500px] h-[500px] bg-primary-500/10 rounded-full blur-[200px]" />
      <div class="absolute bottom-20 right-10 w-[400px] h-[400px] bg-secondary-500/10 rounded-full blur-[180px]" />

      <!-- Grid pattern -->
      <div class="absolute inset-0 opacity-[0.02]" style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 50px 50px;" />

      <!-- Floating particles -->
      <div class="absolute top-1/4 left-1/3 w-2 h-2 bg-primary-400/60 rounded-full animate-float" style="animation-delay: 0.4s;" />
      <div class="absolute bottom-1/3 right-1/4 w-3 h-3 bg-secondary-400/50 rounded-full animate-float" style="animation-delay: 1.6s;" />
      <div class="absolute top-2/3 left-1/4 w-2 h-2 bg-cyan-400/60 rounded-full animate-float" style="animation-delay: 2.2s;" />
    </div>

    <div class="container-custom relative z-10">
      <!-- Header -->
      <div
        v-motion
        :initial="{ opacity: 0, y: 30 }"
        :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 600 } }"
        class="text-center mb-12"
      >
        <span class="inline-block px-4 py-2 bg-primary-500/10 text-primary-400 text-sm font-medium rounded-full mb-4">
          {{ $t('portfolio.badge') }}
        </span>
        <h2 class="text-4xl md:text-5xl font-bold text-white mb-4">
          {{ $t('portfolio.heading') }} <span class="bg-gradient-to-r from-primary-400 to-secondary-400 bg-clip-text text-transparent">{{ $t('portfolio.headingHighlight') }}</span>
        </h2>
        <p class="text-gray-400 max-w-2xl mx-auto">
          {{ $t('portfolio.description') }}
        </p>
      </div>

      <!-- Filter tabs -->
      <div class="flex flex-wrap justify-center gap-3 mb-12">
        <button
          v-for="tab in filterTabs"
          :key="tab.value"
          type="button"
          :class="[
            'px-5 py-2.5 rounded-full font-medium transition-all duration-300 relative overflow-hidden',
            activeFilter === tab.value
              ? 'bg-primary-500 text-white shadow-lg shadow-primary-500/30'
              : 'bg-gray-800/50 text-gray-400 hover:bg-gray-800 hover:text-white border border-gray-700 hover:border-primary-500/50'
          ]"
          @click="activeFilter = tab.value"
        >
          <!-- Active shine effect -->
          <div v-if="activeFilter === tab.value" class="absolute inset-0 bg-gradient-to-r from-transparent via-white/20 to-transparent animate-shine" style="background-size: 200% 100%;" />
          <span class="relative">{{ tab.label }}</span>
        </button>
      </div>

      <!-- Projects grid -->
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        <div
          v-for="(project, index) in filteredProjects"
          :key="project.id"
          v-motion
          :initial="{ opacity: 0, scale: 0.9 }"
          :visibleOnce="{ opacity: 1, scale: 1, transition: { delay: (index % 3) * 100, duration: 400 } }"
          class="group cursor-pointer"
          @click="openProject(project)"
        >
          <!-- Card -->
          <div class="relative rounded-2xl overflow-hidden bg-gray-900 border border-gray-800 hover:border-gray-700 transition-all duration-500 hover:-translate-y-2 hover:shadow-2xl hover:shadow-primary-500/20">
            <!-- Image/Cover -->
            <div class="relative h-56 overflow-hidden">
              <NuxtImg
                :src="getProjectImage(project.category[0] || 'development')"
                :alt="project.title"
                width="600"
                height="400"
                format="webp"
                quality="80"
                sizes="xs:100vw sm:100vw md:50vw lg:33vw"
                loading="lazy"
                class="w-full h-full object-cover transition-transform duration-700 group-hover:scale-110"
              />
              <!-- Overlay -->
              <div class="absolute inset-0 bg-gradient-to-t from-gray-900 via-gray-900/50 to-transparent" />

              <!-- Animated gradient overlay on hover -->
              <div
                class="absolute inset-0 opacity-0 group-hover:opacity-30 transition-opacity duration-500"
                :style="{ background: `linear-gradient(135deg, ${getCategoryColor(project.category[0] || 'development')}30 0%, transparent 60%)` }"
              />

              <!-- Category badges -->
              <div class="absolute top-4 left-4 flex flex-wrap gap-2">
                <span
                  v-for="cat in project.category"
                  :key="cat"
                  class="inline-flex items-center gap-2 px-3 py-1.5 rounded-full text-xs font-medium backdrop-blur-sm transition-all duration-300 group-hover:scale-105"
                  :style="{ backgroundColor: getCategoryColor(cat) + '20', color: getCategoryColor(cat) }"
                >
                  <span class="w-1.5 h-1.5 rounded-full animate-pulse" :style="{ backgroundColor: getCategoryColor(cat) }" />
                  {{ getCategoryLabel(cat) }}
                </span>
              </div>

              <!-- Icon -->
              <div class="absolute top-4 right-4">
                <div class="w-10 h-10 rounded-full bg-white/10 backdrop-blur-sm flex items-center justify-center group-hover:scale-110 group-hover:rotate-12 transition-all duration-300">
                  <Icon :name="getCategoryIcon(project.category[0] || 'development')" class="text-white/70" />
                </div>
              </div>
            </div>

            <!-- Content -->
            <div class="relative p-6">
              <h3 class="text-xl font-bold text-white mb-2 group-hover:text-primary-400 transition-colors duration-300">
                {{ project.title }}
              </h3>
              <p class="text-gray-400 text-sm mb-4 line-clamp-2">
                {{ project.description }}
              </p>

              <!-- Tech stack -->
              <div class="flex flex-wrap gap-2">
                <span
                  v-for="tech in project.technologies.slice(0, 3)"
                  :key="tech"
                  class="px-2 py-1 text-xs bg-gray-800 text-gray-400 rounded group-hover:bg-gray-700 group-hover:text-gray-300 transition-colors"
                >
                  {{ tech }}
                </span>
                <span v-if="project.technologies.length > 3" class="px-2 py-1 text-xs bg-gray-800 text-gray-400 rounded">
                  +{{ project.technologies.length - 3 }}
                </span>
              </div>
            </div>

            <!-- Hover arrow -->
            <div class="absolute bottom-6 right-6 w-10 h-10 rounded-full bg-primary-500 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-all duration-300 transform translate-y-2 group-hover:translate-y-0 shadow-lg shadow-primary-500/50">
              <ArrowRight :size="20" :stroke-width="2" class="text-white" />
            </div>

            <!-- Corner accent with rounded border -->
            <div class="absolute bottom-0 left-0 w-12 h-12 border-l-2 border-b-2 border-primary-500/50 rounded-bl-2xl opacity-0 group-hover:opacity-100 transition-opacity duration-300" />
          </div>
        </div>
      </div>

      <!-- View all -->
      <div class="text-center mt-8 sm:mt-10 md:mt-12">
        <UiButton
          variant="outline"
          size="md"
          class="border-gray-700 hover:border-primary-500 hover:shadow-lg hover:shadow-primary-500/20 transition-all duration-300 !px-4 !py-2 sm:!px-5 sm:!py-2.5 md:!px-6 md:!py-3 lg:!px-6 lg:!py-3 xl:!px-6 xl:!py-3 !text-sm sm:!text-base md:!text-base lg:!text-lg xl:!text-lg"
          @click="navigateTo(localePath('/portfolio'))"
        >
          <span>{{ $t('portfolio.button') }}</span>
          <ArrowRight :size="20" :stroke-width="2" class="ml-1.5 sm:ml-2" />
        </UiButton>
      </div>
    </div>

    <!-- Section decorations -->
    <div class="absolute top-0 right-0 w-40 h-40 border-r-2 border-t-2 border-secondary-500/20 pointer-events-none" />
    <div class="absolute bottom-0 left-0 w-40 h-40 border-l-2 border-b-2 border-primary-500/20 pointer-events-none" />
  </section>
</template>

<script setup lang="ts">
import { ArrowRight } from 'lucide-vue-next'
import { projects as rawProjects } from '~/lib/data'
import type { Project } from '~/types'

const { t } = useI18n()
const localePath = useLocalePath()

/** Map projects with i18n translations */
const projects = computed(() => rawProjects.map(project => ({
  ...project,
  title: t(`portfolio.items.${project.id}.title`),
  description: t(`portfolio.items.${project.id}.description`)
})))

const filterTabs = computed(() => [
  { label: t('portfolio.filters.all'), value: 'all' },
  { label: t('portfolio.filters.development'), value: 'development' },
  { label: t('portfolio.filters.ecommerce'), value: 'ecommerce' },
  { label: t('portfolio.filters.saas'), value: 'saas' }
])

const activeFilter = ref('all')

const filteredProjects = computed(() => {
  let filtered = projects.value
  if (activeFilter.value !== 'all') {
    filtered = projects.value.filter(p => p.category.includes(activeFilter.value as any))
  }
  return filtered.slice(0, 6)
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
    development: t('portfolio.filters.development'),
    ecommerce: t('portfolio.filters.ecommerce'),
    saas: t('portfolio.filters.saas'),
    seo: t('portfolio.filters.seo')
  }
  return labels[category] || category
}

const getCategoryIcon = (category: string) => {
  const icons: Record<string, string> = {
    development: 'mdi:code-tags',
    ecommerce: 'mdi:cart',
    saas: 'mdi:cloud',
    seo: 'mdi:chart-line'
  }
  return icons[category] || 'mdi:code-tags'
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

const openProject = (project: Project) => {
  navigateTo(localePath(`/portfolio/${project.id}`))
}
</script>

<style scoped>
.line-clamp-2 {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
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
