/**
 * The sidebar store: whether the desktop sidebar is folded to its icon rail.
 *
 * The choice is remembered in localStorage, so the shell opens the way the operator left it. The
 * phone menu drawer ignores it: a drawer is always drawn in full.
 */
import { defineStore } from 'pinia'
import { ref } from 'vue'

/** localStorage key holding the folded flag. */
export const SIDEBAR_STORAGE_KEY = 'hostpanel.sidebar.collapsed'

/** Stored value of a folded sidebar. */
const COLLAPSED_VALUE = '1'

/** Stored value of an open sidebar. */
const OPEN_VALUE = '0'

/**
 * Reads the stored choice; a missing or unreadable value means the sidebar is open.
 *
 * @returns True when the operator folded the sidebar.
 */
const readCollapsed = (): boolean => {
  try {
    return window.localStorage.getItem(SIDEBAR_STORAGE_KEY) === COLLAPSED_VALUE
  } catch {
    return false
  }
}

/**
 * Persists the choice. Storage that throws only costs the choice surviving a reload.
 *
 * @param collapsed - Whether the sidebar is folded.
 * @returns Nothing; the flag is stored when storage allows it.
 */
const writeCollapsed = (collapsed: boolean): void => {
  try {
    window.localStorage.setItem(SIDEBAR_STORAGE_KEY, collapsed ? COLLAPSED_VALUE : OPEN_VALUE)
  } catch {
    // Private mode or blocked site data: the choice still applies for this page.
  }
}

/** Pinia store holding the folded state of the desktop sidebar. */
export const useSidebarStore = defineStore('sidebar', () => {
  /** True while the desktop sidebar is folded to the icon rail. */
  const collapsed = ref<boolean>(readCollapsed())

  /**
   * Folds or opens the sidebar and remembers the choice.
   *
   * @param value - The new folded state.
   * @returns Nothing; the state and the stored flag are updated.
   */
  const setCollapsed = (value: boolean): void => {
    collapsed.value = value
    writeCollapsed(value)
  }

  /**
   * Flips the folded state.
   *
   * @returns Nothing; the state and the stored flag are updated.
   */
  const toggle = (): void => {
    setCollapsed(!collapsed.value)
  }

  return { collapsed, setCollapsed, toggle }
})
