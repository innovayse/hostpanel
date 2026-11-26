<script setup lang="ts">
/**
 * The button that opens a search (the command palette): an icon when its container is narrow, a field-like bar when wide.
 *
 * The container's own width decides the shape (container queries, since the space a bar has depends on the sidebar beside
 * it, not on the screen): from `4xl` it is a bar showing `text`, from `5xl` it also announces the `shortcut`, and narrower it
 * is the bare icon, drawn like the other icon triggers of the bar (no fill, no border). It must sit inside an `@container`.
 * The required `label` is the accessible name. `data-testid` and listeners fall through to the button.
 */
import UiIcon from '@/components/ui/UiIcon.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { buttonBaseClasses, UiButtonType } from '@/utils/ui/button'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

/** Props for UiSearchTrigger. */
defineProps<{
  /** Accessible name of the button. */
  label: string
  /** Text of the bar, such as what can be searched; shown only when the container is wide. */
  text: string
  /** Keys announced beside the text, such as "Ctrl K"; shown only when the container is very wide. */
  shortcut?: string
  /** `data-testid` of the shortcut chip. */
  shortcutTestid?: string
}>()
</script>

<template>
  <button
    :type="UiButtonType.Button"
    :aria-label="label"
    :class="[buttonBaseClasses, 'h-[2.625rem] w-10.5 min-w-0 gap-2.5 rounded-control border border-transparent bg-transparent px-0 text-sm text-text-secondary transition-colors hover:bg-surface-hover hover:text-text-primary @4xl:ml-6 @4xl:w-full @4xl:max-w-[460px] @4xl:flex-1 @4xl:justify-start @4xl:border-border-control @4xl:bg-surface-field @4xl:px-3.5 @4xl:text-text-muted']"
  >
    <UiIcon :markup="Icon.Search" :size="UiSize.Sm" />
    <UiText :tone="UiTextTone.Muted" class="hidden flex-1 truncate text-left @4xl:block">{{ text }}</UiText>
    <UiText
      v-if="shortcut !== undefined"
      :tone="UiTextTone.Muted"
      :size="UiTextSize.Xs"
      class="hidden shrink-0 rounded-md border border-border-strong px-[7px] py-0.5 @5xl:block"
      :data-testid="shortcutTestid"
    >{{ shortcut }}</UiText>
  </button>
</template>
