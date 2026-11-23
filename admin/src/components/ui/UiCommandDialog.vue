<script setup lang="ts">
/**
 * The modal surface of a command palette: a dimmed, blurred backdrop with a floating panel near the top of the
 * screen (the whole screen on a phone). It is teleported to `<body>`, animates like the kit's dialogs, closes on
 * Escape or a backdrop click, keeps Tab inside, returns focus to the opener and moves it to the first control once
 * the enter has ended (`useModalSurface`). The content (default slot) owns its own keys: a `keydown` listener passed
 * by the caller runs beside the shared handling, because the attributes go onto the panel, the root being a Teleport.
 */
import { ref } from 'vue'
import UiTransition from '@/components/ui/UiTransition.vue'
import { FLOATING_PANEL_CLASSES } from '@/utils/ui/floating'
import { useModalSurface } from '@/utils/ui/modalSurface'
import { UiTransitionKind } from '@/utils/ui/transition'

/** The root is a Teleport, which cannot take attributes, so a caller's `data-testid` and listeners go onto the panel. */
defineOptions({ inheritAttrs: false })

/** Props for UiCommandDialog. */
const props = defineProps<{
  /** Whether the surface is shown. */
  open: boolean
  /** Accessible name of the dialog (already translated). */
  label: string
}>()

/** Events emitted by UiCommandDialog. */
const emit = defineEmits<{
  /** The user asked to close it: Escape or a click on the backdrop. */
  close: []
}>()

/** The panel, for focus handling. */
const panel = ref<HTMLElement | null>(null)

/** Asks the owner to close the surface. */
const close = (): void => {
  emit('close')
}

/** Escape, Tab trapping and focus moves shared with the other modal surfaces. */
const { onKeydown, focusIn } = useModalSurface(panel, () => props.open, close)
</script>

<template>
  <Teleport to="body">
    <UiTransition :kind="UiTransitionKind.Dialog" @after-enter="focusIn">
      <div
        v-if="open"
        data-testid="command-dialog-backdrop"
        class="fixed inset-0 z-50 flex items-start justify-center bg-black/60 backdrop-blur-sm sm:px-4 sm:pt-[12vh]"
        @click.self="close"
      >
        <div
          v-bind="$attrs"
          ref="panel"
          role="dialog"
          aria-modal="true"
          :aria-label="label"
          tabindex="-1"
          :class="FLOATING_PANEL_CLASSES"
          class="flex h-dvh w-full flex-col overflow-hidden outline-none sm:h-auto sm:max-h-[70dvh] sm:max-w-xl sm:rounded-panel"
          @keydown="onKeydown"
        >
          <slot />
        </div>
      </div>
    </UiTransition>
  </Teleport>
</template>
