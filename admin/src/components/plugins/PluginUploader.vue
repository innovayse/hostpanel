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
import { UiActionKind } from '@/utils/ui/actions'
import { UiCardPadding } from '@/utils/ui/card'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiSize } from '@/utils/ui/size'

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
  <UiCard :padding="UiCardPadding.Md" data-testid="plugin-uploader">
    <UiStack :gap="UiGap.Md">
      <UiUploadZone accept=".zip" input-testid="plugin-zip-input" @select="handleSelect" />

      <UiStack v-if="picked !== null" :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Md" wrap>
        <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md" class="font-mono" data-testid="plugin-selected-name">
          {{ picked.name }}
        </UiText>
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
          {{ t('plugins.pluginUploader.valueKbReadyToInstall', { value: (picked.size / BYTES_PER_KB).toFixed(1) }) }}
        </UiText>
        <UiButton :action="UiActionKind.Cancel" :type="UiButtonType.Button" :variant="UiButtonVariant.Ghost" :size="UiSize.Sm" data-testid="plugin-cancel" @click="clearFile">
          {{ t('shared.cancel') }}
        </UiButton>
        <UiButton :action="UiActionKind.Upload" :type="UiButtonType.Button" :variant="UiButtonVariant.Primary" :size="UiSize.Sm" data-testid="plugin-install" @click="handleUpload">
          {{ t('shared.install') }}
        </UiButton>
      </UiStack>
    </UiStack>
  </UiCard>
</template>
