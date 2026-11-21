import { buildHallLayout, defaultAisles, DEFAULT_LAYOUT, LayoutRowInput } from './seat-layout';

function grid(rows: number, perRow: number): LayoutRowInput[] {
  let id = 1;
  return Array.from({ length: rows }, (_, r) => ({
    label: String.fromCharCode(65 + r),
    seats: Array.from({ length: perRow }, (_, n) => ({ id: id++, number: n + 1 })),
  }));
}

describe('defaultAisles', () => {
  it('adds no aisle to small rows, one to medium rows and two to wide rows', () => {
    expect(defaultAisles(8)).toEqual([]);
    expect(defaultAisles(12)).toEqual([6]);
    expect(defaultAisles(18)).toEqual([5, 13]);
  });
});

describe('buildHallLayout', () => {
  const layout = buildHallLayout(grid(12, 18));

  it('places every seat exactly once', () => {
    expect(layout.seats).toHaveLength(12 * 18);
    expect(new Set(layout.seats.map((s) => s.seatId)).size).toBe(12 * 18);
  });

  it('centres each row on x = 0', () => {
    const front = layout.rows[0];
    expect(front.minX).toBeCloseTo(-front.maxX, 6);
  });

  it('steps rows back and up (stadium seating), with row A at the front', () => {
    expect(layout.rows[0].label).toBe('A');
    for (let i = 1; i < layout.rows.length; i++) {
      expect(layout.rows[i].z).toBeGreaterThan(layout.rows[i - 1].z);
      expect(layout.rows[i].y).toBeGreaterThan(layout.rows[i - 1].y);
    }
  });

  it('leaves an aisle-sized gap after the aisle seats', () => {
    const rowA = layout.seats.filter((s) => s.rowIndex === 0).sort((a, b) => a.number - b.number);
    const pitch = DEFAULT_LAYOUT.seatWidth + DEFAULT_LAYOUT.seatGap;
    const gap = (n: number) => rowA[n].x - rowA[n - 1].x; // gap between seat n and n+1
    expect(gap(1)).toBeCloseTo(pitch, 6);
    expect(gap(5)).toBeCloseTo(pitch + DEFAULT_LAYOUT.aisleWidth, 6); // after seat 5
    expect(gap(13)).toBeCloseTo(pitch + DEFAULT_LAYOUT.aisleWidth, 6); // after seat 13
    expect(layout.aisleXs).toHaveLength(2);
  });

  it('puts the screen in front of the first row and wide enough for the hall', () => {
    expect(layout.screen.z).toBeLessThan(layout.rows[0].z);
    expect(layout.screen.width).toBeGreaterThanOrEqual(layout.width * 0.9);
  });

  it('orders seats by number even if the input is shuffled', () => {
    const shuffled: LayoutRowInput[] = [
      {
        label: 'A',
        seats: [
          { id: 3, number: 3 },
          { id: 1, number: 1 },
          { id: 2, number: 2 },
        ],
      },
    ];
    const xs = buildHallLayout(shuffled)
      .seats.sort((a, b) => a.number - b.number)
      .map((s) => s.x);
    expect(xs[0]).toBeLessThan(xs[1]);
    expect(xs[1]).toBeLessThan(xs[2]);
  });

  it('handles an empty hall', () => {
    const empty = buildHallLayout([]);
    expect(empty.seats).toEqual([]);
    expect(empty.screen.width).toBeGreaterThan(0);
  });
});
