<script setup>
import { onMounted, onUnmounted, ref, computed } from 'vue'
import { useRoute, RouterLink } from 'vue-router'

const route = useRoute()
const sessionId = route.params.id
const nicknameKey = `nickname:${sessionId}`
const voteKey = `vote:${sessionId}`

const POLL_INTERVAL_MS = 1500

const loading = ref(false)
const error = ref('')
const session = ref(null)
const joined = ref(false)
const joinError = ref('')
const copied = ref(false)
const myNickname = ref('')
const myVote = ref(localStorage.getItem(voteKey) || '')
const voteError = ref('')
const voting = ref(false)

let pollHandle = null

const shareUrl = computed(() => window.location.href)

const voteCounts = computed(() => {
  const map = {}
  if (session.value?.votes) {
    for (const v of session.value.votes) map[v.name] = v.count
  }
  return map
})

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
      stopPolling()
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

async function refreshSession() {
  try {
    const res = await fetch(`/api/sessions/${sessionId}`)
    if (!res.ok) return
    session.value = await res.json()
  } catch {
    // ignore transient polling errors
  }
}

function startPolling() {
  if (pollHandle) return
  pollHandle = setInterval(refreshSession, POLL_INTERVAL_MS)
}

function stopPolling() {
  if (pollHandle) {
    clearInterval(pollHandle)
    pollHandle = null
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
    localStorage.setItem(nicknameKey, nickname)
    myNickname.value = nickname
    joined.value = true
    await loadSession()
    startPolling()
  } catch (e) {
    joinError.value = `Network error: ${e.message}`
  }
}

async function castVote(candidateName) {
  if (myVote.value || voting.value) return
  voteError.value = ''
  voting.value = true
  try {
    const res = await fetch(`/api/sessions/${sessionId}/votes`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ nickname: myNickname.value, candidateName }),
    })
    if (res.status === 409) {
      // server says we already voted - lock in the choice locally
      myVote.value = candidateName
      localStorage.setItem(voteKey, candidateName)
      voteError.value = 'You have already voted in this session.'
      return
    }
    if (res.status === 404) {
      voteError.value = 'Session or candidate not found.'
      return
    }
    if (!res.ok) {
      voteError.value = `Failed to vote (${res.status})`
      return
    }
    myVote.value = candidateName
    localStorage.setItem(voteKey, candidateName)
    refreshSession()
  } catch (e) {
    voteError.value = `Network error: ${e.message}`
  } finally {
    voting.value = false
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
  let nickname = localStorage.getItem(nicknameKey)
  if (nickname) {
    myNickname.value = nickname
    joined.value = true
    await loadSession()
    startPolling()
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

onUnmounted(() => {
  stopPolling()
})
</script>

<template>
  <div class="session-room">
    <header class="topbar">
      <RouterLink to="/" class="back">&larr; New session</RouterLink>
      <span v-if="myNickname" class="me">{{ myNickname }}</span>
    </header>

    <h1 v-if="joined && session" class="title">Candidates starting with “{{ session.letter }}”</h1>
    <h1 v-else class="title">Joining session…</h1>

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

      <p v-if="voteError" class="error">{{ voteError }}</p>
      <p v-if="myVote" class="voted-msg">You voted for <strong>{{ myVote }}</strong>.</p>

      <p v-if="loading">Loading candidates…</p>
      <p v-else-if="error" class="error">{{ error }}</p>

      <div v-else-if="session && session.candidates.length === 0" class="empty">
        No Pokemon found for this letter.
      </div>

      <ul v-else-if="session" class="grid">
        <li
          v-for="c in session.candidates"
          :key="c.name"
          class="candidate"
          :class="{ 'my-vote': myVote === c.name }"
        >
          <img :src="c.spriteUrl" :alt="c.name" loading="lazy" />
          <span class="name">{{ c.name }}</span>
          <span class="count">{{ voteCounts[c.name] ?? 0 }} vote{{ (voteCounts[c.name] ?? 0) === 1 ? '' : 's' }}</span>
          <button
            type="button"
            class="vote-btn"
            :disabled="!!myVote || voting"
            @click="castVote(c.name)"
          >
            {{ myVote === c.name ? 'Your vote' : 'Vote' }}
          </button>
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
.topbar {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  font-size: 0.8rem;
}
.back {
  font-size: 0.8rem;
}
.me {
  margin-left: auto;
  font-size: 0.75rem;
  font-family: monospace;
  padding: 0.15rem 0.5rem;
  border-radius: 999px;
  background: #eef2f7;
  color: #2a3a55;
  border: 1px solid #d6dee8;
}
.title {
  margin-top: 2rem;
  margin-bottom: 1rem;
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
.voted-msg {
  font-size: 0.9rem;
  color: #2a3a55;
  background: #eef9f1;
  border: 1px solid #b9e0c4;
  padding: 0.4rem 0.6rem;
  border-radius: 6px;
}
.grid {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
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
  transition: border-color 0.15s, box-shadow 0.15s;
}
.candidate.my-vote {
  border-color: #2a7a3a;
  box-shadow: 0 0 0 2px rgba(42, 122, 58, 0.2);
  background: #f3fbf5;
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
.count {
  margin-top: 0.25rem;
  font-size: 0.8rem;
  color: #555;
}
.vote-btn {
  margin-top: 0.5rem;
  padding: 0.35rem 0.8rem;
  font-size: 0.85rem;
  border: 1px solid #2a3a55;
  border-radius: 4px;
  background: #2a3a55;
  color: #fff;
  cursor: pointer;
}
.vote-btn:disabled {
  background: #c8cdd6;
  border-color: #c8cdd6;
  color: #555;
  cursor: not-allowed;
}
.candidate.my-vote .vote-btn:disabled {
  background: #2a7a3a;
  border-color: #2a7a3a;
  color: #fff;
}
.error {
  color: #c0392b;
}
.empty {
  font-style: italic;
  color: #666;
}
</style>
