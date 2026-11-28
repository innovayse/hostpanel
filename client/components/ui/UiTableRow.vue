<template>
  <tr
    :class="[
      'transition-colors',
      hoverable && 'hover:bg-gray-50 dark:hover:bg-white/5',
      group && 'group',
      to && 'cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary-500'
    ]"
    :role="to ? 'link' : undefined"
    :tabindex="to ? 0 : undefined"
    @click="onClick"
    @auxclick="onAuxClick"
    @keydown.enter="onEnter"
  >
    <slot />
  </tr>
</template>

<script setup lang="ts">
/**
 * TableRow (`UiTableRow`): a row of a UiTable with an optional hover highlight and a `group`
 * marker so cells can react to row hover (`group-hover:`).
 *
 * With `to` the whole row opens that route: a click on the row's own space opens it in this tab, Ctrl, Cmd or middle click
 * opens it in a new one, and Enter does the same from the keyboard (the row is a focusable `link`). A click that lands on a
 * link, button, menu, field or checkbox inside the row is left to that control.
 */
import { isOwnControlClick } from '~/utils/rowLink'

/** Props of TableRow. */
interface Props {
  /** Highlights the row on hover. */
  hoverable?: boolean
  /** Adds the `group` class so children can use `group-hover:`. */
  group?: boolean
  /** Route the whole row opens; the row is a plain row when absent. */
  to?: string
}

/** The props of the component. */
const props = withDefaults(defineProps<Props>(), {
  hoverable: true,
  group: false,
  to: undefined,
})

/** The router, which resolves the address of a new tab. */
const router = useRouter()

/**
 * Opens the row's route: in a new tab when asked, in this one otherwise.
 *
 * @param route - The route the row opens.
 * @param newTab - Whether to open it beside the current page.
 */
const open = (route: string, newTab: boolean): void => {
  if (newTab) {
    window.open(router.resolve(route).href, '_blank', 'noopener')
    return
  }
  void navigateTo(route)
}

/**
 * Opens the route on a click on the row itself: in this tab, or in a new one with Ctrl or Cmd.
 *
 * @param event - The click.
 */
const onClick = (event: MouseEvent): void => {
  if (!props.to || isOwnControlClick(event.target, event.currentTarget as Element)) { // the listener sits on the row, so currentTarget is an Element
    return
  }
  open(props.to, event.ctrlKey || event.metaKey)
}

/**
 * Opens the route in a new tab on a middle click on the row itself.
 *
 * @param event - The auxiliary click.
 */
const onAuxClick = (event: MouseEvent): void => {
  if (!props.to || event.button !== 1 || isOwnControlClick(event.target, event.currentTarget as Element)) { // the listener sits on the row, so currentTarget is an Element
    return
  }
  event.preventDefault()
  open(props.to, true)
}

/**
 * Opens the route on Enter while the row itself has focus; Enter on a control inside the row is that control's.
 *
 * @param event - The key press.
 */
const onEnter = (event: KeyboardEvent): void => {
  if (!props.to || event.target !== event.currentTarget) {
    return
  }
  open(props.to, event.ctrlKey || event.metaKey)
}
</script>
