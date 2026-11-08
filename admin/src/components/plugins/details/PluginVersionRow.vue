<script setup lang="ts">
/**
 * One version of a plugin on the details page: its number and release date, what it is on this
 * server (installed, staged, newest), whether it runs on this host, its release notes, the
 * permissions it asks for, and its status. A deprecated or yanked version shows the publisher's
 * reason and the version to use instead. The button installs the version; it reads Install,
 * Update or Downgrade by how the version compares with the installed one, and is disabled for a
 * yanked version and one that does not run on this host; it is left out for the installed version and the one already staged. The component owns no
 * data and installs nothing: it reports the click.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { relationTo } from '@/components/plugins/details/versionOrder'
import { formatDate } from '@/lib/format'
import { PluginVersionStatus, VersionRelation } from '@/types/plugin'
import type { RegistryPluginVersionDto } from '@/types/plugin'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginVersionRow. */
const props = defineProps<{
  /** The version to show. */
  version: RegistryPluginVersionDto
  /** The installed version, null when the plugin is not installed. */
  installedVersion: string | null
  /** The version staged for the next restart, null when none. */
  stagedVersion: string | null
  /** The newest version the registry offers. */
  latestVersion: string
  /** True while an install of this plugin is in flight; every install button is then blocked. */
  busy: boolean
}>()

/** Events emitted by PluginVersionRow. */
const emit = defineEmits<{
  /** Emitted with the version number when the operator asks to install it. */
  install: [version: string]
}>()

/** How this version compares with the installed one. */
const relation = computed<VersionRelation>(() => relationTo(props.version.version, props.installedVersion))

/** Whether this is the version running on the server. */
const isInstalled = computed<boolean>(() => relation.value === VersionRelation.Current)

/** Whether the publisher withdrew this version. */
const isYanked = computed<boolean>(() => props.version.status === PluginVersionStatus.Yanked)

/** Whether this version is already staged for the next restart, so installing it again would do nothing. */
const isStaged = computed<boolean>(() => props.version.version === props.stagedVersion)

/** Whether the install button is blocked: busy, the installed version, a yanked one or one that does not run here. */
const installBlocked = computed<boolean>(
  () => props.busy || isInstalled.value || isYanked.value || !props.version.compatible,
)

/** Badge tone per version status. */
const statusTone: Record<PluginVersionStatus, UiTone> = {
  [PluginVersionStatus.Published]: UiTone.Success,
  [PluginVersionStatus.Deprecated]: UiTone.Warning,
  [PluginVersionStatus.Yanked]: UiTone.Danger,
}

/** The sentence about which host versions this version runs on. */
const hostRangeText = computed<string>(() => {
  const { minHostVersion, maxHostVersion } = props.version
  if (minHostVersion !== null && maxHostVersion !== null) {
    return t('pluginDetails.versions.hostRange', { min: minHostVersion, max: maxHostVersion })
  }
  if (minHostVersion !== null) {
    return t('pluginDetails.versions.hostMin', { min: minHostVersion })
  }
  if (maxHostVersion !== null) {
    return t('pluginDetails.versions.hostMax', { max: maxHostVersion })
  }
  return t('pluginDetails.versions.hostAny')
})

/** Label of the compatibility badge. */
const compatibilityText = computed<string>(() =>
  props.version.compatible ? t('pluginDetails.versions.compatible') : t('pluginDetails.versions.incompatible'),
)

/** Button look: a downgrade is a caution, anything else the primary action. */
const installVariant = computed<UiButtonVariant>(() =>
  relation.value === VersionRelation.Older ? UiButtonVariant.Secondary : UiButtonVariant.Primary,
)
</script>

<template>
  <UiCard padding="sm" :data-testid="`plugin-version-${version.version}`">
    <UiStack gap="sm">
      <UiStack direction="row" align="center" justify="between" gap="md" wrap>
        <UiStack direction="row" align="center" gap="sm" wrap>
          <UiText tone="primary" size="lg" class="font-semibold font-mono">{{ version.version }}</UiText>
          <UiBadge :tone="statusTone[version.status]" small :data-testid="`plugin-version-status-${version.version}`">
            {{ t(`pluginDetails.versions.status.${version.status}`) }}
          </UiBadge>
          <UiBadge
            v-if="isInstalled"
            :tone="UiTone.Info"
            small
            :data-testid="`plugin-version-installed-${version.version}`"
          >
            {{ t('pluginDetails.versions.installed') }}
          </UiBadge>
          <UiBadge
            v-if="isStaged"
            :tone="UiTone.Warning"
            small
            :data-testid="`plugin-version-staged-${version.version}`"
          >
            {{ t('pluginDetails.versions.staged') }}
          </UiBadge>
          <UiBadge
            v-if="version.version === latestVersion"
            :tone="UiTone.Neutral"
            small
            :data-testid="`plugin-version-latest-${version.version}`"
          >
            {{ t('pluginDetails.versions.latest') }}
          </UiBadge>
          <UiBadge
            :tone="version.compatible ? UiTone.Success : UiTone.Danger"
            small
            :data-testid="`plugin-version-compatibility-${version.version}`"
          >
            {{ compatibilityText }}
          </UiBadge>
        </UiStack>
        <UiButton
          v-if="!isInstalled && !isStaged"
          type="button"
          :variant="installVariant"
          size="sm"
          :disabled="installBlocked"
          :data-testid="`plugin-version-install-${version.version}`"
          @click="emit('install', version.version)"
        >
          {{ t(`pluginDetails.versions.action.${relation}`) }}
        </UiButton>
      </UiStack>

      <UiText tone="muted" size="xs" :data-testid="`plugin-version-date-${version.version}`">
        {{ t('pluginDetails.versions.published', { date: formatDate(version.publishedAt) }) }}
        ·
        {{ hostRangeText }}
      </UiText>

      <UiAlert
        v-if="version.status !== PluginVersionStatus.Published"
        :variant="isYanked ? UiTone.Danger : UiTone.Warning"
        :data-testid="`plugin-version-notice-${version.version}`"
      >
        <UiStack gap="xs">
          <span :data-testid="`plugin-version-reason-${version.version}`">
            {{
              version.statusReason === null
                ? t(`pluginDetails.versions.noReason.${version.status}`)
                : t('pluginDetails.versions.reason', { reason: version.statusReason })
            }}
          </span>
          <span
            v-if="version.replacementVersion !== null"
            :data-testid="`plugin-version-replacement-${version.version}`"
          >
            {{ t('pluginDetails.versions.replacement', { version: version.replacementVersion }) }}
          </span>
        </UiStack>
      </UiAlert>

      <UiStack gap="xs">
        <UiText tone="secondary" size="sm" class="font-semibold">{{ t('pluginDetails.versions.changelog') }}</UiText>
        <UiText
          v-if="version.changelog !== null && version.changelog !== ''"
          tone="muted"
          size="sm"
          class="whitespace-pre-line"
          :data-testid="`plugin-version-changelog-${version.version}`"
        >
          {{ version.changelog }}
        </UiText>
        <UiText v-else tone="muted" size="sm" :data-testid="`plugin-version-no-changelog-${version.version}`">
          {{ t('pluginDetails.versions.noChangelog') }}
        </UiText>
      </UiStack>

      <UiStack gap="xs">
        <UiText tone="secondary" size="sm" class="font-semibold">{{ t('pluginDetails.versions.permissions') }}</UiText>
        <UiStack
          v-if="version.permissions.length > 0"
          direction="row"
          gap="xs"
          wrap
          :data-testid="`plugin-version-permissions-${version.version}`"
        >
          <UiBadge v-for="permission in version.permissions" :key="permission" :tone="UiTone.Neutral" small>
            {{ permission }}
          </UiBadge>
        </UiStack>
        <UiText v-else tone="muted" size="sm" :data-testid="`plugin-version-no-permissions-${version.version}`">
          {{ t('pluginDetails.versions.noPermissions') }}
        </UiText>
      </UiStack>
    </UiStack>
  </UiCard>
</template>
