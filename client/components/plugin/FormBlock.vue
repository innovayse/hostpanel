<template>
  <UiCard :title="block.title" data-testid="plugin-form">
    <UiForm :error="failed ? message : undefined" :success="failed ? undefined : message || undefined" @submit="submit">
      <UiStack gap="md">
        <template v-for="field in block.fields" :key="field.key">
          <UiToggle
            v-if="field.type === PluginFieldType.Boolean"
            :model-value="flags[field.key] ?? false"
            :label="field.label"
            :testid="`plugin-form-${field.key}`"
            @update:model-value="flags[field.key] = $event"
          />
          <UiSelect
            v-else-if="field.type === PluginFieldType.Select"
            v-model="texts[field.key]"
            :label="field.label"
            :options="optionsOf(field)"
            :required="field.required"
            :placeholder="field.placeholder ?? field.label"
            :error="fieldErrors[field.key]"
            :testid="`plugin-form-${field.key}`"
          />
          <UiTextarea
            v-else-if="field.type === PluginFieldType.Textarea"
            v-model="texts[field.key]"
            :label="field.label"
            :required="field.required"
            :placeholder="field.placeholder ?? field.label"
            :error="fieldErrors[field.key]"
            :testid="`plugin-form-${field.key}`"
          />
          <UiInput
            v-else
            v-model="texts[field.key]"
            :type="inputType(field.type)"
            :label="field.label"
            :required="field.required"
            :placeholder="field.placeholder ?? field.label"
            :error="fieldErrors[field.key]"
            :autocomplete="field.type === PluginFieldType.Secret ? 'off' : undefined"
            :testid="`plugin-form-${field.key}`"
          />
        </template>
      </UiStack>
      <template #actions>
        <UiButton type="submit" :loading="sending" :disabled="sending" data-testid="plugin-form-submit">{{ block.submit.label }}</UiButton>
      </template>
    </UiForm>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * PluginFormBlock. Fields sent as one JSON object to one of the plugin's routes, named by `{ method, path }`:
 * text, number, address, secret, several lines, a choice or a switch. A refusal's sentence is shown by the form and
 * its field sentences under their fields (both from the store, so from the API's `detail` and `errors`); a success
 * shows the plugin's `message`, if any, reads the named blocks again, empties every field when the submit asks for
 * `resetOnSuccess`, and runs its `followUp` (`usePluginFollowUp`). Validation is the server's: the form sends what was
 * typed and renders the answer.
 */
import { usePluginFollowUp, usePluginView } from '~/composables/usePluginView'
import { usePluginsStore } from '~/stores/plugins'
import type { PluginFormBlock, PluginFormField } from '~/types/pluginblock'
import { PluginFieldType } from '~/types/pluginfieldtype'
import { UiInputType } from '~/utils/ui/enums'
import { readPluginField } from '~/utils/plugins'

/** Props of PluginFormBlock. */
interface Props {
  /** The block. */
  block: PluginFormBlock
}

/** One choice of a select field, as the kit draws it. */
interface FieldOption {
  /** The value sent. */
  value: string
  /** The text shown: the value itself, since the view gives choices no labels. */
  label: string
}

/** The props. */
const props = defineProps<Props>()

/** The view's context: values and refresh. */
const context = usePluginView()
/** The plugin store, which makes the call. */
const store = usePluginsStore()
/** Starts the follow-up refresh; the view ends it when the page is left, not when this block hides. */
const followUp = usePluginFollowUp(() => props.block.id)

/**
 * The empty text values: every text field empty.
 *
 * @returns The values, by field key.
 */
const emptyTexts = (): Record<string, string> => Object.fromEntries(props.block.fields
  .filter(field => field.type !== PluginFieldType.Boolean)
  .map(field => [field.key, '']))

/**
 * The empty switch values: every switch off.
 *
 * @returns The values, by field key.
 */
const emptyFlags = (): Record<string, boolean> => Object.fromEntries(props.block.fields
  .filter(field => field.type === PluginFieldType.Boolean)
  .map(field => [field.key, false]))

/** The text values, by field key; every text field starts empty. */
const texts = ref<Record<string, string>>(emptyTexts())
/** The switch values, by field key; every switch starts off. */
const flags = ref<Record<string, boolean>>(emptyFlags())
/** True while the form is being sent. */
const sending = ref(false)
/** The sentence of the last send: the plugin's `message`, or the refusal; empty when there is none. */
const message = ref('')
/** Whether the last send failed. */
const failed = ref(false)
/** The refusal's sentence per field. */
const fieldErrors = ref<Record<string, string>>({})

/**
 * The input type of a one-line field.
 *
 * @param type - The field type.
 * @returns The kit's input type; a secret is a password field, so it is not shown back.
 */
const inputType = (type: PluginFieldType): UiInputType => {
  switch (type) {
    case PluginFieldType.Number: return UiInputType.Number
    case PluginFieldType.Url: return UiInputType.Url
    case PluginFieldType.Secret: return UiInputType.Password
    default: return UiInputType.Text
  }
}

/**
 * The choices of a select field.
 *
 * @param field - The field.
 * @returns The options, each shown as the value it sends.
 */
const optionsOf = (field: PluginFormField): FieldOption[] => (field.options ?? []).map(option => ({ value: option, label: option }))

/**
 * The body sent: every field under its key, numbers as numbers and switches as booleans. A number field that does
 * not parse is sent as typed, so the plugin's own refusal names it.
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
