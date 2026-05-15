<template>
  <div>
    <!-- Hero -->
    <section class="py-10 md:py-24 bg-[#0a0a0f] relative overflow-hidden">
      <!-- Background -->
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-br from-primary-500/10 via-transparent to-secondary-500/10" />
        <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/20 rounded-full blur-[150px] animate-blob" />
        <div class="absolute bottom-0 right-1/4 w-[450px] h-[450px] bg-secondary-500/20 rounded-full blur-[140px] animate-blob animation-delay-2000" />

        <!-- Floating particles -->
        <div class="absolute top-1/4 right-1/3 w-2 h-2 bg-primary-400/70 rounded-full animate-float" style="animation-delay: 0.6s;" />
        <div class="absolute bottom-1/3 left-1/4 w-3 h-3 bg-secondary-400/60 rounded-full animate-float" style="animation-delay: 1.5s;" />

        <!-- Grid pattern -->
        <div class="absolute inset-0 opacity-[0.02]">
          <div style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 70px 70px;" class="w-full h-full" />
        </div>
      </div>

      <div
        v-motion
        :initial="{ opacity: 0, y: 30 }"
        :enter="{ opacity: 1, y: 0, transition: { duration: 600 } }"
        class="container-custom relative z-10 text-center"
      >
        <span class="inline-block px-4 py-1.5 bg-primary-500/20 text-primary-400 text-sm font-medium rounded-full mb-6">
          {{ $t('blog.badge') }}
        </span>
        <h1 class="text-4xl md:text-6xl font-bold text-white mb-6 break-words">
          {{ $t('blog.title') }}
        </h1>
        <p class="text-lg text-gray-400 max-w-2xl mx-auto">
          {{ $t('blog.subtitle') }}
        </p>
      </div>

      <!-- Corner decorations -->
      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/30 pointer-events-none" />
      <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/30 pointer-events-none" />
    </section>

    <!-- SEO Topic Headings -->
    <section class="py-6 bg-[#0c0c11] border-b border-white/5">
      <div class="container-custom">
        <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
          <div
            v-for="topic in blogTopics"
            :key="topic.key"
            class="flex items-center gap-2 p-3 rounded-xl bg-white/3 border border-white/5 hover:border-primary-500/20 transition-colors"
          >
            <Icon :name="topic.icon" class="text-primary-400 text-lg flex-shrink-0" />
            <h2 class="text-xs sm:text-sm font-medium text-gray-300 leading-tight">
              {{ $t(`blog.topics.${topic.key}`) }}
            </h2>
          </div>
        </div>
      </div>
    </section>

    <!-- Blog Content -->
    <section class="py-8 md:py-20 bg-[#0d0d12] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-r from-primary-500/5 via-transparent to-secondary-500/5" />
      </div>

      <div class="container-custom relative z-10">
        <!-- Category Filter -->
        <div
          v-motion
          :initial="{ opacity: 0, y: 20 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 500 } }"
          class="flex flex-wrap justify-center gap-3 mb-12"
        >
          <button
            v-for="category in categories"
            :key="category.id"
            :class="[
              'px-5 py-2.5 rounded-full text-sm font-medium transition-all duration-300',
              selectedCategory === category.id
                ? 'bg-primary-500 text-white shadow-lg shadow-primary-500/30'
                : 'bg-white/5 text-gray-400 hover:bg-white/10 hover:text-white border border-white/10'
            ]"
            @click="selectedCategory = category.id"
          >
            {{ $t(`blog.categories.${category.id}`) }}
          </button>
        </div>

        <!-- Blog Grid -->
        <div class="grid md:grid-cols-2 lg:grid-cols-3 gap-8">
          <NuxtLink
            v-for="(post, index) in filteredPosts"
            :key="post.slug"
            :to="localePath(`/blog/${post.slug}`)"
            v-motion
            :initial="{ opacity: 0, y: 40 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: index * 100, duration: 500 } }"
            class="group relative bg-white/5 rounded-2xl overflow-hidden border border-white/10 hover:border-primary-500/30 transition-all duration-500 hover:-translate-y-2 block cursor-pointer"
          >
            <!-- Image -->
            <div class="relative h-52 overflow-hidden">
              <div
                class="absolute inset-0 bg-gradient-to-br transition-transform duration-700 group-hover:scale-110"
                :style="{ background: `linear-gradient(135deg, ${post.color}30, ${post.color}10)` }"
              />
              <div class="absolute inset-0 flex items-center justify-center">
                <Icon :name="post.icon" class="text-6xl opacity-30" :style="{ color: post.color }" />
              </div>
              <!-- Category badge -->
              <div class="absolute top-4 left-4">
                <span
                  class="px-3 py-1 rounded-full text-xs font-medium"
                  :style="{ backgroundColor: post.color + '20', color: post.color }"
                >
                  {{ $t(`blog.categories.${post.category}`) }}
                </span>
              </div>
            </div>

            <!-- Content -->
            <div class="p-6">
              <!-- Meta -->
              <div class="flex items-center gap-4 text-sm text-gray-500 mb-3">
                <span class="flex items-center gap-1">
                  <Calendar :size="16" :stroke-width="2" />
                  {{ post.date }}
                </span>
                <span class="flex items-center gap-1">
                  <Clock :size="16" :stroke-width="2" />
                  {{ post.readTime }} {{ $t('blog.minRead') }}
                </span>
              </div>

              <!-- Title -->
              <h3 class="text-xl font-bold text-white mb-3 group-hover:text-primary-400 transition-colors line-clamp-2">
                {{ $t(`blog.posts.${post.slug}.title`) }}
              </h3>

              <!-- Excerpt -->
              <p class="text-gray-400 text-sm mb-4 line-clamp-3">
                {{ $t(`blog.posts.${post.slug}.excerpt`) }}
              </p>

              <!-- Author -->
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-3">
                  <div
                    class="w-8 h-8 rounded-full flex items-center justify-center text-sm font-bold"
                    :style="{ backgroundColor: post.color + '20', color: post.color }"
                  >
                    {{ post.author.charAt(0) }}
                  </div>
                  <span class="text-sm text-gray-400">{{ post.author }}</span>
                </div>
                <span class="flex items-center gap-1 text-primary-400 hover:text-primary-300 text-sm font-medium transition-colors">
                  {{ $t('blog.readMore') }}
                  <ArrowRight :size="16" :stroke-width="2" class="group-hover:translate-x-1 transition-transform" />
                </span>
              </div>
            </div>

            <!-- Hover glow -->
            <div
              class="absolute inset-0 opacity-0 group-hover:opacity-100 transition-opacity duration-500 pointer-events-none"
              :style="{ boxShadow: `inset 0 0 60px ${post.color}15` }"
            />
          </NuxtLink>
        </div>

        <!-- Empty state -->
        <div
          v-if="filteredPosts.length === 0"
          class="text-center py-16"
        >
          <FileText :size="64" :stroke-width="2" class="text-gray-600 mb-4 mx-auto" />
          <p class="text-gray-400">{{ $t('blog.noPosts') }}</p>
        </div>

        <!-- Newsletter CTA -->
        <div
          v-motion
          :initial="{ opacity: 0, y: 30 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 200, duration: 500 } }"
          class="mt-20 p-8 md:p-12 rounded-2xl bg-gradient-to-br from-primary-500/10 to-secondary-500/10 border border-white/10 text-center"
        >
          <h3 class="text-2xl md:text-3xl font-bold text-white mb-4">{{ $t('blog.newsletter.title') }}</h3>
          <p class="text-gray-400 mb-8 max-w-xl mx-auto">{{ $t('blog.newsletter.description') }}</p>
          <form class="flex flex-col sm:flex-row gap-4 max-w-md mx-auto" @submit.prevent="subscribe">
            <input
              v-model="email"
              type="email"
              :placeholder="$t('blog.newsletter.placeholder')"
              class="flex-1 px-5 py-3 rounded-xl bg-white/5 border border-white/10 text-white placeholder-gray-500 focus:outline-none focus:border-primary-500/50 transition-colors"
              required
            >
            <button
              type="submit"
              class="px-6 py-3 rounded-xl bg-primary-500 text-white font-semibold hover:bg-primary-600 transition-colors"
            >
              {{ $t('blog.newsletter.button') }}
            </button>
          </form>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
/**
 * Blog index page with posts and category filtering
 */

import { Calendar, Clock, ArrowRight, FileText } from 'lucide-vue-next'

const localePath = useLocalePath()
const selectedCategory = ref('all')
const email = ref('')

const blogTopics = [
  { key: 'digitalMarketing', icon: 'mdi:trending-up' },
  { key: 'seoStrategy', icon: 'mdi:magnify' },
  { key: 'ppcSuccess', icon: 'mdi:target' },
  { key: 'techInnovation', icon: 'mdi:lightning-bolt' },
  { key: 'careerGrowth', icon: 'mdi:account-group' }
]

const categories = [
  { id: 'all' },
  { id: 'development' },
  { id: 'seo' },
  { id: 'marketing' },
  { id: 'design' },
  { id: 'business' }
]

const posts = [
  {
    slug: 'modern-web-development-2024',
    category: 'development',
    date: '2024-01-15',
    readTime: 8,
    author: 'Alex Chen',
    icon: 'mdi:code-tags',
    color: '#0ea5e9'
  },
  {
    slug: 'seo-strategies-ai-era',
    category: 'seo',
    date: '2024-01-12',
    readTime: 6,
    author: 'Sarah Miller',
    icon: 'mdi:chart-line',
    color: '#8b5cf6'
  },
  {
    slug: 'ppc-optimization-tips',
    category: 'marketing',
    date: '2024-01-10',
    readTime: 5,
    author: 'Mike Johnson',
    icon: 'mdi:target',
    color: '#f59e0b'
  },
  {
    slug: 'ui-ux-trends-2024',
    category: 'design',
    date: '2024-01-08',
    readTime: 7,
    author: 'Emma Wilson',
    icon: 'mdi:palette',
    color: '#ec4899'
  },
  {
    slug: 'scaling-saas-business',
    category: 'business',
    date: '2024-01-05',
    readTime: 10,
    author: 'David Lee',
    icon: 'mdi:rocket-launch',
    color: '#10b981'
  },
  {
    slug: 'typescript-best-practices',
    category: 'development',
    date: '2024-01-03',
    readTime: 9,
    author: 'Alex Chen',
    icon: 'mdi:language-typescript',
    color: '#3b82f6'
  }
]

const filteredPosts = computed(() => {
  if (selectedCategory.value === 'all') return posts
  return posts.filter(post => post.category === selectedCategory.value)
})

const subscribe = () => {
  // Handle newsletter subscription
  console.log('Subscribe:', email.value)
  email.value = ''
}

const { t } = useI18n()

// SEO setup with canonical, hreflang, OG, Twitter tags
const { baseUrl: blogBaseUrl } = useSeo({
  title: t('seo.blog.title'),
  description: t('seo.blog.description'),
  keywords: t('seo.blog.keywords'),
  type: 'website',
  path: '/blog'
})

// Schema.org
const { injectSchema: blogInjectSchema } = useSchemaOrg()
blogInjectSchema([
  {
    '@context': 'https://schema.org',
    '@type': 'Blog',
    '@id': `${blogBaseUrl}/blog#blog`,
    url: `${blogBaseUrl}/blog`,
    name: 'Innovayse Blog',
    description: t('seo.blog.description'),
    inLanguage: ['en', 'ru', 'hy'],
    publisher: { '@id': `${blogBaseUrl}/#organization` }
  }
])
</script>

<style scoped>
.line-clamp-2 {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.line-clamp-3 {
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
</style>
