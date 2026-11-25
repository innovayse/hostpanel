<script setup lang="ts">
/**
 * Write-only control for a stored secret: a gateway key, a registrar or mail password, a plugin or server credential.
 *
 * The API never sends a secret back, only whether one is stored, so there is nothing to pre-fill. Every secret in
 * the admin is edited through this one control, so it looks the same everywhere. A stored secret is a read-only box
 * of the same height and look as `UiInput`, holding a masked value and the "Set" badge, with small Replace and Clear
 * buttons to its right on the same row. Replace swaps the box for an empty password input (or a text area with
 * `multiline`) whose text becomes the new value, with Cancel to keep the stored one. Clear asks the parent to remove
 * the stored value on save; while the parent reports `cleared` the box says so and offers Cancel to take it back.
 * Nothing stored is an empty password input from the start. An empty input means "keep what is stored", which is
 * also what the API does with it. The hint of the field stays under it, in the surrounding `UiField`.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import { fieldBaseClasses, fieldWidthClasses, UiInputType } from '@/utils/ui/input'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
import { fieldSizeClasses, UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** Props for UiSecretField. */
const props = withDefaults(defineProps<{
  /** Whether a value is stored on the server. */
  hasValue: boolean
  /** The new value typed so far; empty while the stored value is kept. */
  modelValue: string
  /** Test id prefix of the control; the parts add `-masked`, `-input`, `-replace`, `-clear`, `-cancel`, `-pending-clear` and `-cancel-clear`. */
  testid: string
  /** Whether the operator asked to remove the stored value on save; the parent owns this state. */
  cleared?: boolean
  /** Draws the replacement as a text area, for a multi-line secret such as a key block. */
  multiline?: boolean
  /** Id of the input, so a surrounding field label points at it. */
  id?: string
  /** Hint of the empty input; "Not set" when nothing is stored, "Type the new value" when one is. */
  placeholder?: string
  /** Marks the value as refused by the server. */
  invalid?: boolean
  /** Id of the element that describes the field (its hint or error), for `aria-describedby`. */
  describedby?: string
  /** Padding and type size of the box and the input; the buttons are always small. */
  size?: UiSize
  /** Blocks every action, e.g. while a save is in flight. */
  disabled?: boolean
}>(), {
  cleared: false,
  multiline: false,
  id: undefined,
  placeholder: undefined,
  invalid: false,
  describedby: undefined,
  size: UiSize.Md,
  disabled: false,
})

/** Events emitted by UiSecretField. */
const emit = defineEmits<{
  /** Emitted with the new value as the operator types it. */
  'update:modelValue': [value: string]
  /** Emitted when the operator asks to remove the stored value. */
  clear: []
  /** Emitted when the operator takes the removal back. */
  cancelClear: []
}>()

/** True while the operator is typing a replacement for a stored value. */
const replacing = ref(false)

/** The dots that stand for the stored value; the real length is never known to the screen. */
const mask = '••••••••'

/** Classes of the read-only box: the same surface, radius and height as a text input. */
const boxClasses = `${fieldWidthClasses} ${fieldBaseClasses} ${fieldSizeClasses[props.size]} flex min-w-0 flex-1 items-center gap-2`

/** Hint of the empty input: the screen's own, else "Not set" or "Type the new value" by whether a value is stored. */
const inputPlaceholder = computed<string>(() =>
  props.placeholder ?? (props.hasValue ? t('settings.secret.placeholder') : t('settings.secret.notSet')),
)

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

/**
 * Forwards what was typed.
 *
 * @param value - The text of the input.
 */
const onInput = (value: string | number | null): void => {
  emit('update:modelValue', String(value ?? ''))
}
</script>

<template>
  <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm" :data-testid="testid">
    <template v-if="hasValue && cleared">
      <div :class="boxClasses" :data-testid="`${testid}-pending-clear`">
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ t('settings.secret.pendingClear') }}</UiText>
      </div>
      <UiButton
        :action="UiActionKind.Cancel"
        :type="UiButtonType.Button"
        :variant="UiButtonVariant.Secondary"
        :size="UiSize.Sm"
        :disabled="disabled"
        :data-testid="`${testid}-cancel-clear`"
        @click="emit('cancelClear')"
      >
        {{ t('shared.cancel') }}
      </UiButton>
    </template>
    <template v-else-if="hasValue && !replacing">
      <div :class="boxClasses" :data-testid="`${testid}-masked`">
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm" class="font-mono tracking-widest" aria-hidden="true">{{ mask }}</UiText>
        <UiBadge :tone="UiTone.Success" small>{{ t('settings.secret.set') }}</UiBadge>
      </div>
      <UiButton
        :action="UiActionKind.Edit"
        :type="UiButtonType.Button"
        :variant="UiButtonVariant.Secondary"
        :size="UiSize.Sm"
        :disabled="disabled"
        :data-testid="`${testid}-replace`"
        @click="startReplace"
      >
        {{ t('settings.secret.replace') }}
      </UiButton>
      <UiButton
        :action="UiActionKind.Clear"
        :type="UiButtonType.Button"
        :variant="UiButtonVariant.Danger"
        :size="UiSize.Sm"
        :disabled="disabled"
        :data-testid="`${testid}-clear`"
        @click="emit('clear')"
      >
        {{ t('shared.clear') }}
      </UiButton>
    </template>
    <template v-else>
      <UiTextarea
        v-if="multiline"
        :id="id"
        :model-value="modelValue"
        rows="4"
        :invalid="invalid"
        :aria-describedby="describedby"
        :placeholder="inputPlaceholder"
        :disabled="disabled"
        class="min-w-0 flex-1 font-mono"
        :data-testid="`${testid}-input`"
        @update:model-value="onInput"
      />
      <UiInput
        v-else
        :id="id"
        :model-value="modelValue"
        :type="UiInputType.Password"
        :size="size"
        :invalid="invalid"
        :aria-describedby="describedby"
        autocomplete="new-password"
        class="min-w-0 flex-1 font-mono"
        :placeholder="inputPlaceholder"
        :disabled="disabled"
        :data-testid="`${testid}-input`"
        @update:model-value="onInput"
      />
      <UiButton
        v-if="hasValue"
        :action="UiActionKind.Cancel"
        :type="UiButtonType.Button"
        :variant="UiButtonVariant.Secondary"
        :size="UiSize.Sm"
        :disabled="disabled"
        :data-testid="`${testid}-cancel`"
        @click="cancelReplace"
      >
        {{ t('shared.cancel') }}
      </UiButton>
    </template>
  </UiStack>
</template>
