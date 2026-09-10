<script setup lang="ts">
/**
 * Brand colours card — the primary and accent colour the storefront derives its whole
 * palette from.
 *
 * A colour well beside a hex field, both bound to the same draft, a preview strip built
 * from the same ramp the storefront uses (`utils/brandPalette.ts`, a copy of the client's),
 * and a contrast line under each colour saying whether white text survives on it and what
 * the storefront will do if not. The API refuses anything that is not `#rrggbb`; the
 * field shows that refusal in the page banner and keeps the typed text.
 */
import { computed, toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSettingDraft } from '../composables/useSettingDraft'
import SettingSaveButton from './SettingSaveButton.vue'
import {
  WCAG_AA,
  bestTextOn,
  buildScale,
  contrastRatio,
  hexToRgb,
  rgbToHex,
} from '../../../utils/brandPalette'
import type { Setting } from '../../../types/setting'

const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
  /** True while a save is in flight. */
  saving?: boolean
}>()

const emit = defineEmits<{ save: [id: number, value: string] }>()

const { t } = useI18n()

const settings = toRef(props, 'settings')

/** The storefront's built-in colours, shown when a setting is empty. Mirrors the Tailwind fallbacks. */
const DEFAULTS = { 'portal.brand.primary': '#0ea5e9', 'portal.brand.accent': '#a855f7' } as const

/** One colour row. */
interface ColourField {
  key: keyof typeof DEFAULTS
  labelKey: string
  hintKey: string
  draft: ReturnType<typeof useSettingDraft>
}

const FIELDS: ColourField[] = [
  { key: 'portal.brand.primary', labelKey: 'settings.brandColors.primary.label', hintKey: 'settings.brandColors.primary.hint', draft: useSettingDraft(settings, 'portal.brand.primary') },
  { key: 'portal.brand.accent', labelKey: 'settings.brandColors.accent.label', hintKey: 'settings.brandColors.accent.hint', draft: useSettingDraft(settings, 'portal.brand.accent') },
]

/**
 * The colour a row previews: the draft when it parses, the built-in default otherwise —
 * a half-typed hex must not blank the preview.
 *
 * @param field - The row.
 */
function effective(field: ColourField): string {
  const v = field.draft.value.value
  return hexToRgb(v) ? v.toLowerCase() : DEFAULTS[field.key]
}

/**
 * Whether the row's text is something the API will accept: empty (unset) or `#rrggbb`.
 *
 * @param field - The row.
 */
function isValid(field: ColourField): boolean {
  const v = field.draft.value.value
  return v === '' || hexToRgb(v) !== null
}

/** The preview swatches per row: the eleven shades, lightest first. */
const shades = computed(() => Object.fromEntries(FIELDS.map(f => [
  f.key,
  Object.values(buildScale(effective(f))).map(rgbToHex),
])) as Record<string, string[]>)

/**
 * The contrast line for a row: the ratio of white on the colour, whether it passes AA for
 * normal text, and the text colour the storefront will actually use.
 *
 * @param field - The row.
 */
function contrast(field: ColourField): { ratio: string, passes: boolean, usesDark: boolean } {
  const hex = effective(field)
  const ratio = contrastRatio('#ffffff', hex)
  return {
    ratio: ratio.toFixed(1),
    passes: ratio >= WCAG_AA,
    usesDark: bestTextOn(hex) === '#111111',
  }
}

/**
 * The colour well writes lower-case hex; the text field takes whatever was typed and the
 * validity line explains it.
 *
 * @param field - The row.
 * @param e - The input event.
 */
function onInput(field: ColourField, e: Event): void {
  field.draft.value.value = (e.target as HTMLInputElement).value
}
</script>

<template>
  <section class="bg-surface-card border border-border rounded-2xl p-6 mb-6">
    <h2 class="font-display text-lg font-bold text-text-primary">{{ t('settings.brandColors.title') }}</h2>
    <p class="text-sm text-text-secondary mt-1">{{ t('settings.brandColors.description') }}</p>

    <div class="mt-5 flex flex-col gap-6">
      <div v-for="field in FIELDS" :key="field.key">
        <div
          v-if="!field.draft.setting.value"
          class="rounded-xl border border-status-yellow/30 bg-status-yellow/10 p-4 text-sm text-status-yellow"
          v-html="t('settings.portalAppearance.notSeeded', { key: field.key, env: 'NUXT_PUBLIC_PORTAL_BRAND_PRIMARY' })"
        />
        <template v-else>
          <label class="block text-sm font-medium text-text-secondary mb-1.5">{{ t(field.labelKey) }}</label>

          <div class="flex flex-wrap items-center gap-3">
            <!-- Native colour well: the browser's own picker, no library. -->
            <input
              type="color"
              :value="effective(field)"
              class="h-10 w-12 cursor-pointer rounded-lg border border-border bg-surface-elevated p-1"
              :aria-label="t(field.labelKey)"
              @input="onInput(field, $event)"
            >
            <input
              :value="field.draft.value.value"
              type="text"
              spellcheck="false"
              :placeholder="DEFAULTS[field.key]"
              class="w-32 rounded-lg border bg-surface-elevated px-3 py-2 font-mono text-sm text-text-primary focus:outline-none"
              :class="isValid(field) ? 'border-border focus:border-text-secondary' : 'border-status-red/60'"
              @input="onInput(field, $event)"
              @keyup.enter="field.draft.submit((id, value) => emit('save', id, value))"
            >
            <SettingSaveButton
              :dirty="field.draft.dirty.value && isValid(field)"
              :saving="saving"
              @click="field.draft.submit((id, value) => emit('save', id, value))"
            />
            <button
              v-if="field.draft.setting.value?.value"
              type="button"
              class="text-xs text-text-muted underline-offset-2 hover:text-text-secondary hover:underline"
              @click="field.draft.value.value = ''; field.draft.submit((id, value) => emit('save', id, value))"
            >
              {{ t('settings.brandColors.reset') }}
            </button>
          </div>

          <p class="mt-1 text-xs text-text-muted">{{ t(field.hintKey) }}</p>

          <!-- The ramp the storefront will derive, lightest to darkest, the picked colour in the middle. -->
          <div class="mt-3 flex h-8 overflow-hidden rounded-lg border border-border">
            <div
              v-for="(hex, i) in shades[field.key]"
              :key="hex + i"
              class="flex-1"
              :style="{ backgroundColor: hex }"
              :title="hex"
            />
          </div>

          <!-- What a filled button looks like on a light and a dark surface, with the text the storefront picks. -->
          <div class="mt-3 flex flex-wrap items-center gap-3">
            <div class="flex items-center gap-2 rounded-lg bg-white p-2">
              <span class="rounded-md px-3 py-1.5 text-xs font-semibold" :style="{ backgroundColor: effective(field), color: bestTextOn(effective(field)) }">{{ t('settings.brandColors.previewButton') }}</span>
              <span class="text-xs font-medium" :style="{ color: shades[field.key]![7] }">{{ t('settings.brandColors.previewLink') }}</span>
            </div>
            <div class="flex items-center gap-2 rounded-lg bg-[#08090f] p-2">
              <span class="rounded-md px-3 py-1.5 text-xs font-semibold" :style="{ backgroundColor: effective(field), color: bestTextOn(effective(field)) }">{{ t('settings.brandColors.previewButton') }}</span>
              <span class="text-xs font-medium" :style="{ color: shades[field.key]![3] }">{{ t('settings.brandColors.previewLink') }}</span>
            </div>
          </div>

          <p class="mt-2 text-xs" :class="contrast(field).passes ? 'text-status-green' : 'text-status-yellow'">
            {{ contrast(field).passes
              ? t('settings.brandColors.contrastOk', { ratio: contrast(field).ratio })
              : contrast(field).usesDark
                ? t('settings.brandColors.contrastDark', { ratio: contrast(field).ratio })
                : t('settings.brandColors.contrastLow', { ratio: contrast(field).ratio }) }}
          </p>
        </template>
      </div>
    </div>
  </section>
</template>
