<script setup lang="ts">
/**
 * One pricing table of the TLD form: a row per registration period with the cost and sell price
 * entered side by side and the margin between them worked out live.
 *
 * The form has three of these (registration, transfer, renewal); each is a titled section. The
 * maps are keyed by period and are seeded with every listed period by the form, so a period always
 * has a value; the `?? 0` below is unreachable while that holds and only gives the stepper a
 * definite number. While `loading` the title stays and the table draws one placeholder row per period under its
 * real header.
 */
import { useI18n } from 'vue-i18n'
import SettingsSection from '@/components/settings/SettingsSection.vue'
import UiStepper from '@/components/ui/UiStepper.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { TLD_PERIODS, calcMargin, marginTone } from '@/lib/tldPricing'
import { UiTableDensity } from '@/utils/ui/injection'

const { t } = useI18n()

/** Props for TldPricingTable. */
withDefaults(defineProps<{
  /** Section title (already translated), e.g. "Registration pricing". */
  title: string
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
  /** Draws placeholder rows in place of the steppers while the config loads. */
  loading?: boolean
}>(), {
  loading: false,
})

const emit = defineEmits<{
  /** Emitted with the period and the new cost price. */
  'update:cost': [period: string, value: number]
  /** Emitted with the period and the new sell price. */
  'update:sell': [period: string, value: number]
}>()
</script>

<template>
  <SettingsSection :title="title" :loading="loading">
    <UiTable :density="UiTableDensity.Compact" :loading="loading" :skeleton-rows="TLD_PERIODS.length" :data-testid="testid">
      <UiTableHead>
        <UiTableRow>
          <UiTableHeader align="left">{{ t('settings.tldConfigForm.period') }}</UiTableHeader>
          <UiTableHeader align="left">{{ t('settings.tldConfigForm.costCostsymbol', { costSymbol }) }}</UiTableHeader>
          <UiTableHeader align="left">{{ t('settings.tldConfigForm.sellSellsymbol', { sellSymbol }) }}</UiTableHeader>
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
            <UiText
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
  </SettingsSection>
</template>
