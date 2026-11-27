<script setup lang="ts">
/**
 * The overview of an installed plugin, read from this server's own list (so it exists for every installed plugin,
 * whether or not the registry knows it), as five cards drawn by `UiDetailCards` from the cards of
 * `PluginDetailsView.schema`: Details (who made it, what it is, the rule it broke when the host refused it),
 * Installation (where the package came from, when and by whom it was installed and last replaced, who signed it, its
 * size and digest), Compatibility (installed and staged version, the server versions it runs on), Permissions (what
 * the manifest asks for) and the release notes of the installed version. The facts of a card are its schema rows: each
 * row's caption comes from the schema and its value from here. A fact the server never recorded, such as the install
 * of a plugin that predates install records, is shown as "not recorded" instead of being left out. Dates are written by
 * the shared date-time helper.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import PluginOverviewFact from '@/components/plugins/details/PluginOverviewFact.vue'
import UiDetailCards from '@/components/ui/UiDetailCards.vue'
import UiMarkdown from '@/components/ui/UiMarkdown.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { formatDateTime, formatSizeBytes } from '@/lib/format'
import { INSTALLED_PLUGIN_STATE_LOOK, installedPluginState } from '@/lib/installedPlugins'
import { categoryLabel } from '@/lib/pluginCard'
import { translateOrRaw } from '@/lib/translate'
import type { PluginListItemDto } from '@/types/plugin'
import type { SkeletonDetailCard } from '@/utils/ui/skeletonSchema'
import { UiGap } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import {
  PLUGIN_OVERVIEW_COMPATIBILITY_CARD,
  PLUGIN_OVERVIEW_DETAILS_CARD,
  PLUGIN_OVERVIEW_INSTALLATION_CARD,
  PLUGIN_OVERVIEW_PART,
} from '@/views/PluginDetailsView.schema'

/** Translation function and the active locale (numbers are written in it). */
const { t, locale } = useI18n()

/** Props for PluginInstalledOverview. */
const props = defineProps<{
  /** The installed plugin, as this server lists it. */
  plugin: PluginListItemDto
}>()

/** The plugin's type in words; a type the panel has no words for is shown as the server wrote it. */
const typeLabel = computed<string>(() => translateOrRaw(`pluginDetails.overview.types.${props.plugin.type}`, props.plugin.type))

/** Whether the plugin runs now, waits for a restart to load, failed to load or was refused, in words. */
const stateLabel = computed<string>(() => t(INSTALLED_PLUGIN_STATE_LOOK[installedPluginState(props.plugin)].labelKey))

/** Where the package came from in words, null when no install was recorded. */
const sourceLabel = computed<string | null>(() =>
  props.plugin.source ? t(`pluginDetails.overview.source.${props.plugin.source}`) : null)

/** Who vouched for the package in words, null when no install was recorded. */
const signerLabel = computed<string | null>(() =>
  props.plugin.signer ? t(`pluginDetails.overview.signer.${props.plugin.signer}`) : null)

/** The first install moment, null when none was recorded. */
const installedAt = computed<string | null>(() => (props.plugin.installedAt ? formatDateTime(props.plugin.installedAt) : null))

/** The moment a package last replaced the first one, null while none has. */
const updatedAt = computed<string | null>(() => (props.plugin.updatedAt ? formatDateTime(props.plugin.updatedAt) : null))

/** The size of the package archive in the largest fitting unit, null when no install was recorded. */
const packageSize = computed<string | null>(() =>
  props.plugin.packageSizeBytes === null || props.plugin.packageSizeBytes === undefined
    ? null
    : formatSizeBytes(props.plugin.packageSizeBytes, locale.value))

/** The kinds of access to a client's hosting the manifest asks for. */
const hostAccess = computed<string[]>(() => props.plugin.hostAccess ?? [])

/** Whether the manifest asks for nothing beyond running. */
const asksForNothing = computed<boolean>(() => hostAccess.value.length === 0 && props.plugin.capabilities.length === 0)

/** The server versions the plugin runs on, in words. */
const hostRange = computed<string>(() => {
  const min = props.plugin.minHostVersion ?? null
  const max = props.plugin.maxHostVersion ?? null
  if (min !== null && max !== null) {
    return t('pluginDetails.versions.hostRange', { min, max })
  }
  if (min !== null) {
    return t('pluginDetails.versions.hostMin', { min })
  }
  if (max !== null) {
    return t('pluginDetails.versions.hostMax', { max })
  }
  return t('pluginDetails.versions.hostAny')
})

/** One fact of an overview card: the value under its caption, the words for a value never recorded and how it is drawn. */
interface OverviewFact {
  /** The value as text; null when the server never recorded it. */
  value: string | null
  /** The text shown in place of a missing value. */
  empty?: string
  /** Draws the value in a monospace face (a digest or a key id). */
  mono?: boolean
  /** `data-testid` of the fact. */
  testid?: string
}

/** An overview fact with the caption key of the schema row it fills. */
interface OverviewRow extends OverviewFact {
  /** Locale key of the caption: the schema row the fact fills. */
  row: string
}

/** What each schema row shows, by the row's caption key; a row with no entry (the staged version of a plugin with none staged) is left out. */
const facts = computed<Readonly<Record<string, OverviewFact>>>(() => ({
  'pluginDetails.overview.author': { value: props.plugin.author || null, empty: t('pluginDetails.overview.notGiven') },
  'pluginDetails.overview.type': { value: typeLabel.value },
  'pluginDetails.overview.category': { value: categoryLabel(props.plugin.category), testid: 'plugin-details-overview-category' },
  'pluginDetails.overview.id': { value: props.plugin.id, mono: true },
  'pluginDetails.overview.state.label': { value: stateLabel.value, testid: 'plugin-details-overview-state' },
  'pluginDetails.overview.sourceLabel': { value: sourceLabel.value, empty: t('pluginDetails.overview.notRecorded'), testid: 'plugin-details-overview-source' },
  'pluginDetails.overview.installedAt': { value: installedAt.value, empty: t('pluginDetails.overview.notRecorded'), testid: 'plugin-details-overview-installed-at' },
  'pluginDetails.overview.updatedAt': { value: updatedAt.value, empty: t('pluginDetails.overview.notUpdated'), testid: 'plugin-details-overview-updated-at' },
  'pluginDetails.overview.installedBy': { value: props.plugin.installedBy ?? null, empty: t('pluginDetails.overview.notRecorded'), testid: 'plugin-details-overview-installed-by' },
  'pluginDetails.overview.signerLabel': { value: signerLabel.value, empty: t('pluginDetails.overview.notRecorded'), testid: 'plugin-details-overview-signer' },
  ...(props.plugin.signerKeyId ? { 'pluginDetails.overview.signerKey': { value: props.plugin.signerKeyId, mono: true, testid: 'plugin-details-overview-signer-key' } } : {}),
  'pluginDetails.overview.size': { value: packageSize.value, empty: t('pluginDetails.overview.notRecorded'), testid: 'plugin-details-overview-size' },
  'pluginDetails.overview.sha256': { value: props.plugin.packageSha256 ?? null, empty: t('pluginDetails.overview.notRecorded'), mono: true, testid: 'plugin-details-overview-sha256' },
  'pluginDetails.overview.installedVersion': { value: props.plugin.version, testid: 'plugin-details-overview-version' },
  ...(props.plugin.stagedVersion ? { 'pluginDetails.overview.stagedVersion': { value: t('pluginDetails.staged', { version: props.plugin.stagedVersion }), testid: 'plugin-details-overview-staged' } } : {}),
  'pluginDetails.overview.hostVersions': { value: hostRange.value, testid: 'plugin-details-overview-hosts' },
  'pluginDetails.overview.sdkVersion': { value: props.plugin.sdkPackageVersion ?? null, empty: t('pluginDetails.overview.notGiven'), mono: true, testid: 'plugin-details-overview-sdk' },
}))

/**
 * The facts of one card, in the order of its schema rows.
 *
 * @param card - A card of the overview's schema.
 * @returns The rows that have a fact to show.
 */
const factsOf = (card: SkeletonDetailCard): OverviewRow[] =>
  (card.rows ?? []).flatMap((row) => {
    const fact = facts.value[row]
    return fact === undefined ? [] : [{ row, ...fact }]
  })
</script>

<template>
  <UiDetailCards :columns="PLUGIN_OVERVIEW_PART.columns" data-testid="plugin-details-overview">
    <template #card-details>
      <UiStack v-if="plugin.refusedRule" :gap="UiGap.Xs" data-testid="plugin-details-refusal">
        <UiText :tone="UiTextTone.Danger" :size="UiTextSize.Sm">{{ t('plugins.pluginCard.brokenRule', { id: plugin.id, rule: plugin.refusedRule }) }}</UiText>
        <UiText v-if="plugin.refusedDetail" :tone="UiTextTone.Muted" :size="UiTextSize.Xs" class="font-mono break-all">{{ plugin.refusedDetail }}</UiText>
      </UiStack>
      <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Sm" data-testid="plugin-details-overview-description">
        {{ plugin.description || t('pluginDetails.overview.noDescription') }}
      </UiText>
      <PluginOverviewFact
        v-for="fact in factsOf(PLUGIN_OVERVIEW_DETAILS_CARD)"
        :key="fact.row"
        :label="t(fact.row)"
        :value="fact.value"
        :empty="fact.empty"
        :mono="fact.mono"
        :data-testid="fact.testid"
      />
    </template>

    <template #card-installation>
      <PluginOverviewFact
        v-for="fact in factsOf(PLUGIN_OVERVIEW_INSTALLATION_CARD)"
        :key="fact.row"
        :label="t(fact.row)"
        :value="fact.value"
        :empty="fact.empty"
        :mono="fact.mono"
        :data-testid="fact.testid"
      />
    </template>

    <template #card-compatibility>
      <PluginOverviewFact
        v-for="fact in factsOf(PLUGIN_OVERVIEW_COMPATIBILITY_CARD)"
        :key="fact.row"
        :label="t(fact.row)"
        :value="fact.value"
        :empty="fact.empty"
        :mono="fact.mono"
        :data-testid="fact.testid"
      />
    </template>

    <template #card-permissions>
      <UiText v-if="asksForNothing" :tone="UiTextTone.Muted" :size="UiTextSize.Sm" data-testid="plugin-details-permissions-none">
        {{ t('plugins.pluginInstallDialog.noRequests') }}
      </UiText>
      <UiStack v-else :gap="UiGap.Xs" data-testid="plugin-details-capabilities">
        <UiText
          v-for="kind in hostAccess"
          :key="kind"
          :tone="UiTextTone.Primary"
          :size="UiTextSize.Sm"
          :data-testid="`plugin-details-host-access-${kind}`"
        >
          {{ t(`plugins.pluginInstallDialog.hostAccess.${kind}`) }}
        </UiText>
        <UiText
          v-for="capability in plugin.capabilities"
          :key="capability"
          :tone="UiTextTone.Primary"
          :size="UiTextSize.Sm"
          :data-testid="`plugin-details-capability-${capability}`"
        >
          {{ t(`plugins.pluginInstallDialog.capabilities.${capability}`) }}
        </UiText>
      </UiStack>
    </template>

    <template #card-notes>
      <!-- The notes are the plugin author's text: drawn as Markdown, and links stay plain text since the author's addresses are not vetted. -->
      <UiMarkdown
        :source="plugin.changelog ?? null"
        :links="false"
        :empty="t('pluginDetails.versions.noChangelog')"
        data-testid="plugin-details-changelog"
      />
    </template>
  </UiDetailCards>
</template>
