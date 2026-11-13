<template>
  <UiSearchBar
    v-model="query"
    :placeholder="$t('hero.search.placeholder')"
    :button-label="$t('domains.search')"
    :size="size"
    data-testid="sections-domain-search"
    @search="search"
  />
</template>

<script setup lang="ts">
/**
 * SectionsDomainSearch: the domain search bar of the classic home page (hero and closing band).
 *
 * It does not look the name up itself: it opens the domains page with the typed name in `?domain=` (see `DOMAIN_SEARCH_QUERY_PARAM`),
 * and that page runs the availability search and shows the result and the cart actions.
 */

import { DOMAIN_SEARCH_QUERY_PARAM } from '~/constants/domainSearchQuery'
import { UiSize } from '~/utils/ui/enums'

/** Props of SectionsDomainSearch. */
interface Props {
  /** Size step of the field and the button: large in the hero, medium in the footer band. */
  size?: `${UiSize}`
}

/** Props of the component with their defaults applied. */
withDefaults(defineProps<Props>(), {
  size: UiSize.Lg,
})

/** Prefixes a path with the active language. */
const localePath = useLocalePath()

/** The name typed into the field. */
const query = ref('')

/** Opens the domains page for the typed name; an empty field opens it without a search. */
const search = async (): Promise<void> => {
  const name = query.value.trim()
  const path = localePath('/domains')
  await navigateTo(name ? `${path}?${DOMAIN_SEARCH_QUERY_PARAM}=${encodeURIComponent(name)}` : path)
}
</script>
