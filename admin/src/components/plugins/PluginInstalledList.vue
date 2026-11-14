<script setup lang="ts">
/**
 * The Installed tab's list: a search box and status and category filters (kept in the address under their own
 * keys), then a three-column grid of shared plugin cards. A card opens the plugin's page by its name; its
 * footer holds a gear that opens the same page, or an Update button while the registry offers a newer
 * version. A plugin the host refused explains the broken rule in the card's notice. A failed load offers a
 * retry, an empty server an empty state, and the grid shows card placeholders while the first load runs.
 * The component owns no data: the screen hands it the list and reacts to the update request.
 */
import { computed, reactive } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import PluginCard from '@/components/plugins/PluginCard.vue'
import PluginCardSkeleton from '@/components/plugins/PluginCardSkeleton.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { useFilterQuery } from '@/composables/useFilterQuery'
import { enumValueOf } from '@/lib/enumValue'
import { Icon } from '@/lib/icons'
import { filterInstalledPlugins, InstalledPluginState } from '@/lib/installedPlugins'
import { categoryLabel, fromInstalled } from '@/lib/pluginCard'
import { pluginPath } from '@/lib/routes'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiSize } from '@/utils/ui/size'
import type { PluginCardModel, PluginListItemDto } from '@/types/plugin'
import type { UiFilterChip } from '@/utils/ui/filter'

/** How many card placeholders the grid draws while the list loads (two rows of the three-column grid). */
const PLUGIN_CARD_PLACEHOLDERS = 6

/** Translation function. */
const { t } = useI18n()

/** Router: opens a plugin's page from the gear. */
const router = useRouter()

/** Props for PluginInstalledList. */
const props = defineProps<{
  /** Every installed plugin, before the search and filters. */
  plugins: PluginListItemDto[]
  /** Newer registry version per plugin id, for the plugins that have one on offer. */
  updateVersions: Record<string, string>
  /** True while the list loads. */
  loading: boolean
  /** Sentence for a failed load, null when none. */
  error: string | null
  /** Ids of the plugins whose update is in flight. */
  updatingIds: string[]
}>()

/** Events emitted by PluginInstalledList. */
const emit = defineEmits<{
  /** Emitted with the plugin id and the version when the admin asks to update. */
  update: [id: string, version: string]
  /** Emitted when the admin asks to retry a failed load. */
  retry: []
}>()

/** Closed set of the filter fields; the strings are their keys in the address. */
const InstalledFilterKey = {
  /** The plugin's state on this server. */
  State: 'status',
  /** The plugin's category. */
  Category: 'kind',
} as const

/**
 * The search text and filters, kept in the address under their own keys so they do not collide with the
 * registry's or the tab's.
 */
const filters = reactive(useFilterQuery(Object.values(InstalledFilterKey), { searchKey: 'installedSearch', pageKey: 'installedPage' }))

/** Options of the state filter: any state, then each state a plugin can be in. */
const stateOptions = computed<{ value: string; label: string }[]>(() => [
  { value: '', label: t('shared.any') },
  { value: InstalledPluginState.Active, label: t('shared.active') },
  { value: InstalledPluginState.PendingRestart, label: t('plugins.pluginCard.pendingRestart') },
  { value: InstalledPluginState.Refused, label: t('plugins.pluginCard.refused') },
])

/** Options of the category filter: any category, then each category the installed plugins have. */
const categoryOptions = computed<{ value: string; label: string }[]>(() => [
  { value: '', label: t('plugins.pluginRegistry.allCategories') },
  ...[...new Set(props.plugins.map(plugin => plugin.category))].sort().map(category => ({ value: category, label: categoryLabel(category) })),
])

/** One chip per applied filter, with the value as the operator reads it. */
const chips = computed<UiFilterChip[]>(() => {
  const values = filters.applied
  const state = stateOptions.value.find(option => option.value === values.status)
  const all: UiFilterChip[] = [
    { key: InstalledFilterKey.State, label: t('shared.status'), value: state?.label ?? values.status },
    { key: InstalledFilterKey.Category, label: t('plugins.pluginRegistry.categoryPlaceholder'), value: categoryLabel(values.kind) },
  ]
  return all.filter(chip => chip.value !== '')
})

/**
 * Clears one filter from its chip.
 *
 * @param key - The key the chip reported.
 */
const removeChip = (key: string): void => {
  const known = enumValueOf(InstalledFilterKey, key)
  if (known !== null) {
    void filters.remove(known)
  }
}

/** The installed plugins that match the search text and filters in the address, as card models. */
const cards = computed(() =>
  filterInstalledPlugins(props.plugins, {
    search: filters.appliedSearch,
    state: filters.applied.status,
    category: filters.applied.kind,
  }).map(plugin => fromInstalled(plugin, props.updateVersions[plugin.id] ?? null)),
)

/**
 * Asks the screen to update a plugin to the version its card offers.
 *
 * @param card - The card whose Update button was pressed.
 */
const requestUpdate = (card: PluginCardModel): void => {
  if (card.updateVersion !== null) {
    emit('update', card.id, card.updateVersion)
  }
}

/**
 * Opens a plugin's page.
 *
 * @param id - Plugin id.
 */
const openPlugin = (id: string): void => {
  void router.push(pluginPath(id))
}
</script>

<template>
  <UiErrorState v-if="error" :message="error" :loading="loading" testid="plugins-error" @retry="emit('retry')" />

  <UiGrid v-else-if="loading && plugins.length === 0" :columns="3" gap="md" aria-busy="true" data-testid="plugins-loading">
    <!-- The placeholders are identical and carry no data: the key is the 1-based position in a fixed count, never reordered or removed one by one, so it is stable. -->
    <PluginCardSkeleton v-for="placeholder in PLUGIN_CARD_PLACEHOLDERS" :key="placeholder" />
  </UiGrid>

  <UiEmptyState
    v-else-if="!loading && plugins.length === 0"
    :title="t('plugins.plugins.noPluginsInstalledYet')"
    :description="t('plugins.plugins.noPluginsInstalledHint')"
    data-testid="plugins-empty"
  />

  <UiStack v-else gap="sm" data-testid="plugins-list">
    <UiListToolbar
      v-model:search="filters.search"
      :active-count="filters.activeCount"
      :chips="chips"
      search-testid="plugins-installed-search"
      :search-placeholder="t('plugins.plugins.searchInstalled')"
      @apply="filters.apply()"
      @reset="filters.reset()"
      @cancel="filters.cancel()"
      @remove="removeChip"
      @clear="filters.clearAll()"
    >
      <UiStack>
        <UiField :label="t('shared.status')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="filters.draft.status"
              :options="stateOptions"
              :placeholder="t('shared.status')"
              data-testid="plugins-installed-status"
            />
          </template>
        </UiField>
        <UiField :label="t('plugins.pluginRegistry.categoryPlaceholder')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="filters.draft.kind"
              :options="categoryOptions"
              :placeholder="t('plugins.pluginRegistry.categoryPlaceholder')"
              data-testid="plugins-installed-category"
            />
          </template>
        </UiField>
      </UiStack>
    </UiListToolbar>
    <UiEmptyState v-if="cards.length === 0" :title="t('plugins.plugins.noPluginsMatch')" data-testid="plugins-no-match" />
    <UiGrid v-else :columns="3" gap="md">
      <PluginCard v-for="card in cards" :key="card.id" :plugin="card" :to="pluginPath(card.id)">
        <template v-if="card.refusal" #notice>
          <UiStack gap="xs" :data-testid="`plugin-refusal-${card.id}`">
            <UiText tone="danger" size="sm">{{ t(`plugins.rules.${card.refusal.rule}`) }}</UiText>
            <UiText tone="muted" size="xs" class="font-mono">
              {{ t('plugins.pluginCard.brokenRule', { id: card.id, rule: card.refusal.rule }) }}
            </UiText>
            <UiText v-if="card.refusal.detail" tone="muted" size="xs" class="font-mono break-all">
              {{ card.refusal.detail }}
            </UiText>
          </UiStack>
        </template>
        <template #action>
          <UiButton
            v-if="card.updateVersion"
            type="button"
            :variant="UiButtonVariant.Primary"
            :size="UiSize.Sm"
            :loading="updatingIds.includes(card.id)"
            :data-testid="`plugin-update-button-${card.id}`"
            @click="requestUpdate(card)"
          >
            {{ t('plugins.pluginRegistry.update', { version: card.updateVersion }) }}
          </UiButton>
          <UiIconButton
            v-else
            :label="t('plugins.pluginRegistry.settings')"
            :size="UiSize.Sm"
            :data-testid="`plugin-settings-${card.id}`"
            @click="openPlugin(card.id)"
          >
            <UiIcon :markup="Icon.Settings" size="sm" />
          </UiIconButton>
        </template>
      </PluginCard>
    </UiGrid>
  </UiStack>
</template>
