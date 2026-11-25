/**
 * Tests for useRegistrarModuleOptions: only loaded registrar plugins are offered, with the plugin id as the value and its
 * name as the label, and a stored module is named without regard to letter case.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useRegistrarModuleOptions } from '@/composables/useRegistrarModuleOptions'
import { PluginCapability } from '@/types/plugin'
import { json, stubFetch } from '../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

/**
 * Builds one plugin list row.
 *
 * @param id - The plugin id.
 * @param name - The plugin name.
 * @param type - The plugin type as the list sends it.
 * @param isLoaded - Whether the plugin loaded at startup.
 * @returns The row, with only what the composable reads.
 */
const plugin = (id: string, name: string, type: string, isLoaded = true) => ({ id, name, type, isLoaded, status: 'Loaded' })

describe('useRegistrarModuleOptions', () => {
  it('starts empty and offers only the loaded registrar plugins', async () => {
    setActivePinia(createPinia())
    stubFetch(
      json([
        plugin('registrar-one', 'Registrar One', 'registrar'),
        plugin('registrar-two', 'Registrar Two', 'registrar', false),
        plugin('a-gateway', 'A Gateway', 'payment'),
      ]),
    )
    const { options, load } = useRegistrarModuleOptions()
    expect(options.value).toEqual([])

    await load()

    expect(options.value).toEqual([{ value: 'registrar-one', label: 'Registrar One' }])
  })

  it('offers only the registrars that declare the capability asked for', async () => {
    setActivePinia(createPinia())
    stubFetch(
      json([
        { ...plugin('registrar-one', 'Registrar One', 'registrar'), capabilities: ['registrarOperations', 'registrarDomainList'] },
        { ...plugin('registrar-two', 'Registrar Two', 'registrar'), capabilities: ['registrarOperations'] },
        { ...plugin('a-gateway', 'A Gateway', 'payment'), capabilities: ['registrarDomainList'] },
      ]),
    )
    const { options, load } = useRegistrarModuleOptions(PluginCapability.RegistrarDomainList)

    await load()

    expect(options.value).toEqual([{ value: 'registrar-one', label: 'Registrar One' }])
  })

  it('names a stored module without regard to letter case and falls back to the module itself', async () => {
    setActivePinia(createPinia())
    stubFetch(json([plugin('registrar-one', 'Registrar One', 'registrar')]))
    const { load, labelOf } = useRegistrarModuleOptions()

    await load()

    expect(labelOf('Registrar-One')).toBe('Registrar One')
    expect(labelOf('gone-registrar')).toBe('gone-registrar')
  })

  it('finds the option a stored module selects, whatever its letter case', async () => {
    setActivePinia(createPinia())
    stubFetch(json([plugin('registrar-one', 'Registrar One', 'registrar')]))
    const { load, idOf } = useRegistrarModuleOptions()

    await load()

    expect(idOf('REGISTRAR-ONE')).toBe('registrar-one')
    expect(idOf('gone-registrar')).toBe('gone-registrar')
  })
})
