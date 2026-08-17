<template>
  <div
    class="relative mx-[clamp(20px,5vw,48px)] flex flex-wrap items-center justify-between gap-7 rounded-[18px] border border-line bg-surf px-[clamp(18px,3vw,28px)] py-[22px]"
  >
    <div class="flex flex-wrap items-center gap-7">
      <div v-for="item in badges" :key="item.label" class="flex items-center gap-2.5 text-sm text-tx2">
        <Icon :name="item.icon" class="h-[18px] w-[18px]" :class="item.tone" />
        {{ item.label }}
      </div>
    </div>

    <div class="flex items-center gap-2.5">
      <span class="mr-1 text-[13px] text-mut2">{{ t('aurora.trust.paymentLabel') }}</span>
      <span
        v-for="method in payments"
        :key="method"
        class="grid h-[30px] place-items-center rounded-[7px] border border-line2 bg-surf px-3 text-xs font-bold tracking-[0.04em] text-tx2"
      >{{ method }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
/** aurora trust bar — guarantees and accepted payment methods. */
const { t } = useI18n()

const badges = computed(() => [
  { icon: 'lucide:lock', label: t('aurora.trust.ssl'), tone: 'text-ok' },
  { icon: 'lucide:shield-check', label: t('aurora.trust.refund'), tone: 'text-ac1' },
  { icon: 'lucide:badge-percent', label: t('aurora.trust.noHidden'), tone: 'text-ac2' },
])

// Card brands are proper nouns and are not translated.
const payments = ['VISA', 'Mastercard', 'PayPal', 'Idram']
</script>
