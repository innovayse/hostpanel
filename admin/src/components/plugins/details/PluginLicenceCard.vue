<script setup lang="ts">
/**
 * The licence key of a paid plugin. The key is write only: the field is never filled from the
 * server, the card only says whether a key is saved. The operator types a key and saves it, or
 * removes the saved one. The field empties when the saved state changes; a key that replaces an
 * already saved one leaves that state as it was, so the screen re-creates the card (a new `key`)
 * after every successful save or removal. The component owns no data: it reports the key and the removal.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import { UiInputType } from '@/utils/ui/input'
import { UiTone } from '@/utils/ui/tone'
import { UiActionKind } from '@/utils/ui/actions'
import { UiCardPadding } from '@/utils/ui/card'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiHeadingVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiSize } from '@/utils/ui/size'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginLicenceCard. */
const props = defineProps<{
  /** Whether this server holds a key for the plugin. */
  hasKey: boolean
  /** True while a save or removal is in flight. */
  busy: boolean
}>()

/** Events emitted by PluginLicenceCard. */
const emit = defineEmits<{
  /** Emitted with the typed key when the operator saves it. */
  save: [key: string]
  /** Emitted when the operator removes the saved key. */
  remove: []
}>()

/** What the field holds; never filled from the server. */
const key = ref('')

/** Label of the badge that says whether a key is saved. */
const keyStateText = computed<string>(() =>
  props.hasKey ? t('pluginDetails.licence.saved') : t('pluginDetails.licence.missing'),
)

/** Whether the save button is offered: a non-blank key is typed and nothing is running. */
const canSave = computed<boolean>(() => key.value.trim() !== '' && !props.busy)

// A saved key leaves the field: once the server holds it, the screen must not.
watch(() => props.hasKey, () => {
  key.value = ''
})

/** Reports the typed key, trimmed. */
const submit = (): void => {
  if (canSave.value) {
    emit('save', key.value.trim())
  }
}
</script>

<template>
  <UiCard :padding="UiCardPadding.Md" data-testid="plugin-licence-card">
    <UiStack :gap="UiGap.Sm">
      <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm" wrap>
        <UiHeading :level="2" :variant="UiHeadingVariant.Section">{{ t('pluginDetails.licence.heading') }}</UiHeading>
        <UiBadge
          :tone="hasKey ? UiTone.Success : UiTone.Warning"
          small
          :data-testid="hasKey ? 'plugin-licence-saved' : 'plugin-licence-missing'"
        >
          {{ keyStateText }}
        </UiBadge>
      </UiStack>
      <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ t('pluginDetails.licence.description') }}</UiText>
      <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm" wrap>
        <UiInput
          v-model="key"
          :type="UiInputType.Password"
          :placeholder="t('pluginDetails.licence.placeholder')"
          :aria-label="t('pluginDetails.licence.label')"
          autocomplete="off"
          class="min-w-56 flex-1"
          data-testid="plugin-licence-input"
          @keyup.enter="submit"
        />
        <UiButton :action="UiActionKind.Save"
          :title="t('pluginDetails.licence.save')"
          :aria-label="t('pluginDetails.licence.save')"
          :type="UiButtonType.Button"
          :variant="UiButtonVariant.Primary"
          :size="UiSize.Sm"
          :disabled="!canSave"
          data-testid="plugin-licence-save"
          @click="submit"
        >
          {{ t('shared.save') }}
        </UiButton>
        <UiButton :action="UiActionKind.Remove"
          v-if="hasKey"
          :title="t('pluginDetails.licence.remove')"
          :aria-label="t('pluginDetails.licence.remove')"
          :type="UiButtonType.Button"
          :variant="UiButtonVariant.Danger"
          :size="UiSize.Sm"
          :disabled="busy"
          data-testid="plugin-licence-remove"
          @click="emit('remove')"
        >
          {{ t('shared.remove') }}
        </UiButton>
      </UiStack>
    </UiStack>
  </UiCard>
</template>
