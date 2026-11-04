<template>
  <!--
    A full-width band whose content sits in the same UiContainer as the header, so the pill and
    the text line up with the logo instead of starting at the viewport edge.
  -->
  <UiStack
    v-if="demo.enabled"
    gap="none"
    role="status"
    class="w-full border-b py-2"
    :class="tone.wrap"
    data-testid="demo-banner"
  >
    <UiContainer data-testid="demo-banner-container">
      <UiStack direction="row" align="center" gap="none" wrap class="min-w-0 gap-x-3 gap-y-1">
        <UiBadge variant="info" class="shrink-0" data-testid="demo-banner-label">{{ $t('demo.label') }}</UiBadge>
        <UiText v-if="resetText" as="span" size="sm" tone="inherit" :class="tone.title" data-testid="demo-banner-reset">{{ resetText }}</UiText>
        <UiText as="span" size="sm" tone="inherit" class="min-w-0 break-words" :class="tone.body" data-testid="demo-banner-note">{{ $t('demo.note') }}</UiText>
        <UiLink
          v-if="docsUrl"
          :href="`${docsUrl}/customers/portal`"
          external
          class="shrink-0 text-sm font-medium underline"
          :class="tone.title"
          data-testid="demo-banner-guide"
        >{{ $t('demo.guide') }}</UiLink>
      </UiStack>
    </UiContainer>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Banner shown on every page while the deployment is a public demo: a "Demo mode" label, the time
 * left until the next data reset, and the note that data resets. Renders nothing otherwise.
 */
import { useDemoStore } from '~/stores/demo'
import { UiAlertVariant } from '~/utils/ui/enums'
import { ALERT_TONE_CLASSES } from '~/utils/ui/tones'

/** The info palette the banner shares with UiAlert, applied to a full-width band. */
const tone = ALERT_TONE_CLASSES[UiAlertVariant.Info]

/** How often the countdown is recomputed, in milliseconds. */
const TICK_MS = 30_000

/** Milliseconds in one minute. */
const MINUTE_MS = 60_000

/** Minutes in one hour. */
const HOUR_MINUTES = 60

/** Translator for the strings this file builds itself. */
const { t } = useI18n()

/** Documentation URL from runtime config; empty when the docs site is not configured. */
const { public: { docsUrl } } = useRuntimeConfig()

/** Demo store; owns the status the banner renders. */
const demo = useDemoStore()

/** The current time; shared state so server render and hydration agree, advanced on a timer so the countdown moves. */
const now = useState<number>('demo-now', () => Date.now())

/** Handle of the countdown timer, cleared on unmount. */
let timer: ReturnType<typeof setInterval> | undefined

/** Whole minutes left until the next reset, or null when none is scheduled. */
const minutesLeft = computed<number | null>(() => {
  if (!demo.nextResetAt) {
    return null
  }
  const target = Date.parse(demo.nextResetAt)
  if (Number.isNaN(target)) {
    return null
  }
  return Math.max(0, Math.ceil((target - now.value) / MINUTE_MS))
})

/** The sentence about the next reset; empty when no reset is scheduled. */
const resetText = computed<string>(() => {
  const minutes = minutesLeft.value
  if (minutes === null) {
    return ''
  }
  if (minutes < 1) {
    return t('demo.resetsSoon')
  }
  const time = minutes >= HOUR_MINUTES
    ? t('demo.hoursMinutes', { hours: Math.floor(minutes / HOUR_MINUTES), minutes: minutes % HOUR_MINUTES })
    : t('demo.minutes', { minutes })
  return t('demo.resetsIn', { time })
})

onMounted(() => {
  now.value = Date.now()
  timer = setInterval(() => {
    now.value = Date.now()
  }, TICK_MS)
})

onBeforeUnmount(() => {
  if (timer !== undefined) {
    clearInterval(timer)
  }
})
</script>
