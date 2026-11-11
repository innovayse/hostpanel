<script setup lang="ts">
/**
 * The Browse tab's list: a search box, a category select and a page of registry cards.
 *
 * The card is not interactive itself: its name is a link-looking button that opens the plugin's details page. Top to bottom it shows the
 * picture and name with the Verified badge at the right end, the publisher with its kind, a clamped description, up to three tag chips and
 * a "+N" chip, the category and price chips, a footer under a divider (downloads on the left, latest and SDK version on the right), then
 * the installed version, staged or update state and the actions. Cards in a row share one height.
 * The action is Install for a plugin that is not installed, Update for one with a newer version and
 * Uninstall for an installed one; a paid plugin without a saved key shows Details instead of Install.
 * Only the card whose install or removal runs is disabled. The component owns no data: it
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
import UiIcon from '@/components/ui/UiIcon.vue'
import UiCollapse from '@/components/ui/UiCollapse.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiMarkdown from '@/components/ui/UiMarkdown.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiPluginAvatar from '@/components/ui/UiPluginAvatar.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { PluginCategory, PluginCategoryAll, PluginPricing } from '@/types/plugin'
import type { PluginCategoryFilter, PluginRegistryItemDto } from '@/types/plugin'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTone } from '@/utils/ui/tone'

/** Translation function and the interface language. */
const { t, locale } = useI18n()

/** Milliseconds the search waits after the last keystroke before it is reported. */
const SEARCH_DELAY_MS = 300

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
  /** Current search text. */
  query: string
  /** Current category filter; empty for every category. */
  category: PluginCategoryFilter
  /** True while a page is loading. */
  loading: boolean
  /** Sentence for a failed load, null when none. */
  error: string | null
  /** Ids of the plugins whose install or update is in flight; every other card stays usable. */
  installingIds: string[]
  /** Id of the plugin whose removal is in flight, null when none. */
  removingId: string | null
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
  /** Emitted with the search text once the typing has paused. */
  'update:query': [query: string]
  /** Emitted with the chosen category, empty for every category. */
  'update:category': [category: PluginCategoryFilter]
  /** Emitted with the chosen page. */
  'update:page': [page: number]
  /** Emitted with the plugin id and the version when the admin asks to install or update. */
  install: [id: string, version: string]
  /** Emitted with the plugin id when the admin asks to uninstall it. */
  remove: [id: string]
  /** Emitted with the plugin id when the admin opens its details page. */
  details: [id: string]
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

/** Icon per registry category, drawn in the category chip. */
const categoryIcon: Record<PluginCategory, string> = {
  [PluginCategory.Payment]: Icon.Dollar,
  [PluginCategory.Provisioning]: Icon.Server,
  [PluginCategory.Registrar]: Icon.Globe,
  [PluginCategory.App]: Icon.Puzzle,
  [PluginCategory.Other]: Icon.Box,
}

/** How many tag chips a card shows before the rest collapse into one "+N" chip. */
const VISIBLE_TAGS = 3

/**
 * The tags a card shows as chips: the first {@link VISIBLE_TAGS}.
 *
 * @param item - The listing.
 * @returns The visible tags; empty when the listing has none.
 */
const visibleTags = (item: PluginRegistryItemDto): string[] => (item.tags ?? []).slice(0, VISIBLE_TAGS)

/**
 * How many tags are not shown as chips.
 *
 * @param item - The listing.
 * @returns The number folded into the "+N" chip; 0 when every tag is shown.
 */
const hiddenTagCount = (item: PluginRegistryItemDto): number => Math.max((item.tags ?? []).length - VISIBLE_TAGS, 0)

/** Formatter of download counts in the interface language. */
const downloadFormat = computed<Intl.NumberFormat>(() => new Intl.NumberFormat(locale.value))

/** Badge tone per price. */
const pricingTone: Record<PluginPricing, UiTone> = {
  Free: UiTone.Neutral,
  Paid: UiTone.Warning,
  Private: UiTone.Info,
}

/** Ids of the cards whose release notes are open. */
const notesOpenIds = ref<string[]>([])

/**
 * Opens or closes the release notes of one card.
 *
 * @param id - The listing id.
 */
const toggleNotes = (id: string): void => {
  notesOpenIds.value = notesOpenIds.value.includes(id)
    ? notesOpenIds.value.filter(open => open !== id)
    : [...notesOpenIds.value, id]
}

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

/**
 * Whether the card's own install, update or removal is running; only that card is disabled.
 *
 * @param id - The listing id.
 * @returns True while this card is busy.
 */
const busy = (id: string): boolean => props.installingIds.includes(id) || props.removingId === id
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
        <UiCard
          v-for="item in items"
          :key="item.id"
          padding="sm"
          class="h-full transition-colors hover:border-accent"
          :data-testid="`registry-card-${item.id}`"
        >
          <UiStack gap="sm" class="h-full">
            <UiStack direction="row" align="center" justify="between" gap="sm">
              <UiStack direction="row" align="center" gap="sm" class="min-w-0">
                <UiPluginAvatar :name="item.name" :icon-url="item.iconUrl" />
                <UiButton
                  type="button"
                  :variant="UiButtonVariant.Link"
                  class="min-w-0 text-left text-lg font-semibold"
                  :data-testid="`registry-open-${item.id}`"
                  @click="emit('details', item.id)"
                >
                  {{ item.name }}
                </UiButton>
              </UiStack>
              <UiBadge
                v-if="item.verified"
                :tone="UiTone.Success"
                small
                :data-testid="`registry-verified-${item.id}`"
              >
                {{ t('plugins.pluginRegistry.verified') }}
              </UiBadge>
            </UiStack>
            <UiStack direction="row" align="center" gap="sm" wrap>
              <UiText tone="muted" size="xs" :data-testid="`registry-author-${item.id}`">
                {{ t('plugins.pluginRegistry.byAuthor', { author: item.author }) }}
              </UiText>
              <UiBadge
                v-if="item.publisherKind"
                :tone="UiTone.Neutral"
                small
                :data-testid="`registry-publisher-kind-${item.id}`"
              >
                {{ t(`plugins.pluginRegistry.publisherKind.${item.publisherKind}`) }}
              </UiBadge>
            </UiStack>
            <UiText tone="muted" size="sm" class="line-clamp-3" :data-testid="`registry-description-${item.id}`">
              {{ item.description }}
            </UiText>
            <UiStack v-if="item.latestVersion" gap="xs">
              <UiButton
                type="button"
                :variant="UiButtonVariant.Link"
                size="sm"
                class="self-start"
                :aria-expanded="notesOpenIds.includes(item.id)"
                :data-testid="`registry-notes-toggle-${item.id}`"
                @click="toggleNotes(item.id)"
              >
                {{ notesOpenIds.includes(item.id) ? t('plugins.pluginRegistry.hideNotes') : t('plugins.pluginRegistry.showNotes', { version: item.latestVersion }) }}
              </UiButton>
              <UiCollapse :open="notesOpenIds.includes(item.id)">
                <UiMarkdown
                  :source="item.latestChangelog ?? null"
                  :empty="t('plugins.pluginRegistry.noNotes')"
                  :data-testid="`registry-notes-${item.id}`"
                />
              </UiCollapse>
            </UiStack>
            <UiStack v-if="visibleTags(item).length" direction="row" align="center" gap="xs" wrap :data-testid="`registry-tags-${item.id}`">
              <UiBadge v-for="tag in visibleTags(item)" :key="tag" :tone="UiTone.Neutral" small>
                {{ t('plugins.pluginRegistry.tag', { tag }) }}
              </UiBadge>
              <UiBadge v-if="hiddenTagCount(item)" :tone="UiTone.Neutral" small :data-testid="`registry-more-tags-${item.id}`">
                {{ t('plugins.pluginRegistry.moreTags', { count: hiddenTagCount(item) }) }}
              </UiBadge>
            </UiStack>
            <UiStack direction="row" align="center" gap="sm" wrap>
              <UiBadge :tone="UiTone.Neutral" small :data-testid="`registry-category-${item.id}`">
                <UiStack direction="row" align="center" gap="xs">
                  <UiIcon :markup="categoryIcon[item.category]" size="sm" />
                  {{ t(`plugins.pluginRegistry.category.${item.category}`) }}
                </UiStack>
              </UiBadge>
              <UiBadge :tone="pricingTone[item.pricing]" small :data-testid="`registry-price-${item.id}`">
                {{ t(`plugins.pluginRegistry.pricing.${item.pricing}`) }}
              </UiBadge>
              <UiBadge
                v-if="stagedVersions[item.id]"
                :tone="UiTone.Info"
                small
                :data-testid="`registry-staged-${item.id}`"
              >
                {{ t('plugins.pluginCard.staged', { version: stagedVersions[item.id] }) }}
              </UiBadge>
              <UiBadge
                v-else-if="item.installed && item.updateAvailable"
                :tone="UiTone.Warning"
                small
                :data-testid="`registry-update-state-${item.id}`"
              >
                {{ t('plugins.pluginCard.updateAvailable', { version: item.latestVersion }) }}
              </UiBadge>
            </UiStack>
            <UiStack
              direction="row"
              align="center"
              justify="between"
              gap="sm"
              class="mt-auto border-t border-border pt-2"
              :data-testid="`registry-footer-${item.id}`"
            >
              <UiStack direction="row" align="center" gap="xs" :data-testid="`registry-downloads-${item.id}`">
                <template v-if="item.downloads != null">
                  <UiIcon :markup="Icon.Download" size="sm" />
                  <UiText tone="muted" size="xs">
                    {{ t('plugins.pluginRegistry.downloads', { count: downloadFormat.format(item.downloads) }) }}
                  </UiText>
                </template>
              </UiStack>
              <UiStack direction="row" align="center" gap="xs" :data-testid="`registry-version-${item.id}`">
                <UiIcon :markup="Icon.Tag" size="sm" />
                <UiText tone="muted" size="xs">
                  {{ t('plugins.pluginRegistry.version', { version: item.latestVersion }) }}
                  <template v-if="item.sdkVersion">
                    · {{ t('plugins.pluginRegistry.sdkVersion', { version: item.sdkVersion }) }}
                  </template>
                </UiText>
              </UiStack>
            </UiStack>
            <UiStack direction="row" align="center" gap="sm" wrap>
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
                :disabled="busy(item.id)"
                :data-testid="`registry-install-${item.id}`"
                @click="emit('install', item.id, item.latestVersion)"
              >
                <template v-if="installingIds.includes(item.id)">{{ t('plugins.pluginRegistry.installing') }}</template>
                <template v-else-if="item.installed">
                  {{ t('plugins.pluginRegistry.update', { version: item.latestVersion }) }}
                </template>
                <template v-else>{{ t('plugins.pluginRegistry.install') }}</template>
              </UiButton>
              <UiBadge
                v-if="item.installed"
                :tone="UiTone.Info"
                small
                :data-testid="`registry-installed-${item.id}`"
              >
                {{ t('plugins.pluginRegistry.installed', { version: item.installedVersion ?? item.latestVersion }) }}
              </UiBadge>
              <UiButton
                v-if="item.installed && item.updateAvailable && !needsDetails(item)"
                type="button"
                :variant="UiButtonVariant.Link"
                size="sm"
                :data-testid="`registry-changes-${item.id}`"
                @click="emit('details', item.id)"
              >
                {{ t('plugins.pluginRegistry.whatChanged') }}
              </UiButton>
              <UiButton
                v-if="item.installed"
                type="button"
                :variant="UiButtonVariant.Danger"
                size="sm"
                :disabled="busy(item.id)"
                :data-testid="`registry-uninstall-${item.id}`"
                @click="emit('remove', item.id)"
              >
                {{ removingId === item.id ? t('plugins.pluginCard.removing') : t('plugins.pluginRegistry.uninstall') }}
              </UiButton>
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
