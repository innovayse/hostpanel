<script setup lang="ts">
/**
 * The operator's light, dark or system theme choice, as a row of three icon buttons.
 *
 * The menu drawer on a phone shows it instead of a drop-down list: the list opens downwards from the bottom of
 * the drawer and is cut off by the screen edge, while a row of icons needs no list. It is a radio group, one
 * choice at a time; the arrow keys move the choice and focus with it (Right and Down forward, Left and Up
 * back), Home and End jump to the ends. The caller's `data-testid` goes on the group and each button adds its
 * theme as a suffix (`sidebar-theme-light`).
 */
import { computed, nextTick, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useThemeStore } from '@/stores/theme'
import { AdminTheme } from '@/types/admintheme'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import { Key } from '@/utils/ui/keys'

/** Props of the theme switch. */
const props = defineProps<{
  /** Test id of the group; each button's id is this plus `-` and its theme. */
  testid: string
}>()

/** Translation function. */
const { t } = useI18n()

/** Theme store: the operator's light/dark/system choice. */
const theme = useThemeStore()

/** The group element; the radio that gets focus after a key press is looked up inside it. */
const group = ref<HTMLElement | null>(null)

/** One button of the switch. */
interface ThemeChoice {
  /** The theme the button picks. */
  value: AdminTheme
  /** Accessible name, in the active language. */
  label: string
  /** Inner SVG markup of the icon, drawn on a 24 by 24 grid. */
  icon: string
}

/**
 * Wraps SVG path data into the shape markup `UiIcon` draws.
 *
 * @param path - The `d` attribute of a path.
 * @returns Inner SVG markup holding that single path.
 */
const iconMarkup = (path: string): string => `<path d="${path}"/>`

/**
 * The three choices in display order.
 *
 * @returns One entry per {@link AdminTheme}.
 */
const choices = computed<ThemeChoice[]>(() => [
  { value: AdminTheme.Light, label: t('layout.theme.light'), icon: iconMarkup('M12 3v2m0 14v2M5.6 5.6l1.4 1.4m10 10 1.4 1.4M3 12h2m14 0h2M5.6 18.4 7 17m10-10 1.4-1.4M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8Z') },
  { value: AdminTheme.Dark, label: t('layout.theme.dark'), icon: iconMarkup('M20 14.5A8 8 0 0 1 9.5 4 8 8 0 1 0 20 14.5Z') },
  { value: AdminTheme.System, label: t('layout.theme.system'), icon: iconMarkup('M4 5h16v11H4zM9 20h6m-3-4v4') },
])

/**
 * Index of the radio a key moves to, or null for a key the group does not handle.
 *
 * @param key - The `KeyboardEvent.key` value.
 * @param index - Index of the button that has focus.
 * @returns The target index, wrapping around at the ends.
 */
const targetIndex = (key: string, index: number): number | null => {
  const count = choices.value.length
  switch (key) {
    case Key.ArrowRight:
    case Key.ArrowDown:
      return (index + 1) % count
    case Key.ArrowLeft:
    case Key.ArrowUp:
      return (index - 1 + count) % count
    case Key.Home:
      return 0
    case Key.End:
      return count - 1
    default:
      return null
  }
}

/**
 * Moves the choice and the focus to the radio the key points at. The focused button keeps `tabindex="-1"` once it
 * is no longer checked, so focus has to follow the choice or the next key press starts from the old button.
 *
 * @param event - The keydown event; the arrow keys, Home and End are handled.
 * @param index - Index of the button that has focus.
 * @returns Resolves once the focus has moved.
 */
const onKeydown = async (event: KeyboardEvent, index: number): Promise<void> => {
  const next = targetIndex(event.key, index)
  const target = next === null ? undefined : choices.value[next]
  if (next === null || target === undefined) {
    return
  }
  event.preventDefault()
  theme.setTheme(target.value)
  await nextTick()
  group.value?.querySelectorAll<HTMLElement>('[role="radio"]')[next]?.focus()
}
</script>

<template>
  <div
    ref="group"
    role="radiogroup"
    :aria-label="t('layout.theme.label')"
    :data-testid="props.testid"
    class="grid grid-cols-3 gap-1 rounded-[10px] border border-border bg-surface-card p-1"
  >
    <UiIconButton
      v-for="(choice, index) in choices"
      :key="choice.value"
      :label="choice.label"
      size="sm"
      role="radio"
      :aria-checked="theme.choice === choice.value"
      :tabindex="theme.choice === choice.value ? 0 : -1"
      :data-testid="`${props.testid}-${choice.value}`"
      class="w-full"
      :class="theme.choice === choice.value
        ? 'bg-primary-500/15 text-primary-500 light:text-primary-600'
        : 'text-text-secondary hover:text-text-primary hover:bg-surface-hover'"
      @click="theme.setTheme(choice.value)"
      @keydown="onKeydown($event, index)"
    >
      <UiIcon :markup="choice.icon" size="sm" />
    </UiIconButton>
  </div>
</template>
