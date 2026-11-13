<script setup lang="ts">
/**
 * A titled card that groups one part of a settings screen or form.
 *
 * The title and the optional description sit on top, the default slot carries the controls. Every
 * card on the system-settings page and every section of the slide and TLD forms is one of these,
 * so the spacing and type are defined once. `columns` lays the controls out in a grid that is one
 * column on phones and the given count from `md`; a control that needs the whole row takes
 * `md:col-span-2`. `loading` keeps the real title and description and draws every `UiField` inside as a placeholder
 * of its control (through `UI_FIELD_LOADING`), so a form that is still reading its entry has the shape it will have.
 */
import { computed, inject, provide, ref } from 'vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiText from '@/components/ui/UiText.vue'
import { UI_FIELD_LOADING } from '@/utils/ui/injection'
import type { UiGridColumns } from '@/utils/ui/layout'

/** Props for SettingsSection. */
const props = withDefaults(defineProps<{
  /** Section title (already translated). */
  title: string
  /** Line under the title (already translated). */
  description?: string
  /** Column count of the controls from `md` up; one column on phones. */
  columns?: UiGridColumns
  /** Draws the fields of the section as placeholders while the entry loads. */
  loading?: boolean
}>(), {
  description: undefined,
  columns: 1,
  loading: false,
})

/** The loading flag of a form around this section, if any. */
const formLoading = inject(UI_FIELD_LOADING, ref(false))

// A section inside a loading form stays loading even when it does not set the flag itself.
provide(UI_FIELD_LOADING, computed<boolean>(() => props.loading || formLoading.value))
</script>

<template>
  <UiCard padding="md" class="mb-5" :aria-busy="loading ? 'true' : undefined">
    <UiHeading :level="2" variant="section">
      {{ title }}
    </UiHeading>
    <UiText v-if="description" tone="secondary" size="lg" class="mt-1">
      {{ description }}
    </UiText>
    <UiGrid :columns="columns" class="mt-4">
      <slot />
    </UiGrid>
  </UiCard>
</template>
