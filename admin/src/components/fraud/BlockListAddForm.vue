<script setup lang="ts">
/**
 * The form that adds an entry to one block list: the value to block, an optional note and the Add
 * button. The value is a text field for an address, a range and an email domain, and the kit's
 * country listbox for a country. The screen owns the two fields (`v-model:value`, `v-model:note`)
 * and the request; this component only reports the Add.
 *
 * The card, its two fields and their captions are the add block of `FraudBlockListsView.schema` (the value caption is named
 * for the list on show), which `UiSkeletonOf` also draws while the screen downloads.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiCountrySelect from '@/components/ui/UiCountrySelect.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiSchemaForm from '@/components/ui/UiSchemaForm.vue'
import { FRAUD_NOTE_MAX_LENGTH } from '@/lib/fraud'
import { Icon } from '@/lib/icons'
import { BlockListType } from '@/types/fraud'
import { UiButtonVariant } from '@/utils/ui/button'
import { blockListAddPart } from '@/views/FraudBlockListsView.schema'

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

/** The columns of the add card, its value caption named for what the list holds. */
const columns = computed(() => blockListAddPart(props.type).columns)

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
  <UiSchemaForm :columns="columns" :errors="errors" data-testid="fraud-block-list-form" @submit="submit">
    <template #field-value="{ id, invalid }">
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
    <template #field-note="{ id, invalid }">
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
    <template #section-add>
      <UiButton :icon="Icon.Add"
        type="submit"
        :variant="UiButtonVariant.Primary"
        :loading="adding"
        :disabled="!canAdd"
        data-testid="fraud-block-list-add"
      >{{ t('fraud.blockList.add') }}
      </UiButton>
    </template>
  </UiSchemaForm>
</template>
