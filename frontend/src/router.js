import { createRouter, createWebHistory } from 'vue-router'
import CreateSession from './views/CreateSession.vue'
import SessionRoom from './views/SessionRoom.vue'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'create', component: CreateSession },
    { path: '/session/:id', name: 'session', component: SessionRoom, props: true },
  ],
})
