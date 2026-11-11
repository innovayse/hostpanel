<template>
  <!-- Nothing to offer: no currencies loaded yet, or a guest with a single enabled currency. -->
  <div v-if="visible" ref="wrapperRef" class="relative" data-testid="currency-switcher">
    <!-- A signed-in client's currency is fixed on their record: shown read-only with a hint. -->
    <UiTooltip v-if="isFixed">
      <button
        type="button"
        data-testid="currency-switcher-fixed"
        :aria-label="$t('ui.currencyFixed')"
        :title="$t('ui.currencyFixed')"
        :class="triggerClasses"
      >
        <span class="text-sm font-medium text-gray-900 dark:text-white" data-testid="currency-switcher-current">{{ currentCode }}</span>
        <Lock :size="12" :stroke-width="2" class="text-gray-500 dark:text-gray-400" />
      </button>
      <template #content>
        <span data-testid="currency-switcher-hint">{{ $t('ui.currencyFixed') }}</span>
      </template>
    </UiTooltip>

    <!-- A guest picks from the enabled billing currencies. -->
    <template v-else>
      <button
        type="button"
        data-testid="currency-switcher-trigger"
        :aria-label="$t('ui.currency')"
        :aria-expanded="isOpen"
        :class="triggerClasses"
        @click="isOpen = !isOpen"
      >
        <span class="text-sm font-medium text-gray-900 dark:text-white" data-testid="currency-switcher-current">{{ currentCode }}</span>
        <ChevronDown :size="14" :stroke-width="2" class="text-gray-500 dark:text-gray-400 transition-transform duration-200" :class="isOpen ? 'rotate-180' : ''" />
      </button>

      <Transition
        enter-active-class="transition duration-150 ease-out"
        enter-from-class="opacity-0 scale-95"
        enter-to-class="opacity-100 scale-100"
        leave-active-class="transition duration-100 ease-in"
        leave-from-class="opacity-100 scale-100"
        leave-to-class="opacity-0 scale-95"
      >
        <div v-if="isOpen" role="menu" :class="menuClasses">
          <button
            v-for="option in options"
            :key="option.code"
            type="button"
            role="menuitem"
            :data-testid="`currency-option-${option.code}`"
            class="w-full flex items-center gap-3 px-4 py-2.5 text-sm transition-colors duration-150 focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary-500"
            :class="option.code === currentCode
              ? 'bg-primary-100 dark:bg-primary-500/25 text-primary-700 dark:text-primary-300 font-medium'
              : 'text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-white/10 hover:text-gray-900 dark:hover:text-white'"
            @click="choose(option.code)"
          >
            <span class="flex-1 text-left">{{ option.code }}</span>
            <span class="text-gray-500 dark:text-gray-400">{{ option.symbol }}</span>
            <Check v-if="option.code === currentCode" :size="14" :stroke-width="2.5" class="text-primary-500 dark:text-primary-400 flex-shrink-0" />
          </button>
        </div>
      </Transition>
    </template>
  </div>
</template>

<script setup lang="ts">
/**
 * CurrencySwitcher (`UiCurrencySwitcher`) - the site-wide billing-currency picker.
 *
 * Reads the enabled billing currencies from the currency store (`public/billing-currencies`) and
 * writes the choice back through it, so the selection persists in the store's own localStorage key
 * and every page that watches the payer's currency (home, hosting, products, domains) reads its
 * prices again in the new one. A signed-in client whose currency is fixed on their record gets the
 * same chip read-only, with a hint explaining why: the backend prices them in that currency
 * whatever is requested.
 */
import { computed, onMounted, ref } from 'vue'
import { Check, ChevronDown, Lock } from 'lucide-vue-next'
import { onClickOutside } from '@vueuse/core'
import { useCurrencyStore } from '~/stores/currency'
import { UiMenuAlign, UiPlacement } from '~/utils/ui/enums'
import { menuAlignClasses } from '~/utils/ui/menuAlign'
import { FOCUS_RING, PICKER_TRIGGER } from '~/utils/ui/tones'

/** Props of CurrencySwitcher. */
interface Props {
  /** Whether the menu opens below or above the trigger. */
  placement?: `${UiPlacement}`
  /** Edge of the trigger the menu lines up with: `end` (default) for the right end of a header. */
  align?: `${UiMenuAlign}`
  /** Replaces the default tinted frame of the trigger (shape, spacing and colours); focus and display stay. */
  triggerClass?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  placement: UiPlacement.Bottom,
  align: UiMenuAlign.End,
  triggerClass: undefined,
})

/** The currency store: enabled currencies, the payer's currency and the guest's selection. */
const currencyStore = useCurrencyStore()

/** Whether the menu is showing. */
const isOpen = ref(false)

/** The root element, used to detect outside clicks. */
const wrapperRef = ref<HTMLElement | null>(null)

onClickOutside(wrapperRef, () => {
  isOpen.value = false
})

// The selection lives in localStorage, which only the browser can read, so restore and load on mount.
onMounted(() => {
  currencyStore.init()
  void currencyStore.load()
})

/** True when the signed-in client's currency is fixed and the chip is read-only. */
const isFixed = computed(() => currencyStore.isFixed)

/** The code the visitor is priced in, empty before it resolves. */
const currentCode = computed(() => currencyStore.payerCode ?? '')

/** The currencies offered, each with the symbol printed next to its code. */
const options = computed(() =>
  currencyStore.currencies.map(currency => ({
    code: currency.code,
    symbol: (currency.prefix || currency.suffix).trim(),
  })),
)

/** Whether anything is shown: a fixed client's currency, or a guest with a real choice. */
const visible = computed(() => {
  if (!currentCode.value) {
    return false
  }
  return isFixed.value || options.value.length > 1
})

/** Class list of the trigger chip. */
const triggerClasses = computed(() => [
  'flex',
  props.triggerClass ?? `items-center gap-2 px-3 py-2 rounded-lg ${PICKER_TRIGGER}`,
  FOCUS_RING,
].join(' '))

/** Class list of the menu panel. */
const menuClasses = computed(() => [
  menuAlignClasses(props.align),
  'w-36 rounded-xl border border-gray-200 dark:border-white/15 bg-ui-popover shadow-xl shadow-black/10 dark:shadow-black/40 overflow-hidden py-1 z-50',
  props.placement === UiPlacement.Top ? 'bottom-full mb-2' : 'top-full mt-2',
].join(' '))

/**
 * Chooses a currency for the guest and closes the menu.
 *
 * @param code ISO 4217 code of the chosen currency.
 */
const choose = (code: string): void => {
  currencyStore.select(code)
  isOpen.value = false
}
</script>
