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
        // Edycja w stosie zakładki Ogłoszenia (wejście ze szczegółów). Strona sama sprawdza właściciela i status.
        path: 'announcements/:announcementId/edit',
        canActivate: [authGuard],
        loadComponent: () =>
          import('../features/announcements/edit/edit-announcement.page').then((m) => m.EditAnnouncementPage),
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
        // Wątek w stosie zakładki Rozmowy (wejście też ze szczegółów ogłoszenia; obcy → „Nie ma takiej rozmowy”).
        path: 'conversations/:conversationId',
        canActivate: [authGuard],
        loadComponent: () => import('../features/conversations/conversation.page').then((m) => m.ConversationPage),
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
        path: 'profile/announcements',
        canActivate: [authGuard],
        loadComponent: () => import('../features/profile/my-announcements.page').then((m) => m.MyAnnouncementsPage),
      },
      {
        path: 'profile/liked',
        canActivate: [authGuard],
        loadComponent: () =>
          import('../features/profile/liked-announcements.page').then((m) => m.LikedAnnouncementsPage),
      },
      {
        path: 'profile/alerts',
        canActivate: [authGuard],
        loadComponent: () => import('../features/alerts/alerts.page').then((m) => m.AlertsPage),
      },
      {
        // Nowy alert: `?kind=Area|Route`. Przed `:alertId`, żeby „new” nie było traktowane jak id.
        path: 'profile/alerts/new',
        canActivate: [authGuard],
        loadComponent: () => import('../features/alerts/alert-edit.page').then((m) => m.AlertEditPage),
      },
      {
        path: 'profile/alerts/:alertId',
        canActivate: [authGuard],
        loadComponent: () => import('../features/alerts/alert-edit.page').then((m) => m.AlertEditPage),
      },
      {
        path: '',
        redirectTo: '/announcements',
        pathMatch: 'full',
      },
    ],
  },
];
