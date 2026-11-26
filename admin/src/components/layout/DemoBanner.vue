<script setup lang="ts">
/**
 * Slim glass strip shown across the full width at the top of the layout, above the sidebar and the main column, while the deployment runs in demo mode: the "Demo mode"
 * label, the time left until the next data reset and, from the small breakpoint up, the note that data is reset.
 *
 * Renders nothing when demo mode is off. The countdown ticks once a minute. The strip has a fixed one-line height, so
 * the shell below it does not shift when the countdown or the guide link appears.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { usePoller } from '@/composables/usePoller'
import { useDemoStore } from '@/stores/demo'
import { PollKind } from '@/types/poller'
import UiLink from '@/components/ui/UiLink.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { MS_PER_MINUTE } from '@/lib/limits'
import { getRuntimeConfig } from '@/lib/runtimeConfig'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiBreakpoint, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** Documentation URL from runtime config; empty when the docs site is not configured. */
const { docsUrl } = getRuntimeConfig()

/** Demo store: whether demo mode is on and when the data resets. */
const demo = useDemoStore()

/** Current time in epoch milliseconds, refreshed every minute so the countdown moves. */
const now = ref(Date.now())

/** The shared poller that moves the clock once a minute, pauses with the tab and stops with the banner. */
const clock = usePoller({
  kind: PollKind.DemoCountdown,
  immediate: false,
  /** Refreshes the clock the countdown reads. */
  tick: () => {
    now.value = Date.now()
  },
})

/** Starts the minute tick. */
onMounted(clock.start)

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
    :direction="UiDirection.Row"
    :gap="UiGap.Sm"
    :align="UiAlignItems.Center"
    class="glass h-9 shrink-0 border-b border-border px-4 lg:px-6"
    data-testid="demo-banner"
  >
    <UiBadge :tone="UiTone.Info" small dot data-testid="demo-banner-label">{{ t('demo.banner.label') }}</UiBadge>
    <UiText v-if="timeLeft !== null" :tone="UiTextTone.Primary" :size="UiTextSize.Sm" class="truncate" data-testid="demo-banner-countdown">
      {{ t('demo.banner.resetsIn', { time: timeLeft }) }}
    </UiText>
    <UiStack :visible-from="UiBreakpoint.Sm" class="min-w-0">
      <UiText :size="UiTextSize.Sm" :tone="UiTextTone.Secondary" class="truncate">{{ t('demo.banner.dataResets') }}</UiText>
    </UiStack>
    <UiLink
      v-if="docsUrl"
      :href="docsUrl"
      external
      :variant="UiButtonVariant.Link"
      :size="UiSize.Xs"
      class="ml-auto shrink-0"
      :title="t('demo.banner.guideTitle')"
      :aria-label="t('demo.banner.guideTitle')"
      data-testid="demo-banner-guide"
    >{{ t('demo.banner.guide') }}</UiLink>
  </UiStack>
</template>
