<script setup lang="ts">
/**
 * The form that adds an entry to one block list: the value to block, an optional note and the Add
 * button. The value is a text field for an address, a range and an email domain, and the kit's
 * country listbox for a country. The screen owns the two fields (`v-model:value`, `v-model:note`)
 * and the request; this component only reports the Add.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiCountrySelect from '@/components/ui/UiCountrySelect.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiInput from '@/components/ui/UiInput.vue'
import { FRAUD_NOTE_MAX_LENGTH } from '@/lib/fraud'
import { Icon } from '@/lib/icons'
import { BlockListType } from '@/types/fraud'
import { UiButtonVariant } from '@/utils/ui/button'

const { t } = useI18n()

/** Props for BlockListAddForm. */
const props = defineProps<{
  /** The block list the entry goes to. */
  type: BlockListType
  /** True while the add request is in flight; the button is blocked. */
  adding: boolean
  /** Messages the API refused the last add with, by field name (`value`, `note`). */
  errors: Record<string, string>
}>()

const emit = defineEmits<{
  /** Emitted when the operator submits a non-empty value. */
  add: []
}>()

/** The value to block: an address, a range, a domain or a country code. */
const value = defineModel<string>('value', { required: true })

/** The optional reason, up to the length the API accepts. */
const note = defineModel<string>('note', { required: true })

/** Whether the value is a country, which is chosen from the listbox instead of typed. */
const isCountry = computed<boolean>(() => props.type === BlockListType.Country)

/** Caption of the value field, named for what the list holds. */
const valueLabel = computed<string>(() => t(`fraud.blockList.valueLabel.${props.type}`))

/** Hint of the value field: an example of what the list holds. */
const valuePlaceholder = computed<string>(() => t(`fraud.blockList.valuePlaceholder.${props.type}`))

/** Whether the Add button may be pressed: there is a value to block. */
const canAdd = computed<boolean>(() => value.value.trim() !== '')

/** Reports the Add when there is a value. */
const submit = (): void => {
  if (canAdd.value) {
    emit('add')
  }
}
</script>

<template>
  <UiForm stacked data-testid="fraud-block-list-form" @submit="submit">
    <UiGrid :columns="2">
      <UiField :label="valueLabel" :error="errors.value" required error-testid="fraud-block-list-value-error">
        <template #default="{ id, invalid }">
          <UiCountrySelect
            v-if="isCountry"
            v-model="value"
            :placeholder="valuePlaceholder"
            data-testid="fraud-block-list-value"
          />
          <UiInput
            v-else
            :id="id"
            v-model="value"
            type="text"
            :invalid="invalid"
            :placeholder="valuePlaceholder"
            data-testid="fraud-block-list-value"
          />
        </template>
      </UiField>
      <UiField :label="t('fraud.blockList.note')" :error="errors.note" error-testid="fraud-block-list-note-error">
        <template #default="{ id, invalid }">
          <UiInput
            :id="id"
            v-model="note"
            type="text"
            :invalid="invalid"
            :maxlength="FRAUD_NOTE_MAX_LENGTH"
            :placeholder="t('fraud.blockList.notePlaceholder')"
            data-testid="fraud-block-list-note"
          />
        </template>
      </UiField>
    </UiGrid>
    <UiButton
      type="submit"
      :variant="UiButtonVariant.Primary"
      :loading="adding"
      :disabled="!canAdd"
      data-testid="fraud-block-list-add"
    >
      <UiIcon :markup="Icon.Add" size="sm" />
      {{ t('fraud.blockList.add') }}
    </UiButton>
  </UiForm>
</template>
