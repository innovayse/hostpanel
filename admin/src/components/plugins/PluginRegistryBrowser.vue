<script setup lang="ts">
/**
 * The Browse tab's list: a search box, a category select and a page of registry cards.
 *
 * Each card shows the plugin's picture, name, publisher, versions, a Verified badge for a verified
 * publisher and the price. The action is Install for a plugin that is not installed, Update for one
 * with a newer version, and a plain "Installed" badge otherwise. The component owns no data: it
 * reports the typed search (after a short pause, so a request is not sent per keystroke), the chosen
 * category, the chosen page and the install, and the screen hands the result to the store.
 */
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiPluginAvatar from '@/components/ui/UiPluginAvatar.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { PluginCategory, PluginCategoryAll } from '@/types/plugin'
import type { PluginCategoryFilter, PluginPricing, PluginRegistryItemDto } from '@/types/plugin'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** Milliseconds the search waits after the last keystroke before it is reported. */
const SEARCH_DELAY_MS = 300

/** Props for PluginRegistryBrowser. */
const props = defineProps<{
  /** The listings of the current page. */
  items: PluginRegistryItemDto[]
  /** Listings matching the filter across all pages. */
  total: number
  /** Listings per page. */
  pageSize: number
  /** Current page, starting at 1. */
  page: number
  /** Current search text. */
  query: string
  /** Current category filter; empty for every category. */
  category: PluginCategoryFilter
  /** True while a page is loading. */
  loading: boolean
  /** Sentence for a failed load, null when none. */
  error: string | null
  /** Id of the plugin whose install is in flight, null when none. */
  installingId: string | null
}>()

/** Events emitted by PluginRegistryBrowser. */
const emit = defineEmits<{
  /** Emitted with the search text once the typing has paused. */
  'update:query': [query: string]
  /** Emitted with the chosen category, empty for every category. */
  'update:category': [category: PluginCategoryFilter]
  /** Emitted with the chosen page. */
  'update:page': [page: number]
  /** Emitted with the plugin id and the version when the admin asks to install or update. */
  install: [id: string, version: string]
  /** Emitted when the admin asks to retry a failed load. */
  retry: []
}>()

/** What the search box shows; the store's text follows it after the pause. */
const searchText = ref(props.query)

/** Pending pause before the typed text is reported, null when none. */
let searchTimer: ReturnType<typeof setTimeout> | null = null

// A query changed from outside (a reset) replaces the box.
watch(() => props.query, (next) => {
  searchText.value = next
})

// Reports the typed text after the pause; a newer keystroke restarts the pause.
watch(searchText, (next) => {
  if (searchTimer !== null) {
    clearTimeout(searchTimer)
  }
  searchTimer = setTimeout(() => {
    searchTimer = null
    if (next !== props.query) {
      emit('update:query', next)
    }
  }, SEARCH_DELAY_MS)
})

// A pause still pending when the tab closes must not fire into a gone screen.
onBeforeUnmount(() => {
  if (searchTimer !== null) {
    clearTimeout(searchTimer)
  }
})

/** Options of the category select: every category, then one per registry category. */
const categoryOptions = computed<{ value: PluginCategoryFilter; label: string }[]>(() => [
  { value: PluginCategoryAll, label: t('plugins.pluginRegistry.allCategories') },
  ...Object.values(PluginCategory).map(value => ({ value, label: t(`plugins.pluginRegistry.category.${value}`) })),
])

/** Badge tone per price. */
const pricingTone: Record<PluginPricing, UiTone> = {
  Free: UiTone.Neutral,
  Paid: UiTone.Warning,
  Private: UiTone.Info,
}

/**
 * Whether a listing offers an action: Install when absent, Update when a newer version exists.
 *
 * @param item - The listing.
 * @returns True when the card shows an install or update button.
 */
const actionable = (item: PluginRegistryItemDto): boolean => !item.installed || item.updateAvailable
</script>

<template>
  <UiStack gap="md" data-testid="plugin-registry">
    <UiStack direction="row" align="center" gap="md" wrap>
      <UiInput
        v-model="searchText"
        type="search"
        :placeholder="t('plugins.pluginRegistry.search')"
        :aria-label="t('plugins.pluginRegistry.searchLabel')"
        class="min-w-56 flex-1"
        data-testid="plugin-registry-search"
      />
      <UiSelect
        :model-value="category"
        :options="categoryOptions"
        :placeholder="t('plugins.pluginRegistry.categoryPlaceholder')"
        class="w-56"
        data-testid="plugin-registry-category"
        @update:model-value="emit('update:category', $event)"
      />
    </UiStack>

    <UiErrorState
      v-if="error"
      :message="error"
      :loading="loading"
      testid="plugin-registry-error"
      @retry="emit('retry')"
    />

    <UiSkeleton v-else-if="loading && items.length === 0" :lines="3" data-testid="plugin-registry-loading" />

    <UiEmptyState
      v-else-if="items.length === 0"
      :title="t('plugins.pluginRegistry.emptyTitle')"
      :description="t('plugins.pluginRegistry.emptyDescription')"
      data-testid="plugin-registry-empty"
    />

    <template v-else>
      <UiText tone="muted" size="sm" data-testid="plugin-registry-total">
        {{ t('plugins.pluginRegistry.total', { total }) }}
      </UiText>
      <UiGrid :columns="2" gap="md" data-testid="plugin-registry-list">
        <UiCard v-for="item in items" :key="item.id" padding="sm" :data-testid="`registry-card-${item.id}`">
          <UiStack direction="row" align="start" gap="md">
            <UiPluginAvatar :name="item.name" :icon-url="item.iconUrl" />
            <UiStack gap="xs" class="min-w-0 flex-1">
              <UiStack direction="row" align="center" gap="sm" wrap>
                <UiText tone="primary" size="lg" class="font-semibold">{{ item.name }}</UiText>
                <UiBadge
                  v-if="item.verified"
                  :tone="UiTone.Success"
                  small
                  :data-testid="`registry-verified-${item.id}`"
                >
                  {{ t('plugins.pluginRegistry.verified') }}
                </UiBadge>
                <UiBadge :tone="pricingTone[item.pricing]" small>
                  {{ t(`plugins.pluginRegistry.pricing.${item.pricing}`) }}
                </UiBadge>
              </UiStack>
              <UiText tone="muted" size="sm" class="line-clamp-2">{{ item.description }}</UiText>
              <UiText tone="muted" size="xs">
                {{ t('plugins.pluginRegistry.byAuthor', { author: item.author }) }}
                · {{ t('plugins.pluginRegistry.version', { version: item.latestVersion }) }}
                · {{ t('plugins.pluginRegistry.sdkVersion', { version: item.sdkVersion }) }}
              </UiText>
              <UiStack direction="row" align="center" gap="sm">
                <UiButton
                  v-if="actionable(item)"
                  type="button"
                  :variant="item.installed ? UiButtonVariant.Secondary : UiButtonVariant.Primary"
                  size="sm"
                  :disabled="installingId !== null"
                  :data-testid="`registry-install-${item.id}`"
                  @click="emit('install', item.id, item.latestVersion)"
                >
                  <template v-if="installingId === item.id">{{ t('plugins.pluginRegistry.installing') }}</template>
                  <template v-else-if="item.installed">
                    {{ t('plugins.pluginRegistry.update', { version: item.latestVersion }) }}
                  </template>
                  <template v-else>{{ t('plugins.pluginRegistry.install') }}</template>
                </UiButton>
                <UiBadge
                  v-else
                  :tone="UiTone.Info"
                  small
                  :data-testid="`registry-installed-${item.id}`"
                >
                  {{ t('plugins.pluginRegistry.installed', { version: item.installedVersion ?? item.latestVersion }) }}
                </UiBadge>
              </UiStack>
            </UiStack>
          </UiStack>
        </UiCard>
      </UiGrid>
      <UiPagination
        :page="page"
        :total="total"
        :page-size="pageSize"
        testid="plugin-registry-pagination"
        @update:page="emit('update:page', $event)"
      />
    </template>
  </UiStack>
</template>
