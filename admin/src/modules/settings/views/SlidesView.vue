<script setup lang="ts">
/**
 * Slides settings view — list and manage homepage slides.
 *
 * Displays slides in a dark-themed table ordered by sort order,
 * with drag-and-drop reordering via vuedraggable, audience/status badges,
 * and navigation to dedicated create/edit pages.
 */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import draggable from 'vuedraggable'
import { useSlidesStore } from '../stores/slidesStore'
import type { Slide } from '@/types/models'

const router = useRouter()
const store = useSlidesStore()

/** Local mutable copy of slides for drag-and-drop reordering. */
const localSlides = ref<Slide[]>([])

/** True while saving new order after drag. */
const savingOrder = ref(false)

onMounted(async () => {
  await store.fetchSlides()
  syncLocal()
})

/**
 * Syncs the local draggable list from the store (sorted by sortOrder).
 */
function syncLocal(): void {
  localSlides.value = [...store.slides].sort((a, b) => a.sortOrder - b.sortOrder)
}

/** Audience badge style classes keyed by target audience value. */
const audienceClasses: Record<string, string> = {
  All: 'text-text-muted bg-white/[0.04] border border-border',
  Guest: 'text-primary-400 bg-primary-400/10 border border-primary-400/20',
  Authenticated: 'text-status-green bg-status-green/10 border border-status-green/20',
}

/**
 * Navigates to the create slide page.
 */
function navigateCreate(): void {
  router.push('/settings/slides/create')
}

/**
 * Navigates to the edit page for the given slide.
 *
 * @param slide - The slide to edit.
 */
function navigateEdit(slide: Slide): void {
  router.push(`/settings/slides/${slide.id}/edit`)
}

/**
 * Confirms and deletes a slide.
 *
 * @param slide - The slide to delete.
 */
async function handleDelete(slide: Slide): Promise<void> {
  const title = slide.translations[0]?.title ?? `Slide #${slide.id}`
  if (!confirm(`Delete "${title}"? This action cannot be undone.`)) return
  await store.deleteSlide(slide.id)
  syncLocal()
}

/**
 * Called after drag-and-drop ends. Saves new sort order to the backend.
 */
async function onDragEnd(): Promise<void> {
  savingOrder.value = true
  try {
    const items = localSlides.value.map((slide, index) => ({
      id: slide.id,
      sortOrder: index,
    }))
    await store.updateOrder(items)
    syncLocal()
  } finally {
    savingOrder.value = false
  }
}

/**
 * Formats a date range for display.
 *
 * @param from - ISO date string or null.
 * @param until - ISO date string or null.
 * @returns Formatted date range or "Always".
 */
function formatDateRange(from: string | null, until: string | null): string {
  if (!from && !until) return 'Always'
  const fmt = (d: string) => new Date(d).toLocaleDateString()
  if (from && until) return `${fmt(from)} — ${fmt(until)}`
  if (from) return `From ${fmt(from)}`
  return `Until ${fmt(until!)}`
}
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="flex items-center justify-between mb-7">
      <div>
        <h1 class="font-display text-[1.75rem] font-bold text-text-primary tracking-tight leading-none mb-1.5">
          Slides
        </h1>
        <p class="text-sm text-text-secondary">Manage homepage hero slides — drag to reorder</p>
      </div>
      <div class="flex items-center gap-2">
        <span
          v-if="savingOrder"
          class="text-xs text-text-muted flex items-center gap-1.5"
        >
          <span class="w-3 h-3 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
          Saving order...
        </span>
        <button
          class="gradient-brand text-white rounded-[10px] px-5 py-2.5 text-[0.85rem] font-semibold transition-all duration-150 hover:-translate-y-px"
          @click="navigateCreate"
        >
          Add Slide
        </button>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading slides...
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ store.error }}
    </div>

    <!-- Slides table -->
    <div v-else class="bg-surface-card border border-border rounded-2xl overflow-hidden">
      <table class="w-full text-sm">
        <thead>
          <tr class="border-b border-border">
            <th class="w-10 px-3 py-3.5"></th>
            <th class="px-4 py-3.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Image</th>
            <th class="px-4 py-3.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Title</th>
            <th class="px-4 py-3.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Audience</th>
            <th class="px-4 py-3.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Status</th>
            <th class="px-4 py-3.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Visibility</th>
            <th class="px-4 py-3.5 text-right text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Actions</th>
          </tr>
        </thead>
        <draggable
          v-model="localSlides"
          tag="tbody"
          item-key="id"
          handle=".drag-handle"
          ghost-class="drag-ghost"
          :animation="200"
          class="divide-y divide-border"
          @end="onDragEnd"
        >
          <template #item="{ element: slide }">
            <tr class="hover:bg-white/[0.02] transition-colors">
              <!-- Drag handle -->
              <td class="px-3 py-3.5">
                <div class="drag-handle cursor-grab active:cursor-grabbing flex items-center justify-center w-6 h-6 rounded text-text-muted/40 hover:text-text-muted transition-colors">
                  <svg class="w-4 h-4" viewBox="0 0 24 24" fill="currentColor">
                    <circle cx="9" cy="5" r="1.5" /><circle cx="15" cy="5" r="1.5" />
                    <circle cx="9" cy="12" r="1.5" /><circle cx="15" cy="12" r="1.5" />
                    <circle cx="9" cy="19" r="1.5" /><circle cx="15" cy="19" r="1.5" />
                  </svg>
                </div>
              </td>

              <!-- Thumbnail -->
              <td class="px-4 py-3.5">
                <div
                  v-if="slide.imageUrl"
                  class="w-10 h-10 rounded-lg border border-border overflow-hidden bg-[#1a1a2e]"
                >
                  <img
                    :src="slide.imageUrl"
                    :alt="slide.translations[0]?.title ?? 'Slide'"
                    class="w-full h-full object-cover"
                  />
                </div>
                <div
                  v-else
                  class="w-10 h-10 rounded-lg border border-border bg-[#1a1a2e] flex items-center justify-center"
                >
                  <svg class="w-4 h-4 text-text-muted" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><rect x="3" y="3" width="18" height="18" rx="2" /><circle cx="8.5" cy="8.5" r="1.5" /><path d="m21 15-5-5L5 21" /></svg>
                </div>
              </td>

              <!-- Title -->
              <td class="px-4 py-3.5">
                <span class="font-medium text-text-primary">
                  {{ slide.translations[0]?.title ?? '—' }}
                </span>
                <div v-if="slide.translations[0]?.tagline" class="text-[0.72rem] text-text-muted mt-0.5">
                  {{ slide.translations[0].tagline }}
                </div>
              </td>

              <!-- Audience -->
              <td class="px-4 py-3.5">
                <span
                  class="text-[0.65rem] font-semibold rounded-full px-2.5 py-1"
                  :class="audienceClasses[slide.targetAudience] ?? audienceClasses['All']"
                >
                  {{ slide.targetAudience }}
                </span>
              </td>

              <!-- Active -->
              <td class="px-4 py-3.5">
                <span
                  class="text-[0.65rem] font-semibold rounded-full px-2.5 py-1"
                  :class="slide.isActive
                    ? 'text-status-green bg-status-green/10 border border-status-green/20'
                    : 'text-text-muted bg-white/[0.04] border border-border'"
                >
                  {{ slide.isActive ? 'Active' : 'Inactive' }}
                </span>
              </td>

              <!-- Date Range -->
              <td class="px-4 py-3.5 text-text-secondary text-xs">
                {{ formatDateRange(slide.visibleFrom, slide.visibleUntil) }}
              </td>

              <!-- Actions -->
              <td class="px-4 py-3.5 text-right">
                <div class="flex items-center justify-end gap-1">
                  <button
                    class="text-text-muted hover:text-primary-400 transition-colors p-1"
                    title="Edit slide"
                    @click="navigateEdit(slide)"
                  >
                    <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
                      <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
                    </svg>
                  </button>
                  <button
                    class="text-text-muted hover:text-status-red transition-colors p-1"
                    title="Delete slide"
                    @click="handleDelete(slide)"
                  >
                    <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <path d="M3 6h18"/>
                      <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>
                      <line x1="10" y1="11" x2="10" y2="17"/>
                      <line x1="14" y1="11" x2="14" y2="17"/>
                    </svg>
                  </button>
                </div>
              </td>
            </tr>
          </template>
        </draggable>
        <tbody v-if="localSlides.length === 0">
          <tr>
            <td colspan="7" class="px-5 py-8 text-center text-text-muted">
              No slides found. Add your first slide to get started.
            </td>
          </tr>
        </tbody>
      </table>
    </div>

  </div>
</template>

<style scoped>
.drag-ghost {
  opacity: 0.4;
  background: rgba(14, 165, 233, 0.05);
}
</style>
