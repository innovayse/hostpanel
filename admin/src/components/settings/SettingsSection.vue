<script setup lang="ts">
/**
 * A titled card that groups one part of a settings screen or form.
 *
 * The title and the optional description sit on top, the default slot carries the controls. Every
 * card on the system-settings page and every section of the slide and TLD forms is one of these,
 * so the spacing and type are defined once. A card with many fields groups them under `SettingsSubsection` headings,
 * which span the whole row. `columns` lays the controls out in a grid that is one
 * column until the card itself is 48rem wide and the given count from there (the card is the `@container`, so a card beside
 * a sidebar or on a tablet stays one column however wide the screen is); a control that needs the whole row takes
 * `col-span-full`. `loading` keeps the real title and description and draws every `UiField` inside as a placeholder
 * of its control (through `UI_FIELD_LOADING`), so a form that is still reading its entry has the shape it will have.
 *
 * The root is a `<section>` named by its title (`aria-labelledby`), so each group is a landmark a screen reader can jump to.
 */
import { computed, inject, provide, ref, useId } from 'vue'
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
  /** Column count of the controls once the card is 48rem wide; one column below that. */
  columns?: UiGridColumns
  /** Draws the fields of the section as placeholders while the entry loads. */
  loading?: boolean
}>(), {
  description: undefined,
  columns: 1,
  loading: false,
})

/** Id of the title, which names the section. */
const titleId = useId()

/** The loading flag of a form around this section, if any. */
const formLoading = inject(UI_FIELD_LOADING, ref(false))

// A section inside a loading form stays loading even when it does not set the flag itself.
provide(UI_FIELD_LOADING, computed<boolean>(() => props.loading || formLoading.value))
</script>

<template>
  <section class="mb-4" :aria-labelledby="titleId" :aria-busy="loading ? 'true' : undefined">
    <UiCard padding="md" class="@container">
      <UiHeading :id="titleId" :level="2" variant="section">
        {{ title }}
      </UiHeading>
      <UiText v-if="description" tone="secondary" size="lg" class="mt-1">
        {{ description }}
      </UiText>
      <!-- Tight rows, roomier columns: the fields fill their column and the vertical rhythm is the same on every card. -->
      <UiGrid :columns="columns" gap="sm" container class="mt-3 gap-x-4">
        <slot />
      </UiGrid>
    </UiCard>
  </section>
</template>
