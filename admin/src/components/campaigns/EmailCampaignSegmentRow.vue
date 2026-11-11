<script setup lang="ts">
/**
 * One audience rule of an email campaign: the kind of rule, the value it needs (a product, a
 * country or a number of days; none for "all active clients") and a button that removes the row.
 *
 * Changing the kind clears the value, since a product id means nothing as a country. The row
 * holds no state of its own: it reads the rule through `v-model` and tells the form what changed.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiCountrySelect from '@/components/ui/UiCountrySelect.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { CAMPAIGN_FILTER_TYPES, EXPIRING_DAYS_MAX, EXPIRING_DAYS_MIN } from '@/lib/emailCampaigns'
import { CampaignFilterType } from '@/types/emailcampaign'
import type { EmailCampaignSegmentDraft } from '@/types/emailcampaign'
import { UiButtonVariant } from '@/utils/ui/button'

/** A product the "has a service of" rule can pick. */
export interface CampaignProductOption {
  /** The product id as text, the value the rule stores. */
  value: string
  /** The product name. */
  label: string
}

const { t } = useI18n()

/** Props for EmailCampaignSegmentRow. */
const props = defineProps<{
  /** The rule being edited (two-way bound with `v-model`). */
  modelValue: EmailCampaignSegmentDraft
  /** The products the product rule offers. */
  products: CampaignProductOption[]
}>()

const emit = defineEmits<{
  /** Emitted with the changed rule. */
  'update:modelValue': [value: EmailCampaignSegmentDraft]
  /** Emitted when the operator removes the row. */
  remove: []
}>()

/** The kinds of rule as select options, in the order the form offers them. */
const typeOptions = computed(() =>
  CAMPAIGN_FILTER_TYPES.map((type) => ({ value: type, label: t(`emailCampaigns.filterType.${type}`) })),
)

/** Test id prefix of this row's controls. */
const testid = computed<string>(() => `email-campaign-segment-${props.modelValue.key}`)

/**
 * Switches the kind of rule and clears the value.
 *
 * @param filterType - The kind the operator picked.
 */
const changeType = (filterType: CampaignFilterType): void => {
  emit('update:modelValue', { ...props.modelValue, filterType, filterValue: '' })
}

/**
 * Stores a new value of the rule.
 *
 * @param filterValue - The value as picked or typed.
 */
const changeValue = (filterValue: string): void => {
  emit('update:modelValue', { ...props.modelValue, filterValue })
}

/** The days of the expiry rule as the number input holds them; null while empty. */
const days = computed<number | null>(() =>
  props.modelValue.filterValue === '' ? null : Number(props.modelValue.filterValue),
)

/**
 * Stores the days the number input reports.
 *
 * @param value - The number, or null when the field was cleared.
 */
const changeDays = (value: number | null): void => {
  changeValue(value === null ? '' : String(value))
}
</script>

<template>
  <UiStack direction="row" gap="sm" align="center" wrap :data-testid="testid">
    <div class="w-64 max-w-full">
      <UiSelect
        :model-value="modelValue.filterType"
        :options="typeOptions"
        :placeholder="t('emailCampaigns.filterTypePlaceholder')"
        :data-testid="`${testid}-type`"
        @update:model-value="changeType"
      />
    </div>

    <div v-if="modelValue.filterType === CampaignFilterType.Product" class="w-64 max-w-full">
      <UiSelect
        :model-value="modelValue.filterValue"
        :options="products"
        searchable
        :placeholder="t('emailCampaigns.productPlaceholder')"
        :data-testid="`${testid}-value`"
        @update:model-value="changeValue"
      />
    </div>
    <div v-else-if="modelValue.filterType === CampaignFilterType.Country" class="w-64 max-w-full">
      <UiCountrySelect
        :model-value="modelValue.filterValue"
        :placeholder="t('emailCampaigns.countryPlaceholder')"
        :data-testid="`${testid}-value`"
        @update:model-value="changeValue"
      />
    </div>
    <UiStack v-else-if="modelValue.filterType === CampaignFilterType.ServiceExpiringInDays" direction="row" gap="sm" align="center">
      <div class="w-40">
        <UiNumberInput
          :model-value="days"
          :min="EXPIRING_DAYS_MIN"
          :max="EXPIRING_DAYS_MAX"
          nullable
          :placeholder="t('emailCampaigns.daysPlaceholder')"
          :data-testid="`${testid}-value`"
          @update:model-value="changeDays"
        />
      </div>
      <UiText tone="muted" size="sm">{{ t('emailCampaigns.daysSuffix') }}</UiText>
    </UiStack>

    <UiButton
      type="button"
      :variant="UiButtonVariant.Secondary"
      :data-testid="`${testid}-remove`"
      @click="emit('remove')"
    >
      {{ t('emailCampaigns.removeSegment') }}
    </UiButton>
  </UiStack>
</template>
