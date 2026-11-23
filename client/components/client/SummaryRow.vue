<template>
  <UiStack direction="row" align="center" justify="between" gap="sm" :data-testid="testid">
    <UiText :as="UiTextTag.Span" size="sm" tone="muted">{{ label }}</UiText>
    <slot>
      <UiText :as="UiTextTag.Span" size="sm" weight="bold" class="min-w-0 break-words text-right">{{ value }}</UiText>
    </slot>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * One label/value line of a summary card: the label muted on the left, the value bold on the
 * right. The default slot replaces the value when it needs more than text (a badge).
 */
import { UiTextTag } from '~/utils/ui/enums'

/** Props of ClientSummaryRow. */
interface Props {
  /** What the line is about. */
  label: string
  /** The value as text; ignored when the default slot is used. */
  value?: string
  /** `data-testid` of the row. */
  testid?: string
}

withDefaults(defineProps<Props>(), {
  value: undefined,
  testid: undefined
})
</script>
