<script setup lang="ts">
/**
 * A block of text meant to be copied exactly, a shell command, a public key, in a monospace
 * box with a copy button. The text wraps rather than scrolls, so nothing is hidden off screen.
 * `data-testid` falls through to the box; the button carries `<testid>-copy` when one is given.
 */
import { computed, onUnmounted, ref, useAttrs } from 'vue'
import UiButton from '@/components/ui/UiButton.vue'

defineOptions({ inheritAttrs: false })

/** Props for UiCodeBlock. */
const props = defineProps<{
  /** The text shown and copied. */
  code: string
  /** Label of the copy button. */
  copyLabel: string
  /** Label shown on the button for a moment after copying. */
  copiedLabel: string
}>()

/** How long the copied state lasts, in milliseconds. */
const COPIED_FOR_MS = 2000

/** Attributes passed by the parent, for the button's derived test id. */
const attrs = useAttrs()

/** Whether the text was just copied. */
const copied = ref(false)

/** The pending reset of the copied state, null when none is pending. */
let resetTimer: ReturnType<typeof setTimeout> | null = null

/** Test id of the copy button: the box's own id with `-copy`, none when the box has none. */
const copyTestId = computed<string | undefined>(() => {
  const boxTestId = attrs['data-testid']
  return typeof boxTestId === 'string' && boxTestId !== '' ? `${boxTestId}-copy` : undefined
})

/** Cancels a pending reset of the copied state. */
const cancelReset = (): void => {
  if (resetTimer !== null) {
    clearTimeout(resetTimer)
    resetTimer = null
  }
}

/**
 * Copies the text. A refused clipboard (insecure context, permission) leaves the text selectable
 * in the box, so it needs no message of its own.
 *
 * @returns Resolves when the attempt has ended.
 */
const copy = async (): Promise<void> => {
  try {
    await navigator.clipboard.writeText(props.code)
    copied.value = true
    // A second copy restarts the moment instead of being cut short by the first one's reset.
    cancelReset()
    resetTimer = setTimeout(() => {
      copied.value = false
      resetTimer = null
    }, COPIED_FOR_MS)
  } catch {
    copied.value = false
  }
}

onUnmounted(cancelReset)
</script>

<template>
  <div class="flex items-start gap-2">
    <code
      v-bind="$attrs"
      class="block min-w-0 flex-1 whitespace-pre-wrap break-all rounded-control border border-border bg-surface-field px-3.5 py-2.5 font-mono text-xs text-text-primary"
    >{{ props.code }}</code>
    <UiButton
      type="button"
      variant="secondary"
      size="sm"
      :data-testid="copyTestId"
      @click="copy"
    >
      {{ copied ? props.copiedLabel : props.copyLabel }}
    </UiButton>
  </div>
</template>
