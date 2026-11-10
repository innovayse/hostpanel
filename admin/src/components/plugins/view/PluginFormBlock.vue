<script setup lang="ts">
/**
 * Fields sent as one JSON object to one of the plugin's admin routes, named by `{ method, path }`: text,
 * number, address, secret, several lines, a choice or a switch. A refusal's sentence is shown under the
 * form and its field sentences under their fields (the API's `detail` and `errors`); a success shows the
 * plugin's `message`, if any, reads the named blocks again, empties every field when the submit asks for
 * `resetOnSuccess`, and runs its `followUp`. Validation is the server's: the form sends what was typed.
 */
import { computed, ref } from 'vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { usePluginFollowUp, usePluginView } from '@/composables/usePluginView'
import { readPluginField } from '@/lib/pluginView'
import { usePluginUiStore } from '@/stores/pluginUi'
import { PluginFieldType } from '@/types/pluginview'
import type { PluginFormBlock, PluginFormField } from '@/types/pluginview'
import { UiButtonType } from '@/utils/ui/button'
import { UiCardPadding } from '@/utils/ui/card'
import { UiInputType } from '@/utils/ui/input'
import { UiAlignItems, UiGap, UiJustify } from '@/utils/ui/layout'
import { UiHeadingVariant, UiLabelVariant, UiTextSize } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

/** Props for PluginFormBlock. */
const props = defineProps<{
  /** The block. */
  block: PluginFormBlock
}>()

/** One choice of a select field, as the kit draws it. */
interface FieldOption {
  /** The value sent. */
  value: string
  /** The text shown: the value itself, since the view gives choices no labels. */
  label: string
}

/** The view's context: values and refresh. */
const context = usePluginView()
/** The plugin ui store, which makes the call. */
const store = usePluginUiStore()
/** Starts the follow-up refresh; the view ends it when the page is left. */
const followUp = usePluginFollowUp(() => props.block.id)

/**
 * The empty text values: every text field empty.
 *
 * @returns The values, by field key.
 */
const emptyTexts = (): Record<string, string> =>
  Object.fromEntries(props.block.fields.filter((field) => field.type !== PluginFieldType.Boolean).map((field) => [field.key, '']))

/**
 * The empty switch values: every switch off.
 *
 * @returns The values, by field key.
 */
const emptyFlags = (): Record<string, boolean> =>
  Object.fromEntries(props.block.fields.filter((field) => field.type === PluginFieldType.Boolean).map((field) => [field.key, false]))

/** The text values, by field key. */
const texts = ref<Record<string, string>>(emptyTexts())
/** The switch values, by field key. */
const flags = ref<Record<string, boolean>>(emptyFlags())
/** True while the form is being sent. */
const sending = ref(false)
/** The sentence of the last send: the plugin's `message`, or the refusal; empty when there is none. */
const message = ref('')
/** Whether the last send failed. */
const failed = ref(false)
/** The refusal's sentence per field. */
const fieldErrors = ref<Record<string, string>>({})

/** The tone of the sentence under the form. */
const messageTone = computed<UiTone>(() => (failed.value ? UiTone.Danger : UiTone.Success))

/**
 * The input type of a one-line field.
 *
 * @param type - The field type.
 * @returns The kit's input type; a secret is a password field, so it is not shown back.
 */
const inputType = (type: PluginFieldType): UiInputType => {
  if (type === PluginFieldType.Number) {
    return UiInputType.Number
  }
  if (type === PluginFieldType.Url) {
    return UiInputType.Url
  }
  return type === PluginFieldType.Secret ? UiInputType.Password : UiInputType.Text
}

/**
 * The choices of a select field.
 *
 * @param field - The field.
 * @returns The options, each shown as the value it sends.
 */
const optionsOf = (field: PluginFormField): FieldOption[] => (field.options ?? []).map((option) => ({ value: option, label: option }))

/**
 * Sets one text value.
 *
 * @param key - The field key.
 * @param value - The new value.
 * @returns Nothing.
 */
const setText = (key: string, value: string): void => {
  texts.value = { ...texts.value, [key]: value }
}

/**
 * Sets one switch value.
 *
 * @param key - The field key.
 * @param value - The new value.
 * @returns Nothing.
 */
const setFlag = (key: string, value: boolean): void => {
  flags.value = { ...flags.value, [key]: value }
}

/**
 * The body sent: every field under its key, numbers as numbers and switches as booleans. A number that
 * does not parse is sent as typed, so the plugin's own refusal names it.
 *
 * @returns The body.
 */
const body = (): Record<string, unknown> => Object.fromEntries(props.block.fields.map((field) => {
  if (field.type === PluginFieldType.Boolean) {
    return [field.key, flags.value[field.key] ?? false]
  }
  const text = texts.value[field.key] ?? ''
  const number = Number(text)
  return [field.key, field.type === PluginFieldType.Number && text.trim() !== '' && Number.isFinite(number) ? number : text]
}))

/**
 * Sends the form.
 *
 * @returns Resolves when the call has ended.
 */
const submit = async (): Promise<void> => {
  if (sending.value) {
    return
  }
  sending.value = true
  const result = await store.callRoute(context.pluginId, props.block.submit.route, context.values, body())
  sending.value = false
  failed.value = !result.ok
  fieldErrors.value = result.fieldErrors
  const answered = readPluginField(result.data, 'message')
  message.value = result.ok ? (typeof answered === 'string' ? answered : '') : result.error
  if (result.ok) {
    if (props.block.submit.resetOnSuccess === true) {
      texts.value = emptyTexts()
      flags.value = emptyFlags()
    }
    context.refresh(props.block.submit.refresh ?? [])
    followUp(props.block.submit.followUp)
  }
}
</script>

<template>
  <UiCard :padding="UiCardPadding.Md" data-testid="plugin-form">
    <UiForm stacked @submit="submit">
      <UiHeading v-if="block.title" :level="3" :variant="UiHeadingVariant.Card">{{ block.title }}</UiHeading>
      <template v-for="field in block.fields" :key="field.key">
        <UiLabel v-if="field.type === PluginFieldType.Boolean" :variant="UiLabelVariant.Inline">
          <UiToggle
            :model-value="flags[field.key] ?? false"
            :data-testid="`plugin-form-${field.key}`"
            @update:model-value="setFlag(field.key, $event)"
          />
          <UiText :size="UiTextSize.Sm">{{ field.label }}</UiText>
        </UiLabel>
        <UiField
          v-else
          :label="field.label"
          :required="field.required"
          :error="fieldErrors[field.key]"
          :error-testid="`plugin-form-${field.key}-error`"
        >
          <template #default="{ id, invalid }">
            <UiSelect
              v-if="field.type === PluginFieldType.Select"
              :id="id"
              :model-value="texts[field.key] ?? ''"
              :options="optionsOf(field)"
              :invalid="invalid"
              :placeholder="field.placeholder ?? field.label"
              :data-testid="`plugin-form-${field.key}`"
              @update:model-value="setText(field.key, $event)"
            />
            <UiTextarea
              v-else-if="field.type === PluginFieldType.Textarea"
              :id="id"
              :model-value="texts[field.key] ?? ''"
              :invalid="invalid"
              :required="field.required"
              :placeholder="field.placeholder ?? field.label"
              :data-testid="`plugin-form-${field.key}`"
              @update:model-value="setText(field.key, $event)"
            />
            <UiInput
              v-else
              :id="id"
              :model-value="texts[field.key] ?? ''"
              :type="inputType(field.type)"
              :invalid="invalid"
              :required="field.required"
              :placeholder="field.placeholder ?? field.label"
              :autocomplete="field.type === PluginFieldType.Secret ? 'off' : undefined"
              :data-testid="`plugin-form-${field.key}`"
              @update:model-value="setText(field.key, $event)"
            />
          </template>
        </UiField>
      </template>
      <UiStatusMessage :message="message || null" :tone="messageTone" data-testid="plugin-form-message" />
      <UiStack direction="row" :justify="UiJustify.End" :align="UiAlignItems.Center" :gap="UiGap.Sm">
        <UiButton :type="UiButtonType.Submit" :loading="sending" :disabled="sending" data-testid="plugin-form-submit">{{ block.submit.label }}</UiButton>
      </UiStack>
    </UiForm>
  </UiCard>
</template>
