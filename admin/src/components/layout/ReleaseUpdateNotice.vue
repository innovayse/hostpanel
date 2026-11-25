<script setup lang="ts">
/**
 * Compact notice shown in the sidebar footer when a newer Hostpanel release is available.
 *
 * Renders nothing when no upgrade is available or the release status has not yet been fetched.
 * The upgrade command is shown in a copyable code block so the operator can paste it directly
 * into a terminal.
 */
import { onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useReleaseStatusStore } from '@/stores/releaseStatus'
import UiIcon from '@/components/ui/UiIcon.vue'
import { Icon } from '@/lib/icons'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTag, UiTextTone } from '@/utils/ui/text'

/** Translation function. */
const { t } = useI18n()

/** Release status store; provides the upgrade availability and the command. */
const releaseStatus = useReleaseStatusStore()

/** True for two seconds after the command is copied, to show "Copied" feedback. */
const copied = ref(false)

/** Active timeout handle, kept so it can be cleared on unmount. */
let copyTimeout: ReturnType<typeof setTimeout> | null = null

onUnmounted(() => {
  if (copyTimeout !== null) {
    clearTimeout(copyTimeout)
  }
})

/**
 * Copies the upgrade command to the clipboard and sets the copied flag for two seconds.
 */
const copyCommand = async (): Promise<void> => {
  const command = releaseStatus.status?.upgradeCommand
  if (!command) {
    return
  }
  try {
    await navigator.clipboard.writeText(command)
  } catch {
    return
  }
  copied.value = true
  if (copyTimeout !== null) {
    clearTimeout(copyTimeout)
  }
  copyTimeout = setTimeout(() => {
    copied.value = false
    copyTimeout = null
  }, 2000)
}
</script>

<template>
  <UiStack
    v-if="releaseStatus.status?.isUpgradeAvailable"
    :gap="UiGap.None"
    class="mx-2.5 mb-2 rounded-[9px] border border-status-yellow/30 bg-tint-yellow p-3"
    data-testid="release-update-notice"
  >
    <UiStack :gap="UiGap.Xs">
      <!-- Header: icon + version label -->
      <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Xs">
        <UiIcon :markup="Icon.Download" :size="UiSize.Sm" class="shrink-0 text-status-yellow" />
        <UiText :size="UiTextSize.Sm" class="font-medium text-status-yellow">
          {{ t('layout.releaseUpdate.title', { version: releaseStatus.status.latestVersion }) }}
        </UiText>
      </UiStack>

      <!-- Release notes link -->
      <UiLink
        v-if="releaseStatus.status.releaseNotesUrl"
        :href="releaseStatus.status.releaseNotesUrl"
        external
        :variant="UiButtonVariant.Link"
        :size="UiSize.Sm"
        data-testid="release-update-notes-link"
      >
        {{ t('layout.releaseUpdate.releaseNotes') }}
      </UiLink>

      <!-- Copyable upgrade command -->
      <UiStack
        v-if="releaseStatus.status.upgradeCommand"
        :gap="UiGap.None"
        class="relative mt-1 rounded-[6px] border border-border bg-surface-base px-2.5 py-1.5"
        data-testid="release-update-command"
      >
        <UiText :as="UiTextTag.Span" :tone="UiTextTone.Secondary" class="block break-all pr-6 font-mono text-[0.7rem] leading-relaxed">
          {{ releaseStatus.status.upgradeCommand }}
        </UiText>
        <UiStack :gap="UiGap.None" class="absolute right-1.5 top-1.5">
          <UiIconButton
            :label="t('layout.releaseUpdate.copy')"
            :variant="UiButtonVariant.Ghost"
            :size="UiSize.Xs"
            data-testid="release-update-copy"
            @click="copyCommand"
          >
            <UiIcon :markup="Icon.Copy" :size="UiSize.Sm" />
          </UiIconButton>
        </UiStack>
      </UiStack>

      <!-- Copied feedback -->
      <UiText v-if="copied" :size="UiTextSize.Xs" :tone="UiTextTone.Secondary" data-testid="release-update-copied">
        {{ t('shared.copied') }}
      </UiText>
    </UiStack>
  </UiStack>
</template>
