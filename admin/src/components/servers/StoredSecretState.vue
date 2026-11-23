<script setup lang="ts">
/**
 * Stored-state line under a server credential input on the edit form.
 *
 * The API never sends a server credential back, only whether one is stored, so the input stays
 * empty and an empty input keeps what is stored. This line says that a value is stored and offers
 * Clear, which asks the parent to remove it on save; once cleared it says so and offers Cancel.
 */
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

/** Translation function. */
const { t } = useI18n()

/** Props for StoredSecretState. */
const props = defineProps<{
  /** Whether a value is stored on the server. */
  hasValue: boolean
  /** Whether the operator asked to remove the stored value on save. */
  cleared: boolean
  /** Test id prefix; the parts add `-stored`, `-clear`, `-pending-clear` and `-cancel-clear`. */
  testid: string
  /** Blocks both actions, e.g. while a save is in flight. */
  disabled?: boolean
}>()

/** Events emitted by StoredSecretState. */
const emit = defineEmits<{
  /** Emitted when the operator asks to remove the stored value. */
  clear: []
  /** Emitted when the operator takes the removal back. */
  cancel: []
}>()
</script>

<template>
  <UiStack v-if="props.hasValue" :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm" wrap>
    <template v-if="props.cleared">
      <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm" :data-testid="`${props.testid}-pending-clear`">
        {{ t('settings.secret.pendingClear') }}
      </UiText>
      <UiButton :action="UiActionKind.Cancel"
        :type="UiButtonType.Button"
        :variant="UiButtonVariant.Ghost"
        :size="UiSize.Sm"
        :disabled="props.disabled"
        :data-testid="`${props.testid}-cancel-clear`"
        @click="emit('cancel')"
      >
        {{ t('shared.cancel') }}
      </UiButton>
    </template>
    <template v-else>
      <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm" class="font-mono" :data-testid="`${props.testid}-stored`">
        {{ t('settings.secret.set') }}
      </UiText>
      <UiButton :action="UiActionKind.Clear"
        :type="UiButtonType.Button"
        :variant="UiButtonVariant.Ghost"
        :size="UiSize.Sm"
        :disabled="props.disabled"
        :data-testid="`${props.testid}-clear`"
        @click="emit('clear')"
      >
        {{ t('shared.clear') }}
      </UiButton>
    </template>
  </UiStack>
</template>
