<template>
  <UiIconButton
    :label="copied ? copiedLabel : label"
    :icon="copied ? Check : Copy"
    size="sm"
    :testid="testid"
    @click="copy"
  />
</template>

<script setup lang="ts">
/**
 * UiCopyButton: an icon button that copies a value to the clipboard and shows a check for a moment.
 *
 * A clipboard the browser blocks leaves the value on screen to be selected by hand, so a refusal
 * needs no message and the button simply stays as it was.
 */
import { Check, Copy } from 'lucide-vue-next'
import { onBeforeUnmount, ref } from 'vue'

/** How long the check stays on the button, in milliseconds. */
const COPIED_FLASH_MS = 1500

/** Props of UiCopyButton. */
interface Props {
  /** The text to copy. */
  value: string
  /** Accessible name before the copy, for example "Copy IBAN". */
  label: string
  /** Accessible name while the check shows, for example "Copied". */
  copiedLabel: string
  /** `data-testid` of the button. */
  testid?: string
}

/** The props of the component. */
const props = withDefaults(defineProps<Props>(), {
  testid: undefined
})

/** Whether the value was just copied. */
const copied = ref(false)

/** The pending timer that takes the check away, kept so it can be cancelled. */
let flashTimer: ReturnType<typeof setTimeout> | undefined

/**
 * Copies the value and flags the button briefly.
 *
 * @returns Resolves when the attempt has ended.
 */
const copy = async (): Promise<void> => {
  const written = await navigator.clipboard?.writeText(props.value).then(() => true, () => false)
  if (!written) {
    return
  }
  copied.value = true
  clearTimeout(flashTimer)
  flashTimer = setTimeout(() => {
    copied.value = false
  }, COPIED_FLASH_MS)
}

onBeforeUnmount(() => {
  clearTimeout(flashTimer)
})
</script>
