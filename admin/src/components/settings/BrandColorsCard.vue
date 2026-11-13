<script setup lang="ts">
/**
 * Brand colours card: the primary and accent colour the storefront derives its whole palette from.
 *
 * A hex colour field with a swatch preview (`UiColorField`), bound to the draft, a preview strip built from the
 * same ramp the storefront uses (`lib/brandPalette.ts`, a copy of the client's), and a contrast
 * line under each colour saying whether white text survives on it and what the storefront will do
 * if not. The API refuses anything that is not `#rrggbb`; the field shows that refusal in the page
 * banner and keeps the typed text. The swatches are SVG (`UiColorSwatch`), because a colour the
 * operator typed cannot be a Tailwind class and an inline style is not allowed.
 */
import { computed, toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSettingDraft } from '@/composables/useSettingDraft'
import SettingNotSeeded from '@/components/settings/SettingNotSeeded.vue'
import SettingOutcome from '@/components/settings/SettingOutcome.vue'
import SettingsSection from '@/components/settings/SettingsSection.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiColorField from '@/components/ui/UiColorField.vue'
import UiColorSwatch from '@/components/ui/UiColorSwatch.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import {
  SHADE_STEPS,
  WCAG_AA,
  bestTextOn,
  buildScale,
  contrastRatio,
  hexToRgb,
  rgbToHex,
} from '@/lib/brandPalette'
import type { ShadeStep } from '@/lib/brandPalette'
import { SettingEnv, SettingKey } from '@/types/setting'
import type { Setting } from '@/types/setting'

const { t } = useI18n()

/** Props for BrandColorsCard. */
const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
}>()

/** The settings list as a ref, which is what the draft composable watches. */
const settings = toRef(props, 'settings')

/** The storefront's built-in colours, shown when a setting is empty. Mirrors the Tailwind fallbacks. */
const DEFAULTS = { [SettingKey.BrandPrimary]: '#0ea5e9', [SettingKey.BrandAccent]: '#a855f7' } as const

/** Index of the shade previewed as a link on a light surface. */
const LIGHT_SURFACE_SHADE = 7

/** Index of the shade previewed as a link on a dark surface. */
const DARK_SURFACE_SHADE = 3

/** The text colour the storefront picks on a colour that is too light for white. */
const DARK_TEXT = '#111111'

/** One colour row. */
interface ColourField {
  /** Settings key of the colour. */
  key: keyof typeof DEFAULTS
  /** Environment variable the portal falls back to. */
  env: string
  /** Short name used in test ids. */
  testid: string
  /** Label key. */
  labelKey: string
  /** Hint key. */
  hintKey: string
  /** Draft of the row. */
  draft: ReturnType<typeof useSettingDraft>
}

/** The two colour rows. */
const FIELDS: ColourField[] = [
  {
    key: SettingKey.BrandPrimary,
    env: SettingEnv[SettingKey.BrandPrimary],
    testid: 'primary',
    labelKey: 'settings.brandColors.primary.label',
    hintKey: 'settings.brandColors.primary.hint',
    draft: useSettingDraft(settings, SettingKey.BrandPrimary),
  },
  {
    key: SettingKey.BrandAccent,
    env: SettingEnv[SettingKey.BrandAccent],
    testid: 'accent',
    labelKey: 'settings.brandColors.accent.label',
    hintKey: 'settings.brandColors.accent.hint',
    draft: useSettingDraft(settings, SettingKey.BrandAccent),
  },
]

/**
 * The colour a row previews: the draft when it parses, the built-in default otherwise, so a
 * half-typed hex does not blank the preview.
 *
 * @param field - The row.
 * @returns A `#rrggbb` colour.
 */
const effective = (field: ColourField): string => {
  const typed = field.draft.value.value
  return hexToRgb(typed) ? typed.toLowerCase() : DEFAULTS[field.key]
}

/** The preview swatches per row: the eleven shades, lightest first, each named by its scale step. */
const shades = computed<Record<string, { step: ShadeStep; hex: string }[]>>(() => Object.fromEntries(FIELDS.map((field) => {
  const scale = buildScale(effective(field))
  return [field.key, SHADE_STEPS.map((step) => ({ step, hex: rgbToHex(scale[step]) }))]
})))

/**
 * One shade of a row's ramp, falling back to the row's own colour if the ramp is shorter.
 *
 * @param field - The row.
 * @param index - Position in the ramp, lightest first.
 * @returns A `#rrggbb` colour.
 */
const shadeAt = (field: ColourField, index: number): string => shades.value[field.key]?.[index]?.hex ?? effective(field)

/**
 * The contrast line for a row: the ratio of white on the colour, whether it passes AA for normal
 * text, and the text colour the storefront will actually use.
 *
 * @param field - The row.
 * @returns The ratio as text, whether white passes, and whether the storefront switches to dark text.
 */
const contrast = (field: ColourField): { ratio: string, passes: boolean, usesDark: boolean } => {
  const hex = effective(field)
  const ratio = contrastRatio('#ffffff', hex)
  return {
    ratio: ratio.toFixed(1),
    passes: ratio >= WCAG_AA,
    usesDark: bestTextOn(hex) === DARK_TEXT,
  }
}

/**
 * The i18n key of the contrast sentence for a row.
 *
 * @param field - The row.
 * @returns The key matching the contrast outcome.
 */
const contrastKey = (field: ColourField): string => {
  const outcome = contrast(field)
  if (outcome.passes) {
    return 'settings.brandColors.contrastOk'
  }
  return outcome.usesDark ? 'settings.brandColors.contrastDark' : 'settings.brandColors.contrastLow'
}

/**
 * Empties the colour; once the page's save bar saves it, the template's own colour applies again.
 *
 * @param field - The row.
 */
const reset = (field: ColourField): void => {
  field.draft.value.value = ''
}
</script>

<template>
  <SettingsSection
    :columns="2"
    :title="t('settings.brandColors.title')"
    :description="t('settings.brandColors.description')"
  >
    <template v-for="field in FIELDS" :key="field.key">
      <SettingNotSeeded v-if="!field.draft.setting.value" :setting-key="field.key" :env-name="field.env" />
      <UiStack v-else gap="sm">
        <UiText tone="secondary" size="md">
          {{ t(field.labelKey) }}
        </UiText>

        <UiStack direction="row" align="center" wrap>
          <UiColorField
            :model-value="field.draft.value.value"
            :placeholder="DEFAULTS[field.key]"
            :fallback="DEFAULTS[field.key]"
            :aria-label="t(field.labelKey)"
            :data-testid="`brand-colors-hex-${field.testid}`"
            @update:model-value="field.draft.value.value = $event"
          />
          <SettingOutcome :outcome="field.draft.outcome.value" />
          <UiButton
            v-if="field.draft.setting.value?.value"
            type="button"
            variant="link"
            size="sm"
            :data-testid="`brand-colors-reset-${field.testid}`"
            @click="reset(field)"
          >
            {{ t('settings.brandColors.reset') }}
          </UiButton>
        </UiStack>

        <UiText tone="muted" size="sm">
          {{ t(field.hintKey) }}
        </UiText>

        <!-- The ramp the storefront will derive, lightest to darkest, the picked colour in the middle. -->
        <UiStack direction="row" gap="none" class="overflow-hidden rounded-lg border border-border">
          <UiColorSwatch
            v-for="shade in shades[field.key] ?? []"
            :key="shade.step"
            :color="shade.hex"
            :title="shade.hex"
            class="flex-1"
          />
        </UiStack>

        <!-- What a filled button looks like on a light and a dark surface, with the text the storefront picks. The two grounds are the storefront's fixed white and near-black, not panel tokens, so they stay the same in both panel themes. -->
        <UiStack direction="row" align="center" wrap>
          <UiStack direction="row" align="center" gap="sm" class="rounded-lg bg-white p-2">
            <UiColorSwatch
              :color="effective(field)"
              :text-color="bestTextOn(effective(field))"
              :label="t('settings.brandColors.previewButton')"
            />
            <UiColorSwatch :text-color="shadeAt(field, LIGHT_SURFACE_SHADE)" :label="t('settings.brandColors.previewLink')" />
          </UiStack>
          <UiStack direction="row" align="center" gap="sm" class="rounded-lg bg-neutral-950 p-2">
            <UiColorSwatch
              :color="effective(field)"
              :text-color="bestTextOn(effective(field))"
              :label="t('settings.brandColors.previewButton')"
            />
            <UiColorSwatch :text-color="shadeAt(field, DARK_SURFACE_SHADE)" :label="t('settings.brandColors.previewLink')" />
          </UiStack>
        </UiStack>

        <UiText
          :tone="contrast(field).passes ? 'success' : 'warning'"
          size="sm"
          :data-testid="`brand-colors-contrast-${field.testid}`"
        >
          {{ t(contrastKey(field), { ratio: contrast(field).ratio }) }}
        </UiText>
      </UiStack>
    </template>
  </SettingsSection>
</template>
