<script setup lang="ts">
/**
 * Dynamic config form for a single integration.
 *
 * Renders the fields the API describes in `integration.fieldDefinitions`, each as the kit control
 * its `type` asks for. A plugin's label, help text, placeholder and option labels arrive already translated by the API;
 * those of a built-in integration are the admin catalogue's locale keys, worded here as the form draws, so they follow
 * a language switch. Test Connection is shown only when `integration.canTestConnection` says the integration can be
 * tested. Emits `save` and `test` to the parent, which owns the requests.
 *
 * A secret field (the API marks it `isSecret` and never sends its value) is write only: it shows a
 * masked placeholder with Replace and Clear, its value is sent only when the admin typed a
 * replacement, and Clear is sent as an explicit `clearFields` entry.
 *
 * The form is the body of the config card of `IntegrationDetailView.schema`, which `UiDetailCards` draws (the card, and
 * its skeleton while the detail is still being read), so the form itself only exists for a loaded integration.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { integrationLabelText, integrationMetaOf } from '@/types/integrationmeta'
import type { FieldDefinitionDto, IntegrationConfigPayload, IntegrationDetailDto } from '@/types/integration'
import UiSecretField from '@/components/ui/UiSecretField.vue'
import { fieldControlOf, fieldInputType, INTEGRATION_FIELD_RENDERINGS } from '@/lib/fieldControls'
import { FieldControl } from '@/types/fieldcontrol'
import { SettingToggle } from '@/types/setting'
import UiButton from '@/components/ui/UiButton.vue'
import UiField from '@/components/ui/UiField.vue'
import UiFormActions from '@/components/ui/UiFormActions.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiImage from '@/components/ui/UiImage.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiPluginAvatar from '@/components/ui/UiPluginAvatar.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiDirection, UiGap, UiJustify } from '@/utils/ui/layout'
import { UiHeadingVariant, UiLabelVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'

/** Translation function. */
const { t } = useI18n()

/** Props for IntegrationConfigForm. */
const props = defineProps<{
  /** Full integration detail loaded from the store. */
  integration: IntegrationDetailDto
  /** True while a save request is in flight. */
  loading: boolean
  /** True while a connection test is in flight; apart from `loading` so Save never claims to be saving in a test. */
  testing: boolean
}>()

/** Events emitted by IntegrationConfigForm. */
const emit = defineEmits<{
  /** Emitted when the admin clicks Save Changes. */
  save: [payload: IntegrationConfigPayload]
  /** Emitted when the admin clicks Test Connection. */
  test: []
}>()

/** Catalogue metadata (logo, tile colour, short description); absent for a plugin, which has none in the admin. */
const meta = computed(() => integrationMetaOf(props.integration.slug))

/** The integration's name in the operator's current language. */
const name = computed<string>(() => integrationLabelText(props.integration.name))

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
 * The kit control that draws a field: the secret field whenever the API marks it secret, else the control of its type.
 *
 * @param field - The field definition.
 * @returns The control.
 */
const controlOf = (field: FieldDefinitionDto): FieldControl => fieldControlOf(INTEGRATION_FIELD_RENDERINGS[field.type], isSecretField(field))

/**
 * Whether a secret field has a value stored on the server, whether or not it is marked for clearing.
 *
 * @param field - The field definition.
 * @returns True when the server holds a value.
 */
const secretIsStored = (field: FieldDefinitionDto): boolean => props.integration.config[field.key]?.hasValue === true

/**
 * Takes a pending clear back, so the stored secret is kept on save.
 *
 * @param key - The field key.
 */
const keepSecret = (key: string): void => {
  clearedFields.value = clearedFields.value.filter((k) => k !== key)
}

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
    keepSecret(key)
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
 * A field's label in the operator's current language.
 *
 * @param field - The field definition.
 * @returns The label text.
 */
const fieldText = (field: FieldDefinitionDto): string => integrationLabelText(field.label)

/**
 * The text of one option of a select field; the option's value when it has no wording of its own.
 *
 * @param field - The field definition.
 * @param option - The option's value.
 * @returns The text shown for the option.
 */
const optionLabel = (field: FieldDefinitionDto, option: string): string => {
  const label = field.optionLabels?.[option]
  return label ? integrationLabelText(label) : option
}

/**
 * Select options of a field.
 *
 * @param field - The field definition.
 * @returns The options in the shape the select takes.
 */
const optionsOf = (field: FieldDefinitionDto): { value: string; label: string }[] =>
  (field.options ?? []).map(option => ({ value: option, label: optionLabel(field, option) }))

/**
 * The hint of an empty input: the plugin's own placeholder, already translated by the API, else the generic one.
 *
 * @param field - The field definition.
 * @returns The placeholder text.
 */
const placeholderOf = (field: FieldDefinitionDto): string =>
  field.placeholder ?? t('integrations.integrationConfigForm.enterValue', { label: fieldText(field) })

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
  <UiStack :gap="UiGap.Lg" data-testid="integration-config-form">
    <!-- Card header: logo + name + enabled switch -->
    <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Md">
      <UiPluginAvatar
        v-if="integration.isPlugin"
        :name="name"
        :icon-url="integration.iconUrl"
        decorative
        data-testid="integration-plugin-avatar"
      />
      <UiStack v-else :align="UiAlignItems.Center" :justify="UiJustify.Center" class="h-10 w-10 shrink-0 rounded-control p-2" :class="meta?.color ?? 'bg-text-muted'">
        <UiImage
          v-if="meta?.logo"
          :src="meta.logo"
          :alt="name"
          class="h-full w-full object-contain brightness-0 invert"
        />
      </UiStack>
      <UiStack :gap="UiGap.Xs" class="min-w-0 flex-1">
        <UiHeading :level="2" :variant="UiHeadingVariant.Card">{{ name }}</UiHeading>
        <UiText v-if="meta" :tone="UiTextTone.Muted" :size="UiTextSize.Sm" class="truncate">{{ meta.shortDescription }}</UiText>
      </UiStack>
      <UiLabel :variant="UiLabelVariant.Inline" class="shrink-0">
        <UiToggle v-model="isEnabled" data-testid="integration-enabled" />
        <UiText :tone="isEnabled ? UiTextTone.Success : UiTextTone.Muted" :size="UiTextSize.Sm" class="font-semibold">
          {{ isEnabled ? t('shared.enabled') : t('shared.disabled') }}
        </UiText>
      </UiLabel>
    </UiStack>

    <!-- Dynamic fields -->
    <UiGrid :columns="2" :gap="UiGap.Md">
      <template v-for="field in integration.fieldDefinitions ?? []" :key="field.key">
        <UiField
          :label="fieldText(field)"
          :hint="field.description ?? undefined"
          :required="field.required"
          :class="controlOf(field) === FieldControl.Textarea ? 'md:col-span-2' : ''"
        >
          <template #default="{ id }">
            <UiSecretField
              v-if="controlOf(field) === FieldControl.Secret"
              :id="id"
              :has-value="secretIsStored(field)"
              :cleared="clearedFields.includes(field.key)"
              :model-value="secretDrafts[field.key] ?? ''"
              :disabled="loading"
              :testid="`integration-field-${field.key}`"
              @update:model-value="setSecret(field.key, $event)"
              @clear="clearSecret(field.key)"
              @cancel-clear="keepSecret(field.key)"
            />

            <UiTextarea
              v-else-if="controlOf(field) === FieldControl.Textarea"
              :id="id"
              v-model="localConfig[field.key]"
              rows="3"
              :placeholder="placeholderOf(field)"
              :data-testid="`integration-field-${field.key}`"
            />

            <UiSelect
              v-else-if="controlOf(field) === FieldControl.Select"
              :model-value="localConfig[field.key] ?? ''"
              :options="optionsOf(field)"
              @update:model-value="setValue(field.key, $event)"
              :placeholder="t('integrations.integrationConfigForm.chooseValue', { label: fieldText(field) })"
              :data-testid="`integration-field-${field.key}`"
            />

            <!-- Boolean fields are kept as the strings "true" / "false" -->
            <UiLabel v-else-if="controlOf(field) === FieldControl.Toggle" :variant="UiLabelVariant.Inline">
              <UiToggle
                :model-value="localConfig[field.key] === SettingToggle.On"
                :data-testid="`integration-field-${field.key}`"
                @update:model-value="setToggle(field.key, $event)"
              />
              <UiText :tone="localConfig[field.key] === SettingToggle.On ? UiTextTone.Success : UiTextTone.Muted" :size="UiTextSize.Sm" class="font-semibold">
                {{ localConfig[field.key] === SettingToggle.On ? t('shared.enabled') : t('shared.disabled') }}
              </UiText>
            </UiLabel>

            <UiNumberInput
              v-else-if="controlOf(field) === FieldControl.Number"
              :id="id"
              :model-value="numberOf(field.key)"
              :min="field.min ?? undefined"
              :max="field.max ?? undefined"
              nullable
              :required="field.required"
              :placeholder="placeholderOf(field)"
              :data-testid="`integration-field-${field.key}`"
              @update:model-value="setNumber(field.key, $event)"
            />

            <UiInput
              v-else
              :id="id"
              v-model="localConfig[field.key]"
              :type="fieldInputType(INTEGRATION_FIELD_RENDERINGS[field.type])"
              class="font-mono"
              :placeholder="placeholderOf(field)"
              :data-testid="`integration-field-${field.key}`"
            />
          </template>
        </UiField>
      </template>
    </UiGrid>

    <!-- Actions -->
    <UiFormActions>
      <!-- Hidden when the API says the integration has no test (a plugin without the status capability). -->
      <UiButton :action="UiActionKind.Test"
        v-if="integration.canTestConnection"
        :title="t('shared.testConnection')"
        :aria-label="t('shared.testConnection')"
        type="button"
        :variant="UiButtonVariant.Secondary"
        :loading="testing"
        :disabled="loading"
        data-testid="integration-test"
        @click="emit('test')"
      >
        {{ t('shared.test') }}
      </UiButton>
      <UiButton :action="UiActionKind.Save"
        type="button"
        :variant="UiButtonVariant.Primary"
        :loading="loading"
        :disabled="testing"
        data-testid="integration-save"
        @click="handleSave"
      >
        {{ t('shared.save') }}
      </UiButton>
    </UiFormActions>
  </UiStack>
</template>
