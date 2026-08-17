<template>
  <section class="relative border-t border-line px-[clamp(20px,5vw,48px)] py-[clamp(56px,8vw,84px)]">
    <div class="flex flex-wrap items-center justify-between gap-6">
      <div>
        <div class="text-[13px] uppercase tracking-[0.12em] text-ac3">{{ t('aurora.testimonials.eyebrow') }}</div>
        <h2 class="mt-3 font-display text-[clamp(27px,4vw,40px)] font-bold -tracking-[0.02em] text-tx">
          {{ t('aurora.testimonials.title') }}
        </h2>
      </div>
      <div class="flex gap-2.5">
        <button
          type="button"
          :aria-label="t('aurora.testimonials.prev')"
          class="grid h-[46px] w-[46px] place-items-center rounded-full border border-line2 text-tx hover:border-ac1 hover:text-ac1"
          @click="go(-1)"
        >←</button>
        <button
          type="button"
          :aria-label="t('aurora.testimonials.next')"
          class="grid h-[46px] w-[46px] place-items-center rounded-full border border-line2 text-tx hover:border-ac1 hover:text-ac1"
          @click="go(1)"
        >→</button>
      </div>
    </div>

    <div
      v-if="current"
      class="mt-9 min-h-[260px] rounded-[22px] border border-line2 bg-card-hi p-[clamp(24px,3.5vw,44px)]"
    >
      <blockquote class="max-w-[900px] font-display text-[clamp(19px,2.4vw,26px)] leading-snug -tracking-[0.01em] text-tx">
        «{{ current.quote }}»
      </blockquote>
      <div class="mt-[30px] flex items-center gap-3.5">
        <div class="grid h-11 w-11 place-items-center rounded-full bg-brand font-bold text-[#08090F]">
          {{ current.author.charAt(0) }}
        </div>
        <div>
          <div class="text-base font-semibold text-tx">{{ current.author }}</div>
          <div class="text-sm text-mut2">{{ current.role }}</div>
        </div>
      </div>
      <div class="mt-[30px] flex gap-1.5">
        <button
          v-for="(item, i) in items"
          :key="item.author"
          type="button"
          :aria-label="item.author"
          class="h-1.5 rounded-full transition-all"
          :class="i === index ? 'w-[34px] bg-ac1' : 'w-3 bg-line2'"
          @click="index = i"
        />
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
/** aurora testimonials carousel. Quotes are editorial copy — no API exists for them. */
interface Testimonial { quote: string, author: string, role: string }

const { t, tm, rt } = useI18n()

const items = computed<Testimonial[]>(() => {
  const raw = tm('aurora.testimonials.items') as unknown[]
  if (!Array.isArray(raw)) return []
  return raw.map((entry) => {
    const row = entry as Record<string, unknown>
    return {
      quote: rt(row.quote as string),
      author: rt(row.author as string),
      role: rt(row.role as string),
    }
  })
})

const index = ref(0)
const current = computed(() => items.value[index.value])

/**
 * Moves the carousel, wrapping at both ends.
 *
 * @param step Positions to advance; negative moves back.
 */
const go = (step: number) => {
  const total = items.value.length
  if (!total) return
  index.value = (index.value + step + total) % total
}
</script>
