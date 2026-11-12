<script setup lang="ts">
/**
 * An icon button that copies a value to the clipboard and shows a check for a moment.
 *
 * A clipboard the browser blocks leaves the value on screen to be selected by hand, so a refusal needs no
 * message and the button simply stays as it was. `testid` is the button's `data-testid`.
 */
import { computed, onUnmounted, ref } from 'vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import { Icon } from '@/lib/icons'
import { UiSize } from '@/utils/ui/size'

/** Props for UiCopyButton. */
const props = withDefaults(defineProps<{
  /** The text to copy. */
  value: string
  /** Accessible name before the copy, for example "Copy reply". */
  label: string
  /** Accessible name while the check shows, for example "Copied". */
  copiedLabel: string
  /** `data-testid` of the button. */
  testid?: string
}>(), {
  testid: undefined,
})

/** How long the check stays on the button, in milliseconds. */
const COPIED_FLASH_MS = 1500

/** Whether the value was just copied. */
const copied = ref(false)

/** The pending reset of the copied state, undefined when none is pending. */
let flashTimer: ReturnType<typeof setTimeout> | undefined

/** The icon the button shows: the sheets, or the check after a copy. */
const markup = computed<string>(() => (copied.value ? Icon.Check : Icon.Copy))

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

onUnmounted(() => {
  clearTimeout(flashTimer)
})
</script>

<template>
  <UiIconButton :label="copied ? copiedLabel : label" :size="UiSize.Sm" :data-testid="testid" @click="copy">
    <UiIcon :markup="markup" :size="UiSize.Sm" />
  </UiIconButton>
</template>
