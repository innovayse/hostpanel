<script setup lang="ts">
/**
 * The header buttons of a report (refresh, export, print), placed in the `actions` slot of the report header
 * (the right side of the title row of `UiPageHeader`).
 *
 * The buttons are the `actions` list of the report's schema, drawn in that order, so the skeleton of the report
 * (which counts the same list) and the screen show the same buttons; each tells the screen when it is pressed. The
 * default slot takes the screen's own controls (`UiToggle`, a segmented control) and comes first. A button is its icon
 * plus its label and never wraps, so the group keeps its size beside the title of the header instead of dropping to
 * a row of its own.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
import { ReportAction } from '@/utils/ui/reportParts'

const { t } = useI18n()

/** Props for ReportActions. */
defineProps<{
  /** The buttons to show, in order: the `actions` of the report's schema. */
  actions: readonly ReportAction[]
}>()

/** Emits of ReportActions. */
const emit = defineEmits<{
  /** The operator asked to load the report again. */
  refresh: []
  /** The operator asked for the CSV export. */
  export: []
  /** The operator asked to print. */
  print: []
}>()

/** How a button looks: its action kind, its caption (already translated) and the `data-testid` the tests find it by. */
interface ActionLook {
  /** Kind of the action, which names its icon. */
  action: UiActionKind
  /** Caption. */
  label: string
  /** `data-testid` of the button. */
  testid: string
}

/** The look of each action; read again when the language changes. */
const looks = computed<Readonly<Record<ReportAction, ActionLook>>>(() => ({
  [ReportAction.Refresh]: { action: UiActionKind.Refresh, label: t('shared.refresh'), testid: 'report-refresh' },
  [ReportAction.Export]: { action: UiActionKind.Export, label: t('shared.export'), testid: 'report-export' },
  [ReportAction.Print]: { action: UiActionKind.Print, label: t('shared.print'), testid: 'report-print' },
}))

/** What pressing each button tells the screen. */
const press: Readonly<Record<ReportAction, () => void>> = {
  /**
   * Asks the screen to read the report again.
   *
   * @returns Nothing.
   */
  [ReportAction.Refresh]: () => emit('refresh'),
  /**
   * Asks the screen to export the report.
   *
   * @returns Nothing.
   */
  [ReportAction.Export]: () => emit('export'),
  /**
   * Asks the screen to print the report.
   *
   * @returns Nothing.
   */
  [ReportAction.Print]: () => emit('print'),
}
</script>

<template>
  <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm" class="shrink-0 whitespace-nowrap">
    <slot />
    <UiButton v-for="action in actions" :key="action" :action="looks[action].action" :variant="UiButtonVariant.Secondary" :data-testid="looks[action].testid" @click="press[action]()">
      {{ looks[action].label }}
    </UiButton>
  </UiStack>
</template>
