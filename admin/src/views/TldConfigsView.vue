<script setup lang="ts">
/**
 * TLD pricing list view: searchable, filterable table of all TLD configurations.
 *
 * Shows the TLD, provider, status, cost and sell price, margin, categories, last sync time and the
 * edit and delete actions. Supports import from a registrar provider and a manual price sync, and
 * asks before deleting in a danger prompt. The outcome of an import, sync or delete (success, or the
 * API's sentence) shows in the result line; a refused load offers a retry.
 */
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import SettingsLoading from '@/components/settings/SettingsLoading.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiRowActions, { type UiRowAction } from '@/components/ui/UiRowActions.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
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
import { Icon } from '@/lib/icons'
import { formatRelativeFromNow } from '@/lib/format'
import { formatMarginPercent, formatTldPrice, marginPercentTone } from '@/lib/tldPricing'
import { useTldConfigsStore } from '@/stores/tldConfigs'
import { pageFirstRow, pageLastRow, pageSlice } from '@/lib/paging'
import { RegistrarModule } from '@/types/registrarmodule'
import type { TldConfigListItem } from '@/types/tldconfig'
import { TldStatusFilter } from '@/types/tldstatusfilter'
import { DEFAULT_PAGE_SIZE, SEARCH_DEBOUNCE_MS } from '@/lib/limits'
import { AppRoute, createPath, editPath } from '@/lib/routes'

const { t, locale } = useI18n()

/** Router, used to open the create and edit pages. */
const router = useRouter()

/** TLD configs store: the list, the loading flag and the refusal sentences. */
const store = useTldConfigsStore()

/** The result banner for import, sync and delete. */
const banner = useResultBanner()

/** Tone of the result banner: danger for a refusal, success otherwise. */
const bannerTone = computed<UiTone>(() => (banner.isError.value ? UiTone.Danger : UiTone.Success))

/** Local search model bound to the input. */
const searchInput = ref('')

/** Provider filter value; empty means every provider. */
const providerFilter = ref('')

/** Status filter value. */
const statusFilter = ref<string>(TldStatusFilter.All)

/** Whether an import operation is in progress. */
const importing = ref(false)

/** Whether a sync operation is in progress. */
const syncing = ref(false)

/** Current page number (1-based). */
const currentPage = ref(1)

/** The TLD config pending deletion, or null if the dialog is closed. */
const deleteTarget = ref<TldConfigListItem | null>(null)

/** Whether a delete operation is in progress. */
const deleting = ref(false)

/** Debounce timer handle for the search input. */
let searchTimer: ReturnType<typeof setTimeout> | null = null

/** Badge tone of each registrar module. */
const PROVIDER_TONES: Record<RegistrarModule, UiTone> = {
  [RegistrarModule.NameAm]: 'info',
  [RegistrarModule.Namecheap]: 'warning',
}

/** Options of the provider filter dropdown. */
const providerFilterOptions = computed(() => [
  { value: '', label: t('settings.tldConfigs.allProviders') },
  { value: RegistrarModule.NameAm as string, label: t('settings.tldConfigs.nameAm') },
  { value: RegistrarModule.Namecheap as string, label: t('settings.tldConfigs.namecheap') },
])

/** Options of the status filter dropdown. */
const statusFilterOptions = computed(() => [
  { value: TldStatusFilter.All as string, label: t('settings.tldConfigs.allStatus') },
  { value: TldStatusFilter.Enabled as string, label: t('shared.enabled') },
  { value: TldStatusFilter.Disabled as string, label: t('shared.disabled') },
])

/** Options of the import menu: one entry per registrar module. */
const importOptions = computed(() => [
  { value: RegistrarModule.NameAm as string, label: t('settings.tldConfigs.nameAm') },
  { value: RegistrarModule.Namecheap as string, label: t('settings.tldConfigs.namecheap') },
])

/** Filtered list of TLD configs based on the search, provider and status filters. */
const filteredConfigs = computed<TldConfigListItem[]>(() => {
  let result = store.configs

  if (searchInput.value.trim()) {
    const query = searchInput.value.toLowerCase().trim()
    result = result.filter((c) =>
      c.tld.toLowerCase().includes(query) ||
      c.registrarModule.toLowerCase().includes(query) ||
      c.categories.some((category) => category.toLowerCase().includes(query)),
    )
  }

  if (providerFilter.value) {
    result = result.filter((c) => c.registrarModule === providerFilter.value)
  }

  if (statusFilter.value === TldStatusFilter.Enabled) {
    result = result.filter((c) => c.isEnabled)
  } else if (statusFilter.value === TldStatusFilter.Disabled) {
    result = result.filter((c) => !c.isEnabled)
  }

  return result
})

/** Subset of the filtered configs for the current page. */
const paginatedConfigs = computed<TldConfigListItem[]>(() => pageSlice(filteredConfigs.value, currentPage.value, DEFAULT_PAGE_SIZE))

/** First row number on the current page, for the "Showing x to y of n" line. */
const firstShown = computed<number>(() => pageFirstRow(currentPage.value, DEFAULT_PAGE_SIZE))

/** Last row number on the current page. */
const lastShown = computed<number>(() => pageLastRow(currentPage.value, DEFAULT_PAGE_SIZE, filteredConfigs.value.length))

/** Sends the list back to page one once the operator pauses typing. */
const handleSearch = (): void => {
  if (searchTimer) {
    clearTimeout(searchTimer)
  }
  searchTimer = setTimeout(() => {
    currentPage.value = 1
  }, SEARCH_DEBOUNCE_MS)
}

/** Navigates to the create TLD config page. */
const navigateCreate = (): void => {
  void router.push(createPath(AppRoute.SettingsTldPricing))
}

/**
 * Navigates to the edit page for a given TLD config.
 *
 * @param item - The TLD config to edit.
 */
const navigateEdit = (item: TldConfigListItem): void => {
  void router.push(editPath(AppRoute.SettingsTldPricing, item.id))
}

/**
 * Opens the delete confirmation for a TLD config.
 *
 * @param item - The TLD config to delete.
 */
const promptDelete = (item: TldConfigListItem): void => {
  deleteTarget.value = item
}

/** Confirms and deletes the targeted TLD config. */
const confirmDelete = async (): Promise<void> => {
  if (!deleteTarget.value) {
    return
  }
  banner.clear()
  deleting.value = true
  const removed = await store.remove(deleteTarget.value.id)
  if (removed) {
    banner.show(t('shared.recordDeleted'), false)
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
 * Imports TLDs from the chosen registrar provider.
 *
 * @param module - Registrar module name.
 */
const handleImport = async (module: string): Promise<void> => {
  importing.value = true
  banner.clear()
  const result = await store.importFromProvider(module)
  if (result) {
    banner.show(t('settings.tldConfigs.importComplete', { imported: result.imported, updated: result.updated }), false)
  } else if (store.actionError) {
    banner.show(store.actionError, true)
  }
  importing.value = false
}

/** Triggers a manual price sync from all providers. */
const handleSync = async (): Promise<void> => {
  syncing.value = true
  banner.clear()
  if (await store.syncPrices()) {
    banner.show(t('settings.tldConfigs.priceSyncComplete'), false)
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

onMounted(() => store.fetchAll())

onBeforeUnmount(() => {
  if (searchTimer) {
    clearTimeout(searchTimer)
  }
})

watch(searchInput, handleSearch)
watch(providerFilter, () => {
  currentPage.value = 1
})
watch(statusFilter, () => {
  currentPage.value = 1
})

/**
 * Row actions for a TLD configuration entry.
 *
 * @param item - The TLD entry whose actions to build.
 * @returns The action list passed to UiRowActions.
 */
const tldActions = (item: TldConfigListItem): UiRowAction[] => {
  /**
   * Navigates to the edit page for this TLD entry.
   * @returns Nothing.
   */
  const onEdit = (): void => navigateEdit(item)
  /**
   * Prompts deletion of this TLD entry.
   * @returns Nothing.
   */
  const onDelete = (): void => promptDelete(item)
  return [
    { label: t('settings.tldConfigs.editTld'), testid: `tld-edit-${item.tld}`, onSelect: onEdit },
    { label: t('settings.tldConfigs.deleteTld'), testid: `tld-delete-${item.tld}`, onSelect: onDelete, danger: true },
  ]
}
</script>

<template>
  <UiStack gap="none" data-testid="tld-configs-view">
    <UiPageHeader :title="t('settings.tldConfigs.tldPricing')" :subtitle="t('settings.tldConfigs.configureDomainExtensionPricingAnd')">
      <template #actions>
        <!-- Import from a provider: choosing one starts the import -->
        <UiSelect
          model-value=""
          :options="importOptions"
          :placeholder="importing ? t('settings.tldConfigs.importing') : t('settings.tldConfigs.import')"
          :disabled="importing"
          data-testid="tld-configs-import"
          class="w-40"
          @update:model-value="handleImport"
        />
        <UiButton
          type="button"
          variant="secondary"
          :disabled="syncing"
          data-testid="tld-configs-sync"
          @click="handleSync"
        >
          <UiIcon :markup="Icon.Refresh" size="sm" />
          {{ syncing ? t('settings.tldConfigs.syncing') : t('settings.tldConfigs.syncPrices') }}
        </UiButton>
        <UiButton type="button" variant="primary" data-testid="tld-configs-add" @click="navigateCreate">
          <UiIcon :markup="Icon.Add" size="sm" />
          {{ t('settings.tldConfigs.addTld') }}
        </UiButton>
      </template>
    </UiPageHeader>

    <UiStatusMessage :message="banner.message.value" :tone="bannerTone" class="mb-5" data-testid="tld-configs-result" />

    <!-- Filters -->
    <UiStack direction="row" align="center" wrap class="mb-5">
      <UiInput
        v-model="searchInput"
        type="text"
        :placeholder="t('settings.tldConfigs.searchTlds')"
        data-testid="tld-configs-search"
        class="w-72"
      >
        <template #icon>
          <UiIcon :markup="Icon.Search" size="sm" />
        </template>
      </UiInput>
      <UiSelect
        v-model="providerFilter"
        :options="providerFilterOptions"
        :placeholder="t('settings.tldConfigs.allProviders')"
        data-testid="tld-configs-provider-filter"
        class="w-44"
      />
      <UiSelect
        v-model="statusFilter"
        :options="statusFilterOptions"
        :placeholder="t('settings.tldConfigs.allStatus')"
        data-testid="tld-configs-status-filter"
        class="w-36"
      />
    </UiStack>

    <SettingsLoading v-if="store.loading" :message="t('settings.tldConfigs.loadingTldConfigurations')" />

    <UiErrorState v-else-if="store.error" :message="store.error" testid="tld-configs-error" @retry="store.fetchAll()" />

    <UiCard v-else class="overflow-hidden">
      <UiTable density="comfortable" :empty="paginatedConfigs.length === 0" :colspan="9">
        <template #empty>
          {{ t('settings.tldConfigs.noTldConfigurationsFoundAdd') }}
        </template>
        <UiTableHead>
          <UiTableRow class="border-b border-border">
            <UiTableHeader align="left">{{ t('settings.tldConfigs.tld') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('settings.tldConfigs.provider') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('shared.status') }}</UiTableHeader>
            <UiTableHeader align="right">{{ t('settings.tldConfigs.cost1yr') }}</UiTableHeader>
            <UiTableHeader align="right">{{ t('settings.tldConfigs.sell1yr') }}</UiTableHeader>
            <UiTableHeader align="right">{{ t('settings.tldConfigs.margin') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('shared.categories') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('settings.tldConfigs.lastSynced') }}</UiTableHeader>
            <UiTableHeader align="right">{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow
            v-for="item in paginatedConfigs"
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
            <UiTableCell align="right" muted class="tabular-nums">
              {{ formatTldPrice(item.costRegister1yr, item.currency) }}
            </UiTableCell>
            <UiTableCell align="right" muted class="tabular-nums">
              {{ formatTldPrice(item.sellRegister1yr, item.sellCurrency) }}
            </UiTableCell>
            <UiTableCell align="right" class="tabular-nums">
              <UiText :tone="marginPercentTone(item.marginPercent)" size="lg" class="font-medium">
                {{ formatMarginPercent(item.marginPercent) }}
              </UiText>
            </UiTableCell>
            <UiTableCell>
              <UiStack direction="row" gap="xs" wrap>
                <UiBadge v-for="category in item.categories" :key="category" small>
                  {{ category }}
                </UiBadge>
                <UiText v-if="item.categories.length === 0" tone="muted" size="md">—</UiText>
              </UiStack>
            </UiTableCell>
            <UiTableCell muted>{{ lastSyncedText(item.lastSyncedAt) }}</UiTableCell>
            <UiTableCell align="right">
              <UiRowActions :actions="tldActions(item)" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>
    </UiCard>

    <!-- Pagination -->
    <UiStack
      v-if="filteredConfigs.length > DEFAULT_PAGE_SIZE"
      direction="row"
      align="center"
      justify="between"
      class="mt-4"
    >
      <UiText tone="muted" size="lg">
        {{ t('settings.tldConfigs.showingValueValue2OfLength', { value: firstShown, value2: lastShown, length: filteredConfigs.length }) }}
      </UiText>
      <UiPagination
        v-model:page="currentPage"
        :total="filteredConfigs.length"
        :page-size="DEFAULT_PAGE_SIZE"
        testid="tld-configs-pagination"
      />
    </UiStack>

    <!-- Delete confirmation -->
    <UiDialog
      v-if="deleteTarget"
      :variant="UiDialogVariant.Confirm"
      :title="t('settings.tldConfigs.deleteTldConfiguration')"
      :message="t('settings.tldConfigs.deleteMessage', { tld: deleteTarget.tld })"
      :confirm-label="t('shared.delete')"
      :loading-label="t('shared.deleting')"
      :loading="deleting"
      :tone="UiTone.Danger"
      @confirm="confirmDelete"
      @close="dismissDelete"
    />
  </UiStack>
</template>
