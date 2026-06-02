<script setup>
import { onMounted, ref } from 'vue'
import { useRoute, RouterLink } from 'vue-router'

const route = useRoute()
const loading = ref(true)
const error = ref('')
const session = ref(null)

onMounted(async () => {
  try {
    const res = await fetch(`/api/sessions/${route.params.id}`)
    if (res.status === 404) {
      error.value = 'Session not found.'
      return
    }
    if (!res.ok) {
      error.value = `Failed to load session (${res.status})`
      return
    }
    session.value = await res.json()
  } catch (e) {
    error.value = `Network error: ${e.message}`
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="session-room">
    <header>
      <RouterLink to="/" class="back">&larr; New session</RouterLink>
      <h1 v-if="session">Candidates starting with “{{ session.letter }}”</h1>
    </header>

    <p v-if="loading">Loading candidates…</p>
    <p v-else-if="error" class="error">{{ error }}</p>

    <div v-else-if="session && session.candidates.length === 0" class="empty">
      No Pokemon found for this letter.
    </div>

    <ul v-else-if="session" class="grid">
      <li v-for="c in session.candidates" :key="c.name" class="candidate">
        <img :src="c.spriteUrl" :alt="c.name" loading="lazy" />
        <span class="name">{{ c.name }}</span>
      </li>
    </ul>
  </div>
</template>

<style scoped>
.session-room {
  max-width: 960px;
  margin: 2rem auto;
  padding: 0 1rem;
}
header {
  display: flex;
  align-items: baseline;
  gap: 1rem;
  margin-bottom: 1rem;
}
.back {
  font-size: 0.9rem;
}
.grid {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
  gap: 1rem;
}
.candidate {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 0.75rem;
  border: 1px solid #ddd;
  border-radius: 8px;
  background: #fafafa;
}
.candidate img {
  width: 96px;
  height: 96px;
  image-rendering: pixelated;
}
.name {
  margin-top: 0.5rem;
  text-transform: capitalize;
  font-size: 0.95rem;
}
.error {
  color: #c0392b;
}
.empty {
  font-style: italic;
  color: #666;
}
</style>
