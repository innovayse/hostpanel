<template>
  <section id="faq" class="relative border-t border-line px-[clamp(20px,5vw,48px)] py-[clamp(56px,8vw,84px)]">
    <div class="grid gap-[clamp(32px,4vw,56px)] [grid-template-columns:repeat(auto-fit,minmax(min(100%,300px),1fr))]">
      <h2 class="font-display text-[clamp(25px,3.6vw,36px)] font-bold -tracking-[0.02em] text-tx">
        {{ t('aurora.faq.title') }}
      </h2>

      <div class="flex flex-col">
        <div v-for="(item, i) in items" :key="item.q" class="border-t border-line">
          <button
            type="button"
            class="flex w-full items-center justify-between gap-5 py-5 text-left"
            :aria-expanded="open === i"
            @click="open = open === i ? -1 : i"
          >
            <span class="text-[18px] font-semibold text-tx">{{ item.q }}</span>
            <span class="flex-shrink-0 text-[22px] text-ac1">{{ open === i ? '−' : '+' }}</span>
          </button>
          <!--
            Kept in the DOM rather than v-if'd away: this is indexable content,
            and a crawler should read every answer without running the toggle.
          -->
          <div
            class="overflow-hidden text-base leading-relaxed text-mut transition-all"
            :class="open === i ? 'max-h-96 pb-5' : 'max-h-0'"
          >{{ item.a }}</div>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
/** aurora FAQ accordion. Answers stay in the DOM so they remain indexable. */
interface FaqItem { q: string, a: string }

const { t, tm, rt } = useI18n()

const items = computed<FaqItem[]>(() => {
  const raw = tm('aurora.faq.items') as unknown[]
  if (!Array.isArray(raw)) return []
  return raw.map((entry) => {
    const row = entry as Record<string, unknown>
    return { q: rt(row.q as string), a: rt(row.a as string) }
  })
})

const open = ref(0)
</script>
