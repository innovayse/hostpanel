<script setup lang="ts">
/**
 * Dialog that creates or edits one client group (name, description, discount, colour). It owns the
 * form fields; the screen owns saving: the dialog only reports the finished payload and shows the
 * API's refusal sentence and per-field messages it is given.
 */
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiColorField from '@/components/ui/UiColorField.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import type { ClientGroup, ClientGroupPayload } from '@/types/clientgroup'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTone } from '@/utils/ui/tone'

/** Colour a new group starts with. */
const DEFAULT_COLOR = '#3B82F6'

/** Largest discount a group can carry, in percent. */
const MAX_DISCOUNT = 100

/** Props for ClientGroupFormDialog. */
const props = defineProps<{
  /** Whether the dialog is shown. */
  open: boolean
  /** The group being edited; null creates a new one. */
  group: ClientGroup | null
  /** Whether a save is in flight. */
  saving: boolean
  /** The API's sentence for a refused save, null when none. */
  error: string | null
  /** The API's per-field messages for a refused save, keyed by camelCase field name. */
  fieldErrors: Record<string, string>
}>()

const emit = defineEmits<{
  /** The form was confirmed; carries the payload to save. */
  save: [payload: ClientGroupPayload]
  /** The dialog was dismissed. */
  close: []
}>()

const { t } = useI18n()

/** Form field: group name. */
const name = ref('')

/** Form field: description. */
const description = ref('')

/** Form field: discount in percent. */
const discountPercent = ref(0)

/** Form field: colour text. */
const color = ref(DEFAULT_COLOR)

/**
 * Reports the finished payload.
 *
 * @returns Nothing.
 */
const submit = (): void => {
  emit('save', {
    name: name.value.trim(),
    description: description.value.trim() || null,
    discountPercent: discountPercent.value,
    color: color.value.trim(),
  })
}

// The form starts from the group the screen opens, or empty for a new one.
watch(
  () => [props.open, props.group] as const,
  ([open, group]) => {
    if (open) {
      name.value = group?.name ?? ''
      description.value = group?.description ?? ''
      discountPercent.value = group?.discountPercent ?? 0
      color.value = group?.color ?? DEFAULT_COLOR
    }
  },
  { immediate: true },
)
</script>

<template>
  <UiDialog
    :open="open"
    :title="group ? t('clients.clientGroups.editTitle') : t('clients.clientGroups.createTitle')"
    data-testid="client-group-form"
    @close="emit('close')"
  >
    <UiStack>
      <UiAlert v-if="error" :variant="UiTone.Danger" data-testid="client-group-form-error">{{ error }}</UiAlert>
      <UiField :label="t('shared.name')" :error="fieldErrors.name" required>
        <template #default="{ id, invalid }">
          <UiInput
            :id="id"
            v-model="name"
            :invalid="invalid"
            :placeholder="t('clients.clientGroups.namePlaceholder')"
            data-testid="client-group-form-name"
          />
        </template>
      </UiField>
      <UiField :label="t('shared.description')" :error="fieldErrors.description">
        <template #default="{ invalid }">
          <UiTextarea
            v-model="description"
            :invalid="invalid"
            :placeholder="t('clients.clientGroups.descriptionPlaceholder')"
            data-testid="client-group-form-description"
          />
        </template>
      </UiField>
      <UiField :label="t('clients.clientGroups.discount')" :error="fieldErrors.discountPercent">
        <template #default="{ id, invalid }">
          <UiNumberInput
            :id="id"
            v-model="discountPercent"
            :invalid="invalid"
            :min="0"
            :max="MAX_DISCOUNT"
            :placeholder="t('clients.clientGroups.discountPlaceholder')"
            data-testid="client-group-form-discount"
          />
        </template>
      </UiField>
      <UiField :label="t('clients.clientGroups.color')" :error="fieldErrors.color">
        <UiColorField
          v-model="color"
          :placeholder="t('clients.clientGroups.colorPlaceholder')"
          data-testid="client-group-form-color"
        />
      </UiField>
    </UiStack>
    <template #footer>
      <UiButton :variant="UiButtonVariant.Secondary" data-testid="client-group-form-cancel" @click="emit('close')">
        {{ t('shared.cancel') }}
      </UiButton>
      <UiButton
        :variant="UiButtonVariant.Primary"
        :loading="saving"
        :disabled="!name.trim()"
        data-testid="client-group-form-save"
        @click="submit"
      >
        {{ t('shared.save') }}
      </UiButton>
    </template>
  </UiDialog>
</template>
