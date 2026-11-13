<template>
  <div class="flex flex-col sm:flex-row gap-3">
    <UiInput
      class="flex-1"
      :size="size"
      :model-value="modelValue"
      :placeholder="placeholder"
      :disabled="loading"
      testid="search-bar-input"
      @update:model-value="$emit('update:modelValue', $event)"
      @keydown.enter="$emit('search')"
    />
    <UiButton
      :size="size"
      class="gap-2"
      :disabled="loading"
      :loading="loading"
      data-testid="search-bar-submit"
      @click="$emit('search')"
    >
      <Search v-if="!loading" :size="size === UiSize.Lg ? 20 : 18" :stroke-width="2" />
      {{ buttonLabel ?? $t('ui.searchButton') }}
    </UiButton>
  </div>
</template>

<script setup lang="ts">
/**
 * SearchBar (`UiSearchBar`): a search field with a submit button, for hero-style searches
 * (domain lookup, knowledge base). Enter in the field and the button both emit `search`. The field
 * and the button share one `size` step, so they have the same height and type size.
 */
import { Search } from 'lucide-vue-next'
import { UiSize } from '~/utils/ui/enums'

/** Props of SearchBar. */
interface Props {
  /** Query text (v-model). */
  modelValue?: string
  /** Placeholder of the field; every search field must set one. */
  placeholder?: string
  /** Disables the field and puts the button in its loading state. */
  loading?: boolean
  /** Button text; defaults to the translated "Search". */
  buttonLabel?: string
  /** Size step of the field and the button (large in a hero, medium in a band). */
  size?: `${UiSize}`
}

withDefaults(defineProps<Props>(), {
  modelValue: '',
  placeholder: '',
  loading: false,
  buttonLabel: undefined,
  size: UiSize.Lg,
})

defineEmits<{
  /** The query changed. */
  'update:modelValue': [value: string]
  /** The reader asked to run the search. */
  search: []
}>()
</script>
