<script lang="ts">
/** Where one step of a list stands. */
export const UiStepState = {
  /** Not reached yet: a grey ring. */
  Pending: 'pending',
  /** Being worked on now: the accent spinner. */
  Running: 'running',
  /** Finished: a green check. */
  Done: 'done',
  /** Stopped with an error: a red cross. */
  Failed: 'failed',
} as const

/** One of the {@link UiStepState} values. */
export type UiStepState = (typeof UiStepState)[keyof typeof UiStepState]

/** One row of a {@link UiStepState} list. */
export interface UiStepListItem {
  /** Stable key of the step. */
  key: string
  /** What the step does, already translated. */
  label: string
  /** Where the step stands. */
  state: UiStepState
}
</script>

<script setup lang="ts">
/**
 * An ordered list of the steps of a job, each with an icon and a colour for where it stands: pending grey with a ring,
 * running in the accent with the spinner, done green with a check, failed red with a cross. The state is also written
 * for assistive technology beside each label (`stateLabels`), so it is never carried by colour alone. The running step
 * is marked as the current one.
 */
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLoader from '@/components/ui/UiLoader.vue'
import { Icon } from '@/lib/icons'
import { UiSize } from '@/utils/ui/size'

/** Props for UiStepList. */
defineProps<{
  /** The steps, in the order they run. */
  steps: UiStepListItem[]
  /** The spoken name of each state, already translated. */
  stateLabels: Record<UiStepState, string>
}>()

/** Icon of each finished or waiting state; a running step draws the spinner instead. */
const stateIcons: Record<Exclude<UiStepState, 'running'>, string> = {
  pending: Icon.Circle,
  done: Icon.Check,
  failed: Icon.Close,
}

/** Colour of the icon and the label of each state, from the theme tokens. */
const stateClasses: Record<UiStepState, string> = {
  pending: 'text-text-muted',
  running: 'text-text-accent font-medium',
  done: 'text-status-green',
  failed: 'text-status-red font-medium',
}
</script>

<template>
  <ol class="flex flex-col gap-2" data-testid="ui-step-list">
    <li
      v-for="step in steps"
      :key="step.key"
      class="flex items-center gap-2.5 text-[0.8125rem]"
      :class="stateClasses[step.state]"
      :aria-current="step.state === UiStepState.Running ? 'step' : undefined"
      :data-state="step.state"
      :data-testid="`ui-step-${step.key}`"
    >
      <span class="inline-flex h-5 w-5 shrink-0 items-center justify-center" aria-hidden="true">
        <UiLoader v-if="step.state === UiStepState.Running" :size="UiSize.Sm" />
        <UiIcon v-else :markup="stateIcons[step.state]" :size="UiSize.Md" />
      </span>
      <span>{{ step.label }}</span>
      <span class="sr-only">{{ stateLabels[step.state] }}</span>
    </li>
  </ol>
</template>
