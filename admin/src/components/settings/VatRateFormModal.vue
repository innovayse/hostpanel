<script setup lang="ts">
/**
 * Dialog for adding or editing a VAT rate.
 *
 * A row holds the standard or a reduced rate of one EU member state and the day it applies from. Emits `save`
 * with the payload on submit, and `close` on cancel, Escape or a backdrop click. A refusal of the save shows
 * inside the dialog.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCountrySelect from '@/components/ui/UiCountrySelect.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiFormActions from '@/components/ui/UiFormActions.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { EU_MEMBER_STATES } from '@/lib/euMemberStates'
import { MAX_PERCENT, TAX_RATE_STEP } from '@/lib/limits'
import { VatRateKind } from '@/types/vatrate'
import type { SaveVatRatePayload, VatRateDto } from '@/types/vatrate'
import { UiDatePickerDirection } from '@/utils/ui/datePicker'
import { UiTone } from '@/utils/ui/tone'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiDialogSize } from '@/utils/ui/dialog'

/** Translates the dialog's text into the operator's language. */
const { t } = useI18n()

/** Props for VatRateFormModal. */
const props = defineProps<{
  /** Row to edit, or null when adding a new one. */
  rate: VatRateDto | null
  /** True while the save request is in flight. */
  saving: boolean
  /** The API's sentence for the last refused save, or null when none. */
  error: string | null
}>()

const emit = defineEmits<{
  /** Emitted when the user submits the form, with the row's fields. */
  save: [payload: SaveVatRatePayload]
  /** Emitted when the user closes or cancels the dialog. */
  close: []
}>()

/** Whether the dialog is in edit mode (a row was provided). */
const isEditMode = computed<boolean>(() => props.rate !== null)

/** ISO 3166-1 alpha-2 code of the selected member state. */
const country = ref('')

/** Whether the row holds the standard or a reduced rate. */
const kind = ref<VatRateKind>(VatRateKind.Standard)

/** The rate in percent. */
const ratePercent = ref(0)

/** First day the rate applies, ISO `YYYY-MM-DD`, or null while none is chosen. */
const validFrom = ref<string | null>(null)

/** Fills the fields from the row prop, or resets them to the defaults when adding. */
watch(() => props.rate, (current) => {
  country.value = current?.country ?? ''
  kind.value = current?.kind ?? VatRateKind.Standard
  ratePercent.value = current?.ratePercent ?? 0
  validFrom.value = current?.validFrom ?? null
}, { immediate: true })

/** The kinds the select offers, each named in the reader's language. */
const kindOptions = computed(() =>
  Object.values(VatRateKind).map(value => ({ value, label: t(`vatRates.kind.${value}`) })),
)

/** Title of the dialog. */
const title = computed<string>(() => (isEditMode.value ? t('vatRates.form.editTitle') : t('vatRates.addRate')))

/** Label of the submit button. */
const submitLabel = computed<string>(() => (isEditMode.value ? t('shared.save') : t('shared.create')))

/** Whether every field a row needs is filled: a member state and a first day. */
const complete = computed<boolean>(() => country.value !== '' && validFrom.value !== null)

/** Submits the form by emitting the save event with the current field values; nothing is sent while a field is empty. */
const handleSubmit = (): void => {
  if (!complete.value || validFrom.value === null) {
    return
  }
  emit('save', {
    country: country.value,
    kind: kind.value,
    ratePercent: ratePercent.value,
    validFrom: validFrom.value,
  })
}
</script>

<template>
  <UiDialog :title="title" :size="UiDialogSize.Md" @close="emit('close')">
    <UiForm stacked data-testid="vat-rate-form" @submit="handleSubmit">
      <UiAlert v-if="error" :variant="UiTone.Danger" data-testid="vat-rate-form-error">
        {{ error }}
      </UiAlert>

      <UiField :label="t('vatRates.form.country.label')" required>
        <UiCountrySelect
          v-model="country"
          :only="EU_MEMBER_STATES"
          :placeholder="t('vatRates.form.country.placeholder')"
          data-testid="vat-rate-form-country"
        />
      </UiField>

      <UiField :label="t('vatRates.form.kind.label')" required>
        <template #default="{ id }">
          <UiSelect
            :id="id"
            v-model="kind"
            :options="kindOptions"
            :placeholder="t('vatRates.form.kind.placeholder')"
            data-testid="vat-rate-form-kind"
          />
        </template>
      </UiField>

      <UiField :label="t('vatRates.form.rate.label')" required>
        <UiNumberInput
          v-model="ratePercent"
          :min="0"
          :max="MAX_PERCENT"
          :step="TAX_RATE_STEP"
          :placeholder="t('vatRates.form.rate.placeholder')"
          data-testid="vat-rate-form-rate"
        />
      </UiField>

      <UiField :label="t('vatRates.form.validFrom.label')" :hint="t('vatRates.form.validFrom.hint')" required>
        <UiDatePicker
          v-model="validFrom"
          :direction="UiDatePickerDirection.Future"
          data-testid="vat-rate-form-valid-from"
        />
      </UiField>

      <UiFormActions>
        <UiButton :action="UiActionKind.Cancel" type="button" :variant="UiButtonVariant.Secondary" data-testid="vat-rate-form-cancel" @click="emit('close')">
          {{ t('shared.cancel') }}
        </UiButton>
        <UiButton :action="UiActionKind.Save" type="submit" :variant="UiButtonVariant.Primary" :loading="saving" :disabled="!complete" data-testid="vat-rate-form-save">
          {{ submitLabel }}
        </UiButton>
      </UiFormActions>
    </UiForm>
  </UiDialog>
</template>
