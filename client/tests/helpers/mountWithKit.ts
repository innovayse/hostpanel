/**
 * Mount helper for component tests: the real component with a real Pinia, nothing mocked.
 *
 * `Ui*` components are not stubbed: a component test mounts the same kit the screen renders,
 * so a broken kit prop fails here. Every file in `components/ui/` is registered globally under
 * the name Nuxt would give it (`Ui` prefix, not doubled), which is what lets one kit component
 * render another. Only the Nuxt/i18n/icon runtime that does not exist in a unit test is stood in
 * for: `NuxtLink` and `NuxtImg` render a plain `a` / `img`, `Icon` renders an `i` carrying its
 * name, `ClientOnly` renders its slot, `$t` echoes the key, and `teleport` renders in place so a
 * dialog's content is findable. The real `LayoutDemoBanner` (it renders nothing unless the demo store is enabled)
 * is registered beneath anything a test registers itself, because the auth shell and both layouts mount it.
 * Nuxt auto-imports come from `vitest.setup.ts`.
 *
 * @module tests/helpers/mountWithKit
 */

import { config, flushPromises, mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import type { Pinia } from 'pinia'
import { Suspense, defineComponent, h } from 'vue'
import type { Component } from 'vue'
import LayoutDemoBanner from '~/components/layout/DemoBanner.vue'

/** Every kit component file, loaded once. */
const KIT_MODULES = import.meta.glob<{ default: Component }>('../../components/ui/*.vue', { eager: true })

/**
 * Registers the kit under the names Nuxt derives from the file path.
 *
 * @returns Map of component name to component.
 */
const buildKit = (): Record<string, Component> => {
  const kit: Record<string, Component> = {}
  for (const [path, module] of Object.entries(KIT_MODULES)) {
    const base = path.split('/').pop()!.replace(/\.vue$/, '') // glob keys always end in a file name
    kit[base.startsWith('Ui') ? base : `Ui${base}`] = module.default
  }
  return kit
}

/** The kit, built once for every mount. */
const KIT = buildKit()

/** `NuxtLink` stand-in: an anchor whose `href` is the `to` prop. */
const NuxtLinkStub = defineComponent({
  name: 'NuxtLink',
  props: { to: { type: String, default: undefined } },
  setup(props, { slots }) {
    return () => h('a', { href: props.to }, slots.default?.())
  },
})

/** `NuxtImg` stand-in: a plain `img` carrying the attributes it was given. */
const NuxtImgStub = defineComponent({
  name: 'NuxtImg',
  inheritAttrs: true,
  props: { src: { type: String, default: undefined }, alt: { type: String, default: undefined } },
  setup(props) {
    return () => h('img', { src: props.src, alt: props.alt })
  },
})

/** Nuxt Icon stand-in: an `i` carrying the icon name. */
const IconStub = defineComponent({
  name: 'Icon',
  props: { name: { type: String, default: undefined } },
  setup(props) {
    return () => h('i', { 'data-icon': props.name })
  },
})

/** `ClientOnly` stand-in: renders its slot at once. */
const ClientOnlyStub = defineComponent({
  name: 'ClientOnly',
  setup(_props, { slots }) {
    return () => slots.default?.()
  },
})

/** Options accepted by {@link mountWithKit}. */
export interface MountWithKitOptions {
  /** Props passed to the component. */
  props?: Record<string, unknown>
  /** Slot content by slot name; a string becomes that slot's text. */
  slots?: Record<string, string>
  /** Attributes (`class`, `data-testid`, listeners) passed to the root. */
  attrs?: Record<string, unknown>
  /** Extra global components or stubs for this mount. */
  stubs?: Record<string, Component | boolean>
  /** Extra components registered globally for this mount, by the name Nuxt would auto-import them under. */
  components?: Record<string, Component>
  /** Template globals replacing the defaults, such as a `$t` that also echoes its parameters. */
  mocks?: Record<string, unknown>
  /** Mount into the document so focus and `document` listeners work. */
  attachToBody?: boolean
  /** A Pinia the test filled before mounting; a fresh one is created when omitted. */
  pinia?: Pinia
  /** Receives an error a component raised (such as a page's `createError` thrown from an async setup) instead of it escaping as an unhandled rejection. */
  errorHandler?: (error: unknown) => void
}

/**
 * Mounts a component with a fresh active Pinia and the real kit registered.
 *
 * @param component The component under test.
 * @param options Props, slots, attributes and stubs.
 * @returns The Vue Test Utils wrapper.
 */
export const mountWithKit = (component: Component, options: MountWithKitOptions = {}): VueWrapper => {
  const pinia = options.pinia ?? createPinia()
  setActivePinia(pinia)
  return mount(component, {
    props: options.props,
    slots: options.slots,
    attrs: options.attrs,
    attachTo: options.attachToBody ? document.body : undefined,
    global: {
      plugins: [pinia],
      components: { LayoutDemoBanner, ...config.global.components, ...KIT, ...options.components, NuxtLink: NuxtLinkStub, NuxtImg: NuxtImgStub, Icon: IconStub, ClientOnly: ClientOnlyStub },
      mocks: { $t: (key: string) => key, ...options.mocks },
      stubs: { teleport: true, ...options.stubs },
      config: options.errorHandler ? { errorHandler: options.errorHandler } : {},
    },
  })
}

/**
 * Mounts a page (an async-setup component) inside `Suspense` and waits until it has rendered.
 *
 * The one page mount every helper builds on: a Nuxt page awaits its data at the top of its setup,
 * which makes it an async component that only renders under a `Suspense` boundary.
 *
 * @param page The page component.
 * @param options Mount options as for {@link mountWithKit}; `props` go to the page, not the host.
 * @returns The wrapper around the host; the page's DOM is inside it.
 */
export const mountAsyncPage = async (page: Component, options: MountWithKitOptions = {}): Promise<VueWrapper> => {
  const { props, ...rest } = options
  const host = defineComponent({
    name: 'PageHost',
    setup: () => () => h(Suspense, null, { default: () => h(page, props) }),
  })
  const wrapper = mountWithKit(host, rest)
  await flushPromises()
  return wrapper
}
