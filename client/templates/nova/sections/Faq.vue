<template>
  <section v-if="FAQ_ITEMS.length" class="border-b border-nova-border py-16 lg:py-24">
    <div class="mx-auto max-w-[840px] px-4 sm:px-6 lg:px-8">
      <p class="text-[13px] font-semibold uppercase tracking-[0.1em] text-nova-brand">
        {{ t('nova.faq.eyebrow') }}
      </p>
      <h2 class="mt-3 text-[clamp(1.6rem,3.4vw,2.4rem)] font-bold tracking-tight text-nova-ink">
        {{ t('nova.faq.title') }}
      </h2>

      <div class="mt-9 flex flex-col gap-3">
        <details
          v-for="item in FAQ_ITEMS"
          :key="item.key"
          class="group rounded-2xl border border-nova-border bg-nova-surface px-5"
        >
          <summary
            class="flex min-h-[56px] cursor-pointer list-none items-center justify-between gap-4 py-4 text-[16px] font-semibold text-nova-ink marker:content-none focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-nova-brand"
          >
            <h3 class="min-w-0 text-[16px] font-semibold">{{ t(item.questionKey) }}</h3>
            <Icon
              name="lucide:chevron-down"
              class="h-5 w-5 shrink-0 text-nova-muted transition-transform group-open:rotate-180"
              aria-hidden="true"
            />
          </summary>
          <p class="pb-5 text-[15px] leading-[1.7] text-nova-muted">{{ t(item.answerKey) }}</p>
        </details>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
/**
 * nova FAQ accordion.
 *
 * Built on <details>/<summary> rather than on JavaScript state. The element is
 * already operable by keyboard, already announces its expanded state, and
 * already works before hydration — three things a hand-rolled accordion has to
 * reimplement and usually gets partly wrong. The chevron is the only thing
 * scripted, and it is scripted in CSS.
 */
import { FAQ_ITEMS } from '~/templates/nova/content'

const { t } = useI18n()
</script>
