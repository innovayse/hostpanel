<script setup lang="ts">
/**
 * Fraud settings in three cards: the switch that turns fraud checks on, the risk score thresholds (review and auto reject,
 * with a live preview of the Allow, Review and Reject bands of the 0 to 100 scale) and the velocity limits per IP address
 * (the order window, the orders allowed in it and the accounts allowed). While the switch is off the numbers keep their
 * values but are disabled, with a note saying nothing is applied. The values are checked in the browser the way the API
 * checks them (0 to 100, review below reject), so a bad value is shown under its field before anything is sent, and the
 * shared settings footer saves or discards the edits.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSchemaForm from '@/components/ui/UiSchemaForm.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import FraudNumberInput from '@/components/fraud/FraudNumberInput.vue'
import FraudRiskBand from '@/components/fraud/FraudRiskBand.vue'
import SettingsFormFooter from '@/components/settings/SettingsFormFooter.vue'
import { useResultBanner } from '@/composables/useResultBanner'
import { changedFraudSettingsCount, fraudSettingsErrors } from '@/lib/fraud'
import { schemaHeader } from '@/lib/schemaHeader'
import { useFraudSettingsStore } from '@/stores/fraudSettings'
import { FraudSettingsField } from '@/types/fraud'
import type { FraudSettings } from '@/types/fraud'
import { UiTone } from '@/utils/ui/tone'
import schema from '@/views/FraudSettingsView.schema'

const { t } = useI18n()

/** Fraud settings store: the stored settings and the refusal sentences. */
const store = useFraudSettingsStore()

/** The result banner for a save. */
const banner = useResultBanner()

/** The settings being edited, a copy of the stored ones; null until they are read. */
const draft = ref<FraudSettings | null>(null)

/** True until the settings are read: the form keeps its sections and fields and draws the controls as placeholders. */
const reading = computed<boolean>(() => draft.value === null)

/** What is wrong with each edited field by the API's own rules; empty when the draft is valid. */
const clientErrors = computed<Partial<Record<FraudSettingsField, string>>>(() =>
  draft.value === null ? {} : fraudSettingsErrors(draft.value),
)

/** Whether the draft breaks a rule, which blocks the save. */
const hasErrors = computed<boolean>(() => Object.keys(clientErrors.value).length > 0)

/** How many settings the draft holds differently from the stored ones. */
const changedCount = computed<number>(() =>
  draft.value === null || store.settings === null ? 0 : changedFraudSettingsCount(draft.value, store.settings),
)

/** The footer's status line: how many edits are unsaved. */
const unsavedLabel = computed<string>(() =>
  changedCount.value === 0 ? t('settings.saveBar.clean') : t('settings.saveBar.unsaved', { count: changedCount.value }, changedCount.value),
)

/** Whether protection is off in the draft, which turns the thresholds and limits off with it. */
const protectionOff = computed<boolean>(() => draft.value !== null && !draft.value.enabled)

/** The message to show under each field: the browser's own check first, then the API's refusal. */
const errors = computed<Record<string, string>>(() => Object.assign({}, store.fieldErrors, clientErrors.value))

/** The help line under each field of the schema; the switch is explained by its own sentence. */
const hints = computed<Record<string, string>>(() => ({
  enabled: t('fraud.settings.enabledHint'),
  ...Object.fromEntries(Object.values(FraudSettingsField).map((field) => [field, t(`fraud.settings.fields.${field}.hint`)])),
}))

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

/** Throws the edits away: the draft goes back to the stored settings. */
const discard = (): void => {
  draft.value = store.settings === null ? null : { ...store.settings }
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
    <UiPageHeader v-bind="schemaHeader(schema, t)" />

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

    <UiSchemaForm
      v-else
      :columns="schema.columns"
      :loading="reading"
      :errors="errors"
      :hints="hints"
      data-testid="fraud-settings-form"
      @submit="save"
    >
      <template #field-enabled>
        <UiToggle v-model="enabled" data-testid="fraud-settings-enabled" />
      </template>

      <template #field-manualReviewRiskScore="{ id, invalid, describedby }">
        <FraudNumberInput
          :id="id"
          :field="FraudSettingsField.ManualReviewRiskScore"
          :model-value="numberOf(FraudSettingsField.ManualReviewRiskScore)"
          :invalid="invalid"
          :describedby="describedby"
          :disabled="protectionOff"
          @update:model-value="setNumber(FraudSettingsField.ManualReviewRiskScore, $event)"
        />
      </template>

      <template #field-autoRejectRiskScore="{ id, invalid, describedby }">
        <FraudNumberInput
          :id="id"
          :field="FraudSettingsField.AutoRejectRiskScore"
          :model-value="numberOf(FraudSettingsField.AutoRejectRiskScore)"
          :invalid="invalid"
          :describedby="describedby"
          :disabled="protectionOff"
          @update:model-value="setNumber(FraudSettingsField.AutoRejectRiskScore, $event)"
        />
      </template>

      <template #field-velocityWindowMinutes="{ id, invalid, describedby }">
        <FraudNumberInput
          :id="id"
          :field="FraudSettingsField.VelocityWindowMinutes"
          :model-value="numberOf(FraudSettingsField.VelocityWindowMinutes)"
          :invalid="invalid"
          :describedby="describedby"
          :disabled="protectionOff"
          @update:model-value="setNumber(FraudSettingsField.VelocityWindowMinutes, $event)"
        />
      </template>

      <template #field-velocityMaxOrders="{ id, invalid, describedby }">
        <FraudNumberInput
          :id="id"
          :field="FraudSettingsField.VelocityMaxOrders"
          :model-value="numberOf(FraudSettingsField.VelocityMaxOrders)"
          :invalid="invalid"
          :describedby="describedby"
          :disabled="protectionOff"
          @update:model-value="setNumber(FraudSettingsField.VelocityMaxOrders, $event)"
        />
      </template>

      <template #field-maxAccountsPerIp="{ id, invalid, describedby }">
        <FraudNumberInput
          :id="id"
          :field="FraudSettingsField.MaxAccountsPerIp"
          :model-value="numberOf(FraudSettingsField.MaxAccountsPerIp)"
          :invalid="invalid"
          :describedby="describedby"
          :disabled="protectionOff"
          @update:model-value="setNumber(FraudSettingsField.MaxAccountsPerIp, $event)"
        />
      </template>

      <template #section-protection>
        <UiAlert v-if="protectionOff" :variant="UiTone.Info" data-testid="fraud-settings-off-note">
          {{ t('fraud.settings.offNote') }}
        </UiAlert>
      </template>

      <template #section-thresholds>
        <FraudRiskBand
          :review="numberOf(FraudSettingsField.ManualReviewRiskScore)"
          :reject="numberOf(FraudSettingsField.AutoRejectRiskScore)"
          :class="protectionOff ? 'opacity-60' : ''"
        />
      </template>

      <SettingsFormFooter
        v-if="!reading"
        :save-label="t('shared.saveChanges')"
        :cancel-label="t('settings.saveBar.discard')"
        :saving="store.saving"
        :dirty="changedCount > 0"
        :save-disabled="hasErrors"
        :status="unsavedLabel"
        @save="save"
        @cancel="discard"
      />
    </UiSchemaForm>
  </UiStack>
</template>
