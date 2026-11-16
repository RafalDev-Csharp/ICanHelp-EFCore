import { scrollEdges } from './scroll-row.component';

describe('scrollEdges', () => {
  it('reports nothing hidden when the content fits', () => {
    expect(scrollEdges(0, 700, 700)).toEqual({ start: false, end: false });
  });

  it('reports hidden content at the end at the start of the row', () => {
    expect(scrollEdges(0, 700, 1200)).toEqual({ start: false, end: true });
  });

  it('reports hidden content on both sides in the middle', () => {
    expect(scrollEdges(250, 700, 1200)).toEqual({ start: true, end: true });
  });

  it('ignores sub-pixel rounding at the end', () => {
    expect(scrollEdges(499, 700, 1200)).toEqual({ start: true, end: false });
  });
});
