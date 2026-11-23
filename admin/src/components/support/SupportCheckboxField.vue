<script setup lang="ts">
/**
 * A checkbox with its caption: clicking the caption toggles the box, like a label would.
 * `testid` names the box (`<testid>`); the caption carries `<testid>-label`.
 */
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

/** Props for SupportCheckboxField. */
const props = defineProps<{
  /** Whether the box is checked (two-way bound with `v-model`). */
  modelValue: boolean
  /** Caption shown beside the box. */
  label: string
  /** `data-testid` of the box. */
  testid: string
}>()

/** Events of SupportCheckboxField. */
const emit = defineEmits<{
  /** Emitted with the new checked state. */
  'update:modelValue': [value: boolean]
}>()

/**
 * Flips the box when its caption is clicked.
 */
const toggle = (): void => {
  emit('update:modelValue', !props.modelValue)
}
</script>

<template>
  <UiStack :direction="UiDirection.Row" :gap="UiGap.Sm" :align="UiAlignItems.Center">
    <UiCheckbox :model-value="modelValue" :data-testid="testid" @update:model-value="emit('update:modelValue', $event)" />
    <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Md" class="cursor-pointer" :data-testid="`${testid}-label`" @click="toggle">
      {{ label }}
    </UiText>
  </UiStack>
</template>
