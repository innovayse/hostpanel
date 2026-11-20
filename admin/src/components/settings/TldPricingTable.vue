<script setup lang="ts">
/**
 * One pricing table of the TLD form: a row per registration period with the cost and sell price
 * entered side by side and the margin between them worked out live.
 *
 * The form has three of these (registration, transfer, renewal); each sits in the field of its own section card of the
 * form's schema, which draws the title and, while the configuration loads, the placeholder. The
 * maps are keyed by period and are seeded with every listed period by the form, so a period always
 * has a value; the `?? 0` below is unreachable while that holds and only gives the stepper a
 * definite number.
 */
import { useI18n } from 'vue-i18n'
import UiStepper from '@/components/ui/UiStepper.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTooltip from '@/components/ui/UiTooltip.vue'
import { EMPTY_VALUE } from '@/lib/format'
import { TLD_PERIODS, calcMargin, marginTone } from '@/lib/tldPricing'
import { UiTableDensity } from '@/utils/ui/injection'
import { UiAlign, UiColumnSize } from '@/utils/ui/table'

const { t } = useI18n()

/** Props for TldPricingTable. */
withDefaults(defineProps<{
  /** Prefix of the `data-testid`s (`<prefix>-cost-<period>`, `<prefix>-sell-<period>`). */
  testid: string
  /** Symbol of the cost currency, shown in the cost column header. */
  costSymbol: string
  /** Symbol of the sell currency, shown in the sell column header. */
  sellSymbol: string
  /** Cost prices keyed by period. */
  cost: Record<string, number>
  /** Sell prices keyed by period. */
  sell: Record<string, number>
  /** Whether cost and sell are stated in one currency; when not, no margin is worked out and a dash with a hint stands in. */
  comparable?: boolean
}>(), {
  comparable: true,
})

const emit = defineEmits<{
  /** Emitted with the period and the new cost price. */
  'update:cost': [period: string, value: number]
  /** Emitted with the period and the new sell price. */
  'update:sell': [period: string, value: number]
}>()
</script>

<template>
  <UiTable :density="UiTableDensity.Compact" :data-testid="testid">
    <UiTableHead>
      <UiTableRow>
        <UiTableHeader :align="UiAlign.Left" :size="UiColumnSize.Compact">{{ t('settings.tldConfigForm.period') }}</UiTableHeader>
        <UiTableHeader :align="UiAlign.Left">{{ t('settings.tldConfigForm.costCostsymbol', { costSymbol }) }}</UiTableHeader>
        <UiTableHeader :align="UiAlign.Left">{{ t('settings.tldConfigForm.sellSellsymbol', { sellSymbol }) }}</UiTableHeader>
        <UiTableHeader numeric>{{ t('settings.tldConfigForm.margin') }}</UiTableHeader>
      </UiTableRow>
    </UiTableHead>
    <UiTableBody divided>
      <UiTableRow v-for="period in TLD_PERIODS" :key="period">
        <UiTableCell muted>
          {{ period }} {{ Number(period) === 1 ? t('shared.year') : t('settings.tldConfigForm.years') }}
        </UiTableCell>
        <UiTableCell class="pr-2">
          <UiStepper
            :model-value="cost[period] ?? 0"
            :step="0.01"
            :min="0"
            placeholder="0.00"
            :data-testid="`${testid}-cost-${period}`"
            @update:model-value="emit('update:cost', period, $event)"
          />
        </UiTableCell>
        <UiTableCell class="pr-2">
          <UiStepper
            :model-value="sell[period] ?? 0"
            :step="0.01"
            :min="0"
            placeholder="0.00"
            :data-testid="`${testid}-sell-${period}`"
            @update:model-value="emit('update:sell', period, $event)"
          />
        </UiTableCell>
        <UiTableCell numeric>
          <UiTooltip v-if="!comparable" :text="t('settings.tldConfigForm.marginDifferentCurrencies')">
            <UiText tone="muted" size="sm" class="font-medium" tabindex="0" :data-testid="`${testid}-margin-${period}`">
              {{ EMPTY_VALUE }}
            </UiText>
          </UiTooltip>
          <UiText
            v-else
            :tone="marginTone(cost[period], sell[period])"
            size="sm"
            class="font-medium"
            :data-testid="`${testid}-margin-${period}`"
          >
            {{ calcMargin(cost[period], sell[period]) }}
          </UiText>
        </UiTableCell>
      </UiTableRow>
    </UiTableBody>
  </UiTable>
</template>
