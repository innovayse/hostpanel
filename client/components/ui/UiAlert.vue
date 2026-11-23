<template>
  <div class="flex flex-wrap items-start gap-3 p-4 rounded-xl border" :class="styles.wrap" :role="role">
    <component :is="icon" :size="iconSize" :stroke-width="2" class="flex-shrink-0 mt-0.5" :class="styles.icon" />
    <div class="min-w-0 flex-1 basis-40">
      <p v-if="title" class="font-semibold text-sm" :class="styles.title">{{ title }}</p>
      <div class="text-sm" :class="[styles.body, title ? 'mt-0.5' : '']">
        <slot />
      </div>
    </div>
    <!-- On a phone the action drops under the text, aligned with it, instead of squeezing it -->
    <div v-if="$slots.action" class="max-sm:w-full max-sm:pl-7 sm:shrink-0">
      <slot name="action" />
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * UiAlert: an inline banner for a warning, error, info or success message.
 *
 * Slots: default (message body) and `action` (a trailing button or link).
 *
 * A banner that appears after something happened is a live region: errors and warnings are
 * announced assertively (`role="alert"`), information and success politely (`role="status"`).
 * A banner that is part of the page from its first render is static content, not news: `live`
 * set to false drops the role so a screen reader does not interrupt to read it.
 */
import { computed } from 'vue'
import { AlertTriangle, CheckCircle, Info } from 'lucide-vue-next'
import { UiAlertRole, UiAlertVariant } from '~/utils/ui/enums'
import { ALERT_TONE_CLASSES } from '~/utils/ui/tones'

/** Props of UiAlert. */
interface Props {
  /** What the banner means; picks colours and icon. */
  variant?: `${UiAlertVariant}`
  /** Bold first line above the body. */
  title?: string
  /** Pixel size of the leading icon. */
  iconSize?: number
  /** Whether the banner is a live region; false for content already on the page when it renders. */
  live?: boolean
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  variant: UiAlertVariant.Warning,
  title: undefined,
  iconSize: 18,
  live: true,
})

/** Leading icon per meaning. */
const ICONS = {
  [UiAlertVariant.Warning]: AlertTriangle,
  [UiAlertVariant.Error]: AlertTriangle,
  [UiAlertVariant.Info]: Info,
  [UiAlertVariant.Success]: CheckCircle,
}

/** Colour classes of the chosen meaning. */
const styles = computed(() => ALERT_TONE_CLASSES[props.variant as UiAlertVariant])

/** Live-region role: assertive for errors and warnings, polite otherwise, none for static content. */
const role = computed<UiAlertRole | undefined>(() => {
  if (!props.live) {
    return undefined
  }
  return props.variant === UiAlertVariant.Error || props.variant === UiAlertVariant.Warning ? UiAlertRole.Alert : UiAlertRole.Status
})

/** Icon component of the chosen meaning. */
const icon = computed(() => ICONS[props.variant as UiAlertVariant])
</script>
