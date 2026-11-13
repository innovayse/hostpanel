<script setup lang="ts">
/**
 * Picker for an existing user: a debounced search box that turns into the chosen user's card.
 *
 * The parent owns the lookup (`search`) and the choice (`v-model`); this component owns only the
 * typing, the debounce and the list of hits. Shared by the add-client form and the client users
 * page, which differ only in which store answers the search.
 */
import { ref, watch, onBeforeUnmount } from 'vue'
import { useI18n } from 'vue-i18n'
import type { UserSearchHit } from '@/types/clientuser'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLoader from '@/components/ui/UiLoader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { SEARCH_DEBOUNCE_MS } from '@/lib/limits'

const { t } = useI18n()

/** Props for ClientUserPicker. */
const props = withDefaults(defineProps<{
  /** The chosen user, or null while none is chosen (two-way bound with `v-model`). */
  modelValue: UserSearchHit | null
  /** Looks users up for a search term; owned by the parent's store. */
  search: (term: string) => Promise<UserSearchHit[]>
  /** Marks the search box as invalid. */
  invalid?: boolean
  /** `id` of the search box, for a wrapping label. */
  inputId?: string
  /** Prefix of every `data-testid` (`<prefix>-search`, `<prefix>-result-<id>`, `<prefix>-selected`, `<prefix>-clear`). */
  testid?: string
}>(), {
  invalid: false,
  inputId: undefined,
  testid: 'user-picker',
})

const emit = defineEmits<{
  /** Emitted with the chosen user, or null when the choice is cleared. */
  'update:modelValue': [user: UserSearchHit | null]
  /** Emitted when the reader types, so the parent can drop a stale error. */
  typing: []
}>()

/** Most hits shown. */
const MAX_RESULTS = 5

/** Icon markup (shape tags) of the clear button. */
const CLOSE_ICON = '<line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>'

/** Text in the search box. */
const term = ref('')

/** Hits of the latest search. */
const results = ref<UserSearchHit[]>([])

/** True while a search is in flight. */
const loading = ref(false)

/** Timer handle of the debounced search. */
let timer: ReturnType<typeof setTimeout> | null = null

watch(term, (value) => {
  if (timer) {
    clearTimeout(timer)
  }
  if (!value.trim()) {
    results.value = []
    return
  }
  timer = setTimeout(async () => {
    loading.value = true
    const hits = await props.search(value.trim())
    results.value = hits.slice(0, MAX_RESULTS)
    loading.value = false
  }, SEARCH_DEBOUNCE_MS)
})

/**
 * Builds the two-letter initials of a user.
 *
 * @param user - The user.
 * @returns First letters of the first and last name.
 */
const initials = (user: UserSearchHit): string => `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`

/**
 * Chooses a hit and resets the search.
 *
 * @param user - The chosen user.
 * @returns Nothing.
 */
const choose = (user: UserSearchHit): void => {
  term.value = ''
  results.value = []
  emit('update:modelValue', user)
}

/**
 * Clears the choice so the search box returns.
 *
 * @returns Nothing.
 */
const clear = (): void => {
  emit('update:modelValue', null)
}

onBeforeUnmount(() => {
  if (timer) {
    clearTimeout(timer)
  }
})
</script>

<template>
  <UiStack
    v-if="modelValue"
    direction="row"
    :align="UiAlignItems.Center"
    gap="sm"
    :data-testid="`${testid}-selected`"
  >
    <UiBadge :tone="UiTone.Primary">{{ initials(modelValue) }}</UiBadge>
    <UiStack gap="none" class="flex-1">
      <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md">
        {{ modelValue.firstName }} {{ modelValue.lastName }}
      </UiText>
      <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs">{{ modelValue.email }}</UiText>
    </UiStack>
    <UiIconButton
      :label="t('shared.cancel')"
      :size="UiSize.Sm"
      :data-testid="`${testid}-clear`"
      @click="clear"
    >
      <UiIcon :markup="CLOSE_ICON" :size="UiSize.Sm" />
    </UiIconButton>
  </UiStack>

  <UiStack v-else gap="sm">
    <UiStack direction="row" :align="UiAlignItems.Center" gap="sm">
      <UiInput
        :id="inputId"
        v-model="term"
        type="text"
        :invalid="invalid"
        :placeholder="t('shared.searchByNameOrEmail')"
        :data-testid="`${testid}-search`"
        @input="emit('typing')"
      />
      <UiLoader v-if="loading" />
    </UiStack>
    <UiStack v-if="results.length > 0" gap="xs">
      <UiButton
        v-for="user in results"
        :key="user.id"
        type="button"
        :variant="UiButtonVariant.Ghost"
        block
        :data-testid="`${testid}-result-${user.id}`"
        @click="choose(user)"
      >
        <UiBadge :tone="UiTone.Primary">{{ initials(user) }}</UiBadge>
        {{ user.firstName }} {{ user.lastName }}
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs">{{ user.email }}</UiText>
      </UiButton>
    </UiStack>
  </UiStack>
</template>
