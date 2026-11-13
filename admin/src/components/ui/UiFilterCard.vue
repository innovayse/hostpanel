<script setup lang="ts">
/**
 * The one frame of a filter or action bar: a card with a row of fields and the primary action.
 *
 * Two shapes, chosen by the slots the screen fills:
 * - **Row** (`fields` slot): the controls and the `actions` cluster sit on one wrapping row, the
 *   actions aligned to the bottom edge of the controls (not to their captions). When the row runs
 *   out of width the cluster drops under the fields and, on phones, takes the full width.
 * - **Block** (default slot, a grid of fields): the `actions` cluster is the footer of the card,
 *   right-aligned on wide screens and full width on phones.
 *
 * The `dates` slot is an extra block under the fields. Every gap comes from the shared gap tokens.
 */
import UiCard from '@/components/ui/UiCard.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { UiDirection } from '@/utils/ui/layout'
</script>

<template>
  <UiCard class="mb-6 p-4">
    <UiStack>
      <UiStack
        v-if="$slots.fields"
        :direction="UiDirection.Row"
        align="end"
        wrap
        data-testid="ui-filter-card-row"
      >
        <slot name="fields" />
        <UiStack
          v-if="$slots.actions"
          :direction="UiDirection.Row"
          gap="sm"
          align="center"
          class="max-sm:w-full max-sm:*:flex-1"
          data-testid="ui-filter-card-actions"
        >
          <slot name="actions" />
        </UiStack>
      </UiStack>
      <slot />
      <slot name="dates" />
      <UiStack
        v-if="$slots.actions && !$slots.fields"
        :direction="UiDirection.Row"
        gap="sm"
        align="center"
        justify="end"
        wrap
        class="max-sm:*:flex-1"
        data-testid="ui-filter-card-actions"
      >
        <slot name="actions" />
      </UiStack>
    </UiStack>
  </UiCard>
</template>
