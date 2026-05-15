<template>
  <div>
    <section class="py-12 md:py-20 bg-[#0a0a0f] min-h-screen">
      <div class="container-custom max-w-3xl">

        <!-- Back link -->
        <NuxtLink
          :to="localePath('/knowledgebase')"
          class="inline-flex items-center gap-2 text-sm text-gray-400 hover:text-white transition-colors mb-8"
        >
          <ArrowLeft :size="15" :stroke-width="2" />
          {{ $t('kb.backToKb') }}
        </NuxtLink>

        <!-- Loading -->
        <div v-if="pending" class="space-y-4">
          <div class="h-8 w-3/4 rounded-xl bg-white/5 animate-pulse" />
          <div class="h-4 w-1/3 rounded-lg bg-white/5 animate-pulse" />
          <div class="mt-8 space-y-3">
            <div v-for="i in 8" :key="i" class="h-4 rounded bg-white/5 animate-pulse" :style="{ width: `${90 - i * 5}%` }" />
          </div>
        </div>

        <!-- Error -->
        <div v-else-if="error || !article" class="text-center py-20">
          <BookOpen :size="48" :stroke-width="1.5" class="text-gray-600 mx-auto mb-4" />
          <p class="text-gray-400">{{ $t('kb.errorDesc') }}</p>
        </div>

        <!-- Article -->
        <article v-else>
          <!-- Meta row -->
          <div class="flex flex-wrap items-center gap-3 text-xs text-gray-500 mb-5">
            <span v-if="article.dateupdated" class="flex items-center gap-1">
              <Calendar :size="12" :stroke-width="2" />
              {{ $t('kb.lastUpdated') }}: {{ article.dateupdated }}
            </span>
            <span v-if="article.views" class="flex items-center gap-1">
              <Eye :size="12" :stroke-width="2" />
              {{ article.views }} {{ $t('kb.views') }}
            </span>
          </div>

          <!-- Title -->
          <h1 class="text-3xl md:text-4xl font-bold text-white mb-6 leading-tight">
            {{ article.title }}
          </h1>

          <div class="h-px bg-white/10 mb-8" />

          <!-- HTML body from WHMCS -->
          <div
            class="prose prose-invert prose-sm md:prose-base max-w-none
                   prose-p:text-gray-300 prose-p:leading-relaxed
                   prose-a:text-primary-400 prose-a:no-underline hover:prose-a:underline
                   prose-headings:text-white prose-strong:text-white
                   prose-ul:text-gray-300 prose-ol:text-gray-300 prose-li:marker:text-primary-400
                   prose-blockquote:border-primary-500 prose-blockquote:text-gray-400
                   prose-code:text-cyan-400 prose-code:bg-white/5 prose-code:rounded prose-code:px-1"
            v-html="article.body"
          />

          <div class="h-px bg-white/10 my-10" />

          <!-- Was this helpful? -->
          <div class="flex items-center justify-between gap-4 flex-wrap">
            <p class="text-sm text-gray-400">{{ $t('kb.helpful') }}</p>
            <div class="flex gap-2">
              <button
                class="px-4 py-2 rounded-lg text-sm font-medium border border-green-500/30 text-green-400 hover:bg-green-500/10 transition-colors flex items-center gap-1.5"
                :class="vote === 'yes' ? 'bg-green-500/10' : ''"
                @click="vote = 'yes'"
              >
                <ThumbsUp :size="14" :stroke-width="2" />
                {{ $t('kb.yes') }}
              </button>
              <button
                class="px-4 py-2 rounded-lg text-sm font-medium border border-red-500/30 text-red-400 hover:bg-red-500/10 transition-colors flex items-center gap-1.5"
                :class="vote === 'no' ? 'bg-red-500/10' : ''"
                @click="vote = 'no'"
              >
                <ThumbsDown :size="14" :stroke-width="2" />
                {{ $t('kb.no') }}
              </button>
            </div>
          </div>
        </article>

      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
/**
 * Knowledgebase article detail page.
 * Fetches a single article by ID and renders the full HTML body from WHMCS.
 */

import { ArrowLeft, BookOpen, Calendar, Eye, ThumbsUp, ThumbsDown } from 'lucide-vue-next'

const route = useRoute()
const localePath = useLocalePath()
const { t } = useI18n()

const { data: article, pending, error } = useFetch(
  () => `/api/portal/knowledgebase/${route.params.id}`
)

/** Local helpful vote state (UI only, no API submission) */
const vote = ref<'yes' | 'no' | null>(null)

watchEffect(() => {
  if ((article.value as any)?.title) {
    useSeoMeta({
      title: `${(article.value as any).title} — ${t('kb.title')}`,
      description: (article.value as any).excerpt
    })
  }
})
</script>
