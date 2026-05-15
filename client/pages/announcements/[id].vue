<template>
  <div>
    <section class="py-12 md:py-20 bg-[#0a0a0f] min-h-screen">
      <div class="container-custom max-w-3xl">

        <!-- Back link -->
        <NuxtLink
          :to="localePath('/announcements')"
          class="inline-flex items-center gap-2 text-sm text-gray-400 hover:text-white transition-colors mb-8"
        >
          <ArrowLeft :size="15" :stroke-width="2" />
          {{ $t('announcements.backToList') }}
        </NuxtLink>

        <!-- Loading -->
        <div v-if="pending" class="space-y-4">
          <div class="h-8 w-2/3 rounded-xl bg-white/5 animate-pulse" />
          <div class="h-4 w-1/4 rounded-lg bg-white/5 animate-pulse" />
          <div class="mt-8 space-y-3">
            <div v-for="i in 6" :key="i" class="h-4 rounded bg-white/5 animate-pulse" :style="{ width: `${85 - i * 5}%` }" />
          </div>
        </div>

        <!-- Error / not found -->
        <div v-else-if="error || !item" class="text-center py-20">
          <Megaphone :size="48" :stroke-width="1.5" class="text-gray-600 mx-auto mb-4" />
          <p class="text-gray-400">{{ $t('announcements.errorDesc') }}</p>
        </div>

        <!-- Content -->
        <article v-else>
          <!-- Date -->
          <div class="flex items-center gap-2 text-xs text-gray-500 mb-4">
            <Calendar :size="13" :stroke-width="2" />
            {{ $t('announcements.published') }}: {{ item.date }}
          </div>

          <!-- Title -->
          <h1 class="text-3xl md:text-4xl font-bold text-white mb-8 leading-tight">
            {{ item.title }}
          </h1>

          <!-- Divider -->
          <div class="h-px bg-white/10 mb-8" />

          <!-- Body (HTML from WHMCS) -->
          <div
            class="prose prose-invert prose-sm md:prose-base max-w-none
                   prose-p:text-gray-300 prose-p:leading-relaxed
                   prose-a:text-primary-400 prose-a:no-underline hover:prose-a:underline
                   prose-headings:text-white prose-strong:text-white
                   prose-ul:text-gray-300 prose-ol:text-gray-300
                   prose-blockquote:border-primary-500 prose-blockquote:text-gray-400"
            v-html="item.body"
          />
        </article>

      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
/**
 * Public announcement detail page.
 * Loads a single announcement by id from WHMCS and renders the full HTML body.
 */

import { ArrowLeft, Calendar, Megaphone } from 'lucide-vue-next'

const route = useRoute()
const localePath = useLocalePath()
const { t } = useI18n()

/** Fetch full announcements list and find the one matching the route id */
const { data, pending, error } = useFetch('/api/portal/client/announcements')

const item = computed(() => {
  const id = String(route.params.id)
  return (data.value as any)?.items?.find((a: any) => String(a.id) === id) ?? null
})

watchEffect(() => {
  if (item.value) {
    useSeoMeta({
      title: item.value.title,
      description: item.value.excerpt
    })
  }
})
</script>
