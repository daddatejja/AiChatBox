import { createRouter, createWebHistory } from 'vue-router'
import AppLayout from '../layout/AppLayout.vue'
import { useApi } from '../composables/useApi'


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
      path: '/embed/:projectId',
      name: 'embedded-view',
      component: () => import('../views/EmbeddedView.vue'),
      meta: { requiresAuth: true }
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
          path: 'project/:id/flow',
          name: 'flow-list',
          component: () => import('../views/FlowList.vue')
        },
        {
          path: 'project/:projectId/flow/:flowId',
          name: 'flow-builder',
          component: () => import('../views/FlowBuilder/FlowBuilder.vue')
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
        },
        // ─── Admin Routes ───
        {
          path: 'admin',
          name: 'admin-dashboard',
          component: () => import('../views/admin/AdminDashboard.vue'),
          meta: { requiredRole: 'SystemAdmin' }
        },
        {
          path: 'admin/partners',
          name: 'admin-partners',
          component: () => import('../views/admin/AdminPartners.vue'),
          meta: { requiredRole: 'SystemAdmin' }
        },
        {
          path: 'admin/users',
          name: 'admin-users',
          component: () => import('../views/admin/AdminUsers.vue'),
          meta: { requiredRole: 'SystemAdmin' }
        },
        // ─── Developer/Partner Routes ───
        {
          path: 'developer',
          name: 'dev-dashboard',
          component: () => import('../views/developer/DevDashboard.vue'),
          meta: { requiredRole: 'PartnerDeveloper' }
        },
        {
          path: 'developer/tenants',
          name: 'dev-tenants',
          component: () => import('../views/developer/DevTenants.vue'),
          meta: { requiredRole: 'PartnerDeveloper' }
        },
        {
          path: 'developer/settings',
          name: 'dev-settings',
          component: () => import('../views/developer/DevSettings.vue'),
          meta: { requiredRole: 'PartnerDeveloper' }
        }
      ]
    }
  ]
})

router.beforeEach(async (to) => {
  const tokenQuery = to.query.token as string;
  if (tokenQuery) {
    localStorage.setItem('acb_token', tokenQuery);
  }

  const requiresAuth = to.matched.some(record => record.meta.requiresAuth);
  const token = localStorage.getItem('acb_token');
  let userRaw = localStorage.getItem('acb_user');

  if (token && (!userRaw || tokenQuery)) {
    try {
      const { apiFetch } = useApi();
      const res = await apiFetch('/api/auth/me');
      if (res.ok) {
        const data = await res.json();
        localStorage.setItem('acb_user', JSON.stringify({
          username: data.username,
          email: data.email,
          role: data.role,
          partnerAccountId: data.partnerAccountId
        }));
        localStorage.setItem('acb_username', data.username);
        userRaw = localStorage.getItem('acb_user');
      }
    } catch (err) {
      console.error('Failed to retrieve user profile in router guard', err);
    }
  }

  if (requiresAuth && !token) {
    return '/login';
  } else if (to.path === '/login' && token && !tokenQuery) {
    return '/';
  }

  // Handle Embed token route specifically
  if (to.path.startsWith('/embed') && tokenQuery) {
    return;
  }

  // Role guard check
  const requiredRole = to.meta.requiredRole as string | undefined;
  if (requiredRole) {
    const user = userRaw ? JSON.parse(userRaw) : null;
    
    if (!user || (user.role !== requiredRole && user.role !== 'SystemAdmin')) {
      return '/'; // Unauthorized redirect
    }
  }
});

export default router
