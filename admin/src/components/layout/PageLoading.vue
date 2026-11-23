<script setup lang="ts">
/**
 * What the page shows from the first paint until the router has settled where it is going.
 *
 * On a hard load the router first asks the server who is signed in (and how the deployment signs people in) before it can say
 * whether the visitor gets the panel or the sign-in screen, so no screen can be drawn yet. Without this the page stays
 * blank for as long as those requests take; this is the kit's busy ring, centred, with the shared "Loading" label, so the
 * page answers at once and the screen that follows replaces it. `RoutedView` draws it only while that first navigation is
 * running; a failure ends it with the load error, never with a spinner that spins on.
 */
import UiLoader from '@/components/ui/UiLoader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
</script>

<template>
  <UiStack class="min-h-dvh" :align="UiAlignItems.Center" :justify="UiJustify.Center" data-testid="page-loading">
    <UiLoader :size="UiSize.Md" show-label />
  </UiStack>
</template>
