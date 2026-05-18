import { createRouter, createWebHistory } from 'vue-router'
import AppLayout from '../layout/AppLayout.vue'
import FlowBuilder from '../views/FlowBuilder.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/login',
      name: 'login',
      component: () => import('../views/Login.vue'),
      meta: { requiresAuth: false }
    },
    {
      path: '/',
      component: AppLayout,
      meta: { requiresAuth: true },
      children: [
        {
          path: '',
          name: 'projects',
          component: () => import('../views/Projects.vue')
        },
        {
          path: 'project/:id',
          name: 'project-detail',
          component: () => import('../views/ProjectDetail/ProjectDetail.vue')
        },
        {
          path: 'project/:projectId/config/:configId',
          name: 'config-detail',
          component: () => import('../views/ConfigDetail/ConfigDetail.vue')
        },
        {
          path: 'project/:projectId/knowledge',
          name: 'knowledge-base',
          component: () => import('../views/KnowledgeBase.vue')
        },
        {
          path: 'project/:projectId/rules',
          name: 'rules',
          component: () => import('../views/Rules.vue')
        },
        {
          path: 'project/:projectId/flow',
          name: 'flow-builder',
          component: FlowBuilder
        },
        {
          path: 'analytics',
          name: 'analytics',
          component: () => import('../views/Analytics.vue')
        },
        {
          path: 'logs',
          name: 'logs',
          component: () => import('../views/Logs.vue')
        },
        {
          path: 'playground',
          name: 'playground',
          component: () => import('../views/Playground.vue')
        },
        {
          path: 'docs',
          name: 'docs',
          component: () => import('../views/Docs.vue')
        },
        {
          path: 'live-chat',
          name: 'live-chat',
          component: () => import('../views/LiveChat.vue')
        }
      ]
    }
  ]
})

router.beforeEach((to) => {
  const requiresAuth = to.matched.some(record => record.meta.requiresAuth);
  const token = localStorage.getItem('acb_token');

  if (requiresAuth && !token) {
    return '/login';
  } else if (to.path === '/login' && token) {
    return '/';
  }
});

export default router
