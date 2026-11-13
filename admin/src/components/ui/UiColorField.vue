<script setup lang="ts">
/**
 * Colour field: a hex text input with a swatch preview, no native colour picker.
 *
 * The operator types `#rrggbb`; the swatch beside the input previews it as soon as it parses, and
 * the input is flagged invalid while the text is neither empty (unset) nor a six-digit hex colour.
 * It is a plain text input, so it is reachable and editable from the keyboard like any other
 * field. Attributes (`class`, `aria-label`, `data-testid`, key listeners) land on the input; the
 * wrapper only lays the swatch out beside it. Emits the raw text, so the owner decides when an
 * invalid value blocks saving.
 */
import { computed, useAttrs } from 'vue'
import UiColorSwatch from '@/components/ui/UiColorSwatch.vue'
import UiInput from '@/components/ui/UiInput.vue'
import { hexToRgb } from '@/lib/brandPalette'

defineOptions({ inheritAttrs: false })

/** Props for UiColorField. */
const props = withDefaults(defineProps<{
  /** Current colour text (two-way bound with `v-model`). */
  modelValue?: string
  /** Hint shown while the field is empty. */
  placeholder?: string
  /** Colour previewed while the text is empty or incomplete; nothing is drawn when omitted. */
  fallback?: string
}>(), {
  modelValue: '',
  placeholder: '#rrggbb',
  fallback: undefined,
})

const emit = defineEmits<{
  /** Emitted with the new text on every keystroke. */
  'update:modelValue': [value: string]
}>()

/** Attributes forwarded to the input. */
const attrs = useAttrs()

/** Whether the text is a complete `#rrggbb` colour. */
const isHex = computed<boolean>(() => hexToRgb(props.modelValue) !== null)

/** Whether the text is something other than empty or a complete colour. */
const invalid = computed<boolean>(() => props.modelValue !== '' && !isHex.value)

/** The colour the swatch draws: the typed colour when it parses, else the fallback. */
const preview = computed<string | undefined>(() => (isHex.value ? props.modelValue.toLowerCase() : props.fallback))

/** Classes of the input: the caller's classes are kept next to the field's own. */
const inputClass = computed<unknown[]>(() => ['w-32 font-mono', attrs.class])

/** Attributes for the input without the class, which is merged above. */
const inputAttrs = computed<Record<string, unknown>>(() =>
  Object.fromEntries(Object.entries(attrs).filter(([name]) => name !== 'class')),
)
</script>

<template>
  <div class="flex items-center gap-2">
    <span class="block w-10.5 shrink-0 overflow-hidden rounded-control border border-border">
      <UiColorSwatch :color="preview" :title="preview" />
    </span>
    <UiInput
      v-bind="inputAttrs"
      type="text"
      spellcheck="false"
      :model-value="modelValue"
      :placeholder="placeholder"
      :invalid="invalid"
      :class="inputClass"
      @update:model-value="emit('update:modelValue', $event)"
    />
  </div>
</template>
