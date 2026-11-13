<script setup lang="ts">
/**
 * Fraud settings: the switch that turns fraud checks on, the review and auto reject thresholds,
 * the order-velocity window and limit, and the number of accounts allowed from one IP address.
 * The values are checked in the browser the way the API checks them (0 to 100, review below
 * reject), so a bad value is shown under its field before anything is sent.
 */
import { computed, onMounted, provide, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiFormActions from '@/components/ui/UiFormActions.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { useResultBanner } from '@/composables/useResultBanner'
import { FRAUD_NUMBER_FIELDS, fraudSettingsErrors } from '@/lib/fraud'
import { useFraudSettingsStore } from '@/stores/fraudSettings'
import type { FraudSettings, FraudSettingsField } from '@/types/fraud'
import { UiButtonVariant } from '@/utils/ui/button'
import { UI_FIELD_LOADING } from '@/utils/ui/injection'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

/** Fraud settings store: the stored settings and the refusal sentences. */
const store = useFraudSettingsStore()

/** The result banner for a save. */
const banner = useResultBanner()

/** The settings being edited, a copy of the stored ones; null until they are read. */
const draft = ref<FraudSettings | null>(null)

/** True until the settings are read: the form keeps its fields and draws them as placeholders. */
const reading = computed<boolean>(() => draft.value === null)

// Every numeric field stays a placeholder until the settings have arrived.
provide(UI_FIELD_LOADING, reading)

/** What is wrong with each edited field by the API's own rules; empty when the draft is valid. */
const clientErrors = computed<Partial<Record<FraudSettingsField, string>>>(() =>
  draft.value === null ? {} : fraudSettingsErrors(draft.value),
)

/** Whether the draft breaks a rule, which blocks the save. */
const hasErrors = computed<boolean>(() => Object.keys(clientErrors.value).length > 0)

/**
 * The message to show under a field: the browser's own check first, then the API's refusal.
 *
 * @param field - The field.
 * @returns The sentence, or undefined when the field is fine.
 */
const errorOf = (field: FraudSettingsField): string | undefined => clientErrors.value[field] ?? store.fieldErrors[field]

/** The switch as a boolean the toggle binds to; off while the settings are not read yet. */
const enabled = computed<boolean>({
  /**
   * Whether fraud detection is on in the draft.
   *
   * @returns The draft's switch.
   */
  get: () => draft.value?.enabled ?? false,
  /**
   * Writes the switch into the draft.
   *
   * @param on - Whether the switch is now on.
   */
  set: (on: boolean) => {
    if (draft.value !== null) {
      draft.value.enabled = on
    }
  },
})

/**
 * The number a numeric field shows.
 *
 * @param field - The field.
 * @returns The draft's number; 0 while the settings are not read yet.
 */
const numberOf = (field: FraudSettingsField): number => draft.value?.[field] ?? 0

/**
 * Writes an edited number into the draft.
 *
 * @param field - The field.
 * @param value - The number the input reports; null (a cleared field) leaves the draft as it is.
 */
const setNumber = (field: FraudSettingsField, value: number | null): void => {
  if (draft.value !== null && value !== null) {
    draft.value[field] = value
  }
}

/** Saves the draft and shows how it went. */
const save = async (): Promise<void> => {
  if (draft.value === null || hasErrors.value) {
    return
  }
  const saved = await store.save({ ...draft.value })
  if (saved) {
    banner.show(t('fraud.settings.saved'), false)
  } else if (store.actionError && Object.keys(store.fieldErrors).length === 0) {
    // A refusal that names a field is shown under that field; any other refusal has no place but the banner.
    banner.show(store.actionError, true)
  }
}

// The draft follows the stored settings: first read, and the read-back after each save.
watch(() => store.settings, (stored) => {
  draft.value = stored === null ? null : { ...stored }
}, { immediate: true })

onMounted(() => store.fetchSettings())
</script>

<template>
  <UiStack data-testid="fraud-settings-view">
    <UiPageHeader :title="t('fraud.settings.title')" :subtitle="t('fraud.settings.subtitle')" />

    <UiAlert
      v-if="banner.message.value"
      :variant="banner.isError.value ? UiTone.Danger : UiTone.Success"
      data-testid="fraud-settings-result"
    >
      {{ banner.message.value }}
    </UiAlert>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="fraud-settings-error"
      @retry="store.fetchSettings()"
    />

    <UiCard v-else padding="md" :aria-busy="reading ? 'true' : undefined">
      <UiForm stacked data-testid="fraud-settings-form" @submit="save">
        <UiStack direction="row" align="center" wrap>
          <UiToggle v-model="enabled" :disabled="reading" data-testid="fraud-settings-enabled" />
          <UiStack gap="xs" class="min-w-56 flex-1">
            <UiText tone="primary" size="md">{{ t('fraud.settings.enabled') }}</UiText>
            <UiText tone="muted" size="sm">{{ t('fraud.settings.enabledHint') }}</UiText>
          </UiStack>
        </UiStack>

        <UiGrid :columns="2">
          <UiField
            v-for="spec in FRAUD_NUMBER_FIELDS"
            :key="spec.field"
            :label="t(`fraud.settings.fields.${spec.field}.label`)"
            :hint="t(`fraud.settings.fields.${spec.field}.hint`)"
            :error="errorOf(spec.field)"
            :error-testid="`fraud-settings-${spec.field}-error`"
          >
            <template #default="{ id, invalid }">
              <UiNumberInput
                :id="id"
                :model-value="numberOf(spec.field)"
                :min="spec.min"
                :max="spec.max"
                :invalid="invalid"
                :placeholder="String(spec.min)"
                :data-testid="`fraud-settings-${spec.field}`"
                @update:model-value="setNumber(spec.field, $event)"
              />
            </template>
          </UiField>
        </UiGrid>

        <UiFormActions>
          <UiButton
            type="submit"
            :variant="UiButtonVariant.Primary"
            :loading="store.saving"
            :disabled="hasErrors || reading"
            data-testid="fraud-settings-save"
          >
            {{ t('shared.saveChanges') }}
          </UiButton>
        </UiFormActions>
      </UiForm>
    </UiCard>
  </UiStack>
</template>
