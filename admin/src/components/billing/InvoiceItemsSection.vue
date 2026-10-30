<script setup lang="ts">
/**
 * The line items of an invoice: a read-only table with the totals, or, when `readonly` is off,
 * an editor that adds, removes and re-prices lines and saves them through the billing store.
 * Every invoice tab shows this block, so the logic lives here once. The outcome of a save
 * (success, or the API's sentence) shows next to the save button.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { useResultBanner } from '@/composables/useResultBanner'
import { Icon } from '@/lib/icons'
import { formatPlainAmount } from '@/lib/money'
import { useBillingStore } from '@/stores/billing'
import type { InvoiceDetail, InvoiceItemUpdate } from '@/types/invoice'
import { MAX_PERCENT } from '@/lib/limits'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

const props = withDefaults(defineProps<{
  /** The invoice whose lines are shown. */
  invoice: InvoiceDetail
  /** True to show the lines without editing controls. */
  readonly?: boolean
  /** Tax percentage previewed on the sub-total; 0 adds no tax row. */
  taxPercent?: number
}>(), {
  readonly: false,
  taxPercent: 0,
})

/** One line while it is being edited. */
interface EditableLine {
  /** Existing line id; absent for a line added in the editor. */
  id?: number
  /** Charge description. */
  description: string
  /** Number of units. */
  quantity: number
  /** Price of one unit. */
  unitPrice: number
  /** Whether the line is ticked for the bulk delete. */
  selected: boolean
}

/** A row of the totals block. */
interface TotalRow {
  /** Stable key of the row. */
  key: string
  /** Row caption. */
  label: string
  /** Row amount. */
  value: number
  /** True for the closing "total due" row. */
  strong: boolean
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
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

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

/** True when every editable line is ticked (and there is at least one). */
const allSelected = computed<boolean>(() => lines.value.length > 0 && lines.value.every((line) => line.selected))

/** How many editable lines are ticked. */
const selectedCount = computed<number>(() => lines.value.filter((line) => line.selected).length)

/** Sub-total of the editable lines. */
const previewSubTotal = computed<number>(() => lines.value.reduce((sum, line) => sum + lineTotal(line), 0))

/** Tax previewed on the sub-total at `taxPercent`; the invoice taxes its whole sub-total. */
const previewTax = computed<number>(() => previewSubTotal.value * (props.taxPercent / MAX_PERCENT))

/** Total due after the invoice credit, as the editor previews it. */
const previewTotal = computed<number>(() => previewSubTotal.value + previewTax.value - (props.invoice.amounts.credit ?? 0))

/** The totals block: what the API computed when read-only, the live preview while editing. */
const totalRows = computed<TotalRow[]>(() => {
  const credit = props.invoice.amounts.credit ?? 0
  const rows: TotalRow[] = [
    {
      key: 'subtotal',
      label: t('shared.subTotal'),
      value: props.readonly ? (props.invoice.amounts.subTotal ?? 0) : previewSubTotal.value,
      strong: false,
    },
  ]
  if (!props.readonly && props.taxPercent > 0) {
    rows.push({ key: 'tax', label: t('billing.invoiceItems.tax'), value: previewTax.value, strong: false })
  }
  rows.push({ key: 'credit', label: t('shared.credit'), value: credit, strong: false })
  rows.push({
    key: 'total',
    label: t('shared.totalDue'),
    value: props.readonly ? (props.invoice.amounts.total ?? 0) : previewTotal.value,
    strong: true,
  })
  return rows
})

/** Resets the editable lines from the invoice. */
const populate = (): void => {
  lines.value = (props.invoice.items ?? []).map((item) => ({
    id: item.id,
    description: item.description,
    quantity: item.quantity,
    unitPrice: item.unitPrice,
    selected: false,
  }))
  removedIds.value = []
  baseline.value = snapshot()
}

/** Appends a blank line. */
const addLine = (): void => {
  lines.value.push({ description: '', quantity: 1, unitPrice: 0, selected: false })
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

/** Ticks every line, or clears the ticks when all were ticked. */
const toggleAll = (): void => {
  const next = !allSelected.value
  lines.value.forEach((line) => {
    line.selected = next
  })
}

/** Drops every ticked line from the editor (saved with the rest). */
const deleteSelected = (): void => {
  const gone = lines.value.filter((line) => line.selected)
  removedIds.value.push(...gone.flatMap((line) => (line.id === undefined ? [] : [line.id])))
  lines.value = lines.value.filter((line) => !line.selected)
}

/**
 * Saves the edited lines through the store and shows the outcome.
 *
 * @returns Resolves when the request has settled.
 */
const save = async (): Promise<void> => {
  feedback.clear()
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
  <UiStack>
    <UiCard padding="md" data-testid="billing-invoice-items">
      <UiStack gap="sm">
        <UiHeading :level="3" variant="card">
          {{ t('shared.invoiceItems') }}
        </UiHeading>

        <!-- Read-only lines -->
        <UiTable
          v-if="readonly"
          density="compact"
          :empty="(invoice.items ?? []).length === 0"
          :colspan="2"
          data-testid="billing-invoice-items-table"
        >
          <UiTableHead>
            <UiTableRow>
              <UiTableHeader>{{ t('shared.description') }}</UiTableHeader>
              <UiTableHeader align="right">{{ t('shared.amount') }}</UiTableHeader>
            </UiTableRow>
          </UiTableHead>
          <UiTableBody divided>
            <UiTableRow v-for="item in invoice.items ?? []" :key="item.id" hoverable>
              <UiTableCell>{{ item.description }}</UiTableCell>
              <UiTableCell align="right">{{ formatPlainAmount(item.amount, props.invoice.currency) }}</UiTableCell>
            </UiTableRow>
          </UiTableBody>
          <template #empty>
            {{ t('shared.noItems') }}
          </template>
        </UiTable>

        <!-- Editable lines -->
        <UiTable
          v-else
          density="compact"
          :empty="lines.length === 0"
          :colspan="6"
          data-testid="billing-invoice-items-editor"
        >
          <UiTableHead>
            <UiTableRow>
              <UiTableHeader>
                <UiCheckbox
                  :model-value="allSelected"
                  :aria-label="t('shared.selectAll')"
                  data-testid="billing-invoice-items-select-all"
                  @update:model-value="toggleAll"
                />
              </UiTableHeader>
              <UiTableHeader>{{ t('shared.description') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.qty') }}</UiTableHeader>
              <UiTableHeader>{{ t('billing.quoteDetail.unitPrice') }}</UiTableHeader>
              <UiTableHeader align="right">{{ t('shared.amount') }}</UiTableHeader>
              <UiTableHeader />
            </UiTableRow>
          </UiTableHead>
          <UiTableBody divided>
            <UiTableRow v-for="(line, index) in lines" :key="line.id ?? `new-${index}`">
              <UiTableCell>
                <UiCheckbox
                  v-model="line.selected"
                  :aria-label="t('shared.selectRow')"
                  :data-testid="`billing-invoice-item-select-${index}`"
                />
              </UiTableCell>
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
              <UiTableCell align="right">
                {{ formatPlainAmount(lineTotal(line), props.invoice.currency) }}
              </UiTableCell>
              <UiTableCell align="right">
                <UiIconButton
                  :label="t('billing.invoiceItems.removeItem')"
                  :data-testid="`billing-invoice-item-remove-${index}`"
                  @click="removeLine(index)"
                >
                  <UiIcon :markup="Icon.Close" size="sm" />
                </UiIconButton>
              </UiTableCell>
            </UiTableRow>
          </UiTableBody>
          <template #empty>
            {{ t('shared.noItems') }}
          </template>
        </UiTable>

        <UiStack v-if="!readonly" direction="row" justify="between" align="center" wrap>
          <UiStack direction="row" gap="sm" align="center">
            <UiText tone="secondary" size="md">{{ t('shared.withSelected') }}</UiText>
            <UiButton
              variant="danger"
              size="sm"
              :disabled="selectedCount === 0"
              data-testid="billing-invoice-items-delete-selected"
              @click="deleteSelected"
            >
              {{ t('shared.delete') }}
            </UiButton>
          </UiStack>
          <UiButton variant="secondary" size="sm" data-testid="billing-invoice-items-add" @click="addLine">
            {{ t('shared.addItem') }}
          </UiButton>
        </UiStack>

        <UiStack align="end" gap="xs" data-testid="billing-invoice-totals">
          <UiStack v-for="row in totalRows" :key="row.key" direction="row" gap="lg" align="center">
            <UiText :tone="row.strong ? 'primary' : 'muted'" size="md">{{ row.label }}</UiText>
            <UiText :tone="row.strong ? 'primary' : 'secondary'" size="md" :data-testid="`billing-invoice-total-${row.key}`">
              {{ formatPlainAmount(row.value, props.invoice.currency) }}
            </UiText>
          </UiStack>
        </UiStack>
      </UiStack>
    </UiCard>

    <UiStack v-if="!readonly" direction="row" justify="center" gap="sm">
      <UiButton
        variant="primary"
        :loading="saving"
        :disabled="saving"
        data-testid="billing-invoice-items-save"
        @click="save"
      >
        {{ t('shared.saveChanges') }}
      </UiButton>
      <UiButton variant="secondary" data-testid="billing-invoice-items-cancel" @click="populate">
        {{ t('shared.cancelChanges') }}
      </UiButton>
    </UiStack>
    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="billing-invoice-items-feedback" />
  </UiStack>
</template>
