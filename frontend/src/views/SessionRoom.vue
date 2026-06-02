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
const joinError = ref('')
const copied = ref(false)
const myNickname = ref('')

const shareUrl = computed(() => window.location.href)

function generateNickname() {
  return fetch('/api/nickname')
    .then(r => r.json())
    .then(d => d.nickname)
}

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

async function joinSession(nickname) {
  joinError.value = ''
  try {
    const res = await fetch(`/api/sessions/${sessionId}/participants`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ nickname }),
    })
    if (res.status === 404) {
      joinError.value = 'Session not found.'
      return
    }
    if (!res.ok) {
      joinError.value = `Failed to join (${res.status})`
      return
    }
    localStorage.setItem(storageKey, nickname)
    myNickname.value = nickname
    joined.value = true
    await loadSession()
  } catch (e) {
    joinError.value = `Network error: ${e.message}`
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

onMounted(async () => {
  let nickname = localStorage.getItem(storageKey)
  if (nickname) {
    myNickname.value = nickname
    joined.value = true
    loadSession()
  } else {
    try {
      nickname = await generateNickname()
    } catch (e) {
      joinError.value = `Failed to generate nickname: ${e.message}`
      return
    }
    joinSession(nickname)
  }
})
</script>

<template>
  <div class="session-room">
    <header>
      <RouterLink to="/" class="back">&larr; New session</RouterLink>
      <h1 v-if="joined && session">Candidates starting with “{{ session.letter }}”</h1>
      <h1 v-else>Joining session…</h1>
      <span v-if="myNickname" class="me">{{ myNickname }}</span>
    </header>

    <p v-if="joinError" class="error">{{ joinError }}</p>

    <template v-if="joined">
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
.me {
  margin-left: auto;
  font-size: 0.9rem;
  font-family: monospace;
  padding: 0.25rem 0.6rem;
  border-radius: 999px;
  background: #eef2f7;
  color: #2a3a55;
  border: 1px solid #d6dee8;
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
  color: #222;
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
