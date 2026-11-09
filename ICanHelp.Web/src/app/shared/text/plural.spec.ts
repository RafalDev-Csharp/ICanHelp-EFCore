import { pluralPl } from './plural';

describe('pluralPl', () => {
  const ratings = (count: number): string => `${count} ${pluralPl(count, 'ocena', 'oceny', 'ocen')}`;

  it('uses the singular only for exactly one', () => {
    expect(ratings(1)).toBe('1 ocena');
  });

  it('uses the "few" form for 2–4, except 12–14', () => {
    expect(ratings(2)).toBe('2 oceny');
    expect(ratings(4)).toBe('4 oceny');
    expect(ratings(22)).toBe('22 oceny');
    expect(ratings(12)).toBe('12 ocen');
    expect(ratings(14)).toBe('14 ocen');
  });

  it('uses the "many" form for the rest', () => {
    expect(ratings(0)).toBe('0 ocen');
    expect(ratings(5)).toBe('5 ocen');
    expect(ratings(11)).toBe('11 ocen');
    expect(ratings(21)).toBe('21 ocen');
    expect(ratings(25)).toBe('25 ocen');
  });
});
