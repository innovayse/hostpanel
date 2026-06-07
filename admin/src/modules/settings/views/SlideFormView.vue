<script setup lang="ts">
/**
 * Full-page form for creating or editing a homepage slide.
 *
 * Detects edit mode from route params, loads slide data when editing,
 * and includes locale-tabbed translations with dynamic feature lists.
 * Provides unsaved-changes guard on navigation away.
 */
import { ref, reactive, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter, onBeforeRouteLeave } from 'vue-router'
import { useSlidesStore } from '../stores/slidesStore'
import { useSettingsStore } from '../stores/settingsStore'
import ToggleSwitch from '@/components/ToggleSwitch.vue'
import { Icon } from '@iconify/vue'
import { VueDatePicker } from '@vuepic/vue-datepicker'
import '@vuepic/vue-datepicker/dist/main.css'
import type { CreateSlidePayload, SlideTranslation, UpdateSlidePayload } from '@/types/models'

const route = useRoute()
const router = useRouter()
const slidesStore = useSlidesStore()
const settingsStore = useSettingsStore()

/** Available locales for translations. Adding a new locale only requires extending this array. */
const LOCALES = ['en', 'ru', 'hy'] as const

/** Display names for each supported locale. */
const LOCALE_LABELS: Record<string, string> = {
  en: 'English',
  ru: 'Russian',
  hy: 'Armenian',
}

/** Target audience options. */
const AUDIENCE_OPTIONS = [
  { value: 'All', label: 'All' },
  { value: 'Guest', label: 'Guest' },
  { value: 'Authenticated', label: 'Authenticated' },
] as const

/** Preset icons for quick selection. Uses MDI icon names rendered via @iconify/vue. */
const ICON_PRESETS = [
  { name: 'cloud-check', label: 'Hosting' },
  { name: 'earth', label: 'Globe' },
  { name: 'brain', label: 'AI' },
  { name: 'chart-bar', label: 'Analytics' },
  { name: 'school', label: 'Education' },
  { name: 'office-building', label: 'Property' },
  { name: 'cart', label: 'E-Commerce' },
  { name: 'silverware-fork-knife', label: 'Restaurant' },
  { name: 'view-dashboard', label: 'Dashboard' },
  { name: 'rocket-launch', label: 'Launch' },
  { name: 'shield-check', label: 'Security' },
  { name: 'star', label: 'Premium' },
  { name: 'server', label: 'Server' },
  { name: 'cellphone', label: 'Mobile' },
  { name: 'code-tags', label: 'Code' },
  { name: 'palette', label: 'Design' },
  { name: 'headset', label: 'Support' },
  { name: 'currency-usd', label: 'Finance' },
  { name: 'camera', label: 'Media' },
  { name: 'heart-pulse', label: 'Health' },
] as const

/** Whether we are in edit mode (slide ID present in route). */
const isEditMode = computed(() => !!route.params.id)

/** Page title based on create/edit mode. */
const pageTitle = computed(() => isEditMode.value ? 'Edit Slide' : 'Create Slide')

/** True while loading existing slide data in edit mode. */
const loadingSlide = ref(false)

/** True while saving the form. */
const saving = ref(false)

/** Error message from save or load operations. */
const saveError = ref<string | null>(null)

/** Tracks whether the form has been modified since loading. */
const isDirty = ref(false)

/** Reactive translation data per locale. */
interface TranslationForm {
  /** Slide headline. */
  title: string
  /** Optional subtitle. */
  tagline: string
  /** Optional body text. */
  description: string
  /** Dynamic feature list. */
  features: string[]
  /** Optional CTA button label. */
  ctaText: string
  /** Optional CTA button link. */
  ctaUrl: string
}

// ── Form fields ──────────────────────────────────────────────

/** MDI icon name input value. */
const iconName = ref('')

/** Hex brand color input value. */
const brandColor = ref('#06b6d4')

/** Image URL input value. */
const imageUrl = ref('')

/** Optional demo link input value. */
const demoUrl = ref('')

/** Optional learn-more link input value. */
const learnMoreUrl = ref('')

/** Selected product ID (0 means none). */
const productId = ref<number>(0)

/** Display order input value. */
const sortOrder = ref(0)

/** Whether the slide is active. */
const isActive = ref(true)

/** Target audience selection. */
const targetAudience = ref('All')

/** Optional visibility start datetime. */
const visibleFrom = ref<Date | null>(null)

/** Optional visibility end datetime. */
const visibleUntil = ref<Date | null>(null)

/** True while an image is being uploaded. */
const uploading = ref(false)

/** True while dragging a file over the drop zone. */
const uploadDragging = ref(false)

/** Upload error message. */
const uploadError = ref<string | null>(null)

/** Currently active translation tab locale. */
const activeLocale = ref<string>('en')

/**
 * Creates a blank translation form object.
 *
 * @returns Empty translation form with default values.
 */
function createEmptyTranslation(): TranslationForm {
  return {
    title: '',
    tagline: '',
    description: '',
    features: [],
    ctaText: '',
    ctaUrl: '',
  }
}

/** Reactive translations keyed by locale code. */
const translations = reactive<Record<string, TranslationForm>>(
  Object.fromEntries(LOCALES.map(loc => [loc, createEmptyTranslation()])),
)


/**
 * Handles file selection from the file input.
 *
 * @param e - The input change event.
 */
async function handleFileSelect(e: Event): Promise<void> {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (file) await uploadImage(file)
  input.value = ''
}

/**
 * Handles file drop on the drop zone.
 *
 * @param e - The drag event.
 */
async function handleFileDrop(e: DragEvent): Promise<void> {
  uploadDragging.value = false
  const file = e.dataTransfer?.files?.[0]
  if (file) await uploadImage(file)
}

/**
 * Uploads an image file to the backend and sets the imageUrl.
 *
 * Uses raw fetch instead of useApi to avoid the default Content-Type: application/json
 * header, which would break multipart/form-data boundary detection.
 *
 * @param file - The image file to upload.
 */
async function uploadImage(file: File): Promise<void> {
  uploadError.value = null
  uploading.value = true
  try {
    const formData = new FormData()
    formData.append('file', file)

    const token = localStorage.getItem('admin_token')
    const headers: Record<string, string> = {}
    if (token) headers['Authorization'] = `Bearer ${token}`

    const response = await fetch('/api/admin/slides/upload-image', {
      method: 'POST',
      body: formData,
      headers,
    })

    if (!response.ok) {
      const body = await response.json().catch(() => null)
      throw new Error(body?.error ?? `Upload failed (${response.status})`)
    }

    const result: { url: string } = await response.json()
    imageUrl.value = result.url
  } catch (err) {
    uploadError.value = err instanceof Error ? err.message : 'Upload failed'
  } finally {
    uploading.value = false
  }
}

// ── Auto-fill Learn More URL from linked product ────────────

/** Whether a linked product is selected. */
const hasLinkedProduct = computed(() => !!productId.value)

watch(productId, (newId) => {
  if (!newId) {
    learnMoreUrl.value = ''
    return
  }
  const product = settingsStore.products.find(p => p.id === newId)
  if (product?.slug) {
    learnMoreUrl.value = `/products/${product.slug}`
  } else if (product?.name) {
    learnMoreUrl.value = `/products/${product.name.toLowerCase().replace(/\s+/g, '-')}`
  }
})

// ── Mark form dirty on any change ────────────────────────────

/**
 * Marks the form as dirty when any field changes.
 * Uses a deep watch on all reactive form state.
 */
watch(
  [iconName, brandColor, imageUrl, demoUrl, learnMoreUrl, productId, sortOrder, isActive, targetAudience, visibleFrom, visibleUntil],
  () => { isDirty.value = true },
  { deep: true },
)

watch(translations, () => { isDirty.value = true }, { deep: true })

// ── Unsaved changes guard ────────────────────────────────────

onBeforeRouteLeave((_to, _from, next) => {
  if (isDirty.value && !saving.value) {
    const leave = confirm('You have unsaved changes. Are you sure you want to leave?')
    next(leave)
  } else {
    next()
  }
})

// ── Load data on mount ───────────────────────────────────────

onMounted(async () => {
  await settingsStore.fetchProducts()

  if (isEditMode.value) {
    loadingSlide.value = true
    try {
      const id = Number(route.params.id)
      const slide = await slidesStore.fetchSlide(id)
      if (!slide) {
        saveError.value = `Slide #${id} not found.`
        return
      }

      iconName.value = slide.iconName
      brandColor.value = slide.brandColor
      imageUrl.value = slide.imageUrl
      demoUrl.value = slide.demoUrl ?? ''
      learnMoreUrl.value = slide.learnMoreUrl ?? ''
      productId.value = slide.productId ?? 0
      sortOrder.value = slide.sortOrder
      isActive.value = slide.isActive
      targetAudience.value = slide.targetAudience
      visibleFrom.value = slide.visibleFrom ? new Date(slide.visibleFrom) : null
      visibleUntil.value = slide.visibleUntil ? new Date(slide.visibleUntil) : null

      for (const locale of LOCALES) {
        translations[locale] = createEmptyTranslation()
      }
      for (const t of slide.translations) {
        if (translations[t.locale]) {
          translations[t.locale] = {
            title: t.title,
            tagline: t.tagline ?? '',
            description: t.description ?? '',
            features: t.features ? [...t.features] : [],
            ctaText: t.ctaText ?? '',
            ctaUrl: t.ctaUrl ?? '',
          }
        }
      }

      // Reset dirty flag after populating form
      isDirty.value = false
    } finally {
      loadingSlide.value = false
    }
  }
})

// ── Feature list management ──────────────────────────────────

/**
 * Adds a new empty feature input to the specified locale's feature list.
 *
 * @param locale - The locale code to add the feature to.
 */
function addFeature(locale: string): void {
  translations[locale].features.push('')
}

/**
 * Removes a feature from the specified locale's feature list by index.
 *
 * @param locale - The locale code to remove the feature from.
 * @param index - The index of the feature to remove.
 */
function removeFeature(locale: string, index: number): void {
  translations[locale].features.splice(index, 1)
}

// ── Save handler ─────────────────────────────────────────────

/**
 * Collects form data and dispatches create or update via the store.
 * Navigates back to the slides list on success.
 */
async function handleSave(): Promise<void> {
  saving.value = true
  saveError.value = null

  try {
    const collectedTranslations: SlideTranslation[] = []

    for (const locale of LOCALES) {
      const t = translations[locale]
      if (t.title.trim()) {
        collectedTranslations.push({
          locale,
          title: t.title.trim(),
          tagline: t.tagline.trim() || null,
          description: t.description.trim() || null,
          features: t.features.filter(f => f.trim()).length > 0
            ? t.features.filter(f => f.trim())
            : null,
          ctaText: t.ctaText.trim() || null,
          ctaUrl: t.ctaUrl.trim() || null,
        })
      }
    }

    const payload: CreateSlidePayload = {
      iconName: iconName.value,
      brandColor: brandColor.value,
      imageUrl: imageUrl.value,
      demoUrl: demoUrl.value || null,
      learnMoreUrl: learnMoreUrl.value || null,
      productId: productId.value || null,
      sortOrder: sortOrder.value,
      isActive: isActive.value,
      targetAudience: targetAudience.value,
      visibleFrom: visibleFrom.value?.toISOString() ?? null,
      visibleUntil: visibleUntil.value?.toISOString() ?? null,
      translations: collectedTranslations,
    }

    if (isEditMode.value) {
      const id = Number(route.params.id)
      await slidesStore.updateSlide(id, { ...payload, id } as UpdateSlidePayload)
    } else {
      await slidesStore.createSlide(payload)
    }

    isDirty.value = false
    router.push('/settings/slides')
  } catch (e) {
    saveError.value = e instanceof Error ? e.message : 'Failed to save slide.'
  } finally {
    saving.value = false
  }
}

/**
 * Navigates back to the slides list.
 * The unsaved changes guard will handle confirmation if needed.
 */
function handleCancel(): void {
  router.push('/settings/slides')
}
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Breadcrumb -->
    <nav class="flex items-center gap-1.5 text-sm mb-6">
      <router-link to="/settings" class="text-text-muted hover:text-primary-400 transition-colors">Settings</router-link>
      <svg class="w-3.5 h-3.5 text-text-muted/40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="9 18 15 12 9 6" /></svg>
      <router-link to="/settings/slides" class="text-text-muted hover:text-primary-400 transition-colors">Slides</router-link>
      <svg class="w-3.5 h-3.5 text-text-muted/40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="9 18 15 12 9 6" /></svg>
      <span class="text-text-primary font-medium">{{ pageTitle }}</span>
    </nav>

    <!-- Loading spinner for edit mode -->
    <div v-if="loadingSlide" class="flex items-center gap-3 text-text-secondary text-sm py-12">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading slide data...
    </div>

    <template v-else>

      <!-- Save error -->
      <div
        v-if="saveError"
        class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mb-5"
      >
        {{ saveError }}
      </div>

      <!-- Section 1: General Settings -->
      <div class="bg-surface-card border border-border rounded-xl p-6 mb-5">
        <h2 class="text-lg font-semibold text-text-primary mb-4">General Settings</h2>

        <div class="flex flex-col gap-4">

          <!-- Icon -->
          <div>
            <label class="block text-sm text-text-secondary mb-1.5">Icon</label>
            <div class="flex flex-wrap gap-1.5 mb-3">
              <button
                v-for="ic in ICON_PRESETS"
                :key="ic.name"
                type="button"
                class="flex flex-col items-center gap-1 w-16 py-2 rounded-lg border text-[0.65rem] transition-all"
                :class="iconName === ic.name
                  ? 'border-primary-500 bg-primary-500/10 text-primary-400'
                  : 'border-border bg-[#1a1a2e] text-text-muted hover:text-text-secondary hover:border-white/10'"
                :title="ic.name"
                @click="iconName = ic.name"
              >
                <Icon :icon="`mdi:${ic.name}`" class="text-lg" />
                <span class="truncate w-full text-center px-0.5">{{ ic.label }}</span>
              </button>
            </div>
            <div class="flex items-center gap-2.5">
              <div
                v-if="iconName"
                class="flex items-center justify-center w-9 h-9 rounded-lg border border-border bg-[#1a1a2e] shrink-0"
              >
                <Icon :icon="`mdi:${iconName}`" class="text-xl text-text-primary" />
              </div>
              <input
                v-model="iconName"
                type="text"
                placeholder="Or type MDI icon name (e.g. cloud-check)"
                class="flex-1 bg-[#1a1a2e] border border-border rounded-lg px-3 py-2 text-sm text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
            </div>
          </div>

          <!-- Brand Color -->
          <div>
            <label class="block text-sm text-text-secondary mb-1.5">Brand Color</label>
            <div class="flex items-center gap-2.5">
              <label class="relative w-9 h-9 rounded-lg border border-border shrink-0 cursor-pointer overflow-hidden group">
                <span class="absolute inset-0 rounded-lg" :style="{ backgroundColor: brandColor }" />
                <input
                  v-model="brandColor"
                  type="color"
                  class="absolute inset-0 w-full h-full opacity-0 cursor-pointer"
                />
                <span class="absolute inset-0 flex items-center justify-center opacity-0 group-hover:opacity-100 bg-black/30 transition-opacity">
                  <svg class="w-4 h-4 text-white" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M7 21h10M12 3v14m0 0l-4-4m4 4l4-4"/></svg>
                </span>
              </label>
              <input
                v-model="brandColor"
                type="text"
                placeholder="#06b6d4"
                class="flex-1 bg-[#1a1a2e] border border-border rounded-lg px-3 py-2 text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors font-mono text-sm"
              />
            </div>
          </div>

          <!-- Image -->
          <div>
            <label class="block text-sm text-text-secondary mb-1.5">Image</label>
            <!-- Preview + Upload area -->
            <div class="flex items-start gap-4">
              <!-- Preview -->
              <div
                v-if="imageUrl"
                class="relative w-32 h-20 rounded-lg border border-border overflow-hidden shrink-0 group"
              >
                <img :src="imageUrl" :alt="'Slide preview'" class="w-full h-full object-cover" />
                <button
                  type="button"
                  class="absolute top-1 right-1 w-5 h-5 flex items-center justify-center rounded-full bg-black/60 text-white/80 hover:text-white opacity-0 group-hover:opacity-100 transition-opacity"
                  title="Remove image"
                  @click="imageUrl = ''"
                >
                  <svg class="w-3 h-3" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" /></svg>
                </button>
              </div>

              <div class="flex-1">
                <!-- Drop zone -->
                <label
                  class="flex flex-col items-center justify-center w-full h-24 border-2 border-dashed rounded-lg cursor-pointer transition-colors"
                  :class="uploadDragging
                    ? 'border-primary-500 bg-primary-500/5'
                    : 'border-border hover:border-white/20 bg-[#1a1a2e]'"
                  @dragover.prevent="uploadDragging = true"
                  @dragleave.prevent="uploadDragging = false"
                  @drop.prevent="handleFileDrop"
                >
                  <div v-if="uploading" class="flex items-center gap-2 text-sm text-text-muted">
                    <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
                    Uploading...
                  </div>
                  <div v-else class="flex flex-col items-center gap-1">
                    <svg class="w-6 h-6 text-text-muted" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5"/></svg>
                    <span class="text-xs text-text-muted">Drop image or <span class="text-primary-400">browse</span></span>
                    <span class="text-[0.65rem] text-text-muted/60">JPEG, PNG, WebP, SVG — max 5MB</span>
                  </div>
                  <input
                    type="file"
                    accept="image/jpeg,image/png,image/webp,image/gif,image/svg+xml"
                    class="hidden"
                    @change="handleFileSelect"
                  />
                </label>

                <!-- Or manual URL -->
                <input
                  v-model="imageUrl"
                  type="text"
                  placeholder="Or paste image URL..."
                  class="w-full mt-2 bg-[#1a1a2e] border border-border rounded-lg px-3 py-1.5 text-xs text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 transition-colors"
                />
              </div>
            </div>
            <p v-if="uploadError" class="text-xs text-status-red mt-1">{{ uploadError }}</p>
          </div>

          <!-- Demo URL -->
          <div>
            <label class="block text-sm text-text-secondary mb-1.5">Demo URL</label>
            <input
              v-model="demoUrl"
              type="text"
              placeholder="https://demo.example.com"
              class="w-full bg-[#1a1a2e] border border-border rounded-lg px-3 py-2 text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            />
            <p class="text-[0.7rem] text-text-muted mt-1">Optional "Try Demo" button link on the slide</p>
          </div>

          <!-- Learn More URL -->
          <div v-if="!hasLinkedProduct">
            <label class="block text-sm text-text-secondary mb-1.5">Learn More URL</label>
            <input
              v-model="learnMoreUrl"
              type="text"
              placeholder="/products/example or https://example.com"
              class="w-full bg-[#1a1a2e] border border-border rounded-lg px-3 py-2 text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            />
            <p class="text-[0.7rem] text-text-muted mt-1">Optional secondary link shown as "Learn More" on the slide</p>
          </div>

          <!-- Product & Sort Order -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label class="block text-sm text-text-secondary mb-1.5">Linked Product</label>
              <select
                v-model.number="productId"
                class="w-full bg-[#1a1a2e] border border-border rounded-lg px-3 py-2 text-text-primary focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors appearance-none"
              >
                <option :value="0">None</option>
                <option v-for="p in settingsStore.products" :key="p.id" :value="p.id">
                  {{ p.name }}
                </option>
              </select>
            </div>
            <div>
              <label class="block text-sm text-text-secondary mb-1.5">Sort Order</label>
              <input
                v-model.number="sortOrder"
                type="number"
                min="0"
                class="w-full bg-[#1a1a2e] border border-border rounded-lg px-3 py-2 text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
            </div>
          </div>

          <!-- Active Toggle -->
          <div class="flex items-center justify-between py-1">
            <div>
              <p class="text-sm text-text-primary font-medium">Active</p>
              <p class="text-xs text-text-muted">Show this slide to visitors</p>
            </div>
            <ToggleSwitch v-model="isActive" />
          </div>

          <!-- Target Audience -->
          <div>
            <label class="block text-sm text-text-secondary mb-2">Target Audience</label>
            <div class="flex items-center gap-4">
              <label
                v-for="opt in AUDIENCE_OPTIONS"
                :key="opt.value"
                class="flex items-center gap-1.5 cursor-pointer"
              >
                <input
                  v-model="targetAudience"
                  type="radio"
                  :value="opt.value"
                  class="w-3.5 h-3.5 accent-primary-500"
                />
                <span class="text-sm text-text-secondary">{{ opt.label }}</span>
              </label>
            </div>
          </div>

          <!-- Visibility Window -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label class="block text-sm text-text-secondary mb-1.5">Visible From</label>
              <VueDatePicker
                v-model="visibleFrom"
                :enable-time-picker="true"
                :clearable="true"
                :dark="true"
                :auto-apply="true"
                placeholder="No start date"
                input-class-name="dp-input-dark"
              />
            </div>
            <div>
              <label class="block text-sm text-text-secondary mb-1.5">Visible Until</label>
              <VueDatePicker
                v-model="visibleUntil"
                :enable-time-picker="true"
                :clearable="true"
                :dark="true"
                :auto-apply="true"
                :min-date="visibleFrom ?? undefined"
                placeholder="No end date"
                input-class-name="dp-input-dark"
              />
            </div>
          </div>
        </div>
      </div>

      <!-- Section 2: Translations -->
      <div class="bg-surface-card border border-border rounded-xl p-6 mb-24">
        <h2 class="text-lg font-semibold text-text-primary mb-4">Translations</h2>

        <!-- Locale Tabs -->
        <div class="flex items-center gap-0 border-b border-border mb-5">
          <button
            v-for="locale in LOCALES"
            :key="locale"
            type="button"
            class="px-4 py-2.5 text-sm font-medium transition-colors -mb-px"
            :class="activeLocale === locale
              ? 'border-b-2 border-primary-500 text-primary-400'
              : 'text-text-muted hover:text-text-secondary'"
            @click="activeLocale = locale"
          >
            {{ LOCALE_LABELS[locale] }}
          </button>
        </div>

        <!-- Translation Fields (per active locale) -->
        <div class="flex flex-col gap-4">

          <!-- Title -->
          <div>
            <label class="block text-sm text-text-secondary mb-1.5">Title</label>
            <input
              v-model="translations[activeLocale].title"
              type="text"
              placeholder="Slide title"
              class="w-full bg-[#1a1a2e] border border-border rounded-lg px-3 py-2 text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            />
          </div>

          <!-- Tagline -->
          <div>
            <label class="block text-sm text-text-secondary mb-1.5">Tagline</label>
            <input
              v-model="translations[activeLocale].tagline"
              type="text"
              placeholder="Short subtitle"
              class="w-full bg-[#1a1a2e] border border-border rounded-lg px-3 py-2 text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            />
          </div>

          <!-- Description -->
          <div>
            <label class="block text-sm text-text-secondary mb-1.5">Description</label>
            <textarea
              v-model="translations[activeLocale].description"
              rows="3"
              placeholder="Slide body text..."
              class="w-full bg-[#1a1a2e] border border-border rounded-lg px-3 py-2 text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors resize-none"
            />
          </div>

          <!-- Features -->
          <div>
            <label class="block text-sm text-text-secondary mb-1.5">Features</label>
            <div class="flex flex-col gap-2">
              <div
                v-for="(_, fIndex) in translations[activeLocale].features"
                :key="fIndex"
                class="flex items-center gap-2"
              >
                <input
                  v-model="translations[activeLocale].features[fIndex]"
                  type="text"
                  :placeholder="`Feature ${fIndex + 1}`"
                  class="flex-1 bg-[#1a1a2e] border border-border rounded-lg px-3 py-2 text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
                />
                <button
                  type="button"
                  class="w-7 h-7 flex items-center justify-center rounded-lg text-text-muted hover:text-red-400 hover:bg-red-500/10 transition-colors shrink-0"
                  title="Remove feature"
                  @click="removeFeature(activeLocale, fIndex)"
                >
                  <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" />
                  </svg>
                </button>
              </div>
              <button
                type="button"
                class="self-start flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-primary-400 hover:text-primary-300 hover:bg-primary-500/10 rounded-lg transition-colors"
                @click="addFeature(activeLocale)"
              >
                <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" />
                </svg>
                Add Feature
              </button>
            </div>
          </div>

          <!-- CTA Text & CTA URL -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label class="block text-sm text-text-secondary mb-1.5">CTA Text</label>
              <input
                v-model="translations[activeLocale].ctaText"
                type="text"
                placeholder="Get Started"
                class="w-full bg-[#1a1a2e] border border-border rounded-lg px-3 py-2 text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
            </div>
            <div>
              <label class="block text-sm text-text-secondary mb-1.5">CTA URL</label>
              <input
                v-model="translations[activeLocale].ctaUrl"
                type="text"
                placeholder="/order/product"
                class="w-full bg-[#1a1a2e] border border-border rounded-lg px-3 py-2 text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
            </div>
          </div>
        </div>
      </div>

      <!-- Sticky Footer -->
      <div class="fixed bottom-0 left-0 right-0 bg-surface-card/95 backdrop-blur-sm border-t border-border px-6 py-4 z-30">
        <div class="flex items-center justify-end gap-3">
          <button
            type="button"
            class="px-5 py-2.5 text-sm font-medium text-text-secondary hover:text-text-primary bg-transparent border border-border rounded-[10px] transition-colors"
            @click="handleCancel"
          >
            Cancel
          </button>
          <button
            type="button"
            :disabled="saving"
            class="gradient-brand px-6 py-2.5 text-sm font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
            @click="handleSave"
          >
            {{ saving ? 'Saving...' : isEditMode ? 'Save Changes' : 'Create Slide' }}
          </button>
        </div>
      </div>

    </template>

  </div>
</template>

<style>
.dp-input-dark {
  background-color: #1a1a2e !important;
  border-color: var(--border) !important;
  color: var(--text-primary) !important;
  border-radius: 0.5rem !important;
  padding: 0.5rem 0.75rem !important;
  font-size: 0.875rem !important;
}
.dp-input-dark::placeholder {
  color: var(--text-muted) !important;
}
</style>
