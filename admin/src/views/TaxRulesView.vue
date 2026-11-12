<script setup lang="ts">
/**
 * Admin Tax Rules settings view: the table of tax rules (name, country, region, rate and status)
 * with create, edit and delete.
 *
 * The add and edit dialog shows a refused save inside itself; a result banner reports a delete, which
 * a confirmation dialog guards.
 */
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import SettingsLoading from '@/components/settings/SettingsLoading.vue'
import TaxRuleFormModal from '@/components/settings/TaxRuleFormModal.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiRowActions, { type UiRowAction } from '@/components/ui/UiRowActions.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { useResultBanner } from '@/composables/useResultBanner'
import { Icon } from '@/lib/icons'
import { reportCountryName } from '@/lib/reportsCountry'
import { useTaxRulesStore } from '@/stores/taxRules'
import type { SaveTaxRulePayload, TaxRuleDto } from '@/types/taxrule'
import { UiTableDensity } from '@/utils/ui/injection'
import { UiTone } from '@/utils/ui/tone'

const { t, locale } = useI18n()

/** Tax rules store: the list, the loading flag and the refusal sentences. */
const store = useTaxRulesStore()

/** The result banner for a delete. */
const banner = useResultBanner()

/** Number of columns of the table (width of the empty row). */
const COLUMN_COUNT = 6

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
 * Saves the rule from the dialog (a new one, or the edited one) and closes the dialog on success.
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
  }
}

/** Deletes the rule the dialog asked about and reports the outcome. */
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
const ruleActions = (rule: TaxRuleDto): UiRowAction[] => [
  {
    label: t('shared.edit'),
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

onMounted(() => store.fetchAll())
</script>

<template>
  <UiStack gap="none" data-testid="tax-rules-view">
    <UiPageHeader :title="t('taxRules.title')" :subtitle="t('taxRules.description')">
      <template #actions>
        <UiButton type="button" variant="primary" data-testid="tax-rules-add" @click="openCreate">
          <UiIcon :markup="Icon.Add" size="sm" />
          {{ t('taxRules.addRule') }}
        </UiButton>
      </template>
    </UiPageHeader>

    <UiAlert
      v-if="banner.message.value"
      :variant="banner.isError.value ? UiTone.Danger : UiTone.Success"
      class="mb-5"
      data-testid="tax-rules-result"
    >
      {{ banner.message.value }}
    </UiAlert>

    <SettingsLoading v-if="store.loading && store.taxRules.length === 0" :message="t('taxRules.loading')" />

    <UiErrorState
      v-else-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="tax-rules-error"
      @retry="store.fetchAll()"
    />

    <UiCard v-else class="overflow-hidden">
      <UiTable :density="UiTableDensity.Comfortable" :empty="store.taxRules.length === 0" :colspan="COLUMN_COUNT">
        <template #empty>
          {{ t('taxRules.empty') }}
        </template>
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader align="left">{{ t('taxRules.table.name') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('taxRules.table.country') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('taxRules.table.region') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('taxRules.table.rate') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('taxRules.table.enabled') }}</UiTableHeader>
            <UiTableHeader actions>{{ t('taxRules.table.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
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
              <UiBadge :tone="rule.isEnabled ? UiTone.Success : UiTone.Danger" small>
                {{ rule.isEnabled ? t('taxRules.enabledBadge') : t('taxRules.disabledBadge') }}
              </UiBadge>
            </UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="ruleActions(rule)" :testid="`tax-rule-actions-${rule.id}`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>
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
      :loading-label="t('taxRules.deleteModal.loadingLabel')"
      :loading="deleting"
      :tone="UiTone.Danger"
      @confirm="confirmDelete"
      @close="dismissDelete"
    />
  </UiStack>
</template>
