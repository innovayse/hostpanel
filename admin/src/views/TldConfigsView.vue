<script setup lang="ts">
/**
 * TLD pricing list view: a search box, the filter dialog with its removable chips, and a paged table of the TLD
 * configurations.
 *
 * The search text, the registrar and enabled filters and the page live in the address (`useListQuery`), so a reload,
 * a shared link and the back button restore the same list, and the API applies them before it pages. Shows the TLD,
 * provider, status, cost and sell price, margin, categories, last sync time and the edit and delete actions. Supports
 * import from a registrar provider and a manual price sync, and asks before deleting in a danger prompt. The outcome
 * of an import, sync or delete (success, or the API's sentence) shows in the result line and reloads the page the
 * address names; a refused load offers a retry.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiResultCount from '@/components/ui/UiResultCount.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { useResultBanner } from '@/composables/useResultBanner'
import { useListQuery } from '@/composables/useListQuery'
import { useDocsGuide } from '@/composables/useDocsGuide'
import PageHelpLink from '@/components/layout/PageHelpLink.vue'
import { ENABLED_FILTER_OPTIONS, REGISTRAR_MODULE_OPTIONS } from '@/lib/constants'
import { enabledOfFilter } from '@/lib/enabledFilter'
import { Icon } from '@/lib/icons'
import { UiButtonVariant } from '@/utils/ui/button'
import { formatRelativeFromNow, EMPTY_VALUE } from '@/lib/format'
import { formatMarginPercent, formatTldPrice, marginPercentTone } from '@/lib/tldPricing'
import { statusLabel } from '@/lib/tones'
import { useTldConfigsStore } from '@/stores/tldConfigs'
import { EnabledFilter } from '@/types/enabledfilter'
import { RegistrarModule } from '@/types/registrarmodule'
import type { TldConfigListFilters, TldConfigListItem } from '@/types/tldconfig'
import { AppRoute, createPath, editPath } from '@/lib/routes'
import { UiTableDensity } from '@/utils/ui/injection'
import { UiTextSize } from '@/utils/ui/text'

const { t, locale } = useI18n()

/** The filter fields the dialog edits; each is one query key and one `TldConfigListFilters` member. */
const FilterKey = {
  Registrar: 'registrarModule',
  Enabled: 'enabled',
} as const

/** TLD configs store: the page, the loading flag and the refusal sentences. */
const store = useTldConfigsStore()

/** The result banner for import, sync and delete. */
const banner = useResultBanner()
/** Link to the docs guide for this screen, or null when docsUrl is not configured. */
const { guideUrl } = useDocsGuide()

/** The filters, the search text and the page of the list, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, narrowed, loadOnChange, stepBackWhenEmpty, enumOf, chipsOf }
  = useListQuery(Object.values(FilterKey))

/** Tone of the result banner: danger for a refusal, success otherwise. */
const bannerTone = computed<UiTone>(() => (banner.isError.value ? UiTone.Danger : UiTone.Success))

/** Whether an import operation is in progress. */
const importing = ref(false)

/** Whether a sync operation is in progress. */
const syncing = ref(false)

/** The TLD config pending deletion, or null if the dialog is closed. */
const deleteTarget = ref<TldConfigListItem | null>(null)

/** Whether a delete operation is in progress. */
const deleting = ref(false)

/** Badge tone of each registrar module. */
const PROVIDER_TONES: Record<RegistrarModule, UiTone> = {
  [RegistrarModule.NameAm]: 'info',
  [RegistrarModule.Namecheap]: 'warning',
}

/** Registrar filter options; the empty value means any. */
const registrarOptions = computed(() => [{ value: '', label: t('shared.any') }, ...REGISTRAR_MODULE_OPTIONS])

/** Enabled-state filter options; the empty value means any. */
const enabledOptions = computed(() => [{ value: '', label: t('shared.any') }, ...ENABLED_FILTER_OPTIONS])

/** The registrar in the address as a registrar module, undefined when none is set or the value is not one. */
const registrarOfAddress = enumOf(FilterKey.Registrar, RegistrarModule)

/** The enabled state in the address as a filter choice, undefined when none is set or the value is not one. */
const enabledOfAddress = enumOf(FilterKey.Enabled, EnabledFilter)

/** What the address narrows the list by, as the store sends it to the API. */
const filters = computed<TldConfigListFilters>(() => ({
  search: appliedSearch.value,
  registrarModule: registrarOfAddress.value,
  enabled: enabledOfFilter(enabledOfAddress.value),
}))

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(() => [
  {
    key: FilterKey.Registrar,
    label: t('shared.registrar'),
    value: registrarOfAddress.value === undefined ? undefined : statusLabel(REGISTRAR_MODULE_OPTIONS, registrarOfAddress.value),
  },
  {
    key: FilterKey.Enabled,
    label: t('shared.status'),
    value: enabledOfAddress.value === undefined ? undefined : statusLabel(ENABLED_FILTER_OPTIONS, enabledOfAddress.value),
  },
])

/**
 * Loads the page the address names with the filters and search it holds.
 *
 * @returns True when loaded; false when refused (see `store.error`).
 */
const load = (): Promise<boolean> => store.fetchAll(page.value, filters.value)

// Every change of a filter, the search or the page loads the list the address names.
loadOnChange(load)

/** Whether the current page has any TLD to list. */
const hasRows = computed<boolean>(() => store.configs.length > 0)

/** True while the first load is in flight and there are no rows yet; the toolbar and the table then keep their layout. */
const initialLoading = computed<boolean>(() => store.loading && !hasRows.value)

/** Route of the create TLD config page, opened by the header link. */
const createRoute = createPath(AppRoute.SettingsTldPricing)

/**
 * Opens the delete confirmation for a TLD config.
 *
 * @param item - The TLD config to delete.
 */
const promptDelete = (item: TldConfigListItem): void => {
  deleteTarget.value = item
}

/**
 * Confirms and deletes the targeted TLD config, then reloads the page. When the delete emptied the last page, the
 * list steps back to the page before it.
 *
 * @returns Resolves when the delete and the reload have settled.
 */
const confirmDelete = async (): Promise<void> => {
  if (!deleteTarget.value) {
    return
  }
  banner.clear()
  deleting.value = true
  const removed = await store.remove(deleteTarget.value.id)
  if (removed) {
    banner.show(t('shared.recordDeleted'), false)
    await load()
    await stepBackWhenEmpty(!hasRows.value)
  } else {
    banner.show(store.actionError ?? t('errors.noAnswer'), true)
  }
  deleting.value = false
  deleteTarget.value = null
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDelete = (): void => {
  if (!deleting.value) {
    deleteTarget.value = null
  }
}

/**
 * Imports TLDs from the chosen registrar provider, then reloads the page.
 *
 * @param module - Registrar module name.
 */
const handleImport = async (module: string): Promise<void> => {
  importing.value = true
  banner.clear()
  const result = await store.importFromProvider(module)
  if (result) {
    banner.show(t('settings.tldConfigs.importComplete', { imported: result.imported, updated: result.updated }), false)
    await load()
  } else if (store.actionError) {
    banner.show(store.actionError, true)
  }
  importing.value = false
}

/** Triggers a manual price sync from all providers, then reloads the page. */
const handleSync = async (): Promise<void> => {
  syncing.value = true
  banner.clear()
  if (await store.syncPrices()) {
    banner.show(t('settings.tldConfigs.priceSyncComplete'), false)
    await load()
  } else if (store.actionError) {
    banner.show(store.actionError, true)
  }
  syncing.value = false
}

/**
 * Badge tone of a registrar module; a module this build does not know reads as neutral.
 *
 * @param module - Registrar module name as the API sent it.
 * @returns The kit tone.
 */
const providerTone = (module: string): UiTone => PROVIDER_TONES[module as RegistrarModule] ?? UiTone.Neutral

/**
 * Describes when a TLD was last synced, in the active language.
 *
 * @param iso - ISO 8601 timestamp, or null when it never was.
 * @returns "Never", or a relative wording like "5 minutes ago".
 */
const lastSyncedText = (iso: string | null): string =>
  iso ? formatRelativeFromNow(iso, locale.value) : t('settings.tldConfigs.never')

/**
 * Row actions for a TLD configuration entry.
 *
 * @param item - The TLD entry whose actions to build.
 * @returns The action list passed to UiRowActions.
 */
const tldActions = (item: TldConfigListItem): UiMenuAction[] => {
  /**
   * Prompts deletion of this TLD entry.
   * @returns Nothing.
   */
  const onDelete = (): void => promptDelete(item)
  return [
    { label: t('settings.tldConfigs.editTld'), testid: `tld-edit-${item.tld}`, to: editPath(AppRoute.SettingsTldPricing, item.id) },
    { label: t('settings.tldConfigs.deleteTld'), testid: `tld-delete-${item.tld}`, onSelect: onDelete, danger: true },
  ]
}
</script>

<template>
  <UiStack gap="none" data-testid="tld-configs-view">
    <UiPageHeader :title="t('settings.tldConfigs.tldPricing')" :subtitle="t('settings.tldConfigs.configureDomainExtensionPricingAnd')">
      <template #actions>
        <PageHelpLink :url="guideUrl" />
        <!-- Import from a provider: choosing one starts the import -->
        <UiSelect
          model-value=""
          :options="REGISTRAR_MODULE_OPTIONS"
          :placeholder="t('settings.tldConfigs.import')"
          :disabled="importing"
          data-testid="tld-configs-import"
          class="w-40"
          @update:model-value="handleImport"
        />
        <UiButton
          type="button"
          variant="secondary"
          :loading="syncing"
          data-testid="tld-configs-sync"
          @click="handleSync"
        >
          <UiIcon :markup="Icon.Refresh" size="sm" />
          {{ t('settings.tldConfigs.syncPrices') }}
        </UiButton>
        <UiLink :to="createRoute" :variant="UiButtonVariant.Primary" data-testid="tld-configs-add">
          <UiIcon :markup="Icon.Add" size="sm" />
          {{ t('settings.tldConfigs.addTld') }}
        </UiLink>
      </template>
    </UiPageHeader>

    <UiStatusMessage :message="banner.message.value" :tone="bannerTone" class="mb-5" data-testid="tld-configs-result" />

    <UiListToolbar
      v-model:search="search"
      search-testid="tld-configs-search"
      :search-placeholder="t('settings.tldConfigs.searchTlds')"
      :active-count="activeCount"
      :chips="chips"
      :loading="initialLoading"
      @apply="apply"
      @reset="reset"
      @cancel="cancel"
      @remove="remove"
      @clear="clearAll"
    >
      <UiStack>
        <UiField :label="t('shared.registrar')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="draft.registrarModule"
              :options="registrarOptions"
              :placeholder="t('shared.any')"
              data-testid="tld-configs-filter-registrar"
            />
          </template>
        </UiField>
        <UiField :label="t('shared.status')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="draft.enabled"
              :options="enabledOptions"
              :placeholder="t('shared.any')"
              data-testid="tld-configs-filter-enabled"
            />
          </template>
        </UiField>
      </UiStack>
    </UiListToolbar>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="tld-configs-error"
      @retry="load"
    />

    <UiCard v-else-if="hasRows || store.loading" class="overflow-hidden">
      <UiTable :density="UiTableDensity.Comfortable" :loading="initialLoading" data-testid="tld-configs-table">
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader align="left">{{ t('settings.tldConfigs.tld') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('settings.tldConfigs.provider') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('shared.status') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('settings.tldConfigs.cost1yr') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('settings.tldConfigs.sell1yr') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('settings.tldConfigs.margin') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('shared.categories') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('settings.tldConfigs.lastSynced') }}</UiTableHeader>
            <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow
            v-for="item in store.configs"
            :key="item.id"
            hoverable
            :data-testid="`tld-row-${item.tld}`"
          >
            <UiTableCell class="font-semibold text-text-primary">.{{ item.tld }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="providerTone(item.registrarModule)" small>
                {{ item.registrarModule }}
              </UiBadge>
            </UiTableCell>
            <UiTableCell>
              <UiBadge :tone="item.isEnabled ? 'success' : 'danger'" small>
                {{ item.isEnabled ? t('shared.enabled') : t('shared.disabled') }}
              </UiBadge>
            </UiTableCell>
            <UiTableCell numeric muted>
              {{ formatTldPrice(item.costRegister1yr, item.currency) }}
            </UiTableCell>
            <UiTableCell numeric muted>
              {{ formatTldPrice(item.sellRegister1yr, item.sellCurrency) }}
            </UiTableCell>
            <UiTableCell numeric>
              <UiText :tone="marginPercentTone(item.marginPercent)" size="lg" class="font-medium">
                {{ formatMarginPercent(item.marginPercent) }}
              </UiText>
            </UiTableCell>
            <UiTableCell>
              <UiStack direction="row" gap="xs" wrap>
                <UiBadge v-for="category in item.categories" :key="category" small>
                  {{ category }}
                </UiBadge>
                <UiText v-if="item.categories.length === 0" tone="muted" size="md">{{ EMPTY_VALUE }}</UiText>
              </UiStack>
            </UiTableCell>
            <UiTableCell muted>{{ lastSyncedText(item.lastSyncedAt) }}</UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="tldActions(item)" :testid="`tld-actions-${item.tld}`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>

      <template #footer>
        <UiStack direction="row" justify="between" align="center" wrap>
          <UiResultCount :loading="initialLoading" :size="UiTextSize.Xs">
            {{ t('settings.tldConfigs.totalcountTldValue', { totalCount: store.totalCount }) }}
          </UiResultCount>
          <UiPagination
            :page="page"
            :total="store.totalCount"
            :page-size="store.pageSize"
            testid="tld-configs-pagination"
            @update:page="setPage"
          />
        </UiStack>
      </template>
    </UiCard>

    <UiCard v-else>
      <UiEmptyState
        :title="narrowed ? t('settings.tldConfigs.noTldsMatchYourFilters') : t('settings.tldConfigs.noTldConfigurationsFoundAdd')"
        data-testid="tld-configs-empty"
      >
        <template #icon>
          <UiIcon :markup="Icon.Globe" size="lg" />
        </template>
        <template v-if="activeCount > 0" #actions>
          <UiButton :variant="UiButtonVariant.Link" data-testid="tld-configs-clear-empty" @click="clearAll">
            {{ t('settings.tldConfigs.clearFilters') }}
          </UiButton>
        </template>
      </UiEmptyState>
    </UiCard>

    <!-- Delete confirmation -->
    <UiDialog
      v-if="deleteTarget"
      :variant="UiDialogVariant.Confirm"
      :title="t('settings.tldConfigs.deleteTldConfiguration')"
      :message="t('settings.tldConfigs.deleteMessage', { tld: deleteTarget.tld })"
      :confirm-label="t('shared.delete')"
      :loading="deleting"
      :tone="UiTone.Danger"
      @confirm="confirmDelete"
      @close="dismissDelete"
    />
  </UiStack>
</template>
