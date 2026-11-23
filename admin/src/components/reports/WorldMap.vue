<script setup lang="ts">
/**
 * World choropleth: every country with active clients is filled within its borders.
 *
 * Draws the country outlines from the map package and fills each one by its client count on a
 * tone scale; countries without data stay neutral. Hovering or focusing a country highlights it
 * and shows its name and clients in a tooltip; clicking it (or Enter / Space) emits its code.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import World from '@svg-maps/world'
import UiCard from '@/components/ui/UiCard.vue'
import UiLegendSwatch from '@/components/ui/UiLegendSwatch.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiSvgMap from '@/components/ui/UiSvgMap.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTooltip from '@/components/ui/UiTooltip.vue'
import { reportCountryName } from '@/lib/reportsCountry'
import { MAP_ACTIVE_FILL, MAP_FILL_STEPS, MAP_NO_DATA_FILL, MAP_NO_DATA_SWATCH, mapFillClass } from '@/lib/reports'
import { UiLegendSwatchShape } from '@/utils/ui/svgMap'
import type { UiMapShape } from '@/utils/ui/svgMap'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import type { ReportCountryRow } from '@/types/reportcountry'
import { UiCardPadding } from '@/utils/ui/card'
import { UiAlignItems, UiDirection, UiGap, UiJustify } from '@/utils/ui/layout'

const { t, locale } = useI18n()

/** Props for WorldMap. */
const props = defineProps<{
  /** Countries (ISO-2) with their client count. */
  countryData: ReportCountryRow[]
}>()

const emit = defineEmits<{
  /** A country was chosen; carries its ISO-2 code. */
  'select-country': [country: string]
}>()

/** A country that has data on the map. */
interface MapCountry {
  /** Upper-case ISO-2 code. */
  code: string
  /** SVG outline. */
  path: string
  /** Name and clients, for the tooltip and the accessible label. */
  label: string
  /** Fill class on the scale. */
  fill: string
}

/** Upper-case ISO-2 code of the country under the pointer or focus, if any. */
const active = ref<string | null>(null)

/** Client count per upper-case ISO-2 code; rows with no resolved country are left out. */
const counts = computed<Map<string, number>>(() => {
  const map = new Map<string, number>()
  for (const row of props.countryData) {
    if (row.country !== null) {
      const code = row.country.toUpperCase()
      map.set(code, (map.get(code) ?? 0) + row.clientCount)
    }
  }
  return map
})

/** Client count of the busiest country, the top of the scale. */
const maxCount = computed<number>(() => Math.max(0, ...counts.value.values()))

/** Outlines of the countries with no data, drawn first. */
const emptyPaths = computed<{ id: string; path: string }[]>(() =>
  World.locations.filter(loc => !counts.value.has(loc.id.toUpperCase())).map(loc => ({ id: loc.id, path: loc.path })),
)

/** Countries with data, drawn over the neutral ones so a highlight is never cut by a neighbour. */
const dataCountries = computed<MapCountry[]>(() =>
  World.locations.flatMap(loc => {
    const code = loc.id.toUpperCase()
    const count = counts.value.get(code)
    if (count === undefined) {
      return []
    }
    const label = `${reportCountryName(code, locale.value)}: ${t('reports.common.clientCount', { count }, count)}`
    return [{ code, path: loc.path, label, fill: mapFillClass(count, maxCount.value) }]
  }),
)

/** The country the tooltip describes. */
const activeCountry = computed<MapCountry | null>(() => dataCountries.value.find(c => c.code === active.value) ?? null)

/**
 * Fill class of a country: the highlight when it is active, else its scale tone.
 *
 * @param country - The map country.
 * @returns The Tailwind fill class.
 */
const fillOf = (country: MapCountry): string => (country.code === active.value ? MAP_ACTIVE_FILL : country.fill)

/**
 * Marks a country as the one under the pointer or focus.
 *
 * @param code - Its ISO-2 code.
 */
const activate = (code: string): void => {
  active.value = code
}

/** Clears the highlight. */
const deactivate = (): void => {
  active.value = null
}

/** Every outline the map draws: the neutral ones first, then the countries with data over them (each filled by its own state). */
const shapes = computed<UiMapShape[]>(() => [
  ...emptyPaths.value.map(empty => ({ id: empty.id, path: empty.path, fill: MAP_NO_DATA_FILL })),
  ...dataCountries.value.map(country => ({
    id: country.code,
    path: country.path,
    fill: fillOf(country),
    label: country.label,
    testid: `report-map-country-${country.code}`,
  })),
])
</script>

<template>
  <UiCard :padding="UiCardPadding.Md" data-testid="report-world-map">
    <UiTooltip class="w-full" :text="activeCountry?.label ?? ''" :open="activeCountry !== null">
      <UiSvgMap
        :view-box="World.viewBox"
        :label="t('reports.clientsByCountry.clientsByCountry')"
        :shapes="shapes"
        @activate="activate"
        @deactivate="deactivate"
        @select="emit('select-country', $event)"
      />
    </UiTooltip>
    <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :justify="UiJustify.Center" :gap="UiGap.Md" wrap role="list" class="mt-3 gap-y-1" data-testid="report-map-legend">
      <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Xs" role="listitem">
        <UiLegendSwatch :fill="MAP_NO_DATA_SWATCH" />
        <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Sm">{{ t('reports.clientsByCountry.legendNoData') }}</UiText>
      </UiStack>
      <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Xs" role="listitem">
        <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Sm">{{ t('reports.clientsByCountry.legendFewer') }}</UiText>
        <UiStack :direction="UiDirection.Row" :gap="UiGap.None" aria-hidden="true">
          <UiLegendSwatch v-for="step in MAP_FILL_STEPS" :key="step.swatch" :fill="step.swatch" :shape="UiLegendSwatchShape.Step" />
        </UiStack>
        <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Sm">{{ t('reports.clientsByCountry.legendMore', { count: maxCount }) }}</UiText>
      </UiStack>
    </UiStack>
  </UiCard>
</template>
