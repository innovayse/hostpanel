<script setup lang="ts">
/**
 * Dialog for creating or editing a tax rule.
 *
 * A rule covers a country, or one region of it, at a rate in percent. Emits `save` with the payload on
 * submit, and `close` on cancel, Escape or a backdrop click. A refusal of the save shows inside the dialog.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCountrySelect from '@/components/ui/UiCountrySelect.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiFormActions from '@/components/ui/UiFormActions.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { MAX_PERCENT, TAX_RATE_STEP, TAX_RULE_NAME_MAX_LENGTH, TAX_RULE_REGION_MAX_LENGTH } from '@/lib/limits'
import type { SaveTaxRulePayload, TaxRuleDto } from '@/types/taxrule'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

/** Props for TaxRuleFormModal. */
const props = defineProps<{
  /** Rule to edit, or null when creating a new one. */
  rule: TaxRuleDto | null
  /** True while the save request is in flight. */
  saving: boolean
  /** The API's sentence for the last refused save, or null when none. */
  error: string | null
}>()

const emit = defineEmits<{
  /** Emitted when the user submits the form, with the rule's fields. */
  save: [payload: SaveTaxRulePayload]
  /** Emitted when the user closes or cancels the dialog. */
  close: []
}>()

/** Whether the dialog is in edit mode (a rule was provided). */
const isEditMode = computed<boolean>(() => props.rule !== null)

/** Display name input value. */
const name = ref('')

/** ISO 3166-1 alpha-2 code of the selected country. */
const country = ref('')

/** Region input value; empty for a rule that covers the whole country. */
const region = ref('')

/** Tax rate in percent. */
const ratePercent = ref(0)

/** Whether new invoices use the rule. */
const isEnabled = ref(true)

/** Fills the fields from the rule prop, or resets them to the defaults when creating. */
watch(() => props.rule, (current) => {
  name.value = current?.name ?? ''
  country.value = current?.country ?? ''
  region.value = current?.region ?? ''
  ratePercent.value = current?.ratePercent ?? 0
  isEnabled.value = current?.isEnabled ?? true
}, { immediate: true })

/** Title of the dialog. */
const title = computed<string>(() => (isEditMode.value ? t('taxRules.form.editTitle') : t('taxRules.form.addTitle')))

/** Label of the submit button. */
const submitLabel = computed<string>(() => (isEditMode.value ? t('taxRules.form.save') : t('taxRules.form.add')))

/** Submits the form by emitting the save event with the current field values. */
const handleSubmit = (): void => {
  emit('save', {
    name: name.value.trim(),
    country: country.value,
    region: region.value.trim() || null,
    ratePercent: ratePercent.value,
    isEnabled: isEnabled.value,
  })
}
</script>

<template>
  <UiDialog :title="title" size="md" @close="emit('close')">
    <UiForm stacked data-testid="tax-rule-form" @submit="handleSubmit">
      <UiAlert v-if="error" :variant="UiTone.Danger" data-testid="tax-rule-form-error">
        {{ error }}
      </UiAlert>

      <UiField :label="t('taxRules.form.name.label')" required>
        <template #default="{ id, invalid, describedby }">
          <UiInput
            :id="id"
            v-model="name"
            required
            :invalid="invalid"
            :aria-describedby="describedby"
            :maxlength="TAX_RULE_NAME_MAX_LENGTH"
            :placeholder="t('taxRules.form.name.placeholder')"
            data-testid="tax-rule-form-name"
          />
        </template>
      </UiField>

      <UiField :label="t('taxRules.form.country.label')" required>
        <UiCountrySelect
          v-model="country"
          :placeholder="t('taxRules.form.country.placeholder')"
          data-testid="tax-rule-form-country"
        />
      </UiField>

      <UiField :label="t('taxRules.form.region.label')" :hint="t('taxRules.form.region.hint')">
        <template #default="{ id, invalid, describedby }">
          <UiInput
            :id="id"
            v-model="region"
            :invalid="invalid"
            :aria-describedby="describedby"
            :maxlength="TAX_RULE_REGION_MAX_LENGTH"
            :placeholder="t('taxRules.form.region.placeholder')"
            data-testid="tax-rule-form-region"
          />
        </template>
      </UiField>

      <UiField :label="t('taxRules.form.rate.label')" required>
        <UiNumberInput
          v-model="ratePercent"
          :min="0"
          :max="MAX_PERCENT"
          :step="TAX_RATE_STEP"
          :placeholder="t('taxRules.form.rate.placeholder')"
          data-testid="tax-rule-form-rate"
        />
      </UiField>

      <UiStack direction="row" align="center" justify="between">
        <UiStack gap="xs">
          <UiText tone="primary" size="md">
            {{ t('taxRules.form.enabled.label') }}
          </UiText>
          <UiText tone="muted" size="sm">
            {{ t('taxRules.form.enabled.hint') }}
          </UiText>
        </UiStack>
        <UiToggle v-model="isEnabled" data-testid="tax-rule-form-enabled" />
      </UiStack>

      <UiFormActions>
        <UiButton type="button" variant="secondary" data-testid="tax-rule-form-cancel" @click="emit('close')">
          {{ t('taxRules.form.cancel') }}
        </UiButton>
        <UiButton type="submit" variant="primary" :loading="saving" data-testid="tax-rule-form-save">
          {{ submitLabel }}
        </UiButton>
      </UiFormActions>
    </UiForm>
  </UiDialog>
</template>
