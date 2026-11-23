<script setup lang="ts">
/**
 * Body of the external account card on the client summary (the card and its title are the schema's, drawn by
 * `UiDetailCards`): see, set or clear the link between the client and an account in
 * another system. The operator types the system name and the account id; Save sends both,
 * Clear (after a confirmation, shown only while a link exists) removes it. The API is the only validator: its sentence
 * for a refused call (a conflict with another client included) is toasted by the screen and its
 * per-field messages (the `fieldErrors` prop) show under their inputs, and go away, through
 * `clearFieldError`, as soon as the operator changes that field.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useFieldErrorClear } from '@/composables/useFieldErrorClear'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { ExternalAccountField } from '@/types/client'
import type { ClientExternalAccount, SetClientExternalAccountPayload } from '@/types/clientexternalaccount'
import { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiDirection } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { UiActionKind } from '@/utils/ui/actions'

const { t } = useI18n()

/** Props for ClientExternalAccountCard. */
const props = withDefaults(defineProps<{
  /** The link the client has now, null when there is none; Clear link shows only while there is one. */
  linked?: ClientExternalAccount | null
  /** Whether a save or clear is in flight. */
  saving?: boolean
  /** The API's per-field messages of the last refused save, keyed by camelCase field name. */
  fieldErrors?: Record<string, string>
}>(), {
  linked: null,
  saving: false,
  /**
   * Default for the field errors.
   *
   * @returns A new empty map, so instances never share one.
   */
  fieldErrors: () => ({}),
})

const emit = defineEmits<{
  /** Emitted with the typed values (blank becomes null) when the operator saves. */
  save: [payload: SetClientExternalAccountPayload]
  /** Emitted when the operator confirms removing the link. */
  clear: []
  /** Emitted when the operator changes a field the API refused, so its message can be dropped. */
  clearFieldError: [field: string]
}>()

/** Name of the other system, edited by the operator. */
const provider = defineModel<string>('provider', { default: '' })

/** Identifier the other system gives the client, edited by the operator. */
const accountId = defineModel<string>('accountId', { default: '' })

/** Whether the confirmation before clearing is open. */
const confirmingClear = ref(false)

/** Whether both inputs are blank; clearing has its own confirmed button, so Save needs a value. */
const bothBlank = computed<boolean>(() => provider.value.trim() === '' && accountId.value.trim() === '')

/**
 * Turns an input text into the value the API takes.
 *
 * @param text - What the operator typed.
 * @returns The trimmed text, or null when it is blank.
 */
const toValue = (text: string): string | null => {
  const trimmed = text.trim()
  return trimmed === '' ? null : trimmed
}

/**
 * Emits the save with the typed values.
 *
 * @returns Nothing.
 */
const handleSave = (): void => {
  emit('save', { provider: toValue(provider.value), accountId: toValue(accountId.value) })
}

/**
 * Closes the confirmation and asks the view to remove the link.
 *
 * @returns Nothing.
 */
const handleConfirmClear = (): void => {
  confirmingClear.value = false
  emit('clear')
}

/** Drops the API's message of each field as soon as the operator edits that field. */
useFieldErrorClear(
  () => props.fieldErrors,
  (field) => emit('clearFieldError', field),
  [
    [ExternalAccountField.Provider, provider],
    [ExternalAccountField.AccountId, accountId],
  ],
)
</script>

<template>
  <UiStack data-testid="client-external-account">
    <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm" data-testid="client-external-account-current">
      {{ linked ? t('clients.externalAccount.linkedTo', { provider: linked.provider, id: linked.id }) : t('clients.externalAccount.notLinked') }}
    </UiText>
    <UiField
      :label="t('clients.externalAccount.provider')"
      :hint="t('clients.externalAccount.providerHint')"
      :error="fieldErrors[ExternalAccountField.Provider]"
      error-testid="client-external-account-provider-error"
    >
      <template #default="{ id, invalid, describedby }">
        <UiInput
          :id="id"
          v-model="provider"
          :invalid="invalid"
          :aria-describedby="describedby"
          :placeholder="t('clients.externalAccount.providerPlaceholder')"
          data-testid="client-external-account-provider"
        />
      </template>
    </UiField>
    <UiField
      :label="t('clients.externalAccount.accountId')"
      :hint="t('clients.externalAccount.accountIdHint')"
      :error="fieldErrors[ExternalAccountField.AccountId]"
      error-testid="client-external-account-id-error"
    >
      <template #default="{ id, invalid, describedby }">
        <UiInput
          :id="id"
          v-model="accountId"
          :invalid="invalid"
          :aria-describedby="describedby"
          :placeholder="t('clients.externalAccount.accountIdPlaceholder')"
          data-testid="client-external-account-id"
        />
      </template>
    </UiField>
    <UiStack :direction="UiDirection.Row">
      <UiButton :action="UiActionKind.Save"
        :type="UiButtonType.Button"
        :variant="UiButtonVariant.Primary"
        :loading="saving"
        :disabled="bothBlank"
        data-testid="client-external-account-save"
        @click="handleSave"
      >
        {{ t('shared.save') }}
      </UiButton>
      <UiButton :action="UiActionKind.Remove"
        v-if="linked"
        :type="UiButtonType.Button"
        :variant="UiButtonVariant.Secondary"
        :disabled="saving"
        data-testid="client-external-account-clear"
        @click="confirmingClear = true"
      >
        {{ t('clients.externalAccount.remove') }}
      </UiButton>
    </UiStack>
    <UiDialog
      v-if="confirmingClear"
      :variant="UiDialogVariant.Confirm"
      :title="t('clients.externalAccount.clearConfirmTitle')"
      :message="t('clients.externalAccount.clearConfirmMessage')"
      :confirm-label="t('shared.remove')"
      :tone="UiTone.Danger"
      data-testid="client-external-account-confirm"
      @confirm="handleConfirmClear"
      @close="confirmingClear = false"
    />
  </UiStack>
</template>
