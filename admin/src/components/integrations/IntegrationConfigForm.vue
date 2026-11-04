<script setup lang="ts">
/**
 * Dynamic config form for a single integration.
 *
 * Renders the fields the API describes in `integration.fieldDefinitions`, each as the kit control
 * its `type` asks for. Emits `save` and `test` to the parent, which owns the requests.
 *
 * A secret field (the API marks it `isSecret` and never sends its value) is write only: it shows a
 * masked placeholder with Replace and Clear, its value is sent only when the admin typed a
 * replacement, and Clear is sent as an explicit `clearFields` entry.
 */
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { INTEGRATION_META } from '@/types/integrationmeta'
import type { FieldDefinitionDto, IntegrationConfigPayload, IntegrationDetailDto, IntegrationSlug } from '@/types/integration'
import SecretSettingField from '@/components/settings/SecretSettingField.vue'
import { IntegrationFieldType } from '@/types/integration'
import { SettingToggle } from '@/types/setting'
import { UiInputType } from '@/utils/ui/input'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiImage from '@/components/ui/UiImage.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import UiToggle from '@/components/ui/UiToggle.vue'

/** Translation function. */
const { t } = useI18n()

/** Props for IntegrationConfigForm. */
const props = defineProps<{
  /** Full integration detail loaded from the store. */
  integration: IntegrationDetailDto
  /** True while a save request is in flight. */
  loading: boolean
  /** True while a connection test is in flight. Separate from `loading` so Save never claims to be saving during a test. */
  testing: boolean
}>()

/** Events emitted by IntegrationConfigForm. */
const emit = defineEmits<{
  /** Emitted when the admin clicks Save Changes. */
  save: [payload: IntegrationConfigPayload]
  /** Emitted when the admin clicks Test Connection. */
  test: []
}>()

/** Static metadata for this integration (logo, tile colour and short description); absent for a plugin the admin has no metadata for. */
const meta = INTEGRATION_META[props.integration.slug as IntegrationSlug]

/**
 * The editable values of the plain fields of an integration; secret fields are left out because
 * the API sends no value for them.
 *
 * @param detail - The integration detail.
 * @returns Field key to stored value, plain fields only.
 */
const plainValuesOf = (detail: IntegrationDetailDto): Record<string, string> => {
  const values: Record<string, string> = {}
  for (const [key, state] of Object.entries(detail.config)) {
    if (!state.isSecret) {
      values[key] = state.value ?? ''
    }
  }
  return values
}

/** Local copy of the plain config values the admin is editing. */
const localConfig = ref<Record<string, string>>(plainValuesOf(props.integration))

/** Replacements typed for secret fields, by field key; empty means "keep the stored secret". */
const secretDrafts = ref<Record<string, string>>({})

/** Secret field keys the admin cleared; sent as `clearFields` on save. */
const clearedFields = ref<string[]>([])

/** Local toggle for enabled/disabled state. */
const isEnabled = ref(props.integration.isEnabled)

// Re-sync local state when a new integration is loaded.
watch(
  () => props.integration,
  (next) => {
    localConfig.value = plainValuesOf(next)
    secretDrafts.value = {}
    clearedFields.value = []
    isEnabled.value = next.isEnabled
  },
  { deep: true },
)

/**
 * Whether a field holds a secret, as the API classifies it.
 *
 * @param field - The field definition.
 * @returns True for a write-only field.
 */
const isSecretField = (field: FieldDefinitionDto): boolean => props.integration.config[field.key]?.isSecret === true

/**
 * Whether a secret field still has a stored value: set on the server and not cleared here.
 *
 * @param field - The field definition.
 * @returns True when the masked placeholder should show.
 */
const secretIsSet = (field: FieldDefinitionDto): boolean =>
  props.integration.config[field.key]?.hasValue === true && !clearedFields.value.includes(field.key)

/**
 * Marks a secret field for clearing on save and drops any replacement typed for it.
 *
 * @param key - The field key.
 */
const clearSecret = (key: string): void => {
  delete secretDrafts.value[key]
  if (!clearedFields.value.includes(key)) {
    clearedFields.value = [...clearedFields.value, key]
  }
}

/**
 * Records a replacement typed for a secret field. Typing undoes a pending clear, because the new
 * value replaces the stored one anyway.
 *
 * @param key - The field key.
 * @param value - The typed value.
 */
const setSecret = (key: string, value: string): void => {
  secretDrafts.value[key] = value
  if (value.length > 0) {
    clearedFields.value = clearedFields.value.filter((k) => k !== key)
  }
}

/**
 * Stores a value the admin picked in a select.
 *
 * @param key - The field key.
 * @param value - The picked option value; null clears the field.
 */
const setValue = (key: string, value: string | number | null): void => {
  localConfig.value[key] = value === null ? '' : String(value)
}

/**
 * Reads a field's stored text as the number the number input takes.
 *
 * @param key - The field key.
 * @returns The number, or null while the field is empty or holds text that is not a number.
 */
const numberOf = (key: string): number | null => {
  const raw = localConfig.value[key]
  if (raw === undefined || raw === '') {
    return null
  }
  const parsed = Number(raw)
  return Number.isNaN(parsed) ? null : parsed
}

/**
 * Stores a number the admin entered as the string the API keeps.
 *
 * @param key - The field key.
 * @param value - The entered number; null (the field was cleared) stores the empty string.
 */
const setNumber = (key: string, value: number | null): void => {
  localConfig.value[key] = value === null ? '' : String(value)
}

/**
 * Select options of a field.
 *
 * @param field - The field definition.
 * @returns The options in the shape the select takes.
 */
const optionsOf = (field: FieldDefinitionDto): { value: string; label: string }[] =>
  (field.options ?? []).map(option => ({ value: option, label: option }))

/**
 * Stores a boolean field as the string the API keeps.
 *
 * @param key - The field key.
 * @param on - The new state.
 */
const setToggle = (key: string, on: boolean): void => {
  localConfig.value[key] = on ? SettingToggle.On : SettingToggle.Off
}

/** Builds and emits the save payload: plain values, typed secret replacements and the clears. */
const handleSave = (): void => {
  const config: Record<string, string> = { ...localConfig.value }
  for (const [key, value] of Object.entries(secretDrafts.value)) {
    // An empty secret would keep the stored one anyway; leaving it out says so plainly.
    if (value.length > 0) {
      config[key] = value
    }
  }
  const payload: IntegrationConfigPayload = { isEnabled: isEnabled.value, config }
  if (clearedFields.value.length > 0) {
    payload.clearFields = [...clearedFields.value]
  }
  emit('save', payload)
}
</script>

<template>
  <UiCard padding="md" data-testid="integration-config-form">
    <UiStack gap="lg">
      <!-- Card header: logo + name + enabled switch -->
      <UiStack direction="row" align="center" gap="md">
        <UiStack align="center" justify="center" class="h-10 w-10 shrink-0 rounded-xl p-2" :class="meta?.color ?? 'bg-text-muted'">
          <UiImage
            v-if="meta?.logo"
            :src="meta.logo"
            :alt="integration.name"
            class="h-full w-full object-contain brightness-0 invert"
          />
        </UiStack>
        <UiStack gap="xs" class="min-w-0 flex-1">
          <UiHeading :level="2" variant="card">{{ integration.name }}</UiHeading>
          <UiText v-if="meta" tone="muted" size="sm" class="truncate">{{ meta.shortDescription }}</UiText>
        </UiStack>
        <UiLabel variant="inline" class="shrink-0">
          <UiToggle v-model="isEnabled" data-testid="integration-enabled" />
          <UiText :tone="isEnabled ? 'success' : 'muted'" size="sm" class="font-semibold">
            {{ isEnabled ? t('shared.enabled') : t('shared.disabled') }}
          </UiText>
        </UiLabel>
      </UiStack>

      <!-- Dynamic fields -->
      <UiGrid :columns="2" gap="md">
        <template v-for="field in props.integration.fieldDefinitions ?? []" :key="field.key">
          <UiField
            :label="field.label"
            :required="field.required"
            :class="field.type === IntegrationFieldType.Textarea ? 'md:col-span-2' : ''"
          >
            <template #default="{ id }">
              <UiStack v-if="isSecretField(field)" gap="xs">
                <SecretSettingField
                  :id="id"
                  :has-value="secretIsSet(field)"
                  :model-value="secretDrafts[field.key] ?? ''"
                  :disabled="loading"
                  :testid="`integration-field-${field.key}`"
                  @update:model-value="setSecret(field.key, $event)"
                  @clear="clearSecret(field.key)"
                />
                <UiText
                  v-if="clearedFields.includes(field.key)"
                  tone="muted"
                  size="sm"
                  :data-testid="`integration-field-${field.key}-pending-clear`"
                >
                  {{ t('settings.secret.pendingClear') }}
                </UiText>
              </UiStack>

              <UiTextarea
                v-else-if="field.type === IntegrationFieldType.Textarea"
                :id="id"
                v-model="localConfig[field.key]"
                rows="3"
                :placeholder="t('integrations.integrationConfigForm.enterValue', { label: field.label })"
                :data-testid="`integration-field-${field.key}`"
              />

              <UiSelect
                v-else-if="field.type === IntegrationFieldType.Select"
                :model-value="localConfig[field.key] ?? ''"
                :options="optionsOf(field)"
                @update:model-value="setValue(field.key, $event)"
                :placeholder="t('integrations.integrationConfigForm.chooseValue', { label: field.label })"
                :data-testid="`integration-field-${field.key}`"
              />

              <!-- Boolean fields are kept as the strings "true" / "false" -->
              <UiLabel v-else-if="field.type === IntegrationFieldType.Toggle" variant="inline">
                <UiToggle
                  :model-value="localConfig[field.key] === SettingToggle.On"
                  :data-testid="`integration-field-${field.key}`"
                  @update:model-value="setToggle(field.key, $event)"
                />
                <UiText :tone="localConfig[field.key] === SettingToggle.On ? 'success' : 'muted'" size="sm" class="font-semibold">
                  {{ localConfig[field.key] === SettingToggle.On ? t('shared.enabled') : t('shared.disabled') }}
                </UiText>
              </UiLabel>

              <UiNumberInput
                v-else-if="field.type === IntegrationFieldType.Number"
                :id="id"
                :model-value="numberOf(field.key)"
                :min="field.min ?? undefined"
                :max="field.max ?? undefined"
                nullable
                :placeholder="t('integrations.integrationConfigForm.enterValue', { label: field.label })"
                :data-testid="`integration-field-${field.key}`"
                @update:model-value="setNumber(field.key, $event)"
              />

              <UiInput
                v-else
                :id="id"
                v-model="localConfig[field.key]"
                :type="field.type === IntegrationFieldType.Url ? UiInputType.Url : UiInputType.Text"
                class="font-mono"
                :placeholder="t('integrations.integrationConfigForm.enterValue', { label: field.label })"
                :data-testid="`integration-field-${field.key}`"
              />
            </template>
          </UiField>
        </template>
      </UiGrid>

      <!-- Actions -->
      <UiStack direction="row" gap="sm">
        <UiButton
          type="button"
          variant="primary"
          :loading="loading"
          :disabled="testing"
          data-testid="integration-save"
          @click="handleSave"
        >
          {{ t('shared.saveChanges') }}
        </UiButton>
        <UiButton
          type="button"
          variant="secondary"
          :loading="testing"
          :disabled="loading"
          data-testid="integration-test"
          @click="emit('test')"
        >
          {{ t('shared.testConnection') }}
        </UiButton>
      </UiStack>
    </UiStack>
  </UiCard>
</template>
