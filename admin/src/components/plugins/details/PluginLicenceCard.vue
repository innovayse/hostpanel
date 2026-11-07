<script setup lang="ts">
/**
 * The licence key of a paid plugin. The key is write only: the field is never filled from the
 * server, the card only says whether a key is saved. The operator types a key and saves it, or
 * removes the saved one. The field is cleared after a key is saved, so a key never lingers on
 * screen. The component owns no data: it reports the key and the removal.
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
import { UiButtonVariant } from '@/utils/ui/button'
import { UiInputType } from '@/utils/ui/input'
import { UiTone } from '@/utils/ui/tone'

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
  <UiCard padding="md" data-testid="plugin-licence-card">
    <UiStack gap="sm">
      <UiStack direction="row" align="center" gap="sm" wrap>
        <UiHeading :level="2" variant="section">{{ t('pluginDetails.licence.heading') }}</UiHeading>
        <UiBadge
          :tone="hasKey ? UiTone.Success : UiTone.Warning"
          small
          :data-testid="hasKey ? 'plugin-licence-saved' : 'plugin-licence-missing'"
        >
          {{ keyStateText }}
        </UiBadge>
      </UiStack>
      <UiText tone="muted" size="sm">{{ t('pluginDetails.licence.description') }}</UiText>
      <UiStack direction="row" align="center" gap="sm" wrap>
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
        <UiButton
          type="button"
          :variant="UiButtonVariant.Primary"
          size="sm"
          :disabled="!canSave"
          data-testid="plugin-licence-save"
          @click="submit"
        >
          {{ t('pluginDetails.licence.save') }}
        </UiButton>
        <UiButton
          v-if="hasKey"
          type="button"
          :variant="UiButtonVariant.Danger"
          size="sm"
          :disabled="busy"
          data-testid="plugin-licence-remove"
          @click="emit('remove')"
        >
          {{ t('pluginDetails.licence.remove') }}
        </UiButton>
      </UiStack>
    </UiStack>
  </UiCard>
</template>
