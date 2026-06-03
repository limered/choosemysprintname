<script setup>
import { onMounted, onUnmounted, ref, computed, watch } from 'vue'
import { useRoute, RouterLink } from 'vue-router'

const route = useRoute()
const sessionId = route.params.id
const nicknameKey = `nickname:${sessionId}`

const POLL_INTERVAL_MS = 1500
const TICK_INTERVAL_MS = 250

const loading = ref(false)
const error = ref('')
const session = ref(null)
const joined = ref(false)
const joinError = ref('')
const copied = ref(false)
const myNickname = ref('')
const myVote = ref('')
const voteError = ref('')
const voting = ref(false)

const timerError = ref('')
const startingTimer = ref(false)
const extendingTimer = ref(false)
const endingTimer = ref(false)
const durationInput = ref(60)

const lastPollAtMs = ref(0)
const nowMs = ref(Date.now())

let pollHandle = null
let tickHandle = null

const shareUrl = computed(() => window.location.href)

const phase = computed(() => session.value?.phase ?? 'Lobby')
const winner = computed(() => session.value?.winner ?? null)
const roundId = computed(() => session.value?.roundId ?? 1)
// localStorage key is scoped per-round so a fresh round naturally clears
// the user's previously stored vote.
const voteKey = computed(() => `vote:${sessionId}:${roundId.value}`)

const roundCandidateSet = computed(() => {
  return new Set(session.value?.roundCandidates ?? [])
})

const voteCounts = computed(() => {
  const map = {}
  if (session.value?.votes) {
    for (const v of session.value.votes) map[v.name] = v.count
  }
  return map
})

const voters = computed(() => {
  const map = {}
  if (session.value?.votes) {
    for (const v of session.value.votes) map[v.name] = v.voters || []
  }
  return map
})

const winnerCandidate = computed(() => {
  if (!winner.value || !session.value?.candidates) return null
  return session.value.candidates.find(c => c.name === winner.value) ?? null
})

const tieBreakerCandidates = computed(() => {
  if (!session.value?.roundCandidates || !session.value?.candidates) return []
  const names = roundCandidateSet.value
  return session.value.candidates.filter(c => names.has(c.name))
})

const liveSecondsRemaining = computed(() => {
  const polled = session.value?.secondsRemaining
  if (polled === null || polled === undefined) return null
  if (phase.value !== 'Voting' && phase.value !== 'TieBreaker') return polled
  const elapsedSec = Math.max(0, (nowMs.value - lastPollAtMs.value) / 1000)
  const remaining = polled - elapsedSec
  return remaining > 0 ? remaining : 0
})

const countdownLabel = computed(() => {
  const s = liveSecondsRemaining.value
  if (s === null || s === undefined) return '--:--'
  const whole = Math.ceil(s)
  const mm = Math.floor(whole / 60).toString().padStart(2, '0')
  const ss = (whole % 60).toString().padStart(2, '0')
  return `${mm}:${ss}`
})

const canVoteNow = computed(() =>
  (phase.value === 'Voting' || phase.value === 'TieBreaker')
  && session.value?.secondsRemaining !== null
  && session.value?.secondsRemaining !== undefined
)

const timerIsRunning = computed(() =>
  session.value?.secondsRemaining !== null && session.value?.secondsRemaining !== undefined
)

// Re-load the stored vote whenever the round changes (new round -> different key).
watch(voteKey, (k) => {
  myVote.value = localStorage.getItem(k) || ''
}, { immediate: true })

function generateNickname() {
  return fetch('/api/nickname')
    .then(r => r.json())
    .then(d => d.nickname)
}

function applySession(payload) {
  session.value = payload
  lastPollAtMs.value = Date.now()
  nowMs.value = lastPollAtMs.value
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
    applySession(await res.json())
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
    applySession(await res.json())
  } catch {
    // ignore transient polling errors
  }
}

function startPolling() {
  if (!pollHandle) pollHandle = setInterval(refreshSession, POLL_INTERVAL_MS)
  if (!tickHandle) tickHandle = setInterval(() => { nowMs.value = Date.now() }, TICK_INTERVAL_MS)
}

function stopPolling() {
  if (pollHandle) { clearInterval(pollHandle); pollHandle = null }
  if (tickHandle) { clearInterval(tickHandle); tickHandle = null }
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
  if (voting.value) return
  if (myVote.value === candidateName) return
  voteError.value = ''
  voting.value = true
  try {
    const res = await fetch(`/api/sessions/${sessionId}/votes`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ nickname: myNickname.value, candidateName }),
    })
    if (res.status === 409) {
      const body = await res.json().catch(() => ({}))
      voteError.value = body?.error || 'Voting is not active right now.'
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
    localStorage.setItem(voteKey.value, candidateName)
    refreshSession()
  } catch (e) {
    voteError.value = `Network error: ${e.message}`
  } finally {
    voting.value = false
  }
}

async function startTimer() {
  timerError.value = ''
  const duration = Number(durationInput.value)
  if (!Number.isFinite(duration) || duration <= 0) {
    timerError.value = 'Duration must be a positive number of seconds.'
    return
  }
  startingTimer.value = true
  try {
    const res = await fetch(`/api/sessions/${sessionId}/timer/start`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ durationSeconds: Math.floor(duration) }),
    })
    if (!res.ok) {
      const body = await res.json().catch(() => ({}))
      timerError.value = body?.error || `Failed to start timer (${res.status})`
      return
    }
    await refreshSession()
  } catch (e) {
    timerError.value = `Network error: ${e.message}`
  } finally {
    startingTimer.value = false
  }
}

async function extendTimer() {
  timerError.value = ''
  extendingTimer.value = true
  try {
    const res = await fetch(`/api/sessions/${sessionId}/timer/extend`, { method: 'POST' })
    if (!res.ok) {
      const body = await res.json().catch(() => ({}))
      timerError.value = body?.error || `Failed to extend timer (${res.status})`
      return
    }
    await refreshSession()
  } catch (e) {
    timerError.value = `Network error: ${e.message}`
  } finally {
    extendingTimer.value = false
  }
}

async function endTimerNow() {
  timerError.value = ''
  endingTimer.value = true
  try {
    const res = await fetch(`/api/sessions/${sessionId}/timer/end`, { method: 'POST' })
    if (!res.ok) {
      const body = await res.json().catch(() => ({}))
      timerError.value = body?.error || `Failed to end timer (${res.status})`
      return
    }
    await refreshSession()
  } catch (e) {
    timerError.value = `Network error: ${e.message}`
  } finally {
    endingTimer.value = false
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

      <section v-if="session" class="timer-panel" :class="`phase-${phase.toLowerCase()}`">
        <div class="phase-row">
          <span class="phase-label">Phase:</span>
          <span class="phase-value">{{ phase }}</span>
        </div>

        <div v-if="phase === 'Lobby'" class="lobby-controls">
          <form @submit.prevent="startTimer" class="timer-form">
            <label>
              Duration (seconds):
              <input type="number" min="1" v-model.number="durationInput" :disabled="startingTimer" />
            </label>
            <button type="submit" :disabled="startingTimer">
              {{ startingTimer ? 'Starting…' : 'Start timer' }}
            </button>
          </form>
        </div>

        <div v-else-if="phase === 'TieBreaker' && !timerIsRunning" class="lobby-controls">
          <form @submit.prevent="startTimer" class="timer-form">
            <label>
              Tie-breaker duration (seconds):
              <input type="number" min="1" v-model.number="durationInput" :disabled="startingTimer" />
            </label>
            <button type="submit" :disabled="startingTimer">
              {{ startingTimer ? 'Starting…' : 'Start tie-breaker' }}
            </button>
          </form>
        </div>

        <div v-else-if="(phase === 'Voting' || phase === 'TieBreaker') && timerIsRunning" class="voting-controls">
          <div class="countdown">{{ countdownLabel }}</div>
          <button type="button" @click="extendTimer" :disabled="extendingTimer">
            {{ extendingTimer ? 'Extending…' : '+1 min' }}
          </button>
          <button type="button" class="end-now-btn" @click="endTimerNow" :disabled="endingTimer">
            {{ endingTimer ? 'Ending…' : 'End now' }}
          </button>
        </div>

        <p v-if="timerError" class="error">{{ timerError }}</p>
      </section>

      <section v-if="phase === 'Finished' && winnerCandidate" class="winner-panel">
        <img :src="winnerCandidate.spriteUrl" :alt="winnerCandidate.name" class="winner-sprite" />
        <div class="winner-name">{{ winnerCandidate.name }}</div>
        <div class="winner-label">Winner!</div>
      </section>

      <section v-else-if="phase === 'TieBreaker'" class="tie-panel">
        <strong>Tie!</strong> New round between:
        <span class="tied-names">{{ tieBreakerCandidates.map(c => c.name).join(', ') }}</span>
      </section>

      <p v-if="voteError" class="error">{{ voteError }}</p>
      <p v-if="myVote && (phase === 'Voting' || phase === 'TieBreaker')" class="voted-msg">
        You've voted in this round. Click another candidate to change your vote.
      </p>

      <p v-if="loading">Loading candidates…</p>
      <p v-else-if="error" class="error">{{ error }}</p>

      <div v-else-if="session && session.candidates.length === 0" class="empty">
        No Pokemon found for this letter.
      </div>

      <ul v-else-if="session && phase !== 'Finished'" class="grid">
        <li v-for="c in session.candidates" :key="c.name">
          <button
            type="button"
            class="candidate"
            :class="{
              'my-vote': myVote === c.name,
              'out-of-round': (phase === 'TieBreaker') && !roundCandidateSet.has(c.name)
            }"
            :disabled="voting || !canVoteNow || !roundCandidateSet.has(c.name) || myVote === c.name"
            :aria-pressed="myVote === c.name"
            :title="roundCandidateSet.has(c.name) && (voters[c.name]?.length)
              ? `Voted by: ${voters[c.name].join(', ')}`
              : ''"
            @click="castVote(c.name)"
          >
            <span v-if="roundCandidateSet.has(c.name)" class="count-badge">
              {{ voteCounts[c.name] ?? 0 }}
            </span>
            <span v-if="myVote === c.name" class="check" aria-hidden="true">✓</span>
            <img :src="c.spriteUrl" :alt="c.name" loading="lazy" />
            <span class="name">{{ c.name }}</span>
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
  font-family: var(--display);
  font-size: 1rem;
  padding: 0.15rem 0.6rem;
  border-radius: 999px;
  background: var(--surface-2);
  color: var(--text);
  border: 2px solid var(--border);
}
.title {
  margin-top: 2rem;
  margin-bottom: 1rem;
}
.share {
  margin-bottom: 1.5rem;
  padding: 0.75rem 1rem;
  background: var(--surface);
  border: 2px solid var(--border);
  border-radius: 8px;
}
.share label {
  font-size: 0.85rem;
  color: var(--text-muted);
}
.share-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-top: 0.4rem;
}
.share-link {
  flex: 1;
  font-family: var(--mono);
  font-size: 1rem;
  padding: 0.4rem 0.5rem;
}
.copied {
  font-size: 0.85rem;
  color: var(--accent);
}
.timer-panel {
  margin-bottom: 1.5rem;
  padding: 0.75rem 1rem;
  border: 2px solid var(--border);
  border-radius: 8px;
  background: var(--surface);
}
.phase-row {
  display: flex;
  gap: 0.5rem;
  align-items: baseline;
  font-size: 0.85rem;
  color: var(--text-muted);
}
.phase-value {
  font-family: var(--display);
  font-size: 1.2rem;
  color: var(--text);
}
.lobby-controls .timer-form {
  display: flex;
  gap: 0.75rem;
  align-items: center;
  margin-top: 0.5rem;
}
.lobby-controls input[type="number"] {
  width: 6rem;
  margin-left: 0.4rem;
}
.voting-controls {
  display: flex;
  gap: 1rem;
  align-items: center;
  margin-top: 0.5rem;
}
.end-now-btn {
  border-color: var(--danger);
  color: var(--danger);
  background: transparent;
}
.countdown {
  font-family: var(--display);
  font-size: 2.5rem;
  color: var(--text);
  min-width: 6rem;
}
.winner-panel {
  margin: 1.5rem 0;
  padding: 1.5rem;
  text-align: center;
  border: 3px solid var(--accent);
  border-radius: 12px;
  background: var(--accent-soft);
  box-shadow: 4px 4px 0 var(--shadow);
}
.winner-sprite {
  width: 192px;
  height: 192px;
  image-rendering: pixelated;
}
.winner-name {
  font-family: var(--display);
  font-size: 2.5rem;
  color: var(--accent-text);
  margin-top: 0.5rem;
  text-transform: capitalize;
}
.winner-label {
  font-family: var(--display);
  font-size: 1.5rem;
  color: var(--accent);
  margin-top: 0.25rem;
}
.tie-panel {
  margin: 1.5rem 0;
  padding: 1rem;
  text-align: center;
  background: var(--highlight);
  border: 2px solid var(--border);
  border-radius: 8px;
  color: var(--text);
}
.voted-msg {
  font-size: 0.9rem;
  color: var(--accent-text);
  background: var(--accent-soft);
  border: 2px solid var(--accent);
  padding: 0.4rem 0.6rem;
  border-radius: 6px;
  margin-bottom: 2rem;
}
.grid {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 1rem;
}
.grid > li {
  display: flex;
}
.candidate {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
  width: 100%;
  padding: 0.75rem;
  border: 2px solid var(--border);
  border-radius: 8px;
  background: var(--surface);
  font: inherit;
  color: inherit;
  text-align: center;
  cursor: pointer;
  box-shadow: 3px 3px 0 var(--shadow);
  transition: transform 0.05s, box-shadow 0.05s, border-color 0.15s, background 0.15s;
}
.candidate:hover:not(:disabled) {
  border-color: var(--border-strong);
  background: var(--highlight);
}
.candidate:active:not(:disabled) {
  transform: translate(2px, 2px);
  box-shadow: 1px 1px 0 var(--shadow);
}
.candidate:disabled {
  cursor: not-allowed;
  box-shadow: none;
}
.candidate.my-vote {
  border-color: var(--accent);
  box-shadow: 4px 4px 0 var(--accent-soft);
  background: var(--accent-soft);
}
.candidate.my-vote:disabled {
  cursor: default;
}
.candidate.out-of-round {
  opacity: 0.35;
  filter: grayscale(0.8);
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
.count-badge {
  position: absolute;
  top: 0.4rem;
  left: 0.5rem;
  min-width: 1.5rem;
  padding: 0.1rem 0.4rem;
  font-family: var(--display);
  font-size: 1rem;
  color: var(--text);
  background: var(--surface-2);
  border: 2px solid var(--border);
  border-radius: 999px;
  line-height: 1.2;
}
.check {
  position: absolute;
  top: 0.4rem;
  right: 0.5rem;
  width: 1.5rem;
  height: 1.5rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 0.95rem;
  font-weight: 700;
  color: var(--accent-text);
  background: var(--accent);
  border-radius: 999px;
}
.tied-names {
  margin-left: 0.4rem;
  font-weight: 600;
}
.error {
  color: var(--danger);
}
.empty {
  font-style: italic;
  color: var(--text-muted);
}
</style>
