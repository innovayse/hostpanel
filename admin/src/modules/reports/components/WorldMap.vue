<script setup lang="ts">
/**
 * SVG world map using @svg-maps/world with city dot markers only (no country fill).
 * Hovering a city shows a styled tooltip. Clicking emits the country code.
 */
import { ref, computed } from 'vue'
import World from '@svg-maps/world'

const props = defineProps<{
  /** City data: array of { city, country (ISO-2), clientCount }. */
  cityData: { city: string; country: string; clientCount: number }[]
}>()

const emit = defineEmits<{
  'select-country': [country: string]
}>()

// Tooltip state
const tooltip = ref<{ show: boolean; x: number; y: number; city: string; country: string; count: number }>({
  show: false, x: 0, y: 0, city: '', country: '', count: 0,
})
const svgRef = ref<SVGSVGElement | null>(null)

const maxCity = computed(() => Math.max(1, ...props.cityData.map(c => c.clientCount)))

const countryNames: Record<string, string> = {
  US: 'United States', GB: 'United Kingdom', AM: 'Armenia', ES: 'Spain',
  KR: 'South Korea', FR: 'France', AU: 'Australia', DE: 'Germany',
  CA: 'Canada', NL: 'Netherlands', JP: 'Japan', BR: 'Brazil',
  IN: 'India', CN: 'China', RU: 'Russia', IT: 'Italy', TR: 'Turkey',
  SA: 'Saudi Arabia', AE: 'UAE', EG: 'Egypt', ZA: 'South Africa',
}

/**
 * City → SVG coordinates.
 * @svg-maps/world viewBox is "0 0 1010 666".
 */
// Calibrated from small-country SVG path starts as ground truth
// svgX = 2.82 * lon + 472.25, svgY = -3.31 * lat + 463.68
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

interface CityMarker { city: string; country: string; count: number; x: number; y: number; r: number }

const markers = computed<CityMarker[]>(() => {
  return props.cityData
    .map(c => {
      const key = `${c.city}|${c.country}`
      const coords = cityCoords[key]
      if (!coords) return null
      const ratio = c.clientCount / maxCity.value
      return { city: c.city, country: c.country, count: c.clientCount, x: coords[0], y: coords[1], r: 5 + ratio * 12 }
    })
    .filter((m): m is CityMarker => m !== null)
})

function showTooltip(m: CityMarker, event: MouseEvent) {
  if (!svgRef.value) return
  const rect = svgRef.value.getBoundingClientRect()
  tooltip.value = {
    show: true,
    x: event.clientX - rect.left,
    y: event.clientY - rect.top - 10,
    city: m.city,
    country: m.country,
    count: m.count,
  }
}

function hideTooltip() {
  tooltip.value.show = false
}

function clickCity(m: CityMarker) {
  emit('select-country', m.country)
}
</script>

<template>
  <div class="w-full rounded-2xl bg-[#0c1219] p-5 overflow-hidden relative">
    <svg ref="svgRef" :viewBox="World.viewBox" class="w-full" style="aspect-ratio: 1010/666;">
      <!-- Country shapes — all grey, no fill highlight -->
      <path
        v-for="loc in World.locations"
        :key="loc.id"
        :d="loc.path"
        fill="#1a2535"
        stroke="#0f1923"
        stroke-width="0.3"
      />

      <!-- City markers — dot only -->
      <template v-for="m in markers" :key="m.city + m.country">
        <!-- Invisible larger hit area for hover -->
        <circle
          :cx="m.x" :cy="m.y" r="8"
          fill="transparent"
          class="cursor-pointer"
          @mouseenter="showTooltip(m, $event)"
          @mousemove="showTooltip(m, $event)"
          @mouseleave="hideTooltip"
          @click="clickCity(m)"
        />
        <!-- Green dot -->
        <circle
          :cx="m.x" :cy="m.y" :r="3 + (m.count / maxCity) * 3"
          fill="#22c55e" fill-opacity="0.9"
          class="pointer-events-none"
        />
      </template>
    </svg>

    <!-- Tooltip -->
    <Transition
      enter-active-class="transition duration-100 ease-out"
      enter-from-class="opacity-0 scale-95"
      enter-to-class="opacity-100 scale-100"
      leave-active-class="transition duration-75 ease-in"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0"
    >
      <div
        v-if="tooltip.show"
        class="absolute pointer-events-none z-50 bg-[#1a2535] border border-[#2a3f55] rounded-xl px-4 py-3 shadow-2xl"
        :style="{ left: tooltip.x + 'px', top: tooltip.y + 'px', transform: 'translate(-50%, -100%)' }"
      >
        <div class="flex items-center gap-2 mb-1">
          <span class="w-2 h-2 rounded-full bg-[#22c55e]" />
          <span class="text-[0.82rem] font-semibold text-white">{{ tooltip.city }}</span>
        </div>
        <div class="text-[0.72rem] text-[#64748b]">{{ countryNames[tooltip.country] ?? tooltip.country }}</div>
        <div class="mt-1.5 flex items-baseline gap-1.5">
          <span class="text-[1.1rem] font-bold text-[#22c55e]">{{ tooltip.count }}</span>
          <span class="text-[0.68rem] text-[#64748b]">{{ tooltip.count === 1 ? 'client' : 'clients' }}</span>
        </div>
      </div>
    </Transition>
  </div>
</template>
