<script setup lang="ts">
/**
 * A form drawn from a screen's schema: columns of section cards, each a grid of fields.
 *
 * The screen hands the `columns` of its `SkeletonSchema` and binds each control in the slot `field-<key>` (the scope is
 * the `{ id, invalid, describedby }` of `UiField`); extra real content under a section's fields goes in `section-<key>`.
 * The default slot sits last inside the form, for its actions. The same component with `loading` is the skeleton of the
 * form, so the form and its skeleton cannot differ. One column is a single stack, never a two column grid; the
 * schema lists up to three.
 */
import { useSlots } from 'vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiFormSection from '@/components/ui/UiFormSection.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { gridColumnsOf } from '@/utils/ui/skeletonLayout'
import type { UiMaxWidth } from '@/utils/ui/layout'
import type { SkeletonSection } from '@/utils/ui/skeletonSchema'

/** Props for UiSchemaForm. */
defineProps<{
  /** Columns of sections side by side; each inner list is one column's stack of cards. */
  columns: readonly (readonly SkeletonSection[])[]
  /** Draws skeletons in place of the controls. */
  loading?: boolean
  /** Server sentences by field key. */
  errors?: Readonly<Record<string, string>>
  /** Help lines (already translated) by field key. */
  hints?: Readonly<Record<string, string>>
  /** Widest the form grows on a wide screen. */
  maxWidth?: UiMaxWidth
}>()

/** Emits for UiSchemaForm. */
const emit = defineEmits<{
  /** Emitted with the (already cancelled) submit event. */
  submit: [event: Event]
}>()

/** The slots the screen filled. */
const slots = useSlots()

/**
 * The names of the slots that belong to a field or a section, which are handed on to the section that owns them.
 *
 * Read on every render, because a screen may fill a slot conditionally.
 *
 * @returns The `field-<key>` and `section-<key>` slot names the screen filled.
 */
const forwardedSlots = (): string[] => Object.keys(slots).filter((name) => name.startsWith('field-') || name.startsWith('section-'))
</script>

<template>
  <UiForm stacked :max-width="maxWidth" @submit="emit('submit', $event)">
    <UiGrid :columns="gridColumnsOf(columns.length)">
      <UiStack v-for="(column, index) in columns" :key="index">
        <UiFormSection v-for="section in column" :key="section.key" :section="section" :loading="loading" :errors="errors" :hints="hints">
          <template v-for="name in forwardedSlots()" #[name]="scope">
            <slot :name="name" v-bind="scope ?? {}" />
          </template>
        </UiFormSection>
      </UiStack>
    </UiGrid>
    <slot />
  </UiForm>
</template>
