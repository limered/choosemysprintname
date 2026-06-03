<script setup>
import { onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()
const letter = ref('Q')
const submitting = ref(false)
const error = ref('')
const activeSessions = ref([])
const sessionsError = ref('')
const pendingDeleteId = ref(null)
const CONFIRM_DELETE_MS = 3000
let pollTimer = null
let pendingDeleteTimer = null

async function loadActiveSessions() {
  try {
    const res = await fetch('/api/sessions')
    if (!res.ok) {
      sessionsError.value = `Failed to load sessions (${res.status})`
      return
    }
    activeSessions.value = await res.json()
    sessionsError.value = ''
  } catch (e) {
    sessionsError.value = `Network error: ${e.message}`
  }
}

function phaseLabel(phase) {
  switch (phase) {
    case 'Lobby': return 'Waiting to start'
    case 'Voting': return 'Voting'
    case 'TieBreaker': return 'Tie-breaker'
    default: return phase
  }
}

function joinSession(id) {
  router.push(`/session/${id}`)
}

function clearPendingDelete() {
  if (pendingDeleteTimer) {
    clearTimeout(pendingDeleteTimer)
    pendingDeleteTimer = null
  }
  pendingDeleteId.value = null
}

async function onDeleteClick(id) {
  if (pendingDeleteId.value === id) {
    clearPendingDelete()
    try {
      const res = await fetch(`/api/sessions/${id}`, { method: 'DELETE' })
      if (res.ok || res.status === 404) {
        activeSessions.value = activeSessions.value.filter(s => s.id !== id)
      } else {
        sessionsError.value = `Failed to delete session (${res.status})`
      }
    } catch (e) {
      sessionsError.value = `Network error: ${e.message}`
    }
    return
  }
  clearPendingDelete()
  pendingDeleteId.value = id
  pendingDeleteTimer = setTimeout(() => {
    pendingDeleteId.value = null
    pendingDeleteTimer = null
  }, CONFIRM_DELETE_MS)
}

async function createSession() {
  error.value = ''
  const trimmed = (letter.value || '').trim()
  if (trimmed.length !== 1 || !/^[A-Za-z]$/.test(trimmed)) {
    error.value = 'Please enter a single letter A-Z.'
    return
  }
  submitting.value = true
  try {
    const res = await fetch('/api/sessions', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ letter: trimmed.toUpperCase() }),
    })
    if (!res.ok) {
      error.value = `Failed to create session (${res.status})`
      return
    }
    const data = await res.json()
    router.push(`/session/${data.id}`)
  } catch (e) {
    error.value = `Network error: ${e.message}`
  } finally {
    submitting.value = false
  }
}

onMounted(() => {
  loadActiveSessions()
  pollTimer = setInterval(loadActiveSessions, 3000)
})
onUnmounted(() => {
  if (pollTimer) clearInterval(pollTimer)
  if (pendingDeleteTimer) clearTimeout(pendingDeleteTimer)
})
</script>

<template>
  <div class="create-session">
    <h1>Choose My Sprint Name</h1>
    <p>Pick a letter to find Pokemon candidates for this sprint.</p>
    <form @submit.prevent="createSession">
      <label for="letter">Letter</label>
      <input
        id="letter"
        v-model="letter"
        maxlength="1"
        autocomplete="off"
        :disabled="submitting"
      />
      <button type="submit" :disabled="submitting">
        {{ submitting ? 'Creating…' : 'Create session' }}
      </button>
    </form>
    <p v-if="error" class="error">{{ error }}</p>

    <section class="active-sessions">
      <h2>Or join an active session</h2>
      <p v-if="sessionsError" class="error">{{ sessionsError }}</p>
      <p v-else-if="activeSessions.length === 0" class="empty">No active sessions right now.</p>
      <ul v-else>
        <li v-for="s in activeSessions" :key="s.id">
          <span class="letter-badge">{{ s.letter }}</span>
          <span class="phase">{{ phaseLabel(s.phase) }}</span>
          <span class="participants">{{ s.participantCount }} joined</span>
          <button type="button" @click="joinSession(s.id)">Join</button>
          <button
            type="button"
            class="delete-btn"
            :class="{ pending: pendingDeleteId === s.id }"
            :style="pendingDeleteId === s.id ? { '--confirm-ms': `${CONFIRM_DELETE_MS}ms` } : null"
            @click="onDeleteClick(s.id)"
          >
            <span class="delete-label">{{ pendingDeleteId === s.id ? 'Confirm' : 'Delete' }}</span>
          </button>
        </li>
      </ul>
    </section>
  </div>
</template>

<style scoped>
.create-session {
  max-width: 560px;
  margin: 4rem auto;
  text-align: center;
  padding: 0 1rem;
}
form {
  display: flex;
  gap: 0.5rem;
  justify-content: center;
  align-items: center;
  margin-top: 1.5rem;
}
form input {
  font-family: var(--display);
  font-size: 2.5rem;
  width: 3rem;
  text-align: center;
  text-transform: uppercase;
  padding: 0.15rem;
}
.error {
  color: var(--danger);
  margin-top: 1rem;
}
.active-sessions {
  margin-top: 3rem;
  text-align: left;
}
.active-sessions h2 {
  text-align: center;
}
.active-sessions ul {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}
.active-sessions li {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.5rem 0.75rem;
  border: 2px solid var(--border);
  border-radius: 6px;
  background: var(--surface);
}
.letter-badge {
  font-family: var(--display);
  font-weight: 400;
  font-size: 1.5rem;
  width: 2rem;
  height: 2rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  background: var(--surface-2);
  border: 2px solid var(--border);
}
.phase {
  color: var(--text-muted);
}
.participants {
  margin-left: auto;
  color: var(--text-muted);
  font-size: 0.9rem;
}
.empty {
  text-align: center;
  color: var(--text-muted);
}
.delete-btn {
  position: relative;
  overflow: hidden;
  border: 2px solid var(--danger);
  background: transparent;
  color: var(--danger);
  box-shadow: 3px 3px 0 var(--shadow);
}
.delete-btn::before {
  content: '';
  position: absolute;
  inset: 0;
  background: var(--danger);
  transform: scaleX(0);
  transform-origin: left;
  pointer-events: none;
}
.delete-btn.pending {
  color: var(--danger-text);
  border-color: var(--danger);
}
.delete-btn.pending::before {
  animation: confirm-fill var(--confirm-ms, 3000ms) linear forwards;
}
.delete-label {
  position: relative;
  z-index: 1;
}
@keyframes confirm-fill {
  from { transform: scaleX(0); }
  to { transform: scaleX(1); }
}
</style>
