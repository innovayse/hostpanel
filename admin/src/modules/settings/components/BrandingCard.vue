<script setup lang="ts">
/**
 * Branding card — uploads the storefront's logo and favicon.
 *
 * Mirrors the slide image uploader (`SlideFormView.vue`): drag-and-drop or
 * browse, uploaded straight to disk via a dedicated endpoint that returns a
 * URL, which is what actually gets saved to the setting. The setting itself
 * still accepts a pasted URL too, for an operator who already hosts the file
 * elsewhere and would rather not upload a copy.
 */
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { Setting } from '../../../types/models'

const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
  /** True while a save is in flight. */
  saving?: boolean
}>()

const emit = defineEmits<{ save: [id: number, value: string] }>()

const { t } = useI18n()

/** One branding image field: which setting key it saves to, and upload constraints. */
interface BrandingField {
  key: string
  kind: 'logo' | 'favicon'
  labelKey: string
  hintKey: string
  accept: string
  /** Preview box size — the favicon is square and small, the logo wide and short. */
  previewClass: string
}

const FIELDS: BrandingField[] = [
  {
    key: 'portal.logo',
    kind: 'logo',
    labelKey: 'settings.branding.logo.label',
    hintKey: 'settings.branding.logo.hint',
    accept: 'image/jpeg,image/png,image/webp,image/gif,image/svg+xml',
    previewClass: 'w-32 h-14',
  },
  {
    key: 'portal.favicon',
    kind: 'favicon',
    labelKey: 'settings.branding.favicon.label',
    hintKey: 'settings.branding.favicon.hint',
    accept: 'image/png,image/svg+xml,image/x-icon',
    previewClass: 'w-10 h-10',
  },
]

/** Per-field UI state, keyed by setting key. */
const draft = ref<Record<string, string>>({})
const uploading = ref<Record<string, boolean>>({})
const dragging = ref<Record<string, boolean>>({})
const uploadError = ref<Record<string, string | null>>({})

/**
 * The stored setting row for a field, if it has been seeded.
 *
 * @param field - The branding field.
 */
function settingOf(field: BrandingField): Setting | undefined {
  return props.settings.find(s => s.key === field.key)
}

/**
 * Current value shown for a field: the operator's unsaved edit, or what is stored.
 *
 * @param field - The branding field.
 */
function valueOf(field: BrandingField): string {
  return draft.value[field.key] ?? settingOf(field)?.value ?? ''
}

/**
 * Whether a field has an edit worth saving.
 *
 * @param field - The branding field.
 */
function isDirty(field: BrandingField): boolean {
  return field.key in draft.value && draft.value[field.key] !== (settingOf(field)?.value ?? '')
}

/**
 * Saves one field. No-op when the setting row does not exist yet or nothing changed.
 *
 * @param field - The branding field.
 */
function save(field: BrandingField): void {
  const setting = settingOf(field)
  if (!setting || !isDirty(field)) return
  // isDirty confirmed field.key is a present, defined entry in draft.
  emit('save', setting.id, draft.value[field.key]!)
}

/**
 * Uploads a file for one field and stages the returned URL as its draft.
 *
 * Raw fetch rather than the JSON-only API helper: a FormData body needs the
 * browser to set its own multipart boundary, which a fixed
 * `Content-Type: application/json` header would break.
 *
 * @param field - The branding field being uploaded for.
 * @param file - The image file to upload.
 */
async function uploadFile(field: BrandingField, file: File): Promise<void> {
  uploadError.value[field.key] = null
  uploading.value[field.key] = true
  try {
    const formData = new FormData()
    formData.append('file', file)

    const response = await fetch(`/api/admin/settings/branding/${field.kind}`, {
      method: 'POST',
      body: formData,
      headers: { 'X-Requested-With': 'XMLHttpRequest' },
    })

    if (!response.ok) {
      const body = await response.json().catch(() => null)
      throw new Error(body?.error ?? `Upload failed (${response.status})`)
    }

    const result: { url: string } = await response.json()
    draft.value[field.key] = result.url
  } catch (err) {
    uploadError.value[field.key] = err instanceof Error ? err.message : 'Upload failed'
  } finally {
    uploading.value[field.key] = false
  }
}

/**
 * Handles a file picked via the hidden input.
 *
 * @param field - The branding field.
 * @param e - The input change event.
 */
function onFileSelect(field: BrandingField, e: Event): void {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (file) void uploadFile(field, file)
  input.value = ''
}

/**
 * Handles a file dropped on the drop zone.
 *
 * @param field - The branding field.
 * @param e - The drag event.
 */
function onFileDrop(field: BrandingField, e: DragEvent): void {
  dragging.value[field.key] = false
  const file = e.dataTransfer?.files?.[0]
  if (file) void uploadFile(field, file)
}
</script>

<template>
  <section class="bg-surface-card border border-border rounded-2xl p-6 mb-6">
    <h2 class="font-display text-lg font-bold text-text-primary">{{ t('settings.branding.title') }}</h2>
    <p class="text-sm text-text-secondary mt-1">{{ t('settings.branding.description') }}</p>

    <div class="mt-5 flex flex-col gap-6">
      <div v-for="field in FIELDS" :key="field.key">
        <label class="block text-sm font-medium text-text-secondary mb-1.5">{{ t(field.labelKey) }}</label>

        <div class="flex items-start gap-4">
          <!-- Preview -->
          <div
            v-if="valueOf(field)"
            class="relative rounded-lg border border-border overflow-hidden shrink-0 bg-[#1a1a2e] flex items-center justify-center group"
            :class="field.previewClass"
          >
            <img :src="valueOf(field)" :alt="t(field.labelKey)" class="max-w-full max-h-full object-contain" />
            <button
              type="button"
              class="absolute top-1 right-1 w-5 h-5 flex items-center justify-center rounded-full bg-black/60 text-white/80 hover:text-white opacity-0 group-hover:opacity-100 transition-opacity"
              @click="draft[field.key] = ''"
            >
              <svg class="w-3 h-3" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" /></svg>
            </button>
          </div>

          <div class="flex-1 min-w-[220px]">
            <!-- Drop zone -->
            <label
              class="flex flex-col items-center justify-center w-full h-20 border-2 border-dashed rounded-lg cursor-pointer transition-colors"
              :class="dragging[field.key]
                ? 'border-primary-500 bg-primary-500/5'
                : 'border-border hover:border-white/20 bg-[#1a1a2e]'"
              @dragover.prevent="dragging[field.key] = true"
              @dragleave.prevent="dragging[field.key] = false"
              @drop.prevent="onFileDrop(field, $event)"
            >
              <div v-if="uploading[field.key]" class="flex items-center gap-2 text-sm text-text-muted">
                <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
                {{ t('settings.branding.uploading') }}
              </div>
              <div v-else class="flex flex-col items-center gap-1">
                <svg class="w-5 h-5 text-text-muted" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5"/></svg>
                <span class="text-xs text-text-muted">{{ t('settings.branding.dropOrBrowsePrefix') }} <span class="text-primary-400">{{ t('settings.branding.browse') }}</span></span>
                <span class="text-[0.65rem] text-text-muted/60">{{ t(field.hintKey) }}</span>
              </div>
              <input
                type="file"
                :accept="field.accept"
                class="hidden"
                @change="onFileSelect(field, $event)"
              />
            </label>

            <div class="flex items-center gap-2 mt-2">
              <input
                :value="valueOf(field)"
                type="text"
                :placeholder="t('settings.branding.pasteUrl')"
                class="flex-1 bg-[#1a1a2e] border border-border rounded-lg px-3 py-1.5 text-xs text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 transition-colors"
                @input="draft[field.key] = ($event.target as HTMLInputElement).value"
              />
              <button
                v-if="isDirty(field)"
                type="button"
                :disabled="saving"
                class="shrink-0 rounded-lg bg-status-green/15 border border-status-green/40 px-3 py-1.5 text-xs font-semibold text-status-green transition-colors hover:bg-status-green/25 disabled:cursor-not-allowed disabled:opacity-40"
                @click="save(field)"
              >
                {{ saving ? t('common.saving') : t('common.save') }}
              </button>
            </div>
            <p v-if="uploadError[field.key]" class="text-xs text-status-red mt-1">{{ uploadError[field.key] }}</p>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>
