<script setup lang="ts">
/**
 * Admin Tax Rules settings view: a search box, the filter dialog with its removable chips, and a paged table of tax
 * rules (name, country, region, rate and status) with create, edit and delete.
 *
 * The search text, the enabled filter and the page live in the address (`useListQuery`), so a reload, a shared link
 * and the back button restore the same list, and the API applies them before it pages. The add and edit dialog shows
 * a refused save inside itself; a result banner reports a delete, which a confirmation dialog guards. A save or a
 * delete reads the page the address names again.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import TaxRuleFormModal from '@/components/settings/TaxRuleFormModal.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiResultCount from '@/components/ui/UiResultCount.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStatusIcon from '@/components/ui/UiStatusIcon.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { useListQuery } from '@/composables/useListQuery'
import { useResultBanner } from '@/composables/useResultBanner'
import { ENABLED_FILTER_OPTIONS } from '@/lib/constants'
import { enabledOfFilter } from '@/lib/enabledFilter'
import { Icon } from '@/lib/icons'
import { reportCountryName } from '@/lib/reportsCountry'
import { statusLabel } from '@/lib/tones'
import { schemaHeader } from '@/lib/schemaHeader'
import { useTaxRulesStore } from '@/stores/taxRules'
import { EnabledFilter } from '@/types/enabledfilter'
import type { SaveTaxRulePayload, TaxRuleDto, TaxRuleListFilters } from '@/types/taxrule'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTableDensity } from '@/utils/ui/injection'
import schema from '@/views/TaxRulesView.schema'
import { UiTextSize } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import type { UiHeaderAction } from '@/utils/ui/actionBar'

const { t, locale } = useI18n()

/** The header actions, each with its icon; on a phone only the primary one stays a button. */
const headerActions = computed<UiHeaderAction[]>(() => [
  { label: t('taxRules.addRule'), icon: Icon.Add, variant: UiButtonVariant.Primary, testid: 'tax-rules-add', onSelect: openCreate },
])

/** The filter field the dialog edits; it is one query key and one `TaxRuleListFilters` member. */
const FilterKey = {
  Enabled: 'enabled',
} as const

/** Tax rules store: the page, the loading flag and the refusal sentences. */
const store = useTaxRulesStore()

/** The enabled filter, the search text and the page of the list, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, narrowed, loadOnChange, stepBackWhenEmpty, enumOf, chipsOf }
  = useListQuery(Object.values(FilterKey))

/** Enabled-state filter options; the empty value means any. */
const enabledOptions = computed(() => [{ value: '', label: t('shared.any') }, ...ENABLED_FILTER_OPTIONS])

/** The enabled state in the address as a filter choice, undefined when none is set or the value is not one. */
const enabledOfAddress = enumOf(FilterKey.Enabled, EnabledFilter)

/** What the address narrows the list by, as the store sends it to the API. */
const filters = computed<TaxRuleListFilters>(() => ({
  search: appliedSearch.value,
  enabled: enabledOfFilter(enabledOfAddress.value),
}))

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(() => [
  {
    key: FilterKey.Enabled,
    label: t('shared.status'),
    value: enabledOfAddress.value === undefined ? undefined : statusLabel(ENABLED_FILTER_OPTIONS, enabledOfAddress.value),
  },
])

/**
 * Loads the page the address names with the filter and search it holds.
 *
 * @returns True when loaded; false when refused (see `store.error`).
 */
const load = (): Promise<boolean> => store.fetchAll(page.value, filters.value)

// Every change of the filter, the search or the page loads the list the address names.
loadOnChange(load)

/** Whether the current page has any rule to list. */
const hasRows = computed<boolean>(() => store.taxRules.length > 0)

/** True while the first load is in flight and there are no rows yet; the toolbar and the table then keep their layout. */
const initialLoading = computed<boolean>(() => store.loading && !hasRows.value)

/** The result banner for a delete. */
const banner = useResultBanner()

/** Whether the add/edit tax rule dialog is visible. */
const showModal = ref(false)

/** Rule being edited, or null for create mode. */
const editingRule = ref<TaxRuleDto | null>(null)

/** True while a rule save is in flight. */
const saving = ref(false)

/** The rule pending a delete confirmation, or null when the dialog is closed. */
const deleteTarget = ref<TaxRuleDto | null>(null)

/** True while a delete request is in flight. */
const deleting = ref(false)

/** Opens the dialog in create mode. */
const openCreate = (): void => {
  store.actionError = null
  editingRule.value = null
  showModal.value = true
}

/**
 * Opens the dialog in edit mode for the given rule.
 *
 * @param rule - The rule to edit.
 */
const openEdit = (rule: TaxRuleDto): void => {
  store.actionError = null
  editingRule.value = rule
  showModal.value = true
}

/**
 * Saves the rule from the dialog (a new one, or the edited one), closes the dialog and reloads the page on success.
 *
 * @param payload - The rule's fields.
 */
const handleSave = async (payload: SaveTaxRulePayload): Promise<void> => {
  saving.value = true
  const saved = editingRule.value === null
    ? await store.create(payload)
    : await store.update(editingRule.value.id, payload)
  saving.value = false
  if (saved) {
    showModal.value = false
    await load()
  }
}

/**
 * Deletes the rule the dialog asked about, reports the outcome and reloads the page. When the delete emptied the last
 * page, the list steps back to the page before it.
 */
const confirmDelete = async (): Promise<void> => {
  if (deleteTarget.value === null) {
    return
  }
  deleting.value = true
  const removed = await store.remove(deleteTarget.value.id)
  deleting.value = false
  deleteTarget.value = null
  if (removed) {
    banner.show(t('taxRules.toasts.deleted'), false)
    await load()
    await stepBackWhenEmpty(!hasRows.value)
  } else if (store.actionError) {
    banner.show(store.actionError, true)
  }
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDelete = (): void => {
  if (!deleting.value) {
    deleteTarget.value = null
  }
}

/**
 * The row actions of a rule: edit, and delete last (it opens the confirmation).
 *
 * @param rule - The row.
 * @returns The action list passed to UiRowActions.
 */
const ruleActions = (rule: TaxRuleDto): UiMenuAction[] => [
  {
    label: t('shared.edit'),
    icon: Icon.Edit,
    testid: `tax-rule-edit-${rule.id}`,
    /**
     * Opens the edit dialog for this rule.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      openEdit(rule)
    },
  },
  {
    label: t('shared.delete'),
    icon: Icon.Trash,
    testid: `tax-rule-delete-${rule.id}`,
    danger: true,
    /**
     * Stages this rule for the delete confirmation.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      deleteTarget.value = rule
    },
  },
]
</script>

<template>
  <UiStack data-testid="tax-rules-view">
    <UiPageHeader :actions="headerActions" v-bind="schemaHeader(schema, t)">
    </UiPageHeader>

    <UiAlert
      v-if="banner.message.value"
      :variant="UiTone.Danger"
      data-testid="tax-rules-result"
    >
      {{ banner.message.value }}
    </UiAlert>

    <UiListToolbar
      v-model:search="search"
      search-testid="tax-rules-search"
      :search-placeholder="t('taxRules.searchPlaceholder')"
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
        <UiField :label="t('shared.status')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="draft.enabled"
              :options="enabledOptions"
              :placeholder="t('shared.any')"
              data-testid="tax-rules-filter-enabled"
            />
          </template>
        </UiField>
      </UiStack>
    </UiListToolbar>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="tax-rules-error"
      @retry="load"
    />

    <UiCard v-else-if="hasRows || store.loading" clip>
      <UiTable :density="UiTableDensity.Comfortable" :loading="initialLoading" data-testid="tax-rules-table">
        <UiSchemaTableHead :columns="schema.table.columns" />
        <UiTableBody divided>
          <UiTableRow
            v-for="rule in store.taxRules"
            :key="rule.id"
            hoverable
            :data-testid="`tax-rule-row-${rule.id}`"
          >
            <UiTableCell class="font-semibold text-text-primary">{{ rule.name }}</UiTableCell>
            <UiTableCell muted>{{ reportCountryName(rule.country, locale) }}</UiTableCell>
            <UiTableCell muted>{{ rule.region ?? t('taxRules.wholeCountry') }}</UiTableCell>
            <UiTableCell numeric muted>{{ rule.ratePercent }}%</UiTableCell>
            <UiTableCell>
              <UiStatusIcon
                :state="rule.isEnabled"
                :on-icon="Icon.CheckCircle"
                :off-icon="Icon.Close"
                :on-label="t('taxRules.enabledBadge')"
                :off-label="t('taxRules.disabledBadge')"
              />
            </UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="ruleActions(rule)" :testid="`tax-rule-actions-${rule.id}`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>

      <template #footer>
        <UiStack direction="row" justify="between" align="center" wrap>
          <UiResultCount :loading="initialLoading" :size="UiTextSize.Xs">
            {{ t('taxRules.totalCount', { totalCount: store.totalCount }) }}
          </UiResultCount>
          <UiPagination
            :page="page"
            :total="store.totalCount"
            :page-size="store.pageSize"
            testid="tax-rules-pagination"
            @update:page="setPage"
          />
        </UiStack>
      </template>
    </UiCard>

    <UiCard v-else>
      <UiEmptyState
        :title="narrowed ? t('taxRules.noMatches') : t('taxRules.empty')"
        data-testid="tax-rules-empty"
      >
        <template #icon>
          <UiIcon :markup="Icon.Dollar" size="lg" />
        </template>
        <template v-if="activeCount > 0" #actions>
          <UiButton :variant="UiButtonVariant.Link" data-testid="tax-rules-clear-empty" @click="clearAll">
            {{ t('taxRules.clearFilters') }}
          </UiButton>
        </template>
      </UiEmptyState>
    </UiCard>

    <!-- Add/edit tax rule dialog -->
    <TaxRuleFormModal
      v-if="showModal"
      :rule="editingRule"
      :saving="saving"
      :error="store.actionError"
      @save="handleSave"
      @close="showModal = false"
    />

    <!-- Delete confirmation -->
    <UiDialog
      v-if="deleteTarget"
      variant="confirm"
      :title="t('taxRules.deleteModal.title')"
      :message="t('taxRules.deleteModal.message', { name: deleteTarget.name })"
      :confirm-label="t('taxRules.deleteModal.confirmLabel')"
      :loading="deleting"
      :tone="UiTone.Danger"
      @confirm="confirmDelete"
      @close="dismissDelete"
    />
  </UiStack>
</template>
