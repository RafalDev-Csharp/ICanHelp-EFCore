import type {
  AbstractControl,
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ValidationErrors,
  ValidatorFn,
} from '@angular/forms';
import { Validators } from '@angular/forms';

import type { MakeOffer, OfferDto } from '../../core/api/api-client';
import { parseApiDate } from '../../shared/pipes/time-ago.pipe';
import { ANNOUNCEMENT_RULES, DEFAULT_CURRENCY } from '../announcements/form/announcement-form';

/** Reguły z backendu (`Offer`, `Description`): cena > 0, koniec po początku, początek nie w przeszłości, wiadomość 2–500. */
export const OFFER_RULES = {
  price: ANNOUNCEMENT_RULES.budget,
  message: { min: 2, max: 500 },
  /** Domyślny czas pracy w formularzu (godziny). */
  defaultDurationHours: 2,
} as const;

const HOUR = 3_600_000;

export type OfferForm = FormGroup<{
  price: FormControl<number | null>;
  /** `datetime-local` – „2026-11-10T17:00”, czas lokalny telefonu. */
  start: FormControl<string>;
  end: FormControl<string>;
  message: FormControl<string>;
}>;

export type OfferFormValue = ReturnType<OfferForm['getRawValue']>;

/** Wartość dla `<input type="datetime-local">` (czas lokalny, bez sekund). */
export function toLocalInput(date: Date): string {
  const pad = (value: number): string => String(value).padStart(2, '0');
  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
    `T${pad(date.getHours())}:${pad(date.getMinutes())}`
  );
}

/** Czas lokalny z pola → ISO w UTC dla API; pusty/niepoprawny → `null`. */
export function fromLocalInput(value: string): Date | null {
  if (!value) {
    return null;
  }
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

function cents(control: AbstractControl<number | null>): ValidationErrors | null {
  const value = control.value;
  return value !== null && Math.round(value * 100) !== value * 100 ? { cents: true } : null;
}

function messageLength(control: AbstractControl<string>): ValidationErrors | null {
  const length = control.value.trim().length;
  return length > 0 && length < OFFER_RULES.message.min ? { minlength: true } : null;
}

/** Początek nie w przeszłości (backend odrzuci), koniec po początku. `now` – do testów. */
export function offerDatesValidator(now: () => Date = (): Date => new Date()): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const start = fromLocalInput((group as OfferForm).controls.start.value);
    const end = fromLocalInput((group as OfferForm).controls.end.value);
    if (!start || !end) {
      return { dates: true };
    }
    // Minuta zapasu: formularz wypełniony „na teraz” nie przepada przez sekundy między wpisaniem a wysłaniem.
    if (start.getTime() < now().getTime() - 60_000) {
      return { startInPast: true };
    }
    return end > start ? null : { endBeforeStart: true };
  };
}

/** Domyślny termin: najbliższa pełna godzina za co najmniej godzinę, na 2 h. */
export function defaultTerm(now: Date): { start: Date; end: Date } {
  const start = new Date(now.getTime() + HOUR);
  start.setMinutes(0, 0, 0);
  if (start.getTime() - now.getTime() < HOUR) {
    start.setTime(start.getTime() + HOUR);
  }
  return { start, end: new Date(start.getTime() + OFFER_RULES.defaultDurationHours * HOUR) };
}

/**
 * Formularz oferty / kontroferty. `basis` – oferta, na którą odpowiadamy (podpowiada cenę i termin, o ile nie minął),
 * `suggestedPrice` – np. budżet ogłoszenia przy pierwszej ofercie.
 */
export function createOfferForm(
  fb: NonNullableFormBuilder,
  options: { basis?: OfferDto; suggestedPrice?: number; now?: () => Date } = {},
): OfferForm {
  const now = options.now ?? ((): Date => new Date());
  const { price, message } = OFFER_RULES;
  let term = defaultTerm(now());
  if (options.basis && parseApiDate(options.basis.proposedStart) > now()) {
    term = { start: parseApiDate(options.basis.proposedStart), end: parseApiDate(options.basis.proposedEnd) };
  }

  return fb.group(
    {
      price: fb.control<number | null>(options.basis?.price.amount ?? options.suggestedPrice ?? null, [
        Validators.required,
        Validators.min(price.min),
        Validators.max(price.max),
        cents,
      ]),
      start: [toLocalInput(term.start), Validators.required],
      end: [toLocalInput(term.end), Validators.required],
      message: ['', [Validators.maxLength(message.max), messageLength]],
    },
    { validators: offerDatesValidator(now) },
  );
}

/** Ciało `POST …/offers` i `…/counter` (te same pola). */
export function toOfferCommand(value: OfferFormValue): MakeOffer {
  const message = value.message.trim();
  return {
    price: { amount: value.price ?? 0, currency: DEFAULT_CURRENCY },
    proposedStart: (fromLocalInput(value.start) ?? new Date()).toISOString(),
    proposedEnd: (fromLocalInput(value.end) ?? new Date()).toISOString(),
    message: message || undefined,
  };
}
