<script setup>
import { RouterView } from 'vue-router'
import { theme, toggleTheme } from './theme.js'
</script>

<template>
  <button class="theme-toggle" type="button" @click="toggleTheme" :title="theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'">
    {{ theme === 'dark' ? 'Light mode' : 'Dark mode' }}
  </button>
  <RouterView />
</template>

<style scoped>
.theme-toggle {
  position: fixed;
  top: 1rem;
  right: 1rem;
  z-index: 100;
  font-family: var(--display);
  font-size: 1.1rem;
  padding: 0.3rem 0.8rem;
}
</style>
