<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()
const letter = ref('Q')
const submitting = ref(false)
const error = ref('')

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
  </div>
</template>

<style scoped>
.create-session {
  max-width: 480px;
  margin: 4rem auto;
  text-align: center;
}
form {
  display: flex;
  gap: 0.5rem;
  justify-content: center;
  align-items: center;
  margin-top: 1.5rem;
}
input {
  font-size: 2rem;
  width: 3rem;
  text-align: center;
  text-transform: uppercase;
  padding: 0.25rem;
}
button {
  font-size: 1rem;
  padding: 0.5rem 1rem;
  cursor: pointer;
}
.error {
  color: #c0392b;
  margin-top: 1rem;
}
</style>
