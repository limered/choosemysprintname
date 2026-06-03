import { ref } from 'vue'

function readInitial() {
  const fromAttr = document.documentElement.dataset.theme
  if (fromAttr === 'light' || fromAttr === 'dark') return fromAttr
  return 'light'
}

export const theme = ref(readInitial())

export function setTheme(next) {
  if (next !== 'light' && next !== 'dark') return
  theme.value = next
  document.documentElement.dataset.theme = next
  try { localStorage.setItem('theme', next) } catch { /* storage unavailable */ }
}

export function toggleTheme() {
  setTheme(theme.value === 'dark' ? 'light' : 'dark')
}
