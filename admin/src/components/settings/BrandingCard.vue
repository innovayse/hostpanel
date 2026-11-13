<script setup lang="ts">
/**
 * Branding card: uploads the storefront's logo and favicon.
 *
 * Mirrors the slide image uploader (`SlideFormView.vue`): drag-and-drop or browse, uploaded straight
 * to disk via a dedicated endpoint that returns a URL, which is what actually gets saved to the
 * setting. The setting itself still accepts a pasted URL too, for an operator who already hosts the
 * file elsewhere and would rather not upload a copy.
 */
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import SettingOutcome from '@/components/settings/SettingOutcome.vue'
import SettingsSection from '@/components/settings/SettingsSection.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiImage from '@/components/ui/UiImage.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiUploadZone from '@/components/ui/UiUploadZone.vue'
import { useSettingsStore } from '@/stores/settings'
import { SettingKey } from '@/types/setting'
import type { Setting, SettingSave, SettingSaveResult } from '@/types/setting'
import { BrandingImageKind } from '@/types/upload'
import { FAVICON_ACCEPT, LOGO_ACCEPT } from '@/lib/mimeTypes'

const { t } = useI18n()

/** Props for BrandingCard. */
const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
  /** Persists one row and answers how it went; each field shows its own outcome. */
  save: SettingSave
}>()

/** Settings store; owns the upload so the component never touches transport. */
const settingsStore = useSettingsStore()

/** One branding image field: which setting key it saves to, and upload constraints. */
interface BrandingField {
  /** Settings key the image URL is saved to. */
  key: string
  /** Route segment of the upload endpoint; the backend's BrandingKind name, lower-cased. */
  kind: BrandingImageKind
  /** Label key. */
  labelKey: string
  /** Hint key (formats and size limit). */
  hintKey: string
  /** Accepted MIME types. */
  accept: string
  /** Preview box size: the favicon and mark are square and small, the logos wide and short. */
  previewClass: string
  /**
   * Preview tile background. A light-background logo previewed on the panel's dark tile is
   * unreadable, which is the very mistake the dark variant exists to fix, so each field
   * previews on the surface it will actually sit on.
   */
  previewBg: string
}

/** The four images the card manages. Each preview ground is the fixed white or dark the image is shown on in the storefront, so it is not a panel token. */
const FIELDS: BrandingField[] = [
  {
    key: SettingKey.Logo,
    kind: BrandingImageKind.Logo,
    labelKey: 'settings.branding.logo.label',
    hintKey: 'settings.branding.logo.hint',
    accept: LOGO_ACCEPT,
    previewClass: 'w-32 h-14',
    previewBg: 'bg-white',
  },
  {
    key: SettingKey.LogoDark,
    kind: BrandingImageKind.LogoDark,
    labelKey: 'settings.branding.logoDark.label',
    hintKey: 'settings.branding.logoDark.hint',
    accept: LOGO_ACCEPT,
    previewClass: 'w-32 h-14',
    previewBg: 'bg-neutral-900',
  },
  {
    key: SettingKey.LogoMark,
    kind: BrandingImageKind.LogoMark,
    labelKey: 'settings.branding.logoMark.label',
    hintKey: 'settings.branding.logoMark.hint',
    accept: LOGO_ACCEPT,
    previewClass: 'w-14 h-14',
    previewBg: 'bg-neutral-900',
  },
  {
    key: SettingKey.Favicon,
    kind: BrandingImageKind.Favicon,
    labelKey: 'settings.branding.favicon.label',
    hintKey: 'settings.branding.favicon.hint',
    accept: FAVICON_ACCEPT,
    previewClass: 'w-10 h-10',
    previewBg: 'bg-neutral-900',
  },
]

/** Unsaved edit per field, keyed by setting key. */
const draft = ref<Record<string, string>>({})

/** Whether an upload is in flight, keyed by setting key. */
const uploading = ref<Record<string, boolean>>({})

/** True while a field's save is in flight, keyed by setting key. */
const savingField = ref<Record<string, boolean>>({})

/** How the last save of a field went, keyed by setting key; cleared by the next edit. */
const outcome = ref<Record<string, SettingSaveResult | null>>({})

/** The refusal sentence of the last failed upload, keyed by setting key. */
const uploadError = ref<Record<string, string | null>>({})

/**
 * The stored setting row for a field, if it has been seeded.
 *
 * @param field - The branding field.
 * @returns The row, or undefined when the key was never seeded.
 */
const settingOf = (field: BrandingField): Setting | undefined => props.settings.find((s) => s.key === field.key)

/**
 * Current value shown for a field: the operator's unsaved edit, or what is stored.
 *
 * @param field - The branding field.
 * @returns The image URL, `''` when there is none.
 */
const valueOf = (field: BrandingField): string => draft.value[field.key] ?? settingOf(field)?.value ?? ''

/**
 * Whether a field has an edit worth saving.
 *
 * @param field - The branding field.
 * @returns True when the draft differs from what is stored.
 */
const isDirty = (field: BrandingField): boolean =>
  field.key in draft.value && draft.value[field.key] !== (settingOf(field)?.value ?? '')

/**
 * Records an edit of a field and forgets the outcome of its previous save.
 *
 * @param field - The branding field.
 * @param url - The image URL now staged for it, `''` to remove the image.
 */
const edit = (field: BrandingField, url: string): void => {
  draft.value[field.key] = url
  outcome.value[field.key] = null
}

/**
 * Saves one field and keeps the outcome. No-op when the setting row does not exist yet, nothing
 * changed or the field is already being saved.
 *
 * @param field - The branding field.
 */
const saveField = async (field: BrandingField): Promise<void> => {
  const setting = settingOf(field)
  const edited = draft.value[field.key]
  if (!setting || !isDirty(field) || edited === undefined || savingField.value[field.key] === true) {
    return
  }
  outcome.value[field.key] = null
  savingField.value[field.key] = true
  outcome.value[field.key] = await props.save(setting.id, edited)
  savingField.value[field.key] = false
}

/**
 * Uploads a file for one field and stages the returned URL as its draft.
 *
 * The store sends it as multipart data; a refusal arrives as the API's own sentence.
 *
 * @param field - The branding field being uploaded for.
 * @param file - The image file to upload.
 */
const uploadFile = async (field: BrandingField, file: File): Promise<void> => {
  uploadError.value[field.key] = null
  uploading.value[field.key] = true
  const url = await settingsStore.uploadBrandingFile(field.kind, file)
  if (url !== null) {
    edit(field, url)
  } else {
    uploadError.value[field.key] = settingsStore.uploadError
  }
  uploading.value[field.key] = false
}
</script>

<template>
  <SettingsSection
    :columns="2"
    :title="t('settings.branding.title')"
    :description="t('settings.branding.description')"
  >
    <UiStack v-for="field in FIELDS" :key="field.key" gap="sm">
      <UiText tone="secondary" size="md">
        {{ t(field.labelKey) }}
      </UiText>

      <UiStack direction="row" align="start">
        <!-- Preview -->
        <UiStack
          v-if="valueOf(field)"
          align="center"
          justify="center"
          gap="none"
          class="group relative shrink-0 overflow-hidden rounded-lg border border-border"
          :class="[field.previewClass, field.previewBg]"
        >
          <UiImage
            :src="valueOf(field)"
            :alt="t(field.labelKey)"
            fit="contain"
            class="max-h-full max-w-full"
            :data-testid="`branding-preview-${field.kind}`"
          />
          <UiIconButton
            :label="t('settings.branding.remove')"
            size="sm"
            class="absolute right-1 top-1 opacity-0 transition-opacity group-hover:opacity-100"
            :data-testid="`branding-remove-${field.kind}`"
            @click="edit(field, '')"
          >
            <svg class="h-3 w-3" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" aria-hidden="true">
              <line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </UiIconButton>
        </UiStack>

        <UiStack gap="sm" class="min-w-56 flex-1">
          <UiUploadZone
            compact
            :accept="field.accept"
            :uploading="uploading[field.key] === true"
            :hint="t(field.hintKey)"
            :input-testid="`branding-file-${field.kind}`"
            @select="uploadFile(field, $event)"
          />

          <UiStack direction="row" align="center" gap="sm">
            <UiInput
              :model-value="valueOf(field)"
              type="text"
              size="sm"
              :placeholder="t('settings.branding.pasteUrl')"
              :data-testid="`branding-url-${field.kind}`"
              class="flex-1"
              @update:model-value="edit(field, $event)"
            />
            <UiButton
              v-if="isDirty(field)"
              type="button"
              variant="primary"
              size="sm"
              :loading="savingField[field.key] === true"
              :data-testid="`branding-save-${field.kind}`"
              @click="saveField(field)"
            >
              {{ t('common.save') }}
            </UiButton>
            <SettingOutcome :outcome="outcome[field.key] ?? null" />
          </UiStack>
          <UiText
            v-if="uploadError[field.key]"
            tone="danger"
            size="sm"
            :data-testid="`branding-error-${field.kind}`"
          >
            {{ uploadError[field.key] }}
          </UiText>
        </UiStack>
      </UiStack>
    </UiStack>
  </SettingsSection>
</template>
