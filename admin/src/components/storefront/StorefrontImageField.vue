<script setup lang="ts">
/**
 * An image picker for storefront content: a preview with a remove button, a drop zone that uploads
 * the picked file, and a box to paste an address instead.
 *
 * The upload itself belongs to the page's store: the field is handed `upload`, which answers the
 * stored URL or null when refused. The store keeps one upload sentence for all its fields, so the
 * field shows `uploadError` only when its own last upload was the one refused.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiField from '@/components/ui/UiField.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiImage from '@/components/ui/UiImage.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiUploadZone from '@/components/ui/UiUploadZone.vue'
import { Icon } from '@/lib/icons'
import { storefrontAssetUrl } from '@/lib/runtimeConfig'
import { ILLUSTRATION_ACCEPT } from '@/lib/mimeTypes'

const { t } = useI18n()

/** Props for StorefrontImageField. */
const props = defineProps<{
  /** Address of the current image; empty when none (two-way bound with `v-model`). */
  modelValue: string
  /** Label of the field (already translated). */
  label: string
  /** Prefix of the test ids: `<prefix>-preview`, `-remove`, `-file`, `-url`. */
  testid: string
  /** Stores the file and answers its URL, or null when refused. */
  upload: (file: File) => Promise<string | null>
  /** Sentence for a refused field value (a validation message), undefined when none. */
  error?: string | null
  /** The store's upload sentence; shown only if this field's own last upload failed. */
  uploadError?: string | null
}>()

const emit = defineEmits<{
  /** Emitted with the new address when it is typed, uploaded or removed. */
  'update:modelValue': [value: string]
}>()

/** True while a file is being uploaded. */
const uploading = ref(false)

/** True when this field's last upload was refused. */
const failed = ref(false)

/** The sentence under the field: the value's own message, else this field's upload failure. */
const shownError = computed<string | undefined>(() => props.error ?? (failed.value ? props.uploadError ?? undefined : undefined))

/**
 * Uploads the picked file and takes over the stored address.
 *
 * @param file - The image file.
 */
const onSelect = async (file: File): Promise<void> => {
  uploading.value = true
  failed.value = false
  const url = await props.upload(file)
  uploading.value = false
  failed.value = url === null
  if (url !== null) {
    emit('update:modelValue', url)
  }
}
</script>

<template>
  <UiField :label="label" :error="shownError">
    <UiStack direction="row" align="start" wrap>
      <UiStack
        v-if="modelValue"
        align="center"
        justify="center"
        gap="none"
        class="group relative h-20 w-32 shrink-0 overflow-hidden rounded-lg border border-border"
      >
        <UiImage
          :src="storefrontAssetUrl(modelValue)"
          :alt="label"
          fit="cover"
          class="h-full w-full"
          :data-testid="`${testid}-preview`"
        />
        <UiIconButton
          :label="t('storefront.form.removeImage')"
          size="sm"
          class="absolute right-1 top-1 opacity-0 transition-opacity focus-visible:opacity-100 group-hover:opacity-100"
          :data-testid="`${testid}-remove`"
          @click="emit('update:modelValue', '')"
        >
          <UiIcon :markup="Icon.Close" size="sm" />
        </UiIconButton>
      </UiStack>

      <UiStack gap="sm" class="min-w-56 flex-1">
        <UiUploadZone
          :accept="ILLUSTRATION_ACCEPT"
          :uploading="uploading"
          :hint="t('storefront.form.imageHint')"
          :input-testid="`${testid}-file`"
          @select="onSelect"
        />
        <UiInput
          :model-value="modelValue"
          type="text"
          size="sm"
          :placeholder="t('storefront.form.pasteImageUrl')"
          :data-testid="`${testid}-url`"
          @update:model-value="emit('update:modelValue', $event)"
        />
      </UiStack>
    </UiStack>
  </UiField>
</template>
