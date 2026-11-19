<script setup lang="ts">
/**
 * One card of a schema driven form: an optional title and a grid of fields.
 *
 * The section is drawn from a `SkeletonSection` of the screen's schema, so the screen and its loading skeleton are made
 * of the same card, title, columns and fields. Every field is a `UiField` (caption, asterisk, hint, error) whose control
 * is the scoped slot `field-<key>` (the `{ id, invalid, describedby }` of `UiField`, to bind on the control); a field
 * with `span: full` takes the whole row. The slot `section-<key>` holds extra real content under the fields (a note, an
 * add button) and is not drawn while `loading`. While `loading` an input shows the `UiField` skeleton, a textarea or a check
 * a bar of its own height, a radio group a dot and the real caption per option; a toggle keeps its row (caption left, a
 * switch shaped bar right), in the real field as in the skeleton.
 */
import { useI18n } from 'vue-i18n'
import UiCard from '@/components/ui/UiCard.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { fieldBarClassOf, ownsFieldBody } from '@/utils/ui/skeletonLayout'
import { SkeletonFieldKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { SkeletonSection } from '@/utils/ui/skeletonSchema'

/** Resolves the locale keys the schema names into the operator's language. */
const { t } = useI18n()

/** Props for UiFormSection. */
withDefaults(defineProps<{
  /** The section of the screen's schema. */
  section: SkeletonSection
  /** Draws skeletons in place of the controls; the title and the captions stay. */
  loading?: boolean
  /** Server sentences by field key; a field with one turns red and announces it. */
  errors?: Readonly<Record<string, string>>
  /** Help lines (already translated) by field key, shown while the field has no error. */
  hints?: Readonly<Record<string, string>>
}>(), {
  loading: false,
  /**
   * Starts with no field in error.
   *
   * @returns An empty map of sentences.
   */
  errors: () => ({}),
  /**
   * Starts with no help line.
   *
   * @returns An empty map of help lines.
   */
  hints: () => ({}),
})
</script>

<template>
  <UiCard padding="md" :aria-busy="loading ? 'true' : undefined" :data-testid="`ui-form-section-${section.key}`">
    <UiStack gap="md" class="@container">
      <UiHeading v-if="section.title" :level="2" variant="card">
        {{ t(section.title) }}
      </UiHeading>
      <UiGrid :columns="section.columns ?? 2" gap="sm" container>
        <UiField
          v-for="field in section.fields"
          :key="field.key"
          :label="field.label ? t(field.label) : undefined"
          :required="field.required"
          :error="errors[field.key]"
          :error-testid="`ui-form-field-${field.key}-error`"
          :hint="hints[field.key]"
          :loading="loading && !ownsFieldBody(field)"
          :inline="field.kind === SkeletonFieldKind.Toggle"
          :class="field.span === SkeletonSpan.Full ? 'col-span-full' : ''"
          :data-testid="`ui-form-field-${field.key}`"
        >
          <template #default="scope">
            <UiStack v-if="loading && field.kind === SkeletonFieldKind.Radio" gap="sm" data-testid="ui-form-radio-options">
              <UiStack v-for="option in field.options ?? []" :key="option" direction="row" align="center" gap="sm">
                <UiSkeletonBar class="h-4 w-4 shrink-0 rounded-full" aria-hidden="true" />
                <UiText size="sm">{{ t(option) }}</UiText>
              </UiStack>
            </UiStack>
            <UiSkeletonBar v-else-if="loading" :class="fieldBarClassOf(field)" aria-hidden="true" data-testid="ui-form-field-bar" />
            <slot v-else :name="`field-${field.key}`" v-bind="scope" />
          </template>
        </UiField>
      </UiGrid>
      <slot v-if="!loading" :name="`section-${section.key}`" />
    </UiStack>
  </UiCard>
</template>
