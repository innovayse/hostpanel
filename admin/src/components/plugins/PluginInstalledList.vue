<script setup lang="ts">
/**
 * The Installed tab's list: a search box and status and category filters (kept in the address under their own
 * keys), then a three-column grid of shared plugin cards. A card opens the plugin's page by its name; its
 * footer always holds a gear that opens the same page, and beside it an Update button while the registry
 * offers a newer version, or a placeholder while the registry is still asked. A plugin the host refused explains the broken rule in the card's notice. A failed load offers a
 * retry, an empty server an empty state, and while the first load runs the same toolbar and grid are drawn with card
 * placeholders.
 * The component owns no data: the screen hands it the list and reacts to the update request.
 */
import { computed, reactive } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import PluginCard from '@/components/plugins/PluginCard.vue'
import PluginCardActions from '@/components/plugins/PluginCardActions.vue'
import PluginCardSkeleton from '@/components/plugins/PluginCardSkeleton.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { useFilterQuery } from '@/composables/useFilterQuery'
import { enumValueOf } from '@/lib/enumValue'
import { filterInstalledPlugins, InstalledPluginState } from '@/lib/installedPlugins'
import { categoryLabel, fromInstalled } from '@/lib/pluginCard'
import { pluginPath } from '@/lib/routes'
import { PluginCardAction } from '@/types/plugin'
import type { PluginCardModel, PluginListItemDto } from '@/types/plugin'
import type { UiFilterChip } from '@/utils/ui/filter'
import { UiGap } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

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
  /** True while the registry is asked which plugins have a newer version; the cards hold placeholders for the update state. */
  checkingUpdates: boolean
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

/** True until the first read of the installed plugins has answered: the toolbar and grid are drawn with placeholder cards. */
const initialLoading = computed<boolean>(() => props.loading && props.plugins.length === 0)

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
  { value: InstalledPluginState.Disabled, label: t('shared.disabled') },
  { value: InstalledPluginState.PendingRestart, label: t('plugins.pluginCard.pendingRestart') },
  { value: InstalledPluginState.Refused, label: t('plugins.pluginCard.refused') },
  { value: InstalledPluginState.LoadFailed, label: t('plugins.pluginCard.loadFailed') },
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
 * Whether a card waits for the registry's answer: a refused plugin is never offered an update, so it has nothing to wait for.
 *
 * @param card - The card.
 * @returns True while the update state of the card is still unknown.
 */
const isChecking = (card: PluginCardModel): boolean => props.checkingUpdates && card.refusal === null

/**
 * The one list the footer of a card is drawn from, in order: the update button (or its placeholder) while there is
 * one, and always the gear.
 *
 * @param card - The card.
 * @returns The actions of the card's footer.
 */
const actionsOf = (card: PluginCardModel): PluginCardAction[] => {
  const actions: PluginCardAction[] = []
  if (isChecking(card)) {
    actions.push(PluginCardAction.Checking)
  } else if (card.updateVersion) {
    actions.push(PluginCardAction.Update)
  }
  actions.push(PluginCardAction.Settings)
  return actions
}

/** Test id prefix of each footer control of the installed list. */
const actionTestids: Partial<Record<PluginCardAction, string>> = {
  [PluginCardAction.Checking]: 'plugin-update-checking',
  [PluginCardAction.Update]: 'plugin-update-button',
  [PluginCardAction.Settings]: 'plugin-settings',
}

/**
 * Reacts to a footer action of a card: Update asks the screen to update the plugin to the version the card offers,
 * the gear opens the plugin's page.
 *
 * @param card - The card whose action was pressed.
 * @param action - The pressed action.
 */
const onAction = (card: PluginCardModel, action: PluginCardAction): void => {
  if (action === PluginCardAction.Update && card.updateVersion !== null) {
    emit('update', card.id, card.updateVersion)
  } else if (action === PluginCardAction.Settings) {
    void router.push(pluginPath(card.id))
  }
}
</script>

<template>
  <UiErrorState v-if="error" :message="error" :loading="loading" testid="plugins-error" @retry="emit('retry')" />

  <UiEmptyState
    v-else-if="!initialLoading && plugins.length === 0"
    :title="t('plugins.plugins.noPluginsInstalledYet')"
    :description="t('plugins.plugins.noPluginsInstalledHint')"
    data-testid="plugins-empty"
  />

  <UiStack v-else :gap="UiGap.Sm" :aria-busy="initialLoading ? 'true' : undefined" data-testid="plugins-list">
    <UiListToolbar
      v-model:search="filters.search"
      :active-count="filters.activeCount"
      :chips="chips"
      :loading="initialLoading"
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
    <UiEmptyState v-if="!initialLoading && cards.length === 0" :title="t('plugins.plugins.noPluginsMatch')" data-testid="plugins-no-match" />
    <UiGrid v-else :columns="3" :gap="UiGap.Md">
      <template v-if="initialLoading">
        <!-- The placeholders are identical and carry no data: the key is the 1-based position in a fixed count, never reordered or removed one by one, so it is stable. -->
        <PluginCardSkeleton v-for="placeholder in PLUGIN_CARD_PLACEHOLDERS" :key="placeholder" />
      </template>
      <PluginCard v-for="card in cards" :key="card.id" :plugin="card" :to="pluginPath(card.id)" :checking-update="isChecking(card)">
        <template v-if="card.refusal" #notice>
          <UiStack :gap="UiGap.Xs" :data-testid="`plugin-refusal-${card.id}`">
            <UiText :tone="UiTextTone.Danger" :size="UiTextSize.Sm">{{ t(`plugins.rules.${card.refusal.rule}`) }}</UiText>
            <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs" class="font-mono">
              {{ t('plugins.pluginCard.brokenRule', { id: card.id, rule: card.refusal.rule }) }}
            </UiText>
            <UiText v-if="card.refusal.detail" :tone="UiTextTone.Muted" :size="UiTextSize.Xs" class="font-mono break-all">
              {{ card.refusal.detail }}
            </UiText>
          </UiStack>
        </template>
        <template #action>
          <PluginCardActions
            :plugin-id="card.id"
            :actions="actionsOf(card)"
            :version="card.updateVersion"
            :busy="updatingIds.includes(card.id)"
            :testid-prefixes="actionTestids"
            @select="onAction(card, $event)"
          />
        </template>
      </PluginCard>
    </UiGrid>
  </UiStack>
</template>
