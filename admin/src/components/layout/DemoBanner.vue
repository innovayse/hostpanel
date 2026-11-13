<script setup lang="ts">
/**
 * Slim glass strip shown across the full width at the top of the layout, above the sidebar and the main column, while the deployment runs in demo mode: the "Demo mode"
 * label, the time left until the next data reset and, from the small breakpoint up, the note that data is reset.
 *
 * Renders nothing when demo mode is off. The countdown ticks once a minute. The strip has a fixed one-line height, so
 * the shell below it does not shift when the countdown or the guide link appears.
 */
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDemoStore } from '@/stores/demo'
import UiLink from '@/components/ui/UiLink.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { MS_PER_MINUTE } from '@/lib/limits'
import { getRuntimeConfig } from '@/lib/runtimeConfig'

/** Translation function. */
const { t } = useI18n()

/** Documentation URL from runtime config; empty when the docs site is not configured. */
const { docsUrl } = getRuntimeConfig()

/** Demo store: whether demo mode is on and when the data resets. */
const demo = useDemoStore()

/** Current time in epoch milliseconds, refreshed every minute so the countdown moves. */
const now = ref(Date.now())

/** Handle of the countdown timer, cleared on unmount. */
let timer: ReturnType<typeof setInterval> | undefined

/** Starts the minute tick. */
onMounted(() => {
  timer = setInterval(() => {
    now.value = Date.now()
  }, MS_PER_MINUTE)
})

/** Stops the minute tick. */
onBeforeUnmount(() => {
  if (timer !== undefined) {
    clearInterval(timer)
  }
})

/** Whole minutes left until the reset, or null when no reset is scheduled. */
const minutesLeft = computed<number | null>(() => {
  if (demo.nextResetAt === null) {
    return null
  }
  const target = Date.parse(demo.nextResetAt)
  if (Number.isNaN(target)) {
    return null
  }
  return Math.max(0, Math.ceil((target - now.value) / MS_PER_MINUTE))
})

/** Reloads the demo description once the countdown hits zero, so the next reset time replaces the stale one. */
watch(minutesLeft, (minutes) => {
  if (minutes === 0 && demo.enabled) {
    void demo.load(true)
  }
})

/** The countdown as a sentence fragment ("1h 5m", "12m", "less than a minute"), or null. */
const timeLeft = computed<string | null>(() => {
  const minutes = minutesLeft.value
  if (minutes === null) {
    return null
  }
  if (minutes === 0) {
    return t('demo.banner.soon')
  }
  const hours = Math.floor(minutes / 60)
  return hours > 0
    ? t('demo.banner.hoursMinutes', { hours, minutes: minutes % 60 })
    : t('demo.banner.minutes', { minutes })
})
</script>

<template>
  <UiStack
    v-if="demo.enabled"
    direction="row"
    gap="sm"
    align="center"
    class="glass h-9 shrink-0 border-b border-border px-4 lg:px-6"
    data-testid="demo-banner"
  >
    <UiBadge tone="info" small dot data-testid="demo-banner-label">{{ t('demo.banner.label') }}</UiBadge>
    <UiText v-if="timeLeft !== null" tone="primary" size="sm" class="truncate" data-testid="demo-banner-countdown">
      {{ t('demo.banner.resetsIn', { time: timeLeft }) }}
    </UiText>
    <UiStack visible-from="sm" class="min-w-0">
      <UiText size="sm" tone="secondary" class="truncate">{{ t('demo.banner.dataResets') }}</UiText>
    </UiStack>
    <UiLink
      v-if="docsUrl"
      :href="docsUrl"
      external
      class="ml-auto shrink-0 text-xs font-medium underline"
      data-testid="demo-banner-guide"
    >{{ t('demo.banner.guide') }}</UiLink>
  </UiStack>
</template>
