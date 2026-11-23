<script setup lang="ts">
/**
 * Write-only control for a secret setting: a gateway key, a registrar or mail password, a plugin
 * credential.
 *
 * The API never sends a secret back, only whether one is stored, so there is nothing to pre-fill.
 * A stored secret shows as a masked placeholder with Replace and Clear; Replace opens an empty
 * password input whose text becomes the new value, and Clear asks the parent to remove the stored
 * one. An empty input means "keep what is stored", which is also what the API does with it.
 */
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import { UiInputType } from '@/utils/ui/input'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiActionKind } from '@/utils/ui/actions'

/** Translation function. */
const { t } = useI18n()

/** Props for SecretSettingField. */
const props = withDefaults(defineProps<{
  /** Whether a value is stored on the server. */
  hasValue: boolean
  /** The new value typed so far; empty while the stored value is kept. */
  modelValue: string
  /** Test id prefix of the control; the parts add `-input`, `-replace`, `-clear` and `-cancel`. */
  testid: string
  /** Id of the input, so a surrounding field label points at it. */
  id?: string
  /** Padding and type size of the input and buttons. */
  size?: UiSize
  /** Blocks every action, e.g. while a save is in flight. */
  disabled?: boolean
}>(), {
  id: undefined,
  size: UiSize.Md,
  disabled: false,
})

/** Events emitted by SecretSettingField. */
const emit = defineEmits<{
  /** Emitted with the new value as the operator types it. */
  'update:modelValue': [value: string]
  /** Emitted when the operator asks to remove the stored value. */
  clear: []
}>()

/** True while the operator is typing a replacement for a stored value. */
const replacing = ref(false)

// A new stored state (saved, cleared or reloaded) closes the replacement input.
watch(() => props.hasValue, () => {
  replacing.value = false
})

/** Opens the empty input that replaces the stored value. */
const startReplace = (): void => {
  replacing.value = true
}

/** Closes the replacement input and drops what was typed, so the stored value is kept. */
const cancelReplace = (): void => {
  replacing.value = false
  emit('update:modelValue', '')
}
</script>

<template>
  <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm" wrap :data-testid="testid">
    <template v-if="hasValue && !replacing">
      <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm" class="font-mono" :data-testid="`${testid}-masked`">
        {{ t('settings.secret.set') }}
      </UiText>
      <UiButton :action="UiActionKind.Edit"
        :type="UiButtonType.Button"
        :variant="UiButtonVariant.Secondary"
        :size="size"
        :disabled="disabled"
        :data-testid="`${testid}-replace`"
        @click="startReplace"
      >
        {{ t('settings.secret.replace') }}
      </UiButton>
      <UiButton :action="UiActionKind.Clear"
        :type="UiButtonType.Button"
        :variant="UiButtonVariant.Ghost"
        :size="size"
        :disabled="disabled"
        :data-testid="`${testid}-clear`"
        @click="emit('clear')"
      >
        {{ t('shared.clear') }}
      </UiButton>
    </template>
    <template v-else>
      <UiInput
        :id="id"
        :model-value="modelValue"
        :type="UiInputType.Password"
        :size="size"
        autocomplete="new-password"
        class="min-w-48 flex-1 font-mono"
        :placeholder="hasValue ? t('settings.secret.placeholder') : t('settings.secret.notSet')"
        :disabled="disabled"
        :data-testid="`${testid}-input`"
        @update:model-value="emit('update:modelValue', String($event ?? ''))"
      />
      <UiButton :action="UiActionKind.Cancel"
        v-if="hasValue"
        :type="UiButtonType.Button"
        :variant="UiButtonVariant.Ghost"
        :size="size"
        :disabled="disabled"
        :data-testid="`${testid}-cancel`"
        @click="cancelReplace"
      >
        {{ t('shared.cancel') }}
      </UiButton>
    </template>
  </UiStack>
</template>
