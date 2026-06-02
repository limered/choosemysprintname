<script setup>
import { onMounted, ref, computed } from 'vue'
import { useRoute, RouterLink } from 'vue-router'

const route = useRoute()
const sessionId = route.params.id
const storageKey = `nickname:${sessionId}`

const loading = ref(false)
const error = ref('')
const session = ref(null)
const joined = ref(false)
const nickname = ref('')
const joining = ref(false)
const joinError = ref('')
const copied = ref(false)

const shareUrl = computed(() => window.location.href)

async function loadSession() {
  loading.value = true
  error.value = ''
  try {
    const res = await fetch(`/api/sessions/${sessionId}`)
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
}

async function joinSession() {
  joinError.value = ''
  const trimmed = (nickname.value || '').trim()
  if (!trimmed) {
    joinError.value = 'Please enter a nickname.'
    return
  }
  joining.value = true
  try {
    const res = await fetch(`/api/sessions/${sessionId}/participants`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ nickname: trimmed }),
    })
    if (res.status === 404) {
      joinError.value = 'Session not found.'
      return
    }
    if (!res.ok) {
      joinError.value = `Failed to join (${res.status})`
      return
    }
    localStorage.setItem(storageKey, trimmed)
    joined.value = true
    await loadSession()
  } catch (e) {
    joinError.value = `Network error: ${e.message}`
  } finally {
    joining.value = false
  }
}

async function copyShareLink() {
  try {
    await navigator.clipboard.writeText(shareUrl.value)
    copied.value = true
    setTimeout(() => { copied.value = false }, 1500)
  } catch {
    // clipboard unavailable; ignore
  }
}

onMounted(() => {
  const existing = localStorage.getItem(storageKey)
  if (existing) {
    joined.value = true
    loadSession()
  }
})
</script>

<template>
  <div class="session-room">
    <header>
      <RouterLink to="/" class="back">&larr; New session</RouterLink>
      <h1 v-if="joined && session">Candidates starting with “{{ session.letter }}”</h1>
      <h1 v-else>Join session</h1>
    </header>

    <section v-if="!joined" class="join">
      <p>Enter a nickname to join this voting session.</p>
      <form @submit.prevent="joinSession">
        <input
          v-model="nickname"
          placeholder="Your nickname"
          autocomplete="off"
          :disabled="joining"
          maxlength="40"
        />
        <button type="submit" :disabled="joining">
          {{ joining ? 'Joining…' : 'Join' }}
        </button>
      </form>
      <p v-if="joinError" class="error">{{ joinError }}</p>
    </section>

    <template v-else>
      <section class="share">
        <label>Share this link:</label>
        <div class="share-row">
          <input class="share-link" :value="shareUrl" readonly @focus="$event.target.select()" />
          <button type="button" @click="copyShareLink">Copy link</button>
          <span v-if="copied" class="copied">Copied!</span>
        </div>
      </section>

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
    </template>
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
.join form {
  display: flex;
  gap: 0.5rem;
  margin-top: 1rem;
}
.join input {
  flex: 1;
  font-size: 1rem;
  padding: 0.5rem;
}
.join button {
  font-size: 1rem;
  padding: 0.5rem 1rem;
  cursor: pointer;
}
.share {
  margin-bottom: 1.5rem;
  padding: 0.75rem 1rem;
  background: #f4f7fb;
  border: 1px solid #d6dee8;
  border-radius: 8px;
}
.share label {
  font-size: 0.85rem;
  color: #555;
}
.share-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-top: 0.4rem;
}
.share-link {
  flex: 1;
  font-family: monospace;
  font-size: 0.9rem;
  padding: 0.4rem 0.5rem;
  border: 1px solid #ccc;
  border-radius: 4px;
  background: #fff;
}
.copied {
  font-size: 0.85rem;
  color: #2a7a3a;
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
