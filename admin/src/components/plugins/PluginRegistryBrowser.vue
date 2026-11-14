<script setup lang="ts">
/**
 * The Browse tab's list: a page of registry listings drawn as the shared {@link PluginCard} (the search
 * and filters sit above it, in the screen).
 *
 * The list has three columns on a wide screen, two on a medium one and one on a narrow one; while the
 * first page loads the same grid, total line and pager are drawn with card placeholders. Each card opens the plugin's details page and carries its
 * action in the footer: Install for a plugin that is not installed, Update for one with a newer version,
 * and Details in place of Install for a plugin that needs a licence key not yet saved. An installed plugin
 * also shows a gear that opens its details page, where it is configured and removed.
 * Only the card whose install or update runs is disabled. The component owns no data: it reports the
 * chosen page and the install, and the screen hands the result to the store.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import PluginCard from '@/components/plugins/PluginCard.vue'
import PluginCardSkeleton from '@/components/plugins/PluginCardSkeleton.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiResultCount from '@/components/ui/UiResultCount.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { Icon } from '@/lib/icons'
import { fromRegistry } from '@/lib/pluginCard'
import { pluginPath } from '@/lib/routes'
import { PluginPricing } from '@/types/plugin'
import type { PluginRegistryItemDto } from '@/types/plugin'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize } from '@/utils/ui/text'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginRegistryBrowser. */
const props = withDefaults(defineProps<{
  /** The listings of the current page. */
  items: PluginRegistryItemDto[]
  /** Listings matching the filter across all pages. */
  total: number
  /** Listings per page. */
  pageSize: number
  /** Current page, starting at 1. */
  page: number
  /** True while a page is loading. */
  loading: boolean
  /** Sentence for a failed load, null when none. */
  error: string | null
  /** Ids of the plugins whose install or update is in flight; every other card stays usable. */
  installingIds: string[]
  /** Ids of paid plugins with a saved licence key; a paid plugin outside this list offers Details, not Install. */
  licensedIds?: string[]
  /** Staged version per plugin id, for plugins whose upgrade waits for the restart. */
  stagedVersions?: Record<string, string>
}>(), {
  /**
   * Default for `licensedIds`.
   *
   * @returns No licensed plugin.
   */
  licensedIds: () => [],
  /**
   * Default for `stagedVersions`.
   *
   * @returns No staged upgrade.
   */
  stagedVersions: () => ({}),
})

/** Events emitted by PluginRegistryBrowser. */
const emit = defineEmits<{
  /** Emitted with the chosen page. */
  'update:page': [page: number]
  /** Emitted with the plugin id and the version when the admin asks to install or update. */
  install: [id: string, version: string]
  /** Emitted with the plugin id when the admin opens its details page from a button. */
  details: [id: string]
  /** Emitted when the admin asks to retry a failed load. */
  retry: []
}>()

/** True until the first page has answered: the list is drawn with placeholder cards in its real grid. */
const initialLoading = computed<boolean>(() => props.loading && props.items.length === 0)

/**
 * Whether a listing offers an action: Install when absent, Update when a newer version exists.
 *
 * @param item - The listing.
 * @returns True when the card shows an install or update button.
 */
const actionable = (item: PluginRegistryItemDto): boolean => !item.installed || item.updateAvailable

/**
 * Whether a listing needs a licence key: what the listing says when it carries `requiresLicence`,
 * otherwise a paid or private price.
 *
 * @param item - The listing.
 * @returns True when installing it needs a saved key.
 */
const requiresKey = (item: PluginRegistryItemDto): boolean =>
  item.requiresLicence ?? (item.pricing === PluginPricing.Paid || item.pricing === PluginPricing.Private)

/**
 * Whether a listing sends the admin to its details page instead of installing: a plugin that needs a key
 * and is not installed with no saved licence key cannot be installed from the card.
 *
 * @param item - The listing.
 * @returns True when the card shows Details in place of Install.
 */
const needsDetails = (item: PluginRegistryItemDto): boolean =>
  !item.installed && requiresKey(item) && !props.licensedIds.includes(item.id)
</script>

<template>
  <UiStack gap="md" data-testid="plugin-registry">
    <UiErrorState
      v-if="error"
      :message="error"
      :loading="loading"
      testid="plugin-registry-error"
      @retry="emit('retry')"
    />

    <UiEmptyState
      v-else-if="!initialLoading && items.length === 0"
      :title="t('plugins.pluginRegistry.emptyTitle')"
      :description="t('plugins.pluginRegistry.emptyDescription')"
      data-testid="plugin-registry-empty"
    />

    <template v-else>
      <UiResultCount :loading="initialLoading" :size="UiTextSize.Sm" data-testid="plugin-registry-total">
        {{ t('plugins.pluginRegistry.total', { total }) }}
      </UiResultCount>
      <UiGrid :columns="3" gap="md" :aria-busy="initialLoading" data-testid="plugin-registry-list">
        <template v-if="initialLoading">
          <!-- The placeholders are identical and carry no data: the key is the 1-based position in a fixed count, never reordered or removed one by one, so it is stable. -->
          <PluginCardSkeleton v-for="slot in pageSize" :key="slot" />
        </template>
        <PluginCard
          v-for="item in items"
          :key="item.id"
          :plugin="fromRegistry(item, stagedVersions[item.id] ?? null)"
          :to="pluginPath(item.id)"
        >
          <template #action>
            <UiButton
              v-if="needsDetails(item)"
              type="button"
              :variant="UiButtonVariant.Primary"
              size="sm"
              :data-testid="`registry-details-${item.id}`"
              @click="emit('details', item.id)"
            >
              {{ t('plugins.pluginRegistry.details') }}
            </UiButton>
            <UiButton
              v-else-if="actionable(item)"
              type="button"
              :variant="item.installed ? UiButtonVariant.Secondary : UiButtonVariant.Primary"
              size="sm"
              :loading="installingIds.includes(item.id)"
              :disabled="installingIds.includes(item.id)"
              :data-testid="`registry-install-${item.id}`"
              @click="emit('install', item.id, item.latestVersion)"
            >
              <template v-if="item.installed">
                {{ t('plugins.pluginRegistry.update', { version: item.latestVersion }) }}
              </template>
              <template v-else>{{ t('plugins.pluginRegistry.install') }}</template>
            </UiButton>
            <UiIconButton
              v-if="item.installed"
              :label="t('plugins.pluginRegistry.settings')"
              :size="UiSize.Sm"
              :data-testid="`registry-settings-${item.id}`"
              @click="emit('details', item.id)"
            >
              <UiIcon :markup="Icon.Settings" size="sm" />
            </UiIconButton>
          </template>
        </PluginCard>
      </UiGrid>
      <UiPagination
        :page="page"
        :total="total"
        :page-size="pageSize"
        :loading="loading"
        testid="plugin-registry-pagination"
        @update:page="emit('update:page', $event)"
      />
    </template>
  </UiStack>
</template>
