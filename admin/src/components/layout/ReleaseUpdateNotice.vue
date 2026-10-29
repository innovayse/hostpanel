<script setup lang="ts">
/**
 * Compact notice shown in the sidebar footer when a newer Hostpanel release is available.
 *
 * Renders nothing when no upgrade is available or the release status has not yet been fetched.
 * The upgrade command is shown in a copyable code block so the operator can paste it directly
 * into a terminal.
 */
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useReleaseStatusStore } from '@/stores/releaseStatus'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'

/** Translation function. */
const { t } = useI18n()

/** Release status store; provides the upgrade availability and the command. */
const releaseStatus = useReleaseStatusStore()

/** SVG path for the download/upgrade arrow icon. */
const UPGRADE_ICON = '<path d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4"/>'

/** SVG path for the copy icon. */
const COPY_ICON = '<rect x="9" y="9" width="13" height="13" rx="2" ry="2"/><path d="M5 15H4a2 2 0 01-2-2V4a2 2 0 012-2h9a2 2 0 012 2v1"/>'

/** True for two seconds after the command is copied, to show "Copied!" feedback. */
const copied = ref(false)

/**
 * Copies the upgrade command to the clipboard and sets the copied flag for two seconds.
 */
async function copyCommand(): Promise<void> {
  const command = releaseStatus.status?.upgradeCommand
  if (!command) {
    return
  }
  await navigator.clipboard.writeText(command)
  copied.value = true
  setTimeout(() => {
    copied.value = false
  }, 2000)
}
</script>

<template>
  <div
    v-if="releaseStatus.status?.isUpgradeAvailable"
    class="mx-2.5 mb-2 rounded-[9px] border border-warning-500/30 bg-warning-500/10 p-3"
    data-testid="release-update-notice"
  >
    <UiStack gap="xs">
      <!-- Header: icon + version label -->
      <UiStack direction="row" align="center" gap="xs">
        <UiIcon :markup="UPGRADE_ICON" size="sm" class="shrink-0 text-warning-500" />
        <UiText size="sm" class="font-medium text-warning-600 dark:text-warning-400">
          {{ t('layout.releaseUpdate.title', { version: releaseStatus.status.latestVersion }) }}
        </UiText>
      </UiStack>

      <!-- Release notes link -->
      <UiLink
        v-if="releaseStatus.status.releaseNotesUrl"
        :href="releaseStatus.status.releaseNotesUrl"
        target="_blank"
        rel="noopener noreferrer"
        size="sm"
        data-testid="release-update-notes-link"
      >
        {{ t('layout.releaseUpdate.releaseNotes') }}
      </UiLink>

      <!-- Copyable upgrade command -->
      <div
        v-if="releaseStatus.status.upgradeCommand"
        class="relative mt-1 rounded-[6px] border border-border bg-surface-base px-2.5 py-1.5"
        data-testid="release-update-command"
      >
        <code class="block break-all font-mono text-[0.7rem] text-text-secondary leading-relaxed pr-6">
          {{ releaseStatus.status.upgradeCommand }}
        </code>
        <button
          type="button"
          class="absolute right-1.5 top-1.5 rounded p-0.5 text-text-muted transition-colors hover:text-text-primary"
          :aria-label="t('layout.releaseUpdate.copy')"
          data-testid="release-update-copy"
          @click="copyCommand"
        >
          <UiIcon :markup="COPY_ICON" size="xs" />
        </button>
      </div>

      <!-- Copied feedback -->
      <UiText v-if="copied" size="xs" tone="secondary" data-testid="release-update-copied">
        {{ t('layout.releaseUpdate.copied') }}
      </UiText>
    </UiStack>
  </div>
</template>
