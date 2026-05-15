<template>
  <div>
    <!-- Hero -->
    <section class="py-10 md:py-24 bg-[#0a0a0f] relative overflow-hidden">
      <!-- Background -->
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-br from-primary-500/10 via-transparent to-secondary-500/10" />
        <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/20 rounded-full blur-[150px] animate-blob" />
        <div class="absolute bottom-0 right-1/4 w-[450px] h-[450px] bg-secondary-500/20 rounded-full blur-[140px] animate-blob animation-delay-2000" />

        <!-- Grid pattern -->
        <div class="absolute inset-0 opacity-[0.02]">
          <div style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 70px 70px;" class="w-full h-full" />
        </div>
      </div>

      <div class="container-custom relative z-10">
        <div class="max-w-4xl mx-auto">
          <!-- Back link -->
          <NuxtLink
            v-motion
            :initial="{ opacity: 0, x: -20 }"
            :enter="{ opacity: 1, x: 0, transition: { duration: 400 } }"
            :to="localePath('/blog')"
            class="inline-flex items-center gap-2 text-gray-400 hover:text-primary-400 transition-colors mb-8"
          >
            <Icon name="mdi:arrow-left" />
            {{ $t('blogPost.backToBlog') }}
          </NuxtLink>

          <!-- Category -->
          <div
            v-motion
            :initial="{ opacity: 0, y: 20 }"
            :enter="{ opacity: 1, y: 0, transition: { delay: 100, duration: 500 } }"
          >
            <span
              class="inline-block px-4 py-1.5 rounded-full text-sm font-medium mb-6"
              :style="{ backgroundColor: post.color + '20', color: post.color }"
            >
              {{ $t(`blog.categories.${post.category}`) }}
            </span>
          </div>

          <!-- Title -->
          <h1
            v-motion
            :initial="{ opacity: 0, y: 30 }"
            :enter="{ opacity: 1, y: 0, transition: { delay: 200, duration: 600 } }"
            class="text-3xl md:text-5xl lg:text-6xl font-bold text-white mb-6 leading-tight"
          >
            {{ $t(`blog.posts.${post.slug}.title`) }}
          </h1>

          <!-- Meta -->
          <div
            v-motion
            :initial="{ opacity: 0, y: 20 }"
            :enter="{ opacity: 1, y: 0, transition: { delay: 300, duration: 500 } }"
            class="flex flex-wrap items-center gap-6 text-gray-400"
          >
            <div class="flex items-center gap-3">
              <div
                class="w-10 h-10 rounded-full flex items-center justify-center text-sm font-bold"
                :style="{ backgroundColor: post.color + '20', color: post.color }"
              >
                {{ post.author.charAt(0) }}
              </div>
              <div>
                <div class="text-white font-medium">{{ post.author }}</div>
                <div class="text-sm text-gray-500">{{ post.role }}</div>
              </div>
            </div>
            <span class="hidden sm:block w-px h-8 bg-gray-700" />
            <div class="flex items-center gap-4 text-sm">
              <span class="flex items-center gap-2">
                <Icon name="mdi:calendar" />
                {{ post.date }}
              </span>
              <span class="flex items-center gap-2">
                <Icon name="mdi:clock-outline" />
                {{ post.readTime }} {{ $t('blog.minRead') }}
              </span>
            </div>
          </div>
        </div>
      </div>

      <!-- Corner decorations -->
      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/30 pointer-events-none" />
      <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/30 pointer-events-none" />
    </section>

    <!-- Article Content -->
    <section class="py-8 md:py-20 bg-[#0d0d12] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-r from-primary-500/5 via-transparent to-secondary-500/5" />
      </div>

      <div class="container-custom relative z-10">
        <div class="max-w-4xl mx-auto">
          <!-- Featured Image -->
          <div
            v-motion
            :initial="{ opacity: 0, y: 30 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 600 } }"
            class="relative h-64 md:h-96 rounded-2xl overflow-hidden mb-12 border border-white/10"
          >
            <div
              class="absolute inset-0 bg-gradient-to-br"
              :style="{ background: `linear-gradient(135deg, ${post.color}30, ${post.color}10)` }"
            />
            <div class="absolute inset-0 flex items-center justify-center">
              <Icon :name="post.icon" class="text-9xl opacity-20" :style="{ color: post.color }" />
            </div>
          </div>

          <!-- Article Body -->
          <article
            v-motion
            :initial="{ opacity: 0, y: 30 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 100, duration: 500 } }"
            class="prose prose-invert prose-lg max-w-none"
          >
            <p class="text-xl text-gray-300 leading-relaxed mb-8">
              {{ $t(`blog.posts.${post.slug}.excerpt`) }}
            </p>

            <!-- Full per-post content (new posts) -->
            <template v-if="hasFullContent">
              <div class="space-y-4 text-gray-400">
                <p>{{ $t(`blogContent.${post.slug}.intro`) }}</p>

                <template v-for="n in 5" :key="n">
                  <template v-if="te(`blogContent.${post.slug}.s${n}h`)">
                    <h2 class="text-2xl font-bold text-white mt-12 mb-4">
                      {{ $t(`blogContent.${post.slug}.s${n}h`) }}
                    </h2>
                    <p class="mb-4">{{ $t(`blogContent.${post.slug}.s${n}p`) }}</p>
                    <p v-if="te(`blogContent.${post.slug}.s${n}p2`)" class="mb-4">
                      {{ $t(`blogContent.${post.slug}.s${n}p2`) }}
                    </p>
                  </template>
                </template>

                <!-- Pull quote -->
                <div class="p-6 rounded-xl bg-white/5 border border-white/10 my-8">
                  <p class="text-gray-300 italic text-lg leading-relaxed">
                    {{ $t(`blogContent.${post.slug}.quote`) }}
                  </p>
                </div>

                <!-- Internal links callout -->
                <div v-if="post.links?.length" class="p-6 rounded-xl bg-primary-500/10 border border-primary-500/30 my-8">
                  <p class="text-primary-400 font-semibold mb-4 flex items-center gap-2">
                    <Icon name="mdi:link-variant" />
                    {{ $t('blogPost.relatedServices') }}
                  </p>
                  <ul class="space-y-2">
                    <li v-for="link in post.links" :key="link.url">
                      <NuxtLink
                        :to="localePath(link.url)"
                        class="flex items-center gap-2 text-gray-300 hover:text-primary-400 transition-colors duration-200 group"
                      >
                        <Icon name="mdi:arrow-right-circle" class="text-primary-500 group-hover:translate-x-0.5 transition-transform" />
                        {{ link.label }}
                      </NuxtLink>
                    </li>
                  </ul>
                </div>

                <!-- Conclusion -->
                <h2 class="text-2xl font-bold text-white mt-12 mb-4">
                  {{ $t(`blogContent.${post.slug}.conclusionH`) }}
                </h2>
                <p>{{ $t(`blogContent.${post.slug}.conclusionP`) }}</p>

                <!-- FAQ Section -->
                <div v-if="postFaqItems.length" class="mt-14">
                  <h2 class="text-2xl font-bold text-white mb-6">{{ $t('blogPost.faqTitle') }}</h2>
                  <div class="space-y-4">
                    <div
                      v-for="(item, i) in postFaqItems"
                      :key="i"
                      class="p-5 rounded-xl bg-white/5 border border-white/10 hover:border-white/20 transition-colors"
                    >
                      <h3 class="text-base font-semibold text-white mb-2">{{ item.q }}</h3>
                      <p class="text-gray-400 text-sm leading-relaxed">{{ item.a }}</p>
                    </div>
                  </div>
                </div>
              </div>
            </template>

            <!-- Placeholder content for older posts -->
            <template v-else>
              <div class="space-y-6 text-gray-400">
                <p>{{ $t('blogPost.placeholder.intro') }}</p>

                <h2 class="text-2xl font-bold text-white mt-12 mb-4">{{ $t('blogPost.placeholder.section1Title') }}</h2>
                <p>{{ $t('blogPost.placeholder.section1Content') }}</p>

                <h2 class="text-2xl font-bold text-white mt-12 mb-4">{{ $t('blogPost.placeholder.section2Title') }}</h2>
                <p>{{ $t('blogPost.placeholder.section2Content') }}</p>

                <h2 class="text-2xl font-bold text-white mt-12 mb-4">{{ $t('blogPost.placeholder.section3Title') }}</h2>
                <p>{{ $t('blogPost.placeholder.section3Content') }}</p>

                <div class="p-6 rounded-xl bg-white/5 border border-white/10 my-8">
                  <p class="text-gray-300 italic">{{ $t('blogPost.placeholder.quote') }}</p>
                </div>

                <h2 class="text-2xl font-bold text-white mt-12 mb-4">{{ $t('blogPost.placeholder.conclusionTitle') }}</h2>
                <p>{{ $t('blogPost.placeholder.conclusionContent') }}</p>
              </div>
            </template>
          </article>

          <!-- Tags -->
          <div
            v-motion
            :initial="{ opacity: 0, y: 20 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 150, duration: 500 } }"
            class="mt-12 pt-8 border-t border-white/10"
          >
            <div class="flex flex-wrap items-center gap-3">
              <span class="text-gray-500">{{ $t('blogPost.tags') }}:</span>
              <span
                v-for="tag in post.tags"
                :key="tag"
                class="px-3 py-1 rounded-full text-sm bg-white/5 text-gray-400 border border-white/10"
              >
                {{ tag }}
              </span>
            </div>
          </div>

          <!-- Share -->
          <ClientOnly>
            <div
              v-motion
              :initial="{ opacity: 0, y: 20 }"
              :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 200, duration: 500 } }"
              class="mt-8 flex items-center gap-4"
            >
              <span class="text-gray-500">{{ $t('blogPost.share') }}:</span>
              <div class="flex gap-3">
                <a
                  v-for="social in socials"
                  :key="social.name"
                  :href="social.url"
                  target="_blank"
                  rel="noopener noreferrer"
                  :aria-label="`Share on ${social.name}`"
                  class="w-10 h-10 rounded-full bg-white/5 border border-white/10 flex items-center justify-center text-gray-400 hover:text-white hover:border-primary-500/50 hover:bg-white/10 transition-all duration-300 hover:scale-110"
                >
                  <Icon :name="social.icon" />
                </a>
              </div>
            </div>
          </ClientOnly>

          <!-- Author Box -->
          <div
            v-motion
            :initial="{ opacity: 0, y: 30 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 250, duration: 500 } }"
            class="mt-12 p-8 rounded-2xl bg-white/5 border border-white/10"
          >
            <div class="flex flex-col sm:flex-row items-start gap-6">
              <div
                class="w-16 h-16 rounded-full flex items-center justify-center text-2xl font-bold flex-shrink-0"
                :style="{ backgroundColor: post.color + '20', color: post.color }"
              >
                {{ post.author.charAt(0) }}
              </div>
              <div>
                <h4 class="text-xl font-bold text-white mb-1">{{ post.author }}</h4>
                <p class="text-primary-400 text-sm mb-3">{{ post.role }}</p>
                <p class="text-gray-400">{{ $t('blogPost.authorBio') }}</p>
              </div>
            </div>
          </div>

          <!-- Related Posts -->
          <div class="mt-16">
            <h3
              v-motion
              :initial="{ opacity: 0, y: 20 }"
              :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 500 } }"
              class="text-2xl font-bold text-white mb-8"
            >
              {{ $t('blogPost.relatedPosts') }}
            </h3>
            <div v-if="relatedPosts.length > 0" class="grid md:grid-cols-2 gap-6">
              <NuxtLink
                v-for="(related, index) in relatedPosts"
                :key="related.slug"
                v-motion
                :initial="{ opacity: 0, y: 30 }"
                :visibleOnce="{ opacity: 1, y: 0, transition: { delay: index * 100, duration: 500 } }"
                :to="localePath(`/blog/${related.slug}`)"
                class="group p-6 rounded-xl bg-white/5 border border-white/10 hover:border-primary-500/30 hover:bg-white/10 transition-all duration-300"
              >
                <span
                  class="inline-block px-3 py-1 rounded-full text-xs font-medium mb-3"
                  :style="{ backgroundColor: related.color + '20', color: related.color }"
                >
                  {{ $t(`blog.categories.${related.category}`) }}
                </span>
                <h4 class="text-lg font-bold text-white group-hover:text-primary-400 transition-colors mb-2">
                  {{ $t(`blog.posts.${related.slug}.title`) }}
                </h4>
                <p class="text-sm text-gray-500 flex items-center gap-2">
                  <Icon name="mdi:clock-outline" class="text-xs" />
                  {{ related.readTime }} {{ $t('blog.minRead') }}
                </p>
              </NuxtLink>
            </div>
            <div v-else class="text-center py-8 text-gray-500">
              {{ $t('blog.noRelatedPosts') }}
            </div>
          </div>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
/**
 * Individual blog post page
 */

const route = useRoute()
const slug = route.params.slug as string
const { t, te, locale } = useI18n()
const { breadcrumbSchema, injectSchema } = useSchemaOrg()

type PostLink = { label: string; url: string }
type Post = {
  slug: string; category: string; date: string; readTime: number
  author: string; role: string; icon: string; color: string
  tags: string[]; links?: PostLink[]
}

const posts: Post[] = [
  {
    slug: 'modern-web-development-2024',
    category: 'development',
    date: '2024-01-15',
    readTime: 8,
    author: 'Alex Chen',
    role: 'Lead Developer',
    icon: 'mdi:code-tags',
    color: '#0ea5e9',
    tags: ['Web Development', 'JavaScript', 'Trends', '2024']
  },
  {
    slug: 'seo-strategies-ai-era',
    category: 'seo',
    date: '2024-01-12',
    readTime: 6,
    author: 'Sarah Miller',
    role: 'SEO Specialist',
    icon: 'mdi:chart-line',
    color: '#8b5cf6',
    tags: ['SEO', 'AI', 'Digital Marketing', 'Strategy']
  },
  {
    slug: 'ppc-optimization-tips',
    category: 'marketing',
    date: '2024-01-10',
    readTime: 5,
    author: 'Mike Johnson',
    role: 'Marketing Manager',
    icon: 'mdi:target',
    color: '#f59e0b',
    tags: ['PPC', 'Advertising', 'ROI', 'Optimization']
  },
  {
    slug: 'ui-ux-trends-2024',
    category: 'design',
    date: '2024-01-08',
    readTime: 7,
    author: 'Emma Wilson',
    role: 'UI/UX Designer',
    icon: 'mdi:palette',
    color: '#ec4899',
    tags: ['UI/UX', 'Design', 'Trends', 'User Experience']
  },
  {
    slug: 'scaling-saas-business',
    category: 'business',
    date: '2024-01-05',
    readTime: 10,
    author: 'David Lee',
    role: 'Business Strategist',
    icon: 'mdi:rocket-launch',
    color: '#10b981',
    tags: ['SaaS', 'Business', 'Scaling', 'Growth']
  },
  {
    slug: 'typescript-best-practices',
    category: 'development',
    date: '2024-01-03',
    readTime: 9,
    author: 'Alex Chen',
    role: 'Lead Developer',
    icon: 'mdi:language-typescript',
    color: '#3b82f6',
    tags: ['TypeScript', 'JavaScript', 'Best Practices', 'Development']
  },
  // ── New full-content posts ──
  {
    slug: 'ecommerce-website-development-2025',
    category: 'development',
    date: '2025-03-01',
    readTime: 10,
    author: 'Alex Chen',
    role: 'Lead Developer',
    icon: 'mdi:shopping',
    color: '#0ea5e9',
    tags: ['E-Commerce', 'Web Development', 'CRO', 'UX', '2025'],
    links: [
      { label: 'E-Commerce Development Services', url: '/services/web-development/e-commerce' },
      { label: 'Web Development Services', url: '/services/web-development' },
      { label: 'Our Portfolio', url: '/portfolio' },
      { label: 'Get a Free Quote', url: '/contact' }
    ]
  },
  {
    slug: 'technical-seo-audit-guide-2025',
    category: 'seo',
    date: '2025-02-20',
    readTime: 12,
    author: 'Sarah Miller',
    role: 'SEO Specialist',
    icon: 'mdi:magnify-scan',
    color: '#8b5cf6',
    tags: ['Technical SEO', 'SEO Audit', 'Core Web Vitals', 'Crawlability', '2025'],
    links: [
      { label: 'Technical SEO Services', url: '/services/technical-seo' },
      { label: 'Technical SEO Audit', url: '/services/technical-seo/technical-audit' },
      { label: 'Core Web Vitals Optimization', url: '/services/technical-seo/core-web-vitals' },
      { label: 'Contact Our SEO Team', url: '/contact' }
    ]
  },
  {
    slug: 'google-ads-vs-yandex-direct-2025',
    category: 'marketing',
    date: '2025-02-10',
    readTime: 9,
    author: 'Mike Johnson',
    role: 'Marketing Manager',
    icon: 'mdi:google-ads',
    color: '#f59e0b',
    tags: ['Google Ads', 'Yandex Direct', 'PPC', 'CIS Markets', 'Advertising'],
    links: [
      { label: 'Google Ads Services', url: '/services/google-ads' },
      { label: 'Yandex Direct Services', url: '/services/yandex-direct' },
      { label: 'All Advertising Services', url: '/services' },
      { label: 'Get a Free PPC Audit', url: '/contact' }
    ]
  },
  {
    slug: 'website-cost-guide-2025',
    category: 'business',
    date: '2025-01-25',
    readTime: 11,
    author: 'David Lee',
    role: 'Business Strategist',
    icon: 'mdi:currency-usd',
    color: '#10b981',
    tags: ['Website Cost', 'Web Development Pricing', 'Budget', 'ROI', '2025'],
    links: [
      { label: 'Web Development Services', url: '/services/web-development' },
      { label: 'Corporate Website Development', url: '/services/web-development/corporate-websites' },
      { label: 'E-Commerce Development', url: '/services/web-development/e-commerce' },
      { label: 'View Our Portfolio', url: '/portfolio' },
      { label: 'Get a Project Quote', url: '/contact' }
    ]
  },
  // ── Batch 2 – April 2025 ──
  {
    slug: 'mobile-app-development-cost-2025',
    category: 'development',
    date: '2025-04-05',
    readTime: 11,
    author: 'Alex Chen',
    role: 'Lead Developer',
    icon: 'mdi:cellphone',
    color: '#3b82f6',
    tags: ['Mobile App', 'iOS', 'Android', 'Flutter', 'App Cost', '2025'],
    links: [
      { label: 'Mobile Development Services', url: '/services/mobile-development' },
      { label: 'Native iOS Development', url: '/services/mobile-development/native-ios' },
      { label: 'Native Android Development', url: '/services/mobile-development/native-android' },
      { label: 'Cross-Platform Development', url: '/services/mobile-development/cross-platform' },
      { label: 'Get a Free App Quote', url: '/contact' }
    ]
  },
  {
    slug: 'core-web-vitals-optimization-guide-2025',
    category: 'seo',
    date: '2025-04-12',
    readTime: 12,
    author: 'Sarah Miller',
    role: 'SEO Specialist',
    icon: 'mdi:speedometer',
    color: '#8b5cf6',
    tags: ['Core Web Vitals', 'LCP', 'CLS', 'INP', 'PageSpeed', 'Technical SEO'],
    links: [
      { label: 'Core Web Vitals Optimization', url: '/services/technical-seo/core-web-vitals' },
      { label: 'Technical SEO Audit', url: '/services/technical-seo/technical-audit' },
      { label: 'Technical SEO Services', url: '/services/technical-seo' },
      { label: 'Request a Free Audit', url: '/contact' }
    ]
  },
  {
    slug: 'wordpress-vs-custom-development-2025',
    category: 'development',
    date: '2025-04-20',
    readTime: 10,
    author: 'Alex Chen',
    role: 'Lead Developer',
    icon: 'mdi:wordpress',
    color: '#0ea5e9',
    tags: ['WordPress', 'Custom Development', 'Nuxt.js', 'Next.js', 'CMS', '2025'],
    links: [
      { label: 'Corporate Website Development', url: '/services/web-development/corporate-websites' },
      { label: 'Custom CMS Development', url: '/services/web-development/custom-cms' },
      { label: 'Web Application Development', url: '/services/web-development/web-applications' },
      { label: 'View Our Portfolio', url: '/portfolio' },
      { label: 'Discuss Your Project', url: '/contact' }
    ]
  },
  {
    slug: 'local-seo-guide-2025',
    category: 'seo',
    date: '2025-04-28',
    readTime: 10,
    author: 'Sarah Miller',
    role: 'SEO Specialist',
    icon: 'mdi:map-marker-radius',
    color: '#ec4899',
    tags: ['Local SEO', 'Google Business Profile', 'Local Pack', 'Reviews', 'Citations', '2025'],
    links: [
      { label: 'Local SEO Services', url: '/services/content-seo/local-seo' },
      { label: 'Content SEO Services', url: '/services/content-seo' },
      { label: 'Technical SEO Services', url: '/services/technical-seo' },
      { label: 'Get a Local SEO Audit', url: '/contact' }
    ]
  }
]

const post = posts.find(p => p.slug === slug) || posts[0]!

/** True when per-post full content exists in blogContent.{slug}.* i18n keys */
const hasFullContent = computed(() => te(`blogContent.${slug}.intro`))

/** Build FAQ items from i18n keys blogContent.{slug}.faqQ1/faqA1 … faqQ4/faqA4 */
const postFaqItems = computed(() => {
  if (!hasFullContent.value) return []
  const items: { q: string; a: string }[] = []
  for (let i = 1; i <= 4; i++) {
    const q = te(`blogContent.${slug}.faqQ${i}`) ? t(`blogContent.${slug}.faqQ${i}`) : ''
    const a = te(`blogContent.${slug}.faqA${i}`) ? t(`blogContent.${slug}.faqA${i}`) : ''
    if (q && a) items.push({ q, a })
  }
  return items
})

// Get related posts: prioritize same category, then fill with others
const relatedPosts = computed(() => {
  // First, get posts from same category
  const sameCategoryPosts = posts.filter(
    p => p.slug !== slug && p.category === post.category
  )

  // If we have enough from same category, return them
  if (sameCategoryPosts.length >= 2) {
    return sameCategoryPosts.slice(0, 2)
  }

  // Otherwise, fill with posts from other categories
  const otherPosts = posts.filter(
    p => p.slug !== slug && p.category !== post.category
  )

  return [...sameCategoryPosts, ...otherPosts].slice(0, 2)
})

const localePath = useLocalePath()

// Get current page URL for sharing (client-only to avoid hydration mismatch)
const shareUrl = ref('')
const shareTitle = ref('')

// Social share links - client-only to prevent hydration mismatch
const socials = ref<Array<{ name: string; icon: string; url: string }>>([])

// Initialize share data on client
onMounted(() => {
  shareUrl.value = encodeURIComponent(window.location.href)
  shareTitle.value = encodeURIComponent(t(`blog.posts.${post.slug}.title`))

  socials.value = [
    {
      name: 'Twitter',
      icon: 'mdi:twitter',
      url: `https://twitter.com/intent/tweet?url=${shareUrl.value}&text=${shareTitle.value}`
    },
    {
      name: 'LinkedIn',
      icon: 'mdi:linkedin',
      url: `https://www.linkedin.com/sharing/share-offsite/?url=${shareUrl.value}`
    },
    {
      name: 'Facebook',
      icon: 'mdi:facebook',
      url: `https://www.facebook.com/sharer/sharer.php?u=${shareUrl.value}`
    }
  ]
})

// SEO - Meta tags
const postTitle = computed(() => t(`blog.posts.${post.slug}.title`))
const postExcerpt = computed(() => t(`blog.posts.${post.slug}.excerpt`))

const _blogConfig = useRuntimeConfig()
const _blogBaseUrl = _blogConfig.public.baseUrl || 'https://innovayse.com'

const _blogPostPath = computed(() =>
  locale.value === 'en' ? `/blog/${slug}` : `/${locale.value}/blog/${slug}`
)
const _blogPostCanonical = computed(() => `${_blogBaseUrl}${_blogPostPath.value}`)

useSeoMeta({
  title: () => `${postTitle.value} | Innovayse Blog`,
  description: () => postExcerpt.value,

  // Open Graph
  ogTitle: () => `${postTitle.value} | Innovayse Blog`,
  ogDescription: () => postExcerpt.value,
  ogType: 'article',
  ogImage: `${_blogBaseUrl}/og-image.jpg`,
  ogUrl: () => _blogPostCanonical.value,
  articleAuthor: [post.author],
  articlePublishedTime: post.date,

  // Twitter Card
  twitterCard: 'summary_large_image',
  twitterTitle: () => `${postTitle.value} | Innovayse`,
  twitterDescription: () => postExcerpt.value,
  twitterImage: `${_blogBaseUrl}/og-image.jpg`
})

// Canonical + hreflang (SSR-safe)
useHead({
  link: [
    { rel: 'canonical', href: () => _blogPostCanonical.value },
    { rel: 'alternate', hreflang: 'en', href: `${_blogBaseUrl}/blog/${slug}` },
    { rel: 'alternate', hreflang: 'ru', href: `${_blogBaseUrl}/ru/blog/${slug}` },
    { rel: 'alternate', hreflang: 'hy', href: `${_blogBaseUrl}/hy/blog/${slug}` },
    { rel: 'alternate', hreflang: 'x-default', href: `${_blogBaseUrl}/blog/${slug}` }
  ]
})

// SEO - Structured data (SSR-safe, using runtimeConfig baseUrl)
const schemaItems: object[] = [
  {
    '@context': 'https://schema.org',
    '@type': 'Article',
    headline: postTitle.value,
    description: postExcerpt.value,
    datePublished: post.date,
    url: _blogPostCanonical.value,
    author: {
      '@type': 'Person',
      name: post.author
    },
    publisher: {
      '@type': 'Organization',
      name: 'Innovayse',
      logo: {
        '@type': 'ImageObject',
        url: `${_blogBaseUrl}/logo.png`
      }
    }
  },
  breadcrumbSchema([
    { name: 'Home', url: _blogBaseUrl },
    { name: 'Blog', url: `${_blogBaseUrl}/blog` },
    { name: postTitle.value, url: _blogPostCanonical.value }
  ])
]

// Add FAQPage schema for posts with FAQ content
if (postFaqItems.value.length > 0) {
  schemaItems.push({
    '@context': 'https://schema.org',
    '@type': 'FAQPage',
    mainEntity: postFaqItems.value.map(item => ({
      '@type': 'Question',
      name: item.q,
      acceptedAnswer: { '@type': 'Answer', text: item.a }
    }))
  })
}

injectSchema(schemaItems)
</script>
