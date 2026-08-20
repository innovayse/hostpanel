<template>
  <section v-if="testimonials.length" class="border-b border-nova-border bg-nova-surface py-16 lg:py-24">
    <div class="mx-auto max-w-[1240px] px-4 sm:px-6 lg:px-8">
      <p class="text-[13px] font-semibold uppercase tracking-[0.1em] text-nova-brand">
        {{ t('nova.testimonials.eyebrow') }}
      </p>
      <h2 class="mt-3 text-[clamp(1.6rem,3.4vw,2.4rem)] font-bold tracking-tight text-nova-ink">
        {{ t('nova.testimonials.title') }}
      </h2>

      <ul class="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
        <li
          v-for="testimonial in testimonials"
          :key="testimonial.key"
          class="flex h-full flex-col rounded-2xl border border-nova-border bg-nova-bg p-6"
        >
          <figure class="flex h-full flex-col">
            <Icon name="lucide:quote" class="h-6 w-6 text-nova-brand" aria-hidden="true" />
            <blockquote class="mt-4 flex-1 text-[15px] leading-[1.7] text-nova-ink">
              {{ testimonial.quote }}
            </blockquote>
            <figcaption class="mt-5 border-t border-nova-border pt-4 text-[15px]">
              <span class="font-semibold text-nova-ink">{{ testimonial.name }}</span>
              <span v-if="testimonial.role" class="block text-sm text-nova-muted">{{ testimonial.role }}</span>
            </figcaption>
          </figure>
        </li>
      </ul>
    </div>
  </section>
</template>

<script setup lang="ts">
/**
 * nova testimonials.
 *
 * No endpoint serves customer quotes, so `TESTIMONIALS` is empty and this
 * section renders nothing at all — not a placeholder, and certainly not an
 * invented name. The markup is here so that when a real source appears, the
 * only thing needed is the data.
 */
import { TESTIMONIALS } from '~/templates/nova/content'
import type { Testimonial } from '~/templates/nova/types'

const props = withDefaults(defineProps<{
  /** Overrides the compiled-in list; both are empty until a source exists. */
  items?: readonly Testimonial[]
}>(), { items: undefined })

const { t } = useI18n()

const testimonials = computed(() => props.items ?? TESTIMONIALS)
</script>
