<script setup lang="ts">
/**
 * World map with one dot per city that has active clients.
 *
 * Draws the country outlines from the map package and the city markers on top, sized by client
 * count. Hovering or focusing a dot names the city under the map; clicking it emits its country.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import World from '@svg-maps/world'
import UiCard from '@/components/ui/UiCard.vue'
import UiText from '@/components/ui/UiText.vue'
import { reportCountryName } from '@/lib/reportsCountry'
import type { ReportCityRow } from '@/types/reportcountry'

const { t, locale } = useI18n()

/** Props for WorldMap. */
const props = defineProps<{
  /** Cities with their country (ISO-2) and client count. */
  cityData: ReportCityRow[]
}>()

const emit = defineEmits<{
  /** A city dot was clicked; carries its ISO-2 country code. */
  'select-country': [country: string]
}>()

/** A city that has a place on the map. */
interface CityMarker {
  /** City name. */
  city: string
  /** ISO-2 country code. */
  country: string
  /** Active clients. */
  count: number
  /** Horizontal position in map units. */
  x: number
  /** Vertical position in map units. */
  y: number
}

/** The marker under the pointer, if any. */
const hovered = ref<CityMarker | null>(null)

/** Client count of the busiest city, so dot sizes are relative. */
const maxCity = computed<number>(() => Math.max(1, ...props.cityData.map(c => c.clientCount)))

/**
 * Position of each known city on the map, keyed `City|ISO2`.
 *
 * The map's viewBox is "0 0 1010 666". Positions were calibrated against the outlines of small
 * countries: svgX = 2.82 * lon + 472.25, svgY = -3.31 * lat + 463.68.
 */
const cityCoords: Record<string, [number, number]> = {
  'New York|US': [264, 329], 'San Francisco|US': [127, 338], 'Los Angeles|US': [139, 351],
  'Chicago|US': [225, 325], 'Austin|US': [197, 363], 'Seattle|US': [127, 306],
  'Miami|US': [246, 378], 'Houston|US': [203, 365], 'Boston|US': [272, 323],
  'Denver|US': [176, 332], 'Toronto|CA': [248, 319], 'Vancouver|CA': [125, 300],
  'Montreal|CA': [265, 313],
  'London|GB': [472, 293], 'Paris|FR': [479, 302], 'Berlin|DE': [510, 290],
  'Madrid|ES': [462, 330], 'Rome|IT': [507, 325], 'Amsterdam|NL': [486, 290],
  'Stockholm|SE': [523, 267], 'Oslo|NO': [503, 265],
  'Helsinki|FI': [543, 264], 'Warsaw|PL': [531, 291], 'Prague|CZ': [513, 298],
  'Dublin|IE': [455, 287], 'Lisbon|PT': [446, 335], 'Brussels|BE': [485, 295],
  'Copenhagen|DK': [508, 279],
  'Yerevan|AM': [598, 330], 'Moscow|RU': [578, 279], 'Istanbul|TR': [554, 328],
  'Dubai|AE': [628, 380], 'Seoul|KR': [830, 339], 'Tokyo|JP': [866, 345],
  'Beijing|CN': [800, 331], 'Shanghai|CN': [815, 360], 'Mumbai|IN': [678, 400],
  'Delhi|IN': [690, 369], 'Bangalore|IN': [691, 421], 'Singapore|SG': [765, 459],
  'Bangkok|TH': [756, 418], 'Hong Kong|CN': [794, 390],
  'Tel Aviv|IL': [570, 357], 'Tbilisi|GE': [599, 325],
  'Sydney|AU': [899, 576], 'Melbourne|AU': [881, 589], 'Auckland|NZ': [965, 586],
  'São Paulo|BR': [341, 542], 'Buenos Aires|AR': [308, 578],
  'Santiago|CL': [273, 575], 'Lima|PE': [255, 504], 'Bogotá|CO': [263, 448],
  'Cairo|EG': [560, 364], 'Cape Town|ZA': [524, 576], 'Lagos|NG': [482, 442],
  'Nairobi|KE': [576, 468], 'Gangnam-gu|KR': [830, 339],
}

/** Markers for the cities the map knows a position for; others are left off. */
const markers = computed<CityMarker[]>(() =>
  props.cityData.flatMap(c => {
    const coords = cityCoords[`${c.city}|${c.country}`]
    return coords ? [{ city: c.city, country: c.country, count: c.clientCount, x: coords[0], y: coords[1] }] : []
  }),
)

/**
 * Radius of a city dot, growing with its share of the busiest city.
 *
 * @param marker - The city marker.
 * @returns Radius in map units.
 */
const radius = (marker: CityMarker): number => 3 + (marker.count / maxCity.value) * 3

/** Caption under the map: the hovered city, or a hint to hover one. */
const caption = computed<string>(() => {
  const marker = hovered.value
  if (marker === null) {
    return t('reports.clientsByCountry.mapHint')
  }
  const clients = t('reports.common.clientCount', { count: marker.count }, marker.count)
  return `${marker.city}, ${reportCountryName(marker.country, locale.value)}: ${clients}`
})
</script>

<template>
  <UiCard padding="md" data-testid="report-world-map">
    <svg
      :viewBox="World.viewBox"
      class="w-full aspect-[1010/666]"
      role="img"
      :aria-label="t('reports.clientsByCountry.clientsByCountry')"
    >
      <path
        v-for="loc in World.locations"
        :key="loc.id"
        :d="loc.path"
        class="fill-border stroke-surface-card"
        stroke-width="0.3"
      />
      <g v-for="marker in markers" :key="marker.city + marker.country">
        <circle
          :cx="marker.x"
          :cy="marker.y"
          r="8"
          class="fill-transparent cursor-pointer"
          :data-testid="`report-map-city-${marker.city}`"
          @mouseenter="hovered = marker"
          @mouseleave="hovered = null"
          @click="emit('select-country', marker.country)"
        />
        <circle
          :cx="marker.x"
          :cy="marker.y"
          :r="radius(marker)"
          class="fill-status-green pointer-events-none"
        />
      </g>
    </svg>
    <UiText tone="secondary" size="sm" class="mt-3 text-center" data-testid="report-map-caption">
      {{ caption }}
    </UiText>
  </UiCard>
</template>
