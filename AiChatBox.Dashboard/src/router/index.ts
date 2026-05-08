import { createRouter, createWebHistory } from 'vue-router'
import AppLayout from '../layout/AppLayout.vue'

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
      path: '/docs',
      name: 'docs',
      component: () => import('../views/Docs.vue'),
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
          component: () => import('../views/ProjectDetail.vue')
        },
        {
          path: 'project/:projectId/config/:configId',
          name: 'config-detail',
          component: () => import('../views/ConfigDetail.vue')
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
        }
      ]
    }
  ]
})

router.beforeEach((to, _from, next) => {
  const requiresAuth = to.matched.some(record => record.meta.requiresAuth);
  const token = localStorage.getItem('acb_token');

  if (requiresAuth && !token) {
    next('/login');
  } else if (to.path === '/login' && token) {
    next('/');
  } else {
    next();
  }
});

export default router
