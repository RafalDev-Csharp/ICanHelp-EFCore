/**
 * `errorCode` z ProblemDetails (nazwa wyjątku backendu bez „Exception”, `DomainExceptionHandler`) → komunikat po polsku.
 * Nowy wyjątek w backendzie = nowa linia tutaj; bez niej użytkownik zobaczy komunikat ogólny dla kodu HTTP.
 */
export const ERROR_MESSAGES: Readonly<Record<string, string>> = {
  // Konto i sesja
  InvalidCredentials: 'Nieprawidłowy e-mail lub hasło.',
  InvalidRefreshToken: 'Sesja wygasła – zaloguj się ponownie.',
  UserLocked: 'To konto jest zablokowane.',
  UserNotFound: 'Nie znaleziono użytkownika.',
  EmailAlreadyInUse: 'Konto z tym adresem e-mail już istnieje.',
  UserNameAlreadyInUse: 'Ta nazwa użytkownika jest już zajęta.',
  // Błędne dane (400)
  InvalidEmail: 'Niepoprawny adres e-mail.',
  InvalidPassword: 'Hasło musi mieć od 6 do 200 znaków.',
  InvalidPhoneNumber: 'Niepoprawny numer telefonu – podaj 8–15 cyfr, np. +48600100200.',
  InvalidFullName: 'Imię i nazwisko musi mieć od 3 do 100 znaków.',
  InvalidStringObject: 'Jedno z pól ma niepoprawną długość – sprawdź formularz.',
  InvalidDescription: 'Opis musi mieć od 2 do 500 znaków.',
  InvalidIdentity: 'Niepoprawny identyfikator.',
  InvalidCategory: 'Niepoprawna kategoria lub podkategoria.',
  InvalidDistanceFilter: 'Niepoprawny filtr odległości.',
  InvalidImage: 'Tego zdjęcia nie da się dodać – sprawdź format i rozmiar.',
  InvalidAlert: 'Niepoprawne ustawienia alertu.',
  // Nie znaleziono (404)
  AnnouncementNotFound: 'Nie znaleziono ogłoszenia – mogło zostać usunięte.',
  OfferNotFound: 'Nie znaleziono oferty.',
  ImageNotFound: 'Nie znaleziono zdjęcia.',
  ConversationNotFound: 'Nie znaleziono rozmowy.',
  NotificationNotFound: 'Nie znaleziono powiadomienia.',
  AlertNotFound: 'Nie znaleziono alertu.',
  // Konflikty (409)
  DomainRule: 'Tej operacji nie można teraz wykonać – stan ogłoszenia lub oferty się zmienił.',
  ConcurrencyConflict: 'Ktoś zmienił te dane w międzyczasie – odśwież i spróbuj ponownie.',
  AlreadyRated: 'To zlecenie jest już przez Ciebie ocenione.',
};

/** Komunikat, gdy nie ma `errorCode` (np. błąd walidacji ASP.NET, 500, brak sieci). */
export function messageForStatus(status: number): string {
  if (status === 0) {
    return 'Brak połączenia z serwerem – sprawdź internet.';
  }
  if (status >= 500) {
    return 'Błąd serwera – spróbuj ponownie za chwilę.';
  }
  switch (status) {
    case 400:
      return 'Niepoprawne dane – sprawdź formularz.';
    case 401:
      return 'Zaloguj się, aby kontynuować.';
    case 403:
      return 'Nie masz uprawnień do tej operacji.';
    case 404:
      return 'Nie znaleziono.';
    case 409:
      return 'Konflikt – odśwież i spróbuj ponownie.';
    case 413:
      return 'Plik jest za duży.';
    case 429:
      return 'Za dużo prób – spróbuj za chwilę.';
    default:
      return 'Coś poszło nie tak – spróbuj ponownie.';
  }
}

export function errorMessage(status: number, errorCode: string | undefined): string {
  return (errorCode && ERROR_MESSAGES[errorCode]) || messageForStatus(status);
}
