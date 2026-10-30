<script setup lang="ts">
/**
 * Card on the client summary to see, set or clear the link between the client and an account in
 * another system. The operator types the system name and the account id; Save sends both,
 * Clear (after a confirmation, shown only while a link exists) removes it. The API is the only validator: its sentence
 * for a refused call (a conflict with another client included) shows under the buttons and its
 * per-field messages (the `fieldErrors` prop) show under their inputs, and go away, through
 * `clearFieldError`, as soon as the operator changes that field.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiText from '@/components/ui/UiText.vue'
import { ExternalAccountField } from '@/types/client'
import type { ClientExternalAccount, SetClientExternalAccountPayload } from '@/types/clientexternalaccount'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

/** Props for ClientExternalAccountCard. */
const props = withDefaults(defineProps<{
  /** The link the client has now, null when there is none; Clear link shows only while there is one. */
  linked?: ClientExternalAccount | null
  /** Whether a save or clear is in flight. */
  saving?: boolean
  /** The API's sentence for the last refused save or clear, null when none. */
  error?: string | null
  /** The API's per-field messages of the last refused save, keyed by camelCase field name. */
  fieldErrors?: Record<string, string>
  /** Outcome line of the last successful save or clear, null when none. */
  notice?: string | null
}>(), {
  linked: null,
  saving: false,
  error: null,
  /**
   * Default for the field errors.
   *
   * @returns A new empty map, so instances never share one.
   */
  fieldErrors: () => ({}),
  notice: null,
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

/** Tone of the status line: danger for a refusal, success for the outcome of a write. */
const statusTone = computed<UiTone>(() => (props.error ? UiTone.Danger : UiTone.Success))

/** The sentence under the buttons: the API's refusal wins over the last outcome. */
const statusMessage = computed<string | null>(() => props.error ?? props.notice)

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

/**
 * Drops the API's message for a field as soon as the operator changes that field.
 *
 * @param field - The wire name of the field.
 * @returns Nothing; emits `clearFieldError` only when that field has a message.
 */
const clearErrorOf = (field: ExternalAccountField): void => {
  if (props.fieldErrors[field] !== undefined) {
    emit('clearFieldError', field)
  }
}

/** Drops the API's provider message once the provider is edited. */
watch(provider, () => {
  clearErrorOf(ExternalAccountField.Provider)
})

/** Drops the API's account id message once the account id is edited. */
watch(accountId, () => {
  clearErrorOf(ExternalAccountField.AccountId)
})
</script>

<template>
  <UiCard padding="md" data-testid="client-external-account">
    <UiStack>
      <UiHeading :level="2" variant="card">{{ t('clients.externalAccount.title') }}</UiHeading>
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
      <UiStack direction="row">
        <UiButton
          type="button"
          :variant="UiButtonVariant.Primary"
          :disabled="saving || bothBlank"
          data-testid="client-external-account-save"
          @click="handleSave"
        >
          {{ saving ? t('shared.saving') : t('shared.save') }}
        </UiButton>
        <UiButton
          v-if="linked"
          type="button"
          :variant="UiButtonVariant.Secondary"
          :disabled="saving"
          data-testid="client-external-account-clear"
          @click="confirmingClear = true"
        >
          {{ t('clients.externalAccount.clear') }}
        </UiButton>
      </UiStack>
      <UiStatusMessage :message="statusMessage" :tone="statusTone" data-testid="client-external-account-status" />
    </UiStack>
    <UiDialog
      v-if="confirmingClear"
      variant="confirm"
      :title="t('clients.externalAccount.clearConfirmTitle')"
      :message="t('clients.externalAccount.clearConfirmMessage')"
      :confirm-label="t('clients.externalAccount.clear')"
      :tone="UiTone.Danger"
      data-testid="client-external-account-confirm"
      @confirm="handleConfirmClear"
      @close="confirmingClear = false"
    />
  </UiCard>
</template>
