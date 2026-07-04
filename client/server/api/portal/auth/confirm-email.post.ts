export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  const apiUrl = (config.apiUrl as string) || 'http://localhost:5000'
  const body = await readBody(event)

  return await $fetch(`${apiUrl}/api/auth/confirm-email`, {
    method: 'POST',
    body,
  })
})
