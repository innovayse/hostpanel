<script setup lang="ts">
/**
 * Dialog for creating or editing a product group.
 *
 * Besides the name and description it carries the payment methods the group may be paid with, as a multiple choice
 * over the methods the checkout offers; none ticked allows every method. A method the group already allows but the
 * checkout does not offer right now (a gateway switched off) stays in the list, ticked, so saving never drops it
 * silently. Emits `save` with the payload and `close` on cancel, Escape or a backdrop click; a refused save is
 * toasted by the screen that saves.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiFormActions from '@/components/ui/UiFormActions.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import type { PaymentMethodOption, ProductGroup, ProductGroupPayload } from '@/types/product'
import { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import { UiDialogSize } from '@/utils/ui/dialog'
import { UiGap } from '@/utils/ui/layout'
import { UiLabelVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiActionKind } from '@/utils/ui/actions'

const { t } = useI18n()

/** Props for ProductGroupFormModal. */
const props = defineProps<{
  /** Group to edit, or null when creating a new one. */
  group: ProductGroup | null
  /** The payment methods the checkout offers right now. */
  methods: PaymentMethodOption[]
  /** True while the save request is in flight. */
  saving: boolean
}>()

const emit = defineEmits<{
  /** Emitted when the user submits a valid form, with the group's new data. */
  save: [payload: ProductGroupPayload]
  /** Emitted when the user closes or cancels the dialog. */
  close: []
}>()

/** Whether the dialog is in edit mode (a group was provided). */
const isEditMode = computed<boolean>(() => props.group !== null)

/** Display name input value. */
const name = ref('')

/** Description input value; empty text is saved as no description. */
const description = ref('')

/** Module ids of the ticked payment methods. */
const allowed = ref<string[]>([])

/** Fills the fields from the group prop, or resets them when creating. */
watch(() => props.group, (current) => {
  name.value = current?.name ?? ''
  description.value = current?.description ?? ''
  allowed.value = [...(current?.allowedPaymentMethods ?? [])]
}, { immediate: true })

/** The choices: every method on offer, then each allowed one the checkout no longer offers, marked as such. */
const choices = computed<PaymentMethodOption[]>(() => {
  const offered = new Set(props.methods.map((method) => method.module))
  const lapsed = allowed.value
    .filter((module) => !offered.has(module))
    .map((module) => ({ module, displayName: t('settings.productGroups.unavailableMethod', { name: module }) }))
  return [...props.methods, ...lapsed]
})

/**
 * Ticks or unticks one payment method.
 *
 * @param module - The method's module id.
 * @param ticked - Whether it is now ticked.
 */
const setTicked = (module: string, ticked: boolean): void => {
  allowed.value = ticked
    ? [...allowed.value, module]
    : allowed.value.filter((id) => id !== module)
}

/** Submits the form by emitting the save event with the current field values. */
const handleSubmit = (): void => {
  const text = description.value.trim()
  emit('save', {
    name: name.value.trim(),
    description: text === '' ? null : text,
    allowedPaymentMethods: allowed.value,
  })
}
</script>

<template>
  <UiDialog
    :title="isEditMode ? t('settings.productGroups.editTitle') : t('settings.productGroups.addTitle')"
    :size="UiDialogSize.Md"
    @close="emit('close')"
  >
    <UiForm stacked data-testid="product-group-form" @submit="handleSubmit">
      <UiField :label="t('settings.productGroups.name')">
        <template #default="{ id, invalid, describedby }">
          <UiInput
            :id="id"
            v-model="name"
            :placeholder="t('settings.productGroups.namePlaceholder')"
            required
            :invalid="invalid"
            :aria-describedby="describedby"
            data-testid="product-group-form-name"
          />
        </template>
      </UiField>

      <UiField :label="t('settings.productGroups.description')">
        <template #default="{ id, invalid, describedby }">
          <UiInput
            :id="id"
            v-model="description"
            :placeholder="t('settings.productGroups.descriptionPlaceholder')"
            :invalid="invalid"
            :aria-describedby="describedby"
            data-testid="product-group-form-description"
          />
        </template>
      </UiField>

      <UiStack :gap="UiGap.Xs" data-testid="product-group-form-methods">
        <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ t('settings.productGroups.allowedMethods') }}</UiText>
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ t('settings.productGroups.allowedMethodsHint') }}</UiText>
        <UiLabel v-for="choice in choices" :key="choice.module" :variant="UiLabelVariant.Inline">
          <UiCheckbox
            :model-value="allowed.includes(choice.module)"
            :aria-label="choice.displayName"
            :data-testid="`product-group-form-method-${choice.module}`"
            @update:model-value="setTicked(choice.module, $event)"
          />
          {{ choice.displayName }}
        </UiLabel>
      </UiStack>

      <UiFormActions>
        <UiButton :action="UiActionKind.Cancel" :type="UiButtonType.Button" :variant="UiButtonVariant.Secondary" data-testid="product-group-form-cancel" @click="emit('close')">
          {{ t('shared.cancel') }}
        </UiButton>
        <UiButton :action="UiActionKind.Save" :type="UiButtonType.Submit" :variant="UiButtonVariant.Primary" :loading="saving" data-testid="product-group-form-save">
          {{ isEditMode ? t('shared.save') : t('settings.productGroups.create') }}
        </UiButton>
      </UiFormActions>
    </UiForm>
  </UiDialog>
</template>
