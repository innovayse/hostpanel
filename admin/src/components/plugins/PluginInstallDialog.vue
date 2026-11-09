<script setup lang="ts">
/**
 * Confirmation before a plugin ZIP is installed: what the package asks for (each kind of hosting access and each
 * capability, in words), the release notes of its version and the server versions it supports. When this server's
 * version is outside that range the dialog says so and offers no install, since the server would refuse it.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import type { PluginPackagePreviewDto } from '@/types/plugin'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiDialogSize } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginInstallDialog. */
const props = withDefaults(defineProps<{
  /** What the package asks for, as the server read it. */
  preview: PluginPackagePreviewDto
  /** Whether the install is running (disables the install button). */
  loading?: boolean
}>(), {
  loading: false,
})

/** Events emitted by PluginInstallDialog. */
const emit = defineEmits<{
  /** Emitted when the admin confirms the install. */
  confirm: []
  /** Emitted when the admin cancels, presses Escape or clicks the backdrop. */
  close: []
}>()

/** Whether the package asks for anything at all: any hosting access or any capability. */
const asksForAnything = computed<boolean>(() => props.preview.hostAccess.length > 0 || props.preview.capabilities.length > 0)

/** Whether the release notes have any text. */
const hasChangelog = computed<boolean>(() => props.preview.changelog !== null && props.preview.changelog.trim() !== '')

/** The supported server versions in words; empty when the package sets no limit. */
const rangeText = computed<string>(() => {
  const { minHostVersion: min, maxHostVersion: max } = props.preview
  if (min !== null && max !== null) {
    return t('plugins.pluginInstallDialog.rangeBetween', { min, max })
  }
  if (min !== null) {
    return t('plugins.pluginInstallDialog.rangeFrom', { min })
  }
  if (max !== null) {
    return t('plugins.pluginInstallDialog.rangeUpTo', { max })
  }
  return ''
})
</script>

<template>
  <UiDialog
    :title="t('plugins.pluginInstallDialog.title', { name: preview.name, version: preview.version })"
    :size="UiDialogSize.Md"
    data-testid="plugin-install-dialog"
    @close="emit('close')"
  >
    <UiStack gap="md">
      <UiText tone="secondary" size="sm">{{ t('plugins.pluginInstallDialog.byAuthor', { author: preview.author }) }}</UiText>

      <UiAlert v-if="!preview.compatible" :variant="UiTone.Danger" data-testid="plugin-install-incompatible">
        {{ t('plugins.pluginInstallDialog.incompatible', { range: rangeText, host: preview.hostVersion }) }}
      </UiAlert>

      <UiStack gap="xs">
        <UiText tone="secondary" size="sm" class="font-semibold">{{ t('plugins.pluginInstallDialog.requests') }}</UiText>
        <UiStack v-if="asksForAnything" gap="xs" data-testid="plugin-install-requests">
          <UiText
            v-for="access in preview.hostAccess"
            :key="`access-${access}`"
            tone="primary"
            size="sm"
            :data-testid="`plugin-install-access-${access}`"
          >
            {{ t(`plugins.pluginInstallDialog.hostAccess.${access}`) }}
          </UiText>
          <UiText
            v-for="capability in preview.capabilities"
            :key="`capability-${capability}`"
            tone="primary"
            size="sm"
            :data-testid="`plugin-install-capability-${capability}`"
          >
            {{ t(`plugins.pluginInstallDialog.capabilities.${capability}`) }}
          </UiText>
        </UiStack>
        <UiText v-else tone="muted" size="sm" data-testid="plugin-install-no-requests">
          {{ t('plugins.pluginInstallDialog.noRequests') }}
        </UiText>
      </UiStack>

      <UiStack gap="xs">
        <UiText tone="secondary" size="sm" class="font-semibold">{{ t('plugins.pluginInstallDialog.changelog') }}</UiText>
        <UiText v-if="hasChangelog" tone="muted" size="sm" class="whitespace-pre-line" data-testid="plugin-install-changelog">
          {{ preview.changelog }}
        </UiText>
        <UiText v-else tone="muted" size="sm" data-testid="plugin-install-no-changelog">
          {{ t('plugins.pluginInstallDialog.noChangelog') }}
        </UiText>
      </UiStack>

      <UiText v-if="rangeText !== '' && preview.compatible" tone="muted" size="sm" data-testid="plugin-install-range">
        {{ rangeText }}
      </UiText>
    </UiStack>

    <template #footer>
      <UiButton :variant="UiButtonVariant.Secondary" data-testid="plugin-install-cancel" @click="emit('close')">
        {{ t('shared.cancel') }}
      </UiButton>
      <UiButton
        v-if="preview.compatible"
        :variant="UiButtonVariant.Primary"
        :disabled="loading"
        data-testid="plugin-install-confirm"
        @click="emit('confirm')"
      >
        {{ t('plugins.pluginInstallDialog.install') }}
      </UiButton>
    </template>
  </UiDialog>
</template>
