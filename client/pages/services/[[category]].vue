<template>
  <div>
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

      <div class="container-custom relative z-10 text-center">
        <span class="inline-block px-4 py-1.5 bg-primary-500/20 text-primary-400 text-sm font-medium rounded-full mb-6">
          {{ $t('services.hero.badge') }}
        </span>
        <h1 class="text-4xl md:text-6xl lg:text-7xl font-bold text-white mb-6 leading-tight">
          {{ $t('services.hero.title', { highlight: '' }) }}<span class="bg-gradient-to-r from-primary-400 to-secondary-400 bg-clip-text text-transparent animate-gradient bg-300">{{ $t('services.hero.highlight') }}</span>
        </h1>
        <p class="text-lg md:text-xl text-gray-400 max-w-3xl mx-auto mb-12">
          {{ $t('services.hero.description') }}
        </p>

        <!-- Quick stats wrapped in a glass container for more weight -->
        <div
          v-motion
          :initial="{ opacity: 0, y: 20 }"
          :enter="{ opacity: 1, y: 0, transition: { delay: 200, duration: 600 } }"
          class="max-w-2xl mx-auto p-6 md:p-8 rounded-3xl glass-dark border border-white/5 shadow-2xl"
        >
          <div class="grid grid-cols-3 gap-4 md:gap-8">
            <div class="text-center">
              <div class="text-2xl sm:text-3xl md:text-4xl font-extrabold bg-gradient-to-r from-primary-400 to-cyan-400 bg-clip-text text-transparent mb-2">6+</div>
              <div class="text-[10px] md:text-xs uppercase tracking-widest font-bold text-gray-500">{{ $t('services.hero.stats.services') }}</div>
            </div>
            <div class="text-center border-x border-white/5">
              <div class="text-2xl sm:text-3xl md:text-4xl font-extrabold bg-gradient-to-r from-secondary-400 to-primary-400 bg-clip-text text-transparent mb-2">150+</div>
              <div class="text-[10px] md:text-xs uppercase tracking-widest font-bold text-gray-500">{{ $t('services.hero.stats.projects') }}</div>
            </div>
            <div class="text-center">
              <div class="text-2xl sm:text-3xl md:text-4xl font-extrabold bg-gradient-to-r from-cyan-400 to-secondary-400 bg-clip-text text-transparent mb-2">8</div>
              <div class="text-[10px] md:text-xs uppercase tracking-widest font-bold text-gray-500">{{ $t('services.hero.stats.years') }}</div>
            </div>
          </div>
        </div>
      </div>

      <!-- Corner decorations -->
      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/30 pointer-events-none" />
      <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/30 pointer-events-none" />
    </section>

    <!-- Services Categories -->
    <section class="py-8 md:py-20 bg-[#0a0a0f] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-bl from-primary-500/5 via-transparent to-secondary-500/5" />
        <div class="absolute top-1/4 left-0 w-[500px] h-[500px] bg-primary-500/10 rounded-full blur-[150px] animate-blob" />
        <div class="absolute bottom-1/4 right-0 w-[400px] h-[400px] bg-secondary-500/10 rounded-full blur-[130px] animate-blob animation-delay-2000" />
        <div class="absolute top-2/3 left-1/2 w-[300px] h-[300px] bg-cyan-500/8 rounded-full blur-[120px] animate-blob animation-delay-4000" />
        <!-- Grid pattern -->
        <div class="absolute inset-0 opacity-[0.015]">
          <div class="w-full h-full" style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 60px 60px;" />
        </div>
        <!-- Floating particles -->
        <div class="absolute top-1/4 right-1/4 w-2 h-2 bg-primary-400/50 rounded-full animate-float" style="animation-delay: 0.5s;" />
        <div class="absolute bottom-1/3 left-1/5 w-1.5 h-1.5 bg-secondary-400/50 rounded-full animate-float" style="animation-delay: 1.5s;" />
        <div class="absolute top-2/3 right-1/3 w-2 h-2 bg-cyan-400/40 rounded-full animate-float" style="animation-delay: 2.5s;" />
      </div>

      <!-- Category anchors for hash navigation with better mobile offset -->
      <div id="development" class="absolute -top-32" />
      <div id="seo" class="absolute -top-32" />
      <div id="ppc" class="absolute -top-32" />

      <div class="container-custom relative z-10">
        <!-- Category tabs -->
        <div class="flex flex-wrap justify-center gap-3 mb-16">
          <button
            v-for="category in serviceCategories"
            :key="category.value"
            type="button"
            :class="[
              'px-6 py-3 rounded-xl font-semibold transition-all duration-300 relative overflow-hidden',
              activeCategory === category.value
                ? 'bg-primary-500 text-white shadow-lg shadow-primary-500/30'
                : 'bg-white/5 text-gray-400 hover:bg-white/10 hover:text-white border border-white/10'
            ]"
            @click="setActiveCategory(category.value)"
          >
            <span class="relative flex items-center gap-2">
              <Icon :name="category.icon" class="text-xl" />
              {{ category.label }}
            </span>
          </button>
        </div>

        <!-- Service Grid Container -->
        <div class="max-w-7xl mx-auto">
          <TransitionGroup
            name="service-fade"
            tag="div"
            class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 md:gap-8"
          >
            <div
              v-for="(service, index) in filteredServices"
              :key="service.id"
              v-motion
              :initial="{ opacity: 0, y: 30 }"
              :visibleOnce="{ opacity: 1, y: 0, transition: { delay: index * 100, duration: 600 } }"
              class="group/service relative flex flex-col h-full rounded-[2.5rem] overflow-hidden border border-white/10 glass-dark transition-all duration-500 hover:border-primary-500/40 hover:-translate-y-2 hover:shadow-2xl hover:shadow-primary-500/10"
            >
              <!-- Accent color background glow -->
              <div
                class="absolute -top-24 -right-24 w-64 h-64 opacity-20 group-hover:opacity-30 transition-opacity duration-700 blur-[80px]"
                :style="{ backgroundColor: getServiceColor(service.id) }"
              />

              <!-- Top Section: Icon & Headline -->
              <div class="relative p-10 pb-0">
                <div
                  class="w-16 h-16 rounded-2xl flex items-center justify-center mb-8 border transition-all duration-500 group-hover:scale-110 group-hover:rotate-3 shadow-2xl"
                  :style="{
                    backgroundColor: getServiceColor(service.id) + '20',
                    borderColor: getServiceColor(service.id) + '40',
                    color: getServiceColor(service.id)
                  }"
                >
                  <Icon :name="`mdi:${service.icon}`" class="text-3xl" />
                </div>

                <h3 class="text-2xl font-bold text-white mb-4 group-hover:text-primary-300 transition-colors">
                  {{ service.title }}
                </h3>
                <p class="text-gray-400 text-sm leading-relaxed mb-8 line-clamp-3 group-hover:text-gray-300 transition-colors">
                  {{ service.description }}
                </p>
              </div>

              <!-- Middle Section: Features List -->
              <div class="relative px-10 mb-8 flex-1">
                <div class="flex flex-wrap gap-2">
                  <span
                    v-for="feature in service.features.slice(0, 4)"
                    :key="feature"
                    class="px-3 py-1 rounded-full bg-white/5 border border-white/10 text-[10px] font-bold text-gray-400 uppercase tracking-wider group-hover:border-primary-500/20 group-hover:text-gray-300 transition-all"
                  >
                    {{ feature }}
                  </span>
                </div>
              </div>

              <!-- Bottom Section: Branches & Link -->
              <div class="relative p-10 pt-0 mt-auto">
                <div v-if="service.branches && service.branches.length > 0" class="mb-10">
                  <div class="text-[10px] font-black text-gray-500 uppercase tracking-[0.2em] mb-4">
                    {{ $t('services.branches.title') }}
                  </div>
                  <div class="grid grid-cols-1 gap-3">
                    <NuxtLink
                      v-for="branch in service.branches.slice(0, 3)"
                      :key="branch.name"
                      :to="localePath(`/services/${service.id}/${branch.key}`)"
                      class="flex items-center justify-between p-4 rounded-2xl bg-white/[0.03] border border-white/5 hover:bg-white/[0.08] hover:border-primary-500/30 transition-all group/branch"
                    >
                      <div class="flex items-center gap-3 min-w-0">
                        <Icon :name="branch.icon || 'mdi:chevron-right'" class="text-primary-400 text-xl flex-shrink-0" />
                        <span class="text-sm font-bold text-gray-300 truncate group-hover/branch:text-white transition-colors">{{ branch.name }}</span>
                      </div>
                      <ArrowUpRight :size="16" class="text-primary-400 opacity-0 group-hover/branch:opacity-100 transition-all" />
                    </NuxtLink>
                    <NuxtLink
                      v-if="service.branches.length > 3"
                      :to="localePath(`/services/${service.id}/${service.branches[3].key}`)"
                      class="text-center py-2 text-[10px] font-bold text-primary-400 hover:text-primary-300 transition-colors"
                    >
                      +{{ service.branches.length - 3 }} {{ $t('services.more') }}
                    </NuxtLink>
                  </div>
                </div>

                <!-- Main CTA for the card -->
                <NuxtLink
                  :to="localePath(`/services/${service.id}/${service.branches[0].key}`)"
                  class="flex items-center justify-center w-full py-5 rounded-2xl bg-primary-500/10 border border-primary-500/20 text-primary-400 font-bold text-sm group-hover:bg-primary-500 group-hover:text-white transition-all duration-300 shadow-lg shadow-primary-500/10 hover:shadow-primary-500/25"
                >
                  {{ $t('services.learnMore') }}
                  <ArrowUpRight :size="18" class="ml-2 group-hover:translate-x-1 group-hover:-translate-y-1 transition-transform" />
                </NuxtLink>
              </div>

              <!-- Decorative corner accent -->
              <div
                class="absolute bottom-0 right-0 w-24 h-24 opacity-0 group-hover:opacity-100 transition-opacity duration-500 pointer-events-none"
              >
                <div class="absolute bottom-6 right-6 w-12 h-12 rounded-full blur-2xl" :style="{ backgroundColor: getServiceColor(service.id) }" />
              </div>
            </div>
          </TransitionGroup>
        </div>
      </div>
    </section>

    <!-- Process Section -->
    <SectionsProcess />

    <!-- CTA Section -->
    <section class="py-8 md:py-20 bg-[#0d0d12] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-r from-primary-500/10 via-transparent to-secondary-500/10" />
        <div class="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[400px] bg-primary-500/15 rounded-full blur-[150px]" />
      </div>

      <div class="container-custom relative z-10">
        <div class="max-w-3xl mx-auto">
          <!-- Contact form card -->
          <div
            v-motion
            :initial="{ opacity: 0, y: 40 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 600 } }"
            class="relative p-8 md:p-12 rounded-3xl bg-gradient-to-br from-white/5 to-white/[0.02] border border-white/10 backdrop-blur-sm"
          >
            <!-- Decorative elements -->
            <div class="absolute top-0 right-0 w-48 h-48 bg-secondary-500/10 rounded-full blur-3xl" />
            <div class="absolute bottom-0 left-0 w-32 h-32 bg-primary-500/10 rounded-full blur-2xl" />

            <div class="relative">
              <div class="text-center mb-8">
                <h2 class="text-3xl md:text-4xl font-bold text-white mb-4">
                  {{ $t('services.cta.title', { highlight: '' }) }} <span class="text-primary-400">{{ $t('services.cta.highlight') }}</span>?
                </h2>
                <p class="text-gray-400">
                  {{ $t('services.cta.description') }}
                </p>
              </div>

              <form class="space-y-4" @submit.prevent="handleSubmit">
                <div class="grid md:grid-cols-2 gap-4">
                  <UiInput
                    v-model="formData.name"
                    :label="$t('services.cta.form.name.label')"
                    :placeholder="$t('services.cta.form.name.placeholder')"
                    required
                  />
                  <UiInput
                    v-model="formData.email"
                    type="email"
                    :label="$t('services.cta.form.email.label')"
                    :placeholder="$t('services.cta.form.email.placeholder')"
                    required
                  />
                </div>

                <UiDropdown
                  v-model="formData.service"
                  :label="$t('services.cta.form.service.label')"
                  :placeholder="$t('services.cta.form.service.placeholder')"
                  :options="serviceOptions"
                  icon="mdi:view-grid"
                  required
                />

                <UiTextarea
                  v-model="formData.message"
                  :label="$t('services.cta.form.message.label')"
                  :placeholder="$t('services.cta.form.message.placeholder')"
                  :rows="5"
                  required
                />

                <UiButton
                  type="submit"
                  variant="primary"
                  size="lg"
                  full-width
                  :loading="isSubmitting"
                  class="hover:shadow-lg hover:shadow-primary-500/50"
                >
                  <Send v-if="!isSubmitting" :size="16" :stroke-width="2" class="mr-2" />
                  {{ $t('services.cta.form.submit') }}
                </UiButton>

                <!-- Success message -->
                <Transition name="fade">
                  <div v-if="showSuccess" class="p-4 bg-green-500/10 border border-green-500/20 rounded-xl">
                    <div class="flex items-center justify-center gap-2">
                      <CheckCircle :size="20" :stroke-width="2" class="text-green-400" />
                      <p class="text-sm text-green-400">{{ $t('services.cta.form.success') }}</p>
                    </div>
                  </div>
                </Transition>
              </form>
            </div>

            <!-- Corner accents with rounded borders -->
            <div class="absolute top-0 left-0 w-16 h-16 border-l-2 border-t-2 border-primary-500/40 rounded-tl-2xl" />
            <div class="absolute bottom-0 right-0 w-16 h-16 border-r-2 border-b-2 border-secondary-500/40 rounded-br-2xl" />
          </div>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
/**
 * Services page - detailed service offerings
 */

import { $fetch } from 'ofetch'
import { services as rawServices } from '~/lib/data'
import { CheckCircle, Send, ArrowUpRight } from 'lucide-vue-next'

const { t, tm } = useI18n()
const localePath = useLocalePath()

// Map raw services with i18n translations
const services = computed(() => rawServices.map(service => ({
  ...service,
  title: t(`services.items.${service.id}.title`),
  description: t(`services.items.${service.id}.description`),
  features: tm(`services.items.${service.id}.features`) as string[],
  branches: service.branchKeys.map(key => ({
    key: key,
    name: t(`services.items.${service.id}.branches.${key}.name`),
    objective: t(`services.items.${service.id}.branches.${key}.objective`),
    icon: service.branchIcons[key]
  }))
})))

// Service categories (computed for i18n)
const serviceCategories = computed(() => [
  { label: t('services.categories.all'), value: 'all', icon: 'mdi:view-grid' },
  { label: t('services.categories.development'), value: 'development', icon: 'mdi:code-tags' },
  { label: t('services.categories.seo'), value: 'seo', icon: 'mdi:magnify' },
  { label: t('services.categories.ppc'), value: 'ppc', icon: 'mdi:target' }
])

const route = useRoute()
const router = useRouter()

// Derive initial category from URL (SSR-friendly)
const getInitialCategory = () => {
  const categories = ['development', 'seo', 'ppc']
  const categoryParam = route.params.category as string
  const categoryQuery = route.query.category as string
  const categoryStr = categoryParam || categoryQuery

  if (categoryStr && categories.includes(categoryStr)) {
    return categoryStr
  }
  return 'all'
}

const activeCategory = ref(getInitialCategory())

// Dynamic SEO based on category (plain strings to avoid unhead/dispose issues on remount)
const currentCat = activeCategory.value
const seoTitle = currentCat !== 'all'
  ? t(`seo.services.categories.${currentCat}.title`)
  : t('seo.services.title')

const seoDescription = currentCat !== 'all'
  ? t(`seo.services.categories.${currentCat}.description`)
  : t('seo.services.description')

const { baseUrl: servicesBaseUrl } = useSeo({
  title: seoTitle,
  description: seoDescription,
  keywords: t('seo.services.keywords'),
  type: 'website'
})

// Schema.org
const { organizationSchema: servicesOrgSchema, injectSchema: servicesInjectSchema } = useSchemaOrg()
servicesInjectSchema([
  servicesOrgSchema(),
  {
    '@context': 'https://schema.org',
    '@type': 'CollectionPage',
    '@id': `${servicesBaseUrl}/services#servicespage`,
    url: `${servicesBaseUrl}/services`,
    name: 'Digital Agency Services - Innovayse',
    description: t('seo.services.description'),
    inLanguage: ['en', 'ru', 'hy'],
    publisher: { '@id': `${servicesBaseUrl}/#organization` }
  }
])

// Function to set active category and update URL
const setActiveCategory = (category: string) => {
  activeCategory.value = category
  // Update URL path instead of query/hash
  if (category === 'all') {
    router.replace(localePath('/services'))
  } else {
    router.replace(localePath(`/services/${category}`))
  }
}

// Handle non-standard redirects and hashes on mount
onMounted(() => {
  const categoryParam = route.params.category as string
  const categoryQuery = route.query.category as string
  const categoryStr = categoryParam || categoryQuery

  // Dedicated check for service names (legacy paths like /services/web-development)
  if (categoryStr && !['development', 'seo', 'ppc', 'all'].includes(categoryStr)) {
    const service = services.value.find(s => s.id === categoryStr)
    if (service) {
      setActiveCategory(service.category)
      nextTick(() => {
        const element = document.getElementById(categoryStr)
        if (element) {
          element.scrollIntoView({ behavior: 'smooth', block: 'center' })
        }
      })
    }
  }

  // Handle legacy hash navigation
  const hash = route.hash.replace('#', '')
  if (hash) {
    const categories = ['development', 'seo', 'ppc']
    if (categories.includes(hash)) {
      setActiveCategory(hash)
    }
  }
})

const filteredServices = computed(() => {
  if (activeCategory.value === 'all') {
    return services.value
  }
  return services.value.filter(s => s.category === activeCategory.value)
})

// Contact form
const formData = ref({
  name: '',
  email: '',
  service: '',
  message: ''
})

// Service options (computed for i18n)
const serviceOptions = computed(() => [
  { value: 'web-development', label: t('services.types.web-development') },
  { value: 'mobile-development', label: t('services.types.mobile-development') },
  { value: 'technical-seo', label: t('services.types.technical-seo') },
  { value: 'content-seo', label: t('services.types.content-seo') },
  { value: 'google-ads', label: t('services.types.google-ads') },
  { value: 'yandex-direct', label: t('services.types.yandex-direct') },
  { value: 'other', label: t('services.types.other') }
])

const isSubmitting = ref(false)
const showSuccess = ref(false)

const handleSubmit = async () => {
  isSubmitting.value = true

  try {
    await $fetch('/api/contact', {
      method: 'POST',
      body: {
        name: formData.value.name,
        email: formData.value.email,
        service: formData.value.service,
        message: formData.value.message,
        timestamp: new Date().toLocaleString()
      }
    })

    showSuccess.value = true
    formData.value = { name: '', email: '', service: '', message: '' }

    setTimeout(() => {
      showSuccess.value = false
    }, 5000)
  } catch (error) {
    console.error('Failed to send message:', error)
    alert(t('services.cta.form.error'))
  } finally {
    isSubmitting.value = false
  }
}

// Slugify function for URLs
const slugify = (text: string) => {
  return text
    .toLowerCase()
    .replace(/[^\w\s-]/g, '')
    .replace(/\s+/g, '-')
    .replace(/-+/g, '-')
    .trim()
}

/** Get unique color identity for each service */
const getServiceColor = (id: string) => {
  const colors: Record<string, string> = {
    'web-development': '#0ea5e9',
    'mobile-development': '#7c3aed',
    'technical-seo': '#10b981',
    'content-seo': '#f59e0b',
    'google-ads': '#ef4444',
    'yandex-direct': '#f97316'
  }
  return colors[id] || '#0ea5e9'
}
</script>

<style scoped>
.service-fade-enter-active,
.service-fade-leave-active {
  transition: all 0.3s ease;
}

.service-fade-enter-from {
  opacity: 0;
  transform: translateY(20px);
}

.service-fade-leave-to {
  opacity: 0;
  transform: translateY(-20px);
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.3s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
