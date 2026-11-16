import { FormControl } from '@angular/forms';

import { cents, minLengthIfPresent, notBlank } from './validators';

describe('form validators', () => {
  it('rejects a text made only of spaces', () => {
    expect(notBlank(new FormControl('   ', { nonNullable: true }))).toEqual({ blank: true });
    expect(notBlank(new FormControl(' kran ', { nonNullable: true }))).toBeNull();
    // Puste pole to sprawa `Validators.required`.
    expect(notBlank(new FormControl('', { nonNullable: true }))).toBeNull();
  });

  it('allows at most two decimal places in an amount', () => {
    expect(cents(new FormControl<number | null>(120))).toBeNull();
    expect(cents(new FormControl<number | null>(99.5))).toBeNull();
    expect(cents(new FormControl<number | null>(99.999))).toEqual({ cents: true });
    expect(cents(new FormControl<number | null>(null))).toBeNull();
  });

  it('requires the minimal length only when an optional text is given', () => {
    const atLeastTwo = minLengthIfPresent(2);

    expect(atLeastTwo(new FormControl(''))).toBeNull();
    expect(atLeastTwo(new FormControl('   '))).toBeNull();
    expect(atLeastTwo(new FormControl(' a '))).toEqual({ minlength: true });
    expect(atLeastTwo(new FormControl('ok'))).toBeNull();
  });
});
