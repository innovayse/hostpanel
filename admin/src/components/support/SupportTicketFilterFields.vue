<script setup lang="ts">
/**
 * The ticket filter fields of a list's filter dialog: status, priority and department, each a `UiSelect` whose first
 * choice is "any". Shared by the staff ticket list and the tickets tab of a client's profile, so both offer the same
 * filters in the same words. The values are the draft the dialog edits (text, an empty string meaning "any"); they
 * reach the address only when the reader applies the dialog.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiField from '@/components/ui/UiField.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { TICKET_PRIORITY_OPTIONS, TICKET_STATUS_OPTIONS } from '@/lib/constants'
import type { Department } from '@/types/department'

const { t } = useI18n()

/** Props for SupportTicketFilterFields. */
const props = defineProps<{
  /** The support departments the department field offers. */
  departments: readonly Department[]
  /** Prefix of the test ids; the fields are `<prefix>-status`, `<prefix>-priority` and `<prefix>-department`. */
  testidPrefix: string
}>()

/** The status being chosen, a ticket status or an empty string for any. */
const status = defineModel<string>('status', { required: true })

/** The priority being chosen, a ticket priority or an empty string for any. */
const priority = defineModel<string>('priority', { required: true })

/** The department being chosen, a department id as text or an empty string for any. */
const departmentId = defineModel<string>('departmentId', { required: true })

/** Status choices; the empty value means any. */
const statusOptions = computed(() => [{ value: '', label: t('shared.any') }, ...TICKET_STATUS_OPTIONS])

/** Priority choices; the empty value means any. */
const priorityOptions = computed(() => [{ value: '', label: t('shared.any') }, ...TICKET_PRIORITY_OPTIONS])

/** Department choices, one per support department; the empty value means any. */
const departmentOptions = computed(() => [
  { value: '', label: t('shared.any') },
  ...props.departments.map(department => ({ value: String(department.id), label: department.name })),
])
</script>

<template>
  <UiStack>
    <UiField :label="t('shared.status')">
      <template #default="{ id }">
        <UiSelect
          :id="id"
          v-model="status"
          :options="statusOptions"
          :placeholder="t('shared.any')"
          :data-testid="`${testidPrefix}-status`"
        />
      </template>
    </UiField>
    <UiField :label="t('shared.priority')">
      <template #default="{ id }">
        <UiSelect
          :id="id"
          v-model="priority"
          :options="priorityOptions"
          :placeholder="t('shared.any')"
          :data-testid="`${testidPrefix}-priority`"
        />
      </template>
    </UiField>
    <UiField :label="t('shared.department')">
      <template #default="{ id }">
        <UiSelect
          :id="id"
          v-model="departmentId"
          :options="departmentOptions"
          :placeholder="t('shared.any')"
          :data-testid="`${testidPrefix}-department`"
        />
      </template>
    </UiField>
  </UiStack>
</template>
