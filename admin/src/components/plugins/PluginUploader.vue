<script setup lang="ts">
/**
 * File upload zone for installing a plugin ZIP.
 *
 * The kit's upload zone takes the archive from the picker or a drop alike, limited to `.zip` through its
 * `accept` filter; whether the archive is a valid plugin is the server's call, and a refusal is shown by the
 * screen as the sentence it returned. The chosen archive is shown with its size until the admin installs it.
 */
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiUploadZone from '@/components/ui/UiUploadZone.vue'
import { BYTES_PER_KB } from '@/lib/limits'

/** Translation function. */
const { t } = useI18n()

/** Events emitted by PluginUploader. */
const emit = defineEmits<{
  /** Emitted with the chosen archive when the admin confirms the install. */
  upload: [file: File]
}>()

/** The archive chosen in the zone, null until one is picked. */
const picked = ref<File | null>(null)

/**
 * Takes the archive chosen in the zone as the selection.
 *
 * @param file - The picked or dropped file.
 */
const handleSelect = (file: File): void => {
  picked.value = file
}

/** Emits the selected archive and clears the selection. */
const handleUpload = (): void => {
  if (picked.value === null) {
    return
  }
  emit('upload', picked.value)
  picked.value = null
}

/** Forgets the selected archive. */
const clearFile = (): void => {
  picked.value = null
}
</script>

<template>
  <UiCard padding="md" data-testid="plugin-uploader">
    <UiStack gap="md">
      <UiUploadZone accept=".zip" input-testid="plugin-zip-input" @select="handleSelect" />

      <UiStack v-if="picked !== null" direction="row" align="center" gap="md" wrap>
        <UiText tone="primary" size="md" class="font-mono" data-testid="plugin-selected-name">
          {{ picked.name }}
        </UiText>
        <UiText tone="muted" size="sm">
          {{ t('plugins.pluginUploader.valueKbReadyToInstall', { value: (picked.size / BYTES_PER_KB).toFixed(1) }) }}
        </UiText>
        <UiButton type="button" variant="ghost" size="sm" data-testid="plugin-cancel" @click="clearFile">
          {{ t('shared.cancel') }}
        </UiButton>
        <UiButton type="button" variant="primary" size="sm" data-testid="plugin-install" @click="handleUpload">
          {{ t('plugins.pluginUploader.installPlugin') }}
        </UiButton>
      </UiStack>
    </UiStack>
  </UiCard>
</template>
