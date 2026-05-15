<template>
  <!-- Knowledgebase article list row -->
  <NuxtLink
    :to="localePath(`/knowledgebase/${article.id}`)"
    class="flex items-start gap-3 px-5 py-4 bg-white/5 hover:bg-white/8 transition-colors group"
  >
    <FileText :size="16" :stroke-width="2" class="text-primary-400 mt-0.5 flex-shrink-0" />
    <div class="min-w-0">
      <div class="text-sm font-medium text-primary-400 group-hover:text-primary-300 transition-colors truncate">
        {{ article.title }}
      </div>
      <div v-if="article.excerpt" class="text-xs text-gray-500 mt-0.5 line-clamp-1">
        {{ article.excerpt }}…
      </div>
    </div>
    <div v-if="article.views" class="ml-auto text-xs text-gray-600 flex-shrink-0 flex items-center gap-1">
      <Eye :size="12" :stroke-width="2" />
      {{ article.views }}
    </div>
  </NuxtLink>
</template>

<script setup lang="ts">
/**
 * Reusable article list row for the knowledgebase index and category pages.
 * Displays title, short excerpt, and view count.
 */

import { FileText, Eye } from 'lucide-vue-next'

/** Article data prop */
defineProps<{
  article: {
    id: string | number
    title: string
    excerpt?: string
    views?: number
  }
}>()

const localePath = useLocalePath()
</script>
