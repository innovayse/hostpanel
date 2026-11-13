<template>
  <UiStack gap="xs" data-testid="ticket-priority">
    <UiText as="span" size="sm" weight="medium">{{ $t('client.tickets.priorityLabel') }}</UiText>
    <UiStack direction="row" gap="sm">
      <UiButton
        v-for="option in options"
        :key="option.value"
        :variant="modelValue === option.value ? 'primary' : 'subtle'"
        class="flex-1"
        :data-testid="`ticket-priority-${option.value}`"
        @click="emit('update:modelValue', option.value)"
      >
        {{ option.label }}
      </UiButton>
    </UiStack>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * TicketPriorityPicker: the three priority buttons of the new-ticket form; the chosen one is
 * filled, the others quiet.
 */
import { computed } from 'vue'
import { TicketPriority } from '~/types/ticketpriority'

/** Translates a key into the active locale. */
const { t } = useI18n()

/** Props of TicketPriorityPicker. */
interface Props {
  /** The chosen priority (v-model). */
  modelValue: TicketPriority
}

defineProps<Props>()

/** Events the component raises. */
const emit = defineEmits<{
  /** The reader chose another priority. */
  'update:modelValue': [value: TicketPriority]
}>()

/** The buttons: the value sent and its translated label. */
const options = computed(() => [
  { value: TicketPriority.Low, label: t('client.tickets.priorityLow') },
  { value: TicketPriority.Medium, label: t('client.tickets.priorityMedium') },
  { value: TicketPriority.High, label: t('client.tickets.priorityHigh') },
])
</script>
