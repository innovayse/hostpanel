<script setup lang="ts">
/**
 * Dialog for creating or editing a configured currency.
 *
 * Code is editable only when creating; the rate-to-base field is disabled for the base currency,
 * since a base is always worth exactly one unit of itself. Emits `save` with the payload on submit,
 * and `close` on cancel, Escape or a backdrop click.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiFormActions from '@/components/ui/UiFormActions.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { formatAffixed } from '@/lib/money'
import type { CurrencyDto, UpsertCurrencyPayload } from '@/types/currency'
import { CURRENCY_CODE_LENGTH, CURRENCY_MAX_DECIMALS, EXCHANGE_RATE_STEP } from '@/lib/limits'

const { t } = useI18n()

/** Props for CurrencyFormModal. */
const props = defineProps<{
  /** Currency to edit, or null when creating a new one. */
  currency: CurrencyDto | null
  /** True while the save request is in flight. */
  saving: boolean
}>()

const emit = defineEmits<{
  /** Emitted when the user submits a valid form, with the code and the upsert payload. */
  save: [code: string, payload: UpsertCurrencyPayload]
  /** Emitted when the user closes or cancels the dialog. */
  close: []
}>()

/** Sample amount used to show how the format renders. */
const SAMPLE_AMOUNT = 2.99

/** Whether the dialog is in edit mode (a currency was provided). */
const isEditMode = computed<boolean>(() => props.currency !== null)

/** Whether the edited currency is the base one. */
const isBase = computed<boolean>(() => props.currency?.isBase ?? false)

/** ISO 4217 alpha code input value; editable only in create mode. */
const code = ref('')

/** ISO 4217 numeric code input value. */
const numeric = ref('')

/** Prefix text printed before a formatted amount. */
const prefix = ref('')

/** Suffix text printed after a formatted amount. */
const suffix = ref('')

/** Decimal places an amount is rounded and shown to. */
const decimals = ref(2)

/** How much of the base one unit of this currency is worth. */
const rateToBase = ref(1)

/** Whether new clients may choose this currency. */
const isEnabled = ref(true)

/** Fills the fields from the currency prop, or resets them to the defaults when creating. */
watch(() => props.currency, (current) => {
  code.value = current?.code ?? ''
  numeric.value = current?.numeric ?? ''
  prefix.value = current?.prefix ?? ''
  suffix.value = current?.suffix ?? ''
  decimals.value = current?.decimals ?? 2
  rateToBase.value = current?.rateToBase ?? 1
  isEnabled.value = current?.isEnabled ?? true
}, { immediate: true })

/** Live sample of how an amount renders with the current prefix/suffix/decimals. */
const formatSample = computed<string>(() => formatAffixed(SAMPLE_AMOUNT, { prefix: prefix.value, suffix: suffix.value, decimals: decimals.value }))

/** Title of the dialog. */
const title = computed<string>(() => (isEditMode.value ? t('currencies.form.editTitle') : t('currencies.form.addTitle')))

/** Label of the submit button. */
const submitLabel = computed<string>(() => (isEditMode.value ? t('currencies.form.save') : t('currencies.form.add')))

/** Submits the form by emitting the save event with the current field values. */
const handleSubmit = (): void => {
  emit('save', code.value.trim().toUpperCase(), {
    numeric: numeric.value.trim(),
    prefix: prefix.value,
    suffix: suffix.value,
    decimals: decimals.value,
    rateToBase: rateToBase.value,
    isEnabled: isEnabled.value,
  })
}
</script>

<template>
  <UiDialog :title="title" size="md" @close="emit('close')">
    <UiForm stacked data-testid="currency-form" @submit="handleSubmit">
      <UiField :label="t('currencies.form.code.label')">
        <template #default="{ id, invalid, describedby }">
          <UiInput
            :id="id"
            v-model="code"
            required
            :disabled="isEditMode"
            :invalid="invalid"
            :aria-describedby="describedby"
            :maxlength="CURRENCY_CODE_LENGTH"
            :placeholder="t('currencies.form.code.placeholder')"
            data-testid="currency-form-code"
            class="uppercase"
          />
        </template>
      </UiField>

      <UiField :label="t('currencies.form.numeric.label')">
        <template #default="{ id, invalid, describedby }">
          <UiInput
            :id="id"
            v-model="numeric"
            required
            :invalid="invalid"
            :aria-describedby="describedby"
            :maxlength="CURRENCY_CODE_LENGTH"
            :placeholder="t('currencies.form.numeric.placeholder')"
            data-testid="currency-form-numeric"
          />
        </template>
      </UiField>

      <UiGrid :columns="2">
        <UiField :label="t('currencies.form.prefix.label')">
          <template #default="{ id, invalid, describedby }">
            <UiInput
              :id="id"
              v-model="prefix"
              :invalid="invalid"
              :aria-describedby="describedby"
              :placeholder="t('currencies.form.prefix.placeholder')"
              data-testid="currency-form-prefix"
            />
          </template>
        </UiField>
        <UiField :label="t('currencies.form.suffix.label')">
          <template #default="{ id, invalid, describedby }">
            <UiInput
              :id="id"
              v-model="suffix"
              :invalid="invalid"
              :aria-describedby="describedby"
              :placeholder="t('currencies.form.suffix.placeholder')"
              data-testid="currency-form-suffix"
            />
          </template>
        </UiField>
      </UiGrid>

      <UiText tone="muted" size="sm" data-testid="currency-form-sample">
        {{ t('currencies.form.formatSample', { sample: formatSample }) }}
      </UiText>

      <UiField :label="t('currencies.form.decimals')">
        <UiNumberInput
          v-model="decimals"
          :min="0"
          :max="CURRENCY_MAX_DECIMALS"
          :step="1"
          placeholder="2"
          data-testid="currency-form-decimals"
        />
      </UiField>

      <UiField
        :label="t('currencies.form.rateToBase')"
        :hint="isBase ? t('currencies.form.rateToBaseBaseHint') : undefined"
      >
        <UiNumberInput
          v-model="rateToBase"
          :min="0"
          :step="EXCHANGE_RATE_STEP"
          :disabled="isBase"
          placeholder="1"
          data-testid="currency-form-rate"
        />
      </UiField>

      <UiStack direction="row" align="center" justify="between">
        <UiStack gap="xs">
          <UiText tone="primary" size="md">
            {{ t('currencies.form.enabled.label') }}
          </UiText>
          <UiText tone="muted" size="sm">
            {{ isBase ? t('currencies.form.enabled.baseHint') : t('currencies.form.enabled.hint') }}
          </UiText>
        </UiStack>
        <!-- Shown disabled (not hidden) for the base row, so its always-on state stays visible. -->
        <UiToggle v-model="isEnabled" :disabled="isBase" data-testid="currency-form-enabled" />
      </UiStack>

      <UiFormActions>
        <UiButton type="button" variant="secondary" data-testid="currency-form-cancel" @click="emit('close')">
          {{ t('currencies.form.cancel') }}
        </UiButton>
        <UiButton type="submit" variant="primary" :loading="saving" data-testid="currency-form-save">
          {{ submitLabel }}
        </UiButton>
      </UiFormActions>
    </UiForm>
  </UiDialog>
</template>
