import type { ElementRef, OnDestroy } from '@angular/core';
import { ChangeDetectionStrategy, Component, afterNextRender, input, signal, viewChild } from '@angular/core';
import { IonIcon } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { chevronBackOutline, chevronForwardOutline } from 'ionicons/icons';

/** Jaka część przewijanego wiersza jest schowana – po której stronie pokazać strzałkę. */
export interface ScrollEdges {
  start: boolean;
  end: boolean;
}

/** Tolerancja w px – zaokrąglenia `scrollLeft` przy skalowaniu ekranu. */
const EDGE_TOLERANCE = 2;

export function scrollEdges(scrollLeft: number, clientWidth: number, scrollWidth: number): ScrollEdges {
  return {
    start: scrollLeft > EDGE_TOLERANCE,
    end: scrollLeft + clientWidth < scrollWidth - EDGE_TOLERANCE,
  };
}

/**
 * Poziomy wiersz (np. kafelki kategorii): na telefonie przewijany palcem, na komputerze – strzałkami po bokach
 * (tylko gdy jest coś schowane z tej strony) i kółkiem myszy (ruch w pionie przewija w poziomie).
 * Treść jest przycinana do szerokości wiersza – nie wystaje poza kolumnę strony.
 */
@Component({
  selector: 'app-scroll-row',
  template: `
    @if (edges().start) {
      <button type="button" class="arrow start" [attr.aria-label]="label() + ' – przewiń w lewo'" (click)="page(-1)">
        <ion-icon name="chevron-back-outline" aria-hidden="true"></ion-icon>
      </button>
    }
    <div #track class="track" [class.fade-start]="edges().start" [class.fade-end]="edges().end" (scroll)="update()">
      <ng-content></ng-content>
    </div>
    @if (edges().end) {
      <button type="button" class="arrow end" [attr.aria-label]="label() + ' – przewiń w prawo'" (click)="page(1)">
        <ion-icon name="chevron-forward-outline" aria-hidden="true"></ion-icon>
      </button>
    }
  `,
  styles: `
    :host {
      position: relative;
      display: block;
      min-width: 0;
    }
    .track {
      display: flex;
      gap: 8px;
      overflow-x: auto;
      scrollbar-width: none;
      scroll-behavior: smooth;
      overscroll-behavior-x: contain;
    }
    .track::-webkit-scrollbar {
      display: none;
    }
    .track.fade-start {
      mask-image: linear-gradient(to right, transparent 0, #000 32px);
    }
    .track.fade-end {
      mask-image: linear-gradient(to left, transparent 0, #000 32px);
    }
    .track.fade-start.fade-end {
      mask-image: linear-gradient(to right, transparent 0, #000 32px, #000 calc(100% - 32px), transparent 100%);
    }
    .arrow {
      position: absolute;
      top: 50%;
      z-index: 1;
      display: none;
      align-items: center;
      justify-content: center;
      width: 32px;
      height: 32px;
      padding: 0;
      transform: translateY(-50%);
      border: 1px solid var(--ion-color-step-200, #ccc);
      border-radius: 50%;
      background: var(--ion-background-color);
      color: var(--ion-text-color);
      box-shadow: 0 1px 4px rgb(0 0 0 / 20%);
      font-size: 1.125rem;
      cursor: pointer;
    }
    .arrow.start {
      left: 0;
    }
    .arrow.end {
      right: 0;
    }
    /* Strzałki tylko przy myszy – na ekranie dotykowym przewija się palcem. */
    @media (hover: hover) and (pointer: fine) {
      .arrow {
        display: flex;
      }
    }
  `,
  imports: [IonIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ScrollRowComponent implements OnDestroy {
  /** Nazwa wiersza dla czytnika ekranu w opisie strzałek, np. „Kategorie”. */
  readonly label = input('Lista');

  protected readonly edges = signal<ScrollEdges>({ start: false, end: false });
  private readonly track = viewChild.required<ElementRef<HTMLDivElement>>('track');
  private resizeObserver: ResizeObserver | null = null;
  private mutationObserver: MutationObserver | null = null;

  constructor() {
    addIcons({ chevronBackOutline, chevronForwardOutline });
    afterNextRender(() => {
      const element = this.track().nativeElement;
      element.addEventListener('wheel', this.onWheel, { passive: false });
      if (typeof ResizeObserver !== 'undefined') {
        this.resizeObserver = new ResizeObserver(() => this.update());
        this.resizeObserver.observe(element);
      }
      if (typeof MutationObserver !== 'undefined') {
        // Kafelki dochodzą po wczytaniu z API – wtedy wiersz może zacząć wystawać.
        this.mutationObserver = new MutationObserver(() => this.update());
        this.mutationObserver.observe(element, { childList: true, subtree: true });
      }
      this.update();
    });
  }

  ngOnDestroy(): void {
    this.track().nativeElement.removeEventListener('wheel', this.onWheel);
    this.resizeObserver?.disconnect();
    this.mutationObserver?.disconnect();
  }

  protected update(): void {
    const { scrollLeft, clientWidth, scrollWidth } = this.track().nativeElement;
    const next = scrollEdges(scrollLeft, clientWidth, scrollWidth);
    const current = this.edges();
    if (next.start !== current.start || next.end !== current.end) {
      this.edges.set(next);
    }
  }

  /** O ~80% szerokości – ostatni widoczny kafelek zostaje na ekranie jako punkt odniesienia. */
  protected page(direction: -1 | 1): void {
    const element = this.track().nativeElement;
    element.scrollBy({ left: direction * element.clientWidth * 0.8, behavior: 'smooth' });
  }

  /** Kółko myszy (ruch pionowy) przewija wiersz w poziomie, dopóki jest dokąd – potem przewija się strona. */
  private readonly onWheel = (event: WheelEvent): void => {
    if (Math.abs(event.deltaY) <= Math.abs(event.deltaX)) {
      return;
    }
    const element = this.track().nativeElement;
    const edges = scrollEdges(element.scrollLeft, element.clientWidth, element.scrollWidth);
    if ((event.deltaY < 0 && !edges.start) || (event.deltaY > 0 && !edges.end)) {
      return;
    }
    event.preventDefault();
    element.scrollBy({ left: event.deltaY, behavior: 'auto' });
  };
}
