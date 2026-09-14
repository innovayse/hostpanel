<script setup lang="ts">
/**
 * Modal dialog for creating or editing a configured currency.
 *
 * Code is editable only when creating; the rate-to-base field is disabled for the
 * base currency, since a base is always worth exactly one unit of itself.
 * Emits `save` with the payload on submit, and `close` on cancel.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { CurrencyDto, UpsertCurrencyPayload } from '@/types/currency'
import UiToggleSwitch from '@/components/ui/UiToggleSwitch.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'

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
  /** Emitted when the user closes or cancels the modal. */
  close: []
}>()

/** Whether the modal is in edit mode (a currency was provided). */
const isEditMode = computed(() => props.currency !== null)

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

/**
 * Initializes or resets form fields based on the currency prop.
 */
watch(() => props.currency, (c) => {
  if (c) {
    code.value = c.code
    numeric.value = c.numeric
    prefix.value = c.prefix
    suffix.value = c.suffix
    decimals.value = c.decimals
    rateToBase.value = c.rateToBase
    isEnabled.value = c.isEnabled
  } else {
    code.value = ''
    numeric.value = ''
    prefix.value = ''
    suffix.value = ''
    decimals.value = 2
    rateToBase.value = 1
    isEnabled.value = true
  }
}, { immediate: true })

/** Live sample of how an amount renders with the current prefix/suffix/decimals. */
const formatSample = computed(() => {
  const amount = (2.99).toFixed(decimals.value)
  return `${prefix.value}${amount}${suffix.value}`
})

/**
 * Submits the form by emitting the save event with the current field values.
 */
function handleSubmit(): void {
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
  <div class="fixed inset-0 z-50 flex items-center justify-center p-4">
    <div class="absolute inset-0 bg-black/60 backdrop-blur-sm" @click="emit('close')" />

    <div class="relative bg-surface-card border border-border rounded-2xl w-full max-w-md max-h-[90dvh] overflow-y-auto shadow-2xl">

      <!-- Header -->
      <div class="flex items-center justify-between px-6 py-4 border-b border-border">
        <h2 class="font-display font-bold text-[1rem] text-text-primary">
          {{ isEditMode ? t('currencies.form.editTitle') : t('currencies.form.addTitle') }}
        </h2>
        <button
          class="w-7 h-7 flex items-center justify-center rounded-lg text-text-muted hover:text-text-primary hover:bg-white/[0.06] transition-colors"
          @click="emit('close')"
        >
          <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" />
          </svg>
        </button>
      </div>

      <form class="px-6 py-5 flex flex-col gap-5" @submit.prevent="handleSubmit">

        <!-- Code -->
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">{{ t('currencies.form.code.label') }}</label>
          <input
            v-model="code"
            required
            :disabled="isEditMode"
            maxlength="3"
            :placeholder="t('currencies.form.code.placeholder')"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted uppercase focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors disabled:opacity-50"
          />
        </div>

        <!-- Numeric code -->
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">{{ t('currencies.form.numeric.label') }}</label>
          <input
            v-model="numeric"
            required
            maxlength="3"
            :placeholder="t('currencies.form.numeric.placeholder')"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          />
        </div>

        <!-- Prefix / Suffix -->
        <div class="grid grid-cols-2 gap-3">
          <div>
            <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">{{ t('currencies.form.prefix.label') }}</label>
            <input
              v-model="prefix"
              :placeholder="t('currencies.form.prefix.placeholder')"
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            />
          </div>
          <div>
            <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">{{ t('currencies.form.suffix.label') }}</label>
            <input
              v-model="suffix"
              :placeholder="t('currencies.form.suffix.placeholder')"
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            />
          </div>
        </div>

        <!-- Format sample -->
        <p class="text-[0.72rem] text-text-muted">{{ t('currencies.form.formatSample', { sample: formatSample }) }}</p>

        <!-- Decimals -->
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">{{ t('currencies.form.decimals') }}</label>
          <UiNumberInput v-model="decimals" :min="0" :max="4" :step="1" />
        </div>

        <!-- Rate to base -->
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">{{ t('currencies.form.rateToBase') }}</label>
          <UiNumberInput
            v-model="rateToBase"
            :min="0"
            :step="0.00000001"
            :disabled="currency?.isBase ?? false"
          />
          <p v-if="currency?.isBase" class="mt-1 text-[0.7rem] text-text-muted">{{ t('currencies.form.rateToBaseBaseHint') }}</p>
        </div>

        <!-- Enabled toggle -->
        <div class="flex items-center justify-between py-1">
          <div>
            <p class="text-[0.82rem] text-text-primary font-medium">{{ t('currencies.form.enabled.label') }}</p>
            <p class="text-[0.7rem] text-text-muted">
              {{ currency?.isBase ? t('currencies.form.enabled.baseHint') : t('currencies.form.enabled.hint') }}
            </p>
          </div>
          <!-- Shown disabled (not hidden) for the base row, so its always-on state stays visible. -->
          <UiToggleSwitch v-model="isEnabled" :disabled="currency?.isBase ?? false" />
        </div>

        <!-- Actions -->
        <div class="flex items-center justify-end gap-2.5 pt-1">
          <button
            type="button"
            class="px-4 py-2 text-[0.84rem] font-medium text-text-secondary hover:text-text-primary bg-white/[0.04] border border-border rounded-[10px] transition-colors"
            @click="emit('close')"
          >
            {{ t('currencies.form.cancel') }}
          </button>
          <button
            type="submit"
            :disabled="saving"
            class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
          >
            {{ saving ? t('currencies.form.saving') : isEditMode ? t('currencies.form.save') : t('currencies.form.add') }}
          </button>
        </div>

      </form>
    </div>
  </div>
</template>
