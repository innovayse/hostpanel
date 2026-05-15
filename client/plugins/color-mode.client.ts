/**
 * Forces dark mode on public pages.
 * The client layout (/client/*) handles its own preference via onMounted.
 */
export default defineNuxtPlugin(() => {
  document.documentElement.classList.add('dark')
  document.documentElement.classList.remove('light')
})
