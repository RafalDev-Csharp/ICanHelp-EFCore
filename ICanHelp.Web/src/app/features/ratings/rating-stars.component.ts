import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { IonIcon } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { star, starHalf, starOutline } from 'ionicons/icons';

const STARS = [1, 2, 3, 4, 5] as const;

/** Ikona gwiazdki `index` (1–5) dla oceny `value` (średnia może mieć połówkę). */
export function starIcon(index: number, value: number): string {
  if (value >= index) {
    return 'star';
  }
  return value >= index - 0.5 ? 'star-half' : 'star-outline';
}

/** Pięć gwiazdek dla oceny albo średniej (z połówkami). Ozdoba – wartość podaje tekst obok albo `aria-label` rodzica. */
@Component({
  selector: 'app-rating-stars',
  template: `
    @for (index of stars; track index) {
      <ion-icon color="warning" [name]="starIcon(index, value())" aria-hidden="true"></ion-icon>
    }
  `,
  styles: `
    :host {
      display: contents;
    }
    ion-icon {
      font-size: 1.125rem;
    }
  `,
  imports: [IonIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RatingStarsComponent {
  readonly value = input.required<number>();

  protected readonly stars = STARS;
  protected readonly starIcon = starIcon;

  constructor() {
    addIcons({ star, starHalf, starOutline });
  }
}
