// Loads the main portal's shared header widget — the app launcher and account chip the
// client area shows (layouts/client.vue and the classic template's header carry its mounts).
//
// A plugin rather than nuxt.config's app.head on purpose: process.env inside nuxt.config
// is read at BUILD time, so the src baked whatever NUXT_PUBLIC_MAIN_URL the image was built
// with. It happened to work here only because docker/client.Dockerfile passes the value as
// a build ARG; read from runtimeConfig, the container's own environment decides.
//
// Not .client — runtimeConfig.public is available during SSR too, so the tag is rendered
// into <head> with the document instead of appearing after hydration.
export default defineNuxtPlugin(() => {
  const mainUrl = useRuntimeConfig().public.mainUrl as string
  // The dev default is a host that exists only on a developer's machine; a stock install
  // with no portal must not request a script from it on every page.
  if (!mainUrl || /\.local(:\d+)?\/?$/.test(mainUrl)) return

  useHead({
    // `?v=` is a ONE-TIME cache bust shared with the other products. Do not bump it per
    // widget release: the portal used to serve /widget/header.js behind `expires 1y` +
    // `immutable`, and this token is what reaches browsers still holding that entry.
    script: [{ src: `${mainUrl}/widget/header.js?v=20260918`, defer: true }]
  })
})
