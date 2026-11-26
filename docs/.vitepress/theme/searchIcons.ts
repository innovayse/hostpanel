/**
 * The icons the search box draws, as components of the icon library (`lucide-vue-next`), keyed by the
 * name a suggested page or a result row carries.
 */
import { BookOpen, ChevronRight, Download, FileText, Rocket, SlidersHorizontal } from 'lucide-vue-next'
import type { Component } from 'vue'
import type { SearchIconName } from './recentPages'

/** The icon component of every icon the search box uses. */
export const SEARCH_ICONS: Record<SearchIconName, Component> = {
  'file-text': FileText,
  'chevron-right': ChevronRight,
  rocket: Rocket,
  download: Download,
  'sliders-horizontal': SlidersHorizontal,
  'book-open': BookOpen,
}
