<template>
  <div>
    <section class="py-12 md:py-20 bg-[#0a0a0f] min-h-screen">
      <div class="container-custom max-w-4xl">

        <!-- Back link -->
        <NuxtLink
          :to="localePath('/knowledgebase')"
          class="inline-flex items-center gap-2 text-sm text-gray-400 hover:text-white transition-colors mb-8"
        >
          <ArrowLeft :size="15" :stroke-width="2" />
          {{ $t('kb.backToKb') }}
        </NuxtLink>

        <!-- Category header -->
        <div class="flex items-center gap-4 mb-8">
          <div class="w-12 h-12 rounded-xl bg-primary-500/10 border border-primary-500/20 flex items-center justify-center flex-shrink-0">
            <Folder :size="22" :stroke-width="2" class="text-primary-400" />
          </div>
          <div>
            <h1 class="text-2xl font-bold text-white">{{ categoryName }}</h1>
            <p class="text-sm text-gray-500">{{ articles.length }} {{ $t('kb.articles') }}</p>
          </div>
        </div>

        <!-- Loading -->
        <div v-if="pending" class="space-y-3">
          <div v-for="i in 6" :key="i" class="h-16 rounded-xl bg-white/5 border border-white/10 animate-pulse" />
        </div>

        <!-- Empty -->
        <div v-else-if="!articles.length" class="text-center py-20">
          <FileText :size="40" :stroke-width="1.5" class="text-gray-600 mx-auto mb-4" />
          <p class="text-gray-400">{{ $t('kb.empty') }}</p>
        </div>

        <!-- Articles list -->
        <div v-else class="divide-y divide-white/5 rounded-2xl border border-white/10 overflow-hidden">
          <KbArticleRow v-for="article in articles" :key="article.id" :article="article" />
        </div>

      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
/**
 * Knowledgebase category page.
 * Lists all articles in the given category.
 */

import { ArrowLeft, Folder, FileText } from 'lucide-vue-next'

const route = useRoute()
const localePath = useLocalePath()
const { t } = useI18n()

const categoryId = computed(() => String(route.params.id))

/** Fetch articles for this category */
const { data, pending } = useFetch('/api/portal/knowledgebase/articles', {
  query: computed(() => ({ categoryid: categoryId.value, limitnum: 100 }))
})

const articles = computed(() => (data.value as any)?.items ?? [])

/** Fetch categories to get the category name */
const { data: catData } = useFetch('/api/portal/knowledgebase/categories')
const categoryName = computed(() => {
  const cats = (catData.value as any[]) ?? []
  return cats.find((c: any) => String(c.id) === categoryId.value)?.name ?? t('kb.categories')
})

watchEffect(() => {
  if (categoryName.value) {
    useSeoMeta({ title: `${categoryName.value} — ${t('kb.title')}` })
  }
})
</script>
