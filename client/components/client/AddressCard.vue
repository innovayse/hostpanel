<template>
  <div
    class="flex items-start gap-3 p-3 rounded-xl border transition-colors"
    :class="[
      selected
        ? 'border-cyan-500/40 bg-cyan-500/5'
        : 'border-gray-200 dark:border-white/10',
      readonly ? 'cursor-default' : 'cursor-pointer hover:border-gray-300 dark:hover:border-white/20'
    ]"
    @click="!readonly && $emit('select')"
  >
    <!-- Radio dot -->
    <span
      class="w-5 h-5 rounded-full flex-shrink-0 mt-0.5 flex items-center justify-center transition-colors"
      :class="selected ? 'bg-cyan-500' : 'border-2 border-gray-300 dark:border-white/25'"
    >
      <Check v-if="selected" :size="11" :stroke-width="3" class="text-white" />
    </span>

    <!-- Content -->
    <div class="min-w-0 flex-1">
      <div class="text-sm font-semibold text-gray-900 dark:text-white">
        {{ addr.firstname }} {{ addr.lastname }}
      </div>
      <div
        v-if="addr.companyname && addr.companyname !== `${addr.firstname} ${addr.lastname}`"
        class="text-xs text-gray-400 dark:text-gray-500"
      >{{ addr.companyname }}</div>
      <div class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
        {{ addressLine || $t('client.payment.noAddress') }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Check } from 'lucide-vue-next'

interface Address {
  id: string
  firstname: string
  lastname: string
  companyname?: string
  address1?: string
  address2?: string
  city?: string
  state?: string
  postcode?: string
  country?: string
  countryname?: string
}

const props = defineProps<{
  addr: Address
  selected: boolean
  readonly?: boolean
}>()

defineEmits<{ select: [] }>()

const addressLine = computed(() =>
  [
    props.addr.address1,
    props.addr.address2,
    props.addr.city,
    props.addr.state,
    props.addr.postcode,
    props.addr.countryname || props.addr.country,
  ].filter(Boolean).join(', ')
)
</script>
