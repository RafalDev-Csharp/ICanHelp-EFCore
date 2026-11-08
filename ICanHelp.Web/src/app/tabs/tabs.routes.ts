import type { Routes } from '@angular/router';

import { TabsPage } from './tabs.page';

// Zakładki bez prefiksu `/tabs` – krótkie adresy, pod które później prowadzą powiadomienia i push (`/announcements/…`).
export const routes: Routes = [
  {
    path: '',
    component: TabsPage,
    children: [
      {
        path: 'announcements',
        loadComponent: () => import('../features/announcements/announcements.page').then((m) => m.AnnouncementsPage),
      },
      {
        path: 'add',
        loadComponent: () =>
          import('../features/announcements/add-announcement.page').then((m) => m.AddAnnouncementPage),
      },
      {
        path: 'conversations',
        loadComponent: () => import('../features/conversations/conversations.page').then((m) => m.ConversationsPage),
      },
      {
        path: 'notifications',
        loadComponent: () => import('../features/notifications/notifications.page').then((m) => m.NotificationsPage),
      },
      {
        path: 'profile',
        loadComponent: () => import('../features/profile/profile.page').then((m) => m.ProfilePage),
      },
      {
        path: '',
        redirectTo: '/announcements',
        pathMatch: 'full',
      },
    ],
  },
];
