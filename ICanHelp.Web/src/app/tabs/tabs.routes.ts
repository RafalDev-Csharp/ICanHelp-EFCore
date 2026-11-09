import type { Routes } from '@angular/router';

import { authGuard } from '../core/auth/auth.guard';

import { TabsPage } from './tabs.page';

// Zakładki bez prefiksu `/tabs` – krótkie adresy, pod które później prowadzą powiadomienia i push (`/announcements/…`).
// Ogłoszenia i profil są dostępne bez logowania (profil pokazuje wtedy „Zaloguj się”), reszta – przez authGuard.
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
        // Szczegóły w stosie zakładki Ogłoszenia (pasek zakładek zostaje). Publiczne – akcje same proszą o logowanie.
        path: 'announcements/:announcementId',
        loadComponent: () =>
          import('../features/announcements/details/announcement-details.page').then((m) => m.AnnouncementDetailsPage),
      },
      {
        path: 'add',
        canActivate: [authGuard],
        loadComponent: () =>
          import('../features/announcements/add-announcement.page').then((m) => m.AddAnnouncementPage),
      },
      {
        path: 'conversations',
        canActivate: [authGuard],
        loadComponent: () => import('../features/conversations/conversations.page').then((m) => m.ConversationsPage),
      },
      {
        path: 'notifications',
        canActivate: [authGuard],
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
