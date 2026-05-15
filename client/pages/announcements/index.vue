<template>
  <div>
    <!-- Hero -->
    <section class="relative py-16 md:py-24 bg-[#0a0a0f] overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute top-0 left-1/4 w-[400px] h-[400px] bg-primary-500/15 rounded-full blur-[130px]" />
        <div class="absolute bottom-0 right-1/3 w-[300px] h-[300px] bg-cyan-500/10 rounded-full blur-[110px]" />
      </div>
      <div class="container-custom relative z-10 text-center max-w-2xl mx-auto">
        <div class="inline-flex items-center gap-2 px-4 py-2 mb-6 rounded-full glass border border-primary-500/20 backdrop-blur-sm">
          <Megaphone :size="14" :stroke-width="2" class="text-primary-400" />
          <span class="text-sm font-medium text-gray-300">{{ $t('announcements.badge') }}</span>
        </div>
        <h1 class="text-4xl md:text-5xl font-bold text-white mb-4">{{ $t('announcements.title') }}</h1>
        <p class="text-gray-400 leading-relaxed">{{ $t('announcements.subtitle') }}</p>
      </div>
    </section>

    <!-- Content -->
    <section class="py-12 bg-[#0a0a0f]">
      <div class="container-custom max-w-3xl">

        <!-- Month filter -->
        <div v-if="months.length > 1" class="flex flex-wrap gap-2 mb-8">
          <button
            v-for="m in months"
            :key="m"
            class="px-3 py-1.5 rounded-lg text-sm font-medium transition-colors border"
            :class="activeMonth === m
              ? 'bg-primary-500/10 text-primary-400 border-primary-500/20'
              : 'text-gray-400 hover:text-white hover:bg-white/5 border-transparent'"
            @click="activeMonth = m"
          >
            {{ m === 'all' ? $t('announcements.allMonths') : m }}
          </button>
        </div>

        <!-- Loading -->
        <div v-if="pending" class="space-y-4">
          <div v-for="i in 4" :key="i" class="h-40 rounded-2xl bg-white/5 border border-white/10 animate-pulse" />
        </div>

        <!-- Error -->
        <div v-else-if="error" class="p-6 rounded-2xl border border-red-500/30 bg-red-500/10 text-red-400 text-sm">
          {{ $t('announcements.errorDesc') }}
        </div>

        <!-- Empty -->
        <div v-else-if="!filtered.length" class="text-center py-24">
          <Megaphone :size="48" :stroke-width="1.5" class="text-gray-600 mx-auto mb-4" />
          <p class="text-gray-400">{{ $t('announcements.empty') }}</p>
        </div>

        <!-- List -->
        <div v-else class="space-y-4">
          <NuxtLink
            v-for="item in filtered"
            :key="item.id"
            :to="localePath(`/announcements/${item.id}`)"
            class="block p-6 rounded-2xl bg-white/5 border border-white/10 hover:border-primary-500/30 hover:bg-white/8 transition-all duration-200 group"
          >
            <div class="flex items-center gap-2 text-xs text-gray-500 mb-3">
              <Calendar :size="13" :stroke-width="2" />
              {{ item.date }}
            </div>
            <h2 class="text-lg font-semibold text-white group-hover:text-primary-400 transition-colors mb-2">
              {{ item.title }}
            </h2>
            <p class="text-sm text-gray-400 leading-relaxed line-clamp-3 mb-4">
              {{ item.excerpt }}
            </p>
            <span class="inline-flex items-center gap-1.5 text-xs font-medium text-primary-400">
              {{ $t('announcements.readMore') }}
              <ArrowRight :size="13" :stroke-width="2" class="group-hover:translate-x-1 transition-transform" />
            </span>
          </NuxtLink>
        </div>

      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
/**
 * Public announcements list page.
 * Fetches all announcements from WHMCS and supports month-based filtering.
 */

import { Megaphone, Calendar, ArrowRight } from 'lucide-vue-next'

const localePath = useLocalePath()
const { t } = useI18n()

useSeoMeta({
  title: t('announcements.title'),
  description: t('announcements.subtitle')
})

const { data, pending, error } = useFetch('/api/portal/client/announcements')

/** All announcement items from API */
const items = computed(() => (data.value as any)?.items ?? [])

/**
 * Unique month labels extracted from announcement dates.
 * Prepends "all" when there is more than one distinct month.
 */
const months = computed(() => {
  const seen = new Set<string>()
  for (const item of items.value) {
    const match = (item.date as string).match(/(\w+ \d{4})$/)
    if (match) seen.add(match[1])
  }
  return seen.size > 1 ? ['all', ...Array.from(seen)] : []
})

const activeMonth = ref('all')

/** Announcements filtered by the active month selection */
const filtered = computed(() => {
  if (activeMonth.value === 'all') return items.value
  return items.value.filter((item: any) => item.date?.includes(activeMonth.value))
})
</script>
