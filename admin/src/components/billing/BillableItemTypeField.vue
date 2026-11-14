<script setup lang="ts">
/**
 * The type field of a billable item list's filter dialog: a `UiSelect` whose first choice is "any". Shared by the
 * admin-wide billable item list and the billable items tab of a client's profile, so both offer the same filter in the
 * same words. The value is the draft the dialog edits (a billable item type, or an empty string meaning "any"); it
 * reaches the address only when the reader applies the dialog.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiField from '@/components/ui/UiField.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { BILLABLE_ITEM_TYPE_OPTIONS } from '@/lib/constants'

const { t } = useI18n()

/** Props for BillableItemTypeField. */
defineProps<{
  /** Test id of the select. */
  testid: string
}>()

/** The type being chosen, a billable item type or an empty string for any. */
const type = defineModel<string>({ required: true })

/** Type choices; the empty value means any. */
const typeOptions = computed(() => [{ value: '', label: t('shared.any') }, ...BILLABLE_ITEM_TYPE_OPTIONS])
</script>

<template>
  <UiField :label="t('shared.type')">
    <template #default="{ id }">
      <UiSelect
        :id="id"
        v-model="type"
        :options="typeOptions"
        :placeholder="t('shared.any')"
        :data-testid="testid"
      />
    </template>
  </UiField>
</template>
