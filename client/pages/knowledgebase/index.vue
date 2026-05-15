<template>
  <div>
    <!-- Hero / Search -->
    <section class="relative py-16 md:py-24 bg-[#0a0a0f] overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute top-0 right-1/4 w-[400px] h-[400px] bg-primary-500/15 rounded-full blur-[130px]" />
        <div class="absolute bottom-0 left-1/3 w-[300px] h-[300px] bg-cyan-500/10 rounded-full blur-[110px]" />
      </div>
      <div class="container-custom relative z-10 text-center max-w-3xl mx-auto">
        <div class="inline-flex items-center gap-2 px-4 py-2 mb-6 rounded-full glass border border-primary-500/20 backdrop-blur-sm">
          <BookOpen :size="14" :stroke-width="2" class="text-primary-400" />
          <span class="text-sm font-medium text-gray-300">{{ $t('kb.badge') }}</span>
        </div>
        <h1 class="text-4xl md:text-5xl font-bold text-white mb-4">{{ $t('kb.title') }}</h1>
        <p class="text-gray-400 mb-10 leading-relaxed">{{ $t('kb.subtitle') }}</p>

        <!-- Search bar -->
        <form class="flex gap-2" @submit.prevent="doSearch">
          <input
            v-model="searchQuery"
            type="text"
            :placeholder="$t('kb.searchPlaceholder')"
            class="flex-1 px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white placeholder-gray-500 text-sm focus:outline-none focus:border-primary-500/50 focus:ring-2 focus:ring-primary-500/20 transition-all"
          />
          <button
            type="submit"
            class="px-6 py-3 rounded-xl bg-gradient-to-r from-cyan-600 to-primary-600 text-white font-semibold text-sm hover:opacity-90 transition-opacity flex items-center gap-2 flex-shrink-0"
          >
            <Search :size="16" :stroke-width="2" />
            {{ $t('kb.search') }}
          </button>
        </form>
      </div>
    </section>

    <!-- Content -->
    <section class="py-12 bg-[#0a0a0f]">
      <div class="container-custom max-w-5xl">

        <!-- Search results -->
        <template v-if="searched">
          <div class="flex items-center justify-between mb-6">
            <h2 class="text-lg font-semibold text-white">
              {{ searchResults.length }} {{ $t('kb.articles') }}
            </h2>
            <button class="text-sm text-gray-400 hover:text-white transition-colors flex items-center gap-1" @click="clearSearch">
              <X :size="14" :stroke-width="2" />
              {{ $t('kb.backToKb') }}
            </button>
          </div>

          <div v-if="searchPending" class="space-y-3">
            <div v-for="i in 5" :key="i" class="h-20 rounded-xl bg-white/5 border border-white/10 animate-pulse" />
          </div>

          <div v-else-if="!searchResults.length" class="text-center py-16">
            <Search :size="40" :stroke-width="1.5" class="text-gray-600 mx-auto mb-4" />
            <p class="text-gray-400">{{ $t('kb.noResults') }}</p>
          </div>

          <div v-else class="space-y-3">
            <KbArticleRow v-for="article in searchResults" :key="article.id" :article="article" />
          </div>
        </template>

        <!-- Default: categories + popular -->
        <template v-else>
          <!-- Categories grid -->
          <div class="mb-12">
            <h2 class="text-xl font-bold text-white mb-6 flex items-center gap-2">
              <FolderOpen :size="20" :stroke-width="2" class="text-primary-400" />
              {{ $t('kb.categories') }}
            </h2>

            <div v-if="catPending" class="grid sm:grid-cols-2 gap-4">
              <div v-for="i in 4" :key="i" class="h-24 rounded-2xl bg-white/5 border border-white/10 animate-pulse" />
            </div>

            <div v-else-if="!categories.length" class="text-gray-400 text-sm">{{ $t('kb.empty') }}</div>

            <div v-else class="grid sm:grid-cols-2 gap-4">
              <NuxtLink
                v-for="cat in categories"
                :key="cat.id"
                :to="localePath(`/knowledgebase/category/${cat.id}`)"
                class="flex items-center justify-between p-5 rounded-2xl bg-white/5 border border-white/10 hover:border-primary-500/30 hover:bg-white/8 transition-all duration-200 group"
              >
                <div class="flex items-center gap-3">
                  <div class="w-10 h-10 rounded-xl bg-primary-500/10 border border-primary-500/20 flex items-center justify-center flex-shrink-0">
                    <Folder :size="18" :stroke-width="2" class="text-primary-400" />
                  </div>
                  <span class="font-semibold text-white group-hover:text-primary-400 transition-colors">{{ cat.name }}</span>
                </div>
                <span class="px-2.5 py-1 rounded-full text-xs font-bold bg-primary-500/15 text-primary-400 flex-shrink-0">
                  {{ cat.articlecount }} {{ cat.articlecount === 1 ? $t('kb.article') : $t('kb.articles') }}
                </span>
              </NuxtLink>
            </div>
          </div>

          <!-- Most popular articles -->
          <div>
            <h2 class="text-xl font-bold text-white mb-6 flex items-center gap-2">
              <Star :size="20" :stroke-width="2" class="text-yellow-400" />
              {{ $t('kb.mostPopular') }}
            </h2>

            <div v-if="artPending" class="space-y-3">
              <div v-for="i in 5" :key="i" class="h-20 rounded-xl bg-white/5 border border-white/10 animate-pulse" />
            </div>

            <div v-else-if="!popularArticles.length" class="text-gray-400 text-sm">{{ $t('kb.empty') }}</div>

            <div v-else class="divide-y divide-white/5 rounded-2xl border border-white/10 overflow-hidden">
              <KbArticleRow v-for="article in popularArticles" :key="article.id" :article="article" />
            </div>
          </div>
        </template>

      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
/**
 * Public knowledgebase index page.
 * Shows categories grid + most popular articles.
 * Supports live search via query param.
 */

import { BookOpen, Search, FolderOpen, Folder, Star, X } from 'lucide-vue-next'

const localePath = useLocalePath()
const route = useRoute()
const router = useRouter()
const { t } = useI18n()

useSeoMeta({
  title: t('kb.title'),
  description: t('kb.subtitle')
})

/** Fetch all top-level categories */
const { data: catData, pending: catPending } = useFetch('/api/portal/knowledgebase/categories')
const categories = computed(() => (catData.value as any[]) ?? [])

/** Fetch popular articles (sorted by views, no category filter) */
const { data: artData, pending: artPending } = useFetch('/api/portal/knowledgebase/articles', {
  query: { limitnum: 10 }
})
const popularArticles = computed(() => {
  const items = (artData.value as any)?.items ?? []
  return [...items].sort((a: any, b: any) => b.views - a.views)
})

// ── Search ──────────────────────────────────────────────────────────────────

const searchQuery = ref(String(route.query.q ?? ''))
const searched = ref(!!route.query.q)

const { data: searchData, pending: searchPending, execute: runSearch } = useFetch(
  '/api/portal/knowledgebase/articles',
  {
    query: computed(() => ({ search: searchQuery.value, limitnum: 50 })),
    immediate: false
  }
)

const searchResults = computed(() => (searchData.value as any)?.items ?? [])

/** Run search and update URL query string */
async function doSearch() {
  if (!searchQuery.value.trim()) { clearSearch(); return }
  searched.value = true
  router.replace({ query: { q: searchQuery.value } })
  await runSearch()
}

/** Clear search and return to default view */
function clearSearch() {
  searchQuery.value = ''
  searched.value = false
  router.replace({ query: {} })
}

// Re-run search if page is loaded with ?q= param
onMounted(async () => {
  if (route.query.q) await runSearch()
})
</script>
