<template>
  <div v-if="apps.length > 0" ref="rootEl" class="relative">
    <button
      type="button"
      :title="t('aurora.apps.title')"
      :aria-expanded="open"
      aria-haspopup="menu"
      class="grid h-10 w-10 place-items-center rounded-[11px] border border-line2 text-tx2"
      :class="open ? 'bg-[rgba(125,227,255,0.12)]' : 'bg-transparent'"
      @click="open = !open"
    >
      <span class="grid grid-cols-[repeat(3,4px)] gap-[3px]" aria-hidden="true">
        <span v-for="dot in 9" :key="dot" class="h-1 w-1 rounded-[1px] bg-current" />
      </span>
    </button>

    <div
      v-if="open"
      role="menu"
      class="fixed right-[clamp(16px,4vw,48px)] top-[74px] z-40 flex w-[min(520px,calc(100vw-32px))] flex-col rounded-[18px] border border-line2 bg-panel p-2.5 shadow-panel"
    >
      <div class="grid grid-cols-[repeat(auto-fit,minmax(150px,1fr))] gap-1">
        <a
          v-for="app in apps"
          :key="app.id"
          :href="app.url"
          role="menuitem"
          class="flex min-w-0 flex-col gap-2 rounded-[13px] px-3 py-[13px] text-tx hover:bg-[rgba(125,227,255,0.09)]"
          @click="open = false"
        >
          <!-- The tile gradient is per-app data, so it cannot come from a class. -->
          <span
            class="grid h-8 w-8 place-items-center rounded-[9px] font-mono text-[13px] font-medium text-[#08090F]"
            :style="{ background: app.tint }"
          >{{ app.tag }}</span>
          <span class="text-sm font-semibold">{{ app.label }}</span>
          <span class="text-xs leading-[1.4] text-mut2">{{ app.desc }}</span>
        </a>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * aurora app launcher.
 *
 * Renders nothing at all unless the deployment has apps to offer, so the header
 * of a stock install is unchanged. The design's "Manage all services" footer
 * link is left out: it points at `#` in the design, and there is no such page.
 */
const { t } = useI18n()
const { apps } = usePortalApps()

const open = ref(false)
const rootEl = ref<HTMLElement | null>(null)

/** Closes the panel when the visitor clicks anywhere outside it. */
const onClickOutside = (event: MouseEvent) => {
  if (open.value && rootEl.value && !rootEl.value.contains(event.target as Node)) open.value = false
}

const onEscape = (event: KeyboardEvent) => {
  if (event.key === 'Escape') open.value = false
}

onMounted(() => {
  document.addEventListener('click', onClickOutside)
  document.addEventListener('keydown', onEscape)
})

onBeforeUnmount(() => {
  document.removeEventListener('click', onClickOutside)
  document.removeEventListener('keydown', onEscape)
})
</script>
