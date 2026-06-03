<script setup>
import { RouterView } from 'vue-router'
import { theme, toggleTheme } from './theme.js'
</script>

<template>
  <label class="theme-toggle" title="Toggle light mode">
    <input type="checkbox" :checked="theme === 'light'" @change="toggleTheme" />
    <span class="track">
      <span class="knob"></span>
    </span>
    <span class="label">Light</span>
  </label>
  <RouterView />
</template>

<style scoped>
.theme-toggle {
  position: fixed;
  top: 1rem;
  right: 1rem;
  z-index: 100;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  cursor: pointer;
  user-select: none;
}
.theme-toggle input {
  position: absolute;
  opacity: 0;
  width: 0;
  height: 0;
}
.track {
  position: relative;
  display: flex;
  align-items: center;
  width: 2.5rem;
  height: 1.4rem;
  border-radius: 999px;
  background: var(--surface-2);
  border: 2px solid var(--border-strong);
  transition: background 0.2s;
}
.knob {
  position: absolute;
  left: 2px;
  width: 1rem;
  height: 1rem;
  border-radius: 999px;
  background: var(--text-muted);
  transition: transform 0.2s, background 0.2s;
}
.theme-toggle input:checked ~ .track .knob {
  transform: translateX(1.1rem);
  background: var(--accent);
}
.theme-toggle input:checked ~ .track {
  background: var(--accent-soft);
  border-color: var(--accent);
}
.theme-toggle input:checked ~ .track .knob {
  transform: translateX(1.1rem);
  background: var(--accent);
}
.label {
  font-family: var(--display);
  font-size: 1.1rem;
  color: var(--text-muted);
}
</style>
