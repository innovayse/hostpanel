<script setup lang="ts">
/**
 * The routed slot of a layout: a `<RouterView>` that answers a click before the next screen has arrived.
 *
 * vue-router keeps the old screen until the chunk of the next one is downloaded. While the navigation
 * store holds a pending target, the one `RoutedView` whose screen that navigation replaces draws what
 * the screen is going to look like instead, and the load error when the download failed for good. Every other
 * level keeps what it shows: a tab change inside the client area keeps the client header and inner sidebar.
 *
 * What it draws for the record the navigation brings to this level:
 * - the record's skeleton schema (`meta.skeleton`) as `UiSkeletonOf`; a layout with tabs (the client area) draws its
 *   own skeleton with the skeleton of the record one level deeper in its `outlet`, so the click shows the whole
 *   page that is coming, not one block of it;
 * - the record itself when it is the eager shell (`meta.shell`, the layout with the sidebar and the top bar): its
 *   code is already here, so a hard load shows the chrome at once and the `RoutedView` inside it draws the skeleton
 *   of the screen below;
 * - `ViewLoading`, the generic stand-in, only for a record that has no schema (see `lib/skeletonExceptions.ts`).
 *
 * Use it wherever a layout would write `<RouterView />`.
 */
import { computed, inject, unref } from 'vue'
import type { Component } from 'vue'
import { RouterView, useRouter, viewDepthKey } from 'vue-router'
import type { RouteLocationNormalized, RouteRecordNormalized } from 'vue-router'
import ViewLoadError from '@/components/layout/ViewLoadError.vue'
import ViewLoading from '@/components/layout/ViewLoading.vue'
import UiSkeletonOf from '@/components/ui/UiSkeletonOf.vue'
import { useNavigationStore } from '@/stores/navigation'
import type { SkeletonSchema } from '@/utils/ui/skeletonSchema'

/** The router, whose current route is what this level shows now. */
const router = useRouter()

/** The navigation in flight, if any. */
const navigation = useNavigationStore()

/** The depth vue-router gives the `<RouterView>` rendered here; the enclosing view provides it. */
const injectedDepth = inject(viewDepthKey, 0)

/**
 * Lists the records a `<RouterView>` at this depth and the ones nested below it render for a route.
 *
 * Mirrors vue-router's own lookup: a record without components (a grouping parent) is skipped.
 *
 * @param matched - The matched records of a route, outermost first.
 * @returns The drawn records from this level down; the first is the one drawn here.
 */
const drawnFrom = (matched: readonly RouteRecordNormalized[]): RouteRecordNormalized[] =>
  matched.slice(unref(injectedDepth)).filter(record => Boolean(record.components))

/**
 * Whether a navigation to a target replaces what this level shows.
 *
 * @param target - The target of a navigation, or null when there is none.
 * @returns True when the target draws a different record here.
 */
const changesHere = (target: RouteLocationNormalized | null): boolean =>
  target !== null && drawnFrom(target.matched)[0] !== drawnFrom(router.currentRoute.value.matched)[0]

/** True while the screen this level is about to show is still downloading. */
const loading = computed<boolean>(() => changesHere(navigation.pending))

/** True when the screen this level was about to show could not be downloaded. */
const failed = computed<boolean>(() => changesHere(navigation.failed))

/** The records the pending navigation draws from this level down; empty while this level is not loading. */
const pendingRecords = computed<RouteRecordNormalized[]>(() =>
  loading.value && navigation.pending !== null ? drawnFrom(navigation.pending.matched) : [],
)

/** The component of the eager shell when the pending navigation brings it to this level, otherwise undefined. */
const shell = computed<Component | undefined>(() => {
  const record = pendingRecords.value[0]
  return record?.meta.shell === true ? record.components?.default : undefined
})

/** The skeleton schema of the record drawn here, when it has one. */
const schema = computed<SkeletonSchema | undefined>(() => pendingRecords.value[0]?.meta.skeleton)

/** The skeleton schema of the record one level deeper, which a layout with tabs draws in its outlet. */
const outletSchema = computed<SkeletonSchema | undefined>(() => pendingRecords.value[1]?.meta.skeleton)
</script>

<template>
  <template v-if="loading && shell === undefined">
    <UiSkeletonOf v-if="schema" :schema="schema">
      <template #outlet>
        <UiSkeletonOf v-if="outletSchema" :schema="outletSchema" />
        <ViewLoading v-else />
      </template>
    </UiSkeletonOf>
    <ViewLoading v-else />
  </template>
  <ViewLoadError v-else-if="failed" />
  <!--
    While the shell is the pending record it is drawn here by its own component, in the place its real record takes
    when the navigation lands, so the sidebar and the top bar are the same instances before and after.
  -->
  <RouterView v-else v-slot="{ Component: routed }">
    <component :is="shell ?? routed" />
  </RouterView>
</template>
