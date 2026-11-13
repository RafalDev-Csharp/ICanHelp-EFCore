import type { Routes } from '@angular/router';

import { authGuard } from '../core/auth/auth.guard';

import { TabsPage } from './tabs.page';

// Zakładki bez prefiksu `/tabs` – krótkie adresy, pod które prowadzą powiadomienia i push (`/announcements/…`,
// `/conversations/…`). Pasek (F11 B): Szukaj (`/announcements`) · Ulubione (`/liked`) · Dodaj (`/add`) · Zlecenia
// (`/conversations` – adres zostaje, żeby wątki z powiadomień były w stosie tej zakładki) · Konto (`/profile`).
// Powiadomienia (`/notifications`) – z dzwonka w nagłówku, bez zakładki.
// Szukaj i Konto są dostępne bez logowania (Konto pokazuje wtedy „Zaloguj się”), reszta – przez authGuard.
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
        // Szczegóły w stosie zakładki Szukaj (pasek zakładek zostaje). Publiczne – akcje same proszą o logowanie.
        path: 'announcements/:announcementId',
        loadComponent: () =>
          import('../features/announcements/details/announcement-details.page').then((m) => m.AnnouncementDetailsPage),
      },
      {
        // Edycja w stosie zakładki Szukaj (wejście ze szczegółów). Strona sama sprawdza właściciela i status.
        path: 'announcements/:announcementId/edit',
        canActivate: [authGuard],
        loadComponent: () =>
          import('../features/announcements/edit/edit-announcement.page').then((m) => m.EditAnnouncementPage),
      },
      {
        // F11 G: publiczny profil (statystyki Pomagam / Zlecam, przerwane zlecenia) – w stosie zakładki, z której
        // weszliśmy (szczegóły ogłoszenia, wątek). Publiczny, jak szczegóły.
        path: 'users/:userId',
        loadComponent: () => import('../features/profile/user-profile.page').then((m) => m.UserProfilePage),
      },
      {
        path: 'liked',
        canActivate: [authGuard],
        loadComponent: () =>
          import('../features/profile/liked-announcements.page').then((m) => m.LikedAnnouncementsPage),
      },
      {
        path: 'add',
        canActivate: [authGuard],
        loadComponent: () =>
          import('../features/announcements/add-announcement.page').then((m) => m.AddAnnouncementPage),
      },
      {
        // Zakładka „Zlecenia”: Zlecam / Pomagam.
        path: 'conversations',
        canActivate: [authGuard],
        loadComponent: () => import('../features/orders/orders.page').then((m) => m.OrdersPage),
      },
      {
        // Wszystkie rozmowy (z nagłówka „Zleceń”). Przed `:conversationId`, żeby „all” nie było traktowane jak id.
        path: 'conversations/all',
        canActivate: [authGuard],
        loadComponent: () => import('../features/conversations/conversations.page').then((m) => m.ConversationsPage),
      },
      {
        // Wątek w stosie zakładki Zlecenia (wejście też ze szczegółów ogłoszenia; obcy → „Nie ma takiej rozmowy”).
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
      // Stare adresy z profilu (F4–F6): moje ogłoszenia są teraz w „Zleceniach → Zlecam”, polubione – w zakładce.
      { path: 'profile/announcements', redirectTo: '/conversations', pathMatch: 'full' },
      { path: 'profile/liked', redirectTo: '/liked', pathMatch: 'full' },
      {
        path: 'profile/ratings',
        canActivate: [authGuard],
        loadComponent: () => import('../features/ratings/ratings.page').then((m) => m.RatingsPage),
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
