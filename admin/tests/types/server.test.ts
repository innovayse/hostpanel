/**
 * Unit tests for the server module names: the host's label for a built in module, the plugin's own name for a plugin
 * module, and the lookup of a stored module among the declared options.
 */
import { describe, expect, it } from 'vitest'
import { moduleNameOf, moduleOptionLabel } from '@/types/server'
import type { ServerModuleOptionDto } from '@/types/server'
import { ServerModuleSource } from '@/types/serverenums'

/** One built in and one plugin module, as the API declares them. */
const options: ServerModuleOptionDto[] = [
  { id: 'CPanel', name: 'CPanel', source: ServerModuleSource.BuiltIn },
  { id: 'ExamplePanel', name: 'Example Panel', source: ServerModuleSource.Plugin },
]

describe('moduleOptionLabel', () => {
  it('names a plugin module by the plugin and a built in one by the host label', () => {
    expect(moduleOptionLabel(options[1] as ServerModuleOptionDto)).toBe('Example Panel')
    expect(moduleOptionLabel(options[0] as ServerModuleOptionDto)).toBe('cPanel / WHM')
  })
})

describe('moduleNameOf', () => {
  it('finds the declared option whatever the spelling of the id', () => {
    expect(moduleNameOf('examplepanel', options)).toBe('Example Panel')
  })

  it('falls back to the host label, then to the id, when no option serves the module', () => {
    expect(moduleNameOf('CPanel', [])).toBe('cPanel / WHM')
    expect(moduleNameOf('Unknown1', options)).toBe('Unknown1')
  })
})
