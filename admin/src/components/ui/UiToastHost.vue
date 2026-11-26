<script setup lang="ts">
/**
 * Draws the toasts of the toast store in the bottom right corner of the window, above dialogs. On a phone (below `sm`) they
 * stand bottom centre, the full width minus the gutter and above the home indicator (the safe-area inset).
 *
 * The container is a polite live region that is always rendered, so a screen reader announces a toast the moment its
 * text arrives; a danger toast (a refusal) is an alert of its own, so it is announced at once. Each toast carries a close button; left alone it dismisses itself (see `stores/toast.ts`). Mounted
 * once, by `App.vue`.
 */
import { onBeforeUnmount } from 'vue'
import { useI18n } from 'vue-i18n'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import { Icon } from '@/lib/icons'
import { useToastStore } from '@/stores/toast'
import { UiSize } from '@/utils/ui/size'
import { UiTone, toneAlertClasses } from '@/utils/ui/tone'

const { t } = useI18n()

/** The toasts on screen. */
const store = useToastStore()

// The host lives as long as the app; when it goes, its timers go with it.
onBeforeUnmount(store.clear)
</script>

<template>
  <div
    class="pointer-events-none fixed inset-x-4 bottom-[calc(1rem+env(safe-area-inset-bottom))] z-[60] flex flex-col gap-2 sm:inset-x-auto sm:right-4 sm:bottom-4 sm:w-96"
    role="status"
    aria-live="polite"
    data-testid="ui-toast-host"
  >
    <!-- The tone tints are translucent, so each toast sits on an opaque elevated surface: the page behind never shows through the text. -->
    <div v-for="toast in store.toasts" :key="toast.id" class="pointer-events-auto rounded-card bg-surface-elevated shadow-popover">
      <div
        class="flex items-center gap-3 rounded-card border py-2 pl-4 pr-2 text-sm"
        :class="toneAlertClasses[toast.tone]"
        :role="toast.tone === UiTone.Danger ? 'alert' : undefined"
        :data-tone="toast.tone"
        data-testid="ui-toast"
      >
        <span class="min-w-0 flex-1 text-text-primary">{{ toast.message }}</span>
        <UiIconButton :label="t('shared.close')" :size="UiSize.Sm" data-testid="ui-toast-close" @click="store.dismiss(toast.id)">
          <UiIcon :markup="Icon.Close" :size="UiSize.Sm" />
        </UiIconButton>
      </div>
    </div>
  </div>
</template>
