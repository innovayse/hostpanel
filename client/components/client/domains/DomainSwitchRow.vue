<template>
  <UiCard padding="sm" class="!bg-gray-50 dark:!bg-white/5" :data-testid="testid">
    <UiStack direction="row" align="center" justify="between">
      <UiStack gap="none">
        <UiText as="span" size="sm" weight="medium">{{ enabled ? onLabel : offLabel }}</UiText>
        <UiText as="span" size="xs" tone="muted">{{ description }}</UiText>
      </UiStack>
      <UiToggle
        :model-value="enabled"
        :label="toggleLabel"
        :loading="saving"
        :testid="testid ? `${testid}-toggle` : undefined"
        @update:model-value="emit('toggle', $event)"
      />
    </UiStack>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * DomainSwitchRow. One setting of a domain as a switch: what it is called while on or off, a
 * line of explanation and the switch. The switch only asks; the parent decides whether the API
 * accepted the change and passes the new state back in.
 */

/** Props of DomainSwitchRow. */
interface Props {
  /** Whether the setting is on. */
  enabled: boolean
  /** Name of the setting while it is on. */
  onLabel: string
  /** Name of the setting while it is off. */
  offLabel: string
  /** One sentence explaining the setting. */
  description: string
  /** Accessible name of the switch. */
  toggleLabel: string
  /** Whether the change is in flight. */
  saving?: boolean
  /** `data-testid` of the row; the switch gets the same id with `-toggle`. */
  testid?: string
}

withDefaults(defineProps<Props>(), {
  saving: false,
  testid: undefined,
})

/** Events the component raises. */
const emit = defineEmits<{
  /** The reader asked for the opposite state. */
  toggle: [value: boolean]
}>()
</script>
