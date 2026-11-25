<script setup lang="ts">
/**
 * The line items of an invoice: a read-only table with the totals, or, when `readonly` is off,
 * an editor that adds, removes and re-prices lines and saves them through the billing store.
 * Only the summary tab shows this block. The outcome of a save
 * (success, or the API's sentence) shows next to the save button.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiFormActions from '@/components/ui/UiFormActions.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiTableTotals from '@/components/ui/UiTableTotals.vue'
import type { UiTotalRow } from '@/components/ui/UiTableTotals.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { MAX_PERCENT } from '@/lib/limits'
import { formatPlainAmount } from '@/lib/money'
import { nextRowKey } from '@/lib/rowKey'
import { useRowSelection } from '@/composables/useRowSelection'
import { useBillingStore } from '@/stores/billing'
import type { InvoiceDetail, InvoiceItemUpdate } from '@/types/invoice'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiCardPadding } from '@/utils/ui/card'
import { UiTableDensity } from '@/utils/ui/injection'
import { UiAlignItems, UiDirection, UiGap, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiHeadingVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiCardRole, UiColumnSize } from '@/utils/ui/table'
import { Icon } from '@/lib/icons'
import { UiActionKind } from '@/utils/ui/actions'
import { INVOICE_ITEMS_TABLE } from '@/views/InvoiceDetailView.schema'

const { t } = useI18n()

const props = withDefaults(defineProps<{
  /** The invoice whose lines are shown. */
  invoice: InvoiceDetail
  /** True to show the lines without editing controls. */
  readonly?: boolean
}>(), {
  readonly: false,
})

/** One line while it is being edited. */
interface EditableLine {
  /** Stable client key of the row; unlike `id` it exists from the moment the row is created. */
  key: number
  /** Existing line id; absent for a line added in the editor. */
  id?: number
  /** Charge description. */
  description: string
  /** Number of units. */
  quantity: number
  /** Price of one unit. */
  unitPrice: number
}

const store = useBillingStore()

/**
 * Total of one editable line.
 *
 * @param line - The line.
 * @returns Quantity times unit price.
 */
const lineTotal = (line: EditableLine): number => line.quantity * line.unitPrice

/** The editable copy of the invoice lines. */
const lines = ref<EditableLine[]>([])

/** True while the lines are being saved. */
const saving = ref(false)

/** The outcome line of the last save, shown next to the save button. */
const feedback = useOutcomeToast()

/** Ids of existing lines the operator removed; they are sent as deletions on save. */
const removedIds = ref<number[]>([])

/** Serialised editor state the last time it matched the stored invoice. */
const baseline = ref('')

/**
 * Serialises what the operator can change, so an edit can be told from an untouched editor.
 *
 * @returns The lines' editable fields and the removed ids as one string.
 */
const snapshot = (): string =>
  JSON.stringify([
    lines.value.map((line) => [line.id ?? null, line.description, line.quantity, line.unitPrice]),
    removedIds.value,
  ])

/** True when the editor holds changes that are not saved yet. */
const dirty = computed<boolean>(() => snapshot() !== baseline.value)

/** The lines ticked for the bulk delete, by their client keys; the table draws the checkboxes and, on a phone, the select-all above its cards. */
const selection = useRowSelection(() => lines.value.map((line) => line.key))

/** Sub-total of the editable lines. */
const previewSubTotal = computed<number>(() => lines.value.reduce((sum, line) => sum + lineTotal(line), 0))

/** True when the invoice carries tax: it has a tax rate and was not raised under the reverse charge. */
const taxed = computed<boolean>(() => props.invoice.amounts.taxRate > 0 && !props.invoice.reverseCharge)

/** Tax previewed on the live sub-total at the invoice's own rate; the invoice taxes its whole sub-total. */
const previewTax = computed<number>(() => (taxed.value ? previewSubTotal.value * (props.invoice.amounts.taxRate / MAX_PERCENT) : 0))

/** Total due as the editor previews it: sub-total plus tax minus the invoice credit. */
const previewTotal = computed<number>(() => previewSubTotal.value + previewTax.value - props.invoice.amounts.credit)

/**
 * Formats an amount of this invoice for the totals block.
 *
 * @param value - The amount.
 * @returns The amount in the invoice's currency.
 */
const money = (value: number): string => formatPlainAmount(value, props.invoice.currency)

/** The totals block: what the API computed when read-only, the live preview while editing. */
const totalRows = computed<UiTotalRow[]>(() => {
  const rows: UiTotalRow[] = [
    {
      key: 'subtotal',
      label: t('shared.subTotal'),
      value: money(props.readonly ? props.invoice.amounts.subTotal : previewSubTotal.value),
      testid: 'billing-invoice-total-subtotal',
    },
  ]
  if (taxed.value) {
    rows.push({
      key: 'tax',
      label: t('billing.invoiceItems.tax'),
      value: money(props.readonly ? props.invoice.amounts.tax : previewTax.value),
      testid: 'billing-invoice-total-tax',
    })
  }
  rows.push({ key: 'credit', label: t('shared.credit'), value: money(props.invoice.amounts.credit), testid: 'billing-invoice-total-credit' })
  rows.push({
    key: 'total',
    label: t('shared.totalDue'),
    value: money(props.readonly ? props.invoice.amounts.total : previewTotal.value),
    strong: true,
    testid: 'billing-invoice-total-total',
  })
  return rows
})

/** Resets the editable lines from the invoice. */
const populate = (): void => {
  lines.value = props.invoice.items.map((item) => ({
    key: nextRowKey(),
    id: item.id,
    description: item.description,
    quantity: item.quantity,
    unitPrice: item.unitPrice,
  }))
  removedIds.value = []
  baseline.value = snapshot()
}

/** Appends a blank line. */
const addLine = (): void => {
  lines.value.push({ key: nextRowKey(), description: '', quantity: 1, unitPrice: 0 })
}

/**
 * Removes one line.
 *
 * @param index - Position of the line.
 */
const removeLine = (index: number): void => {
  const [removed] = lines.value.splice(index, 1)
  if (removed?.id !== undefined) {
    removedIds.value.push(removed.id)
  }
}

/** Drops every ticked line from the editor (saved with the rest). */
const deleteSelected = (): void => {
  const gone = lines.value.filter((line) => selection.isSelected(line.key))
  removedIds.value.push(...gone.flatMap((line) => (line.id === undefined ? [] : [line.id])))
  lines.value = lines.value.filter((line) => !selection.isSelected(line.key))
}

/**
 * Saves the edited lines through the store and shows the outcome.
 *
 * @returns Resolves when the request has settled.
 */
const save = async (): Promise<void> => {
  saving.value = true
  // The API changes only the lines it is told about, so a removed line must be sent as a deletion.
  const kept: InvoiceItemUpdate[] = lines.value.map((line) => ({
    id: line.id ?? null,
    description: line.description,
    unitPrice: line.unitPrice,
    quantity: line.quantity,
    isDeleted: false,
  }))
  const removed: InvoiceItemUpdate[] = removedIds.value.map((id) => ({
    id,
    description: '',
    unitPrice: 0,
    quantity: 1,
    isDeleted: true,
  }))
  const ok = await store.updateItems(props.invoice.id, [...kept, ...removed])
  saving.value = false
  if (ok) {
    // Mark the editor clean so the reloaded invoice (new ids for added lines) replaces the lines.
    baseline.value = snapshot()
    feedback.show(t('shared.changesSavedSuccessfully'), false)
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
}

/**
 * The one row action of an editable line: remove it from the draft (nothing is saved until Save).
 *
 * @param index - The line position.
 * @returns The action list passed to UiRowActions.
 */
const lineActions = (index: number): UiMenuAction[] => [
  {
    label: t('billing.invoiceItems.removeItem'),
    icon: Icon.Trash,
    testid: `billing-invoice-item-remove-${index}`,
    danger: true,
    /**
     * Removes this line from the draft.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      removeLine(index)
    },
  },
]

onMounted(populate)

// Another section of the screen saving reloads the invoice; unsaved line edits must survive that.
watch(
  () => props.invoice,
  () => {
    if (!dirty.value) {
      populate()
    }
  },
)
</script>

<template>
  <UiCard :padding="UiCardPadding.Md" data-testid="billing-invoice-items">
    <UiStack :gap="UiGap.Sm">
      <UiHeading :level="3" :variant="UiHeadingVariant.Card">
        {{ t('shared.invoiceItems') }}
      </UiHeading>

      <!-- Read-only lines -->
      <UiTable
        :empty-text="t('shared.noItems')"
        v-if="readonly"
        :density="UiTableDensity.Compact"
        :empty="invoice.items.length === 0"
        data-testid="billing-invoice-items-table"
      >
        <UiSchemaTableHead :columns="INVOICE_ITEMS_TABLE.columns" />
        <UiTableBody>
          <UiTableRow v-for="item in invoice.items" :key="item.id" hoverable>
            <UiTableCell>{{ item.description }}</UiTableCell>
            <UiTableCell numeric>{{ formatPlainAmount(item.amount, props.invoice.currency) }}</UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <UiTableTotals :rows="totalRows" />
      </UiTable>

      <!-- Editable lines -->
      <UiTable
        :empty-text="t('shared.noItems')"
        v-else
        :density="UiTableDensity.Compact"
        :empty="lines.length === 0"
        :selection="selection"
        select-all-testid="billing-invoice-items-select-all"
        data-testid="billing-invoice-items-editor"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader :size="UiColumnSize.Grow" :card-role="UiCardRole.Control">{{ t('shared.description') }}</UiTableHeader>
            <UiTableHeader :size="UiColumnSize.Compact" :card-role="UiCardRole.Control">{{ t('shared.qty') }}</UiTableHeader>
            <UiTableHeader :size="UiColumnSize.Compact" :card-role="UiCardRole.Control">{{ t('billing.quoteDetail.unitPrice') }}</UiTableHeader>
            <UiTableHeader :size="UiColumnSize.Compact" numeric>{{ t('shared.amount') }}</UiTableHeader>
            <UiTableHeader actions />
          </UiTableRow>
        </UiTableHead>
        <UiTableBody>
          <UiTableRow
            v-for="(line, index) in lines"
            :key="line.key"
            :row-id="line.key"
            :select-testid="`billing-invoice-item-select-${index}`"
          >
            <UiTableCell>
              <UiTextarea
                v-model="line.description"
                rows="1"
                :placeholder="t('shared.description')"
                :data-testid="`billing-invoice-item-description-${index}`"
              />
            </UiTableCell>
            <UiTableCell>
              <UiNumberInput
                v-model="line.quantity"
                :step="1"
                :min="1"
                placeholder="1"
                :data-testid="`billing-invoice-item-quantity-${index}`"
              />
            </UiTableCell>
            <UiTableCell>
              <UiNumberInput
                v-model="line.unitPrice"
                :step="0.01"
                :min="0"
                placeholder="0.00"
                :data-testid="`billing-invoice-item-price-${index}`"
              />
            </UiTableCell>
            <UiTableCell numeric>
              {{ formatPlainAmount(lineTotal(line), props.invoice.currency) }}
            </UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="lineActions(index)" :testid="`billing-invoice-item-actions-${index}`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <UiTableTotals :rows="totalRows" />
      </UiTable>

      <UiStack v-if="!readonly" :direction="UiDirection.Row" :justify="UiJustify.Between" :align="UiAlignItems.Center" wrap>
        <UiStack :direction="UiDirection.Row" :gap="UiGap.Sm" :align="UiAlignItems.Center">
          <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Md">{{ t('shared.withSelected') }}</UiText>
          <UiButton :action="UiActionKind.Delete"
            :variant="UiButtonVariant.Danger"
            :size="UiSize.Sm"
            :disabled="selection.count.value === 0"
            data-testid="billing-invoice-items-delete-selected"
            @click="deleteSelected"
          >
            {{ t('shared.delete') }}
          </UiButton>
        </UiStack>
        <UiButton :action="UiActionKind.Add" :variant="UiButtonVariant.Secondary" :size="UiSize.Sm" data-testid="billing-invoice-items-add" @click="addLine">
          {{ t('billing.actions.addItem') }}
        </UiButton>
      </UiStack>

      <UiFormActions v-if="!readonly">
        <UiButton :action="UiActionKind.Cancel" :variant="UiButtonVariant.Secondary" data-testid="billing-invoice-items-cancel" @click="populate">
          {{ t('shared.cancel') }}
        </UiButton>
        <UiButton :action="UiActionKind.Save"
          :variant="UiButtonVariant.Primary"
          :loading="saving"
          :disabled="saving"
          data-testid="billing-invoice-items-save"
          @click="save"
        >
          {{ t('shared.save') }}
        </UiButton>
      </UiFormActions>
    </UiStack>
  </UiCard>
</template>
