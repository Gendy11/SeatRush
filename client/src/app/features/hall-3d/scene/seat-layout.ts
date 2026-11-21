/**
 * Turns seat data into 3D positions. Pure function, no three.js, so it's easy to test.
 *
 * Coordinate system (metres): x = left/right (0 = centre aisle line), y = up, z = towards the back.
 * The screen sits at negative z; row A is the front row, and every next row is further back and higher
 * (stadium seating).
 */

export interface LayoutSeatInput {
  id: number;
  number: number;
}

export interface LayoutRowInput {
  label: string;
  seats: readonly LayoutSeatInput[];
}

export interface LayoutOptions {
  seatWidth: number;
  seatGap: number;
  aisleWidth: number;
  rowDepth: number;
  /** How much higher each row is than the one in front. */
  rowRise: number;
  /** Distance from the screen to the front row. */
  screenDistance: number;
  /** Eye height above a row's floor when seated. */
  eyeHeight: number;
  /** Width ÷ height of the screen. */
  screenAspect: number;
  /** Seat numbers after which an aisle runs, per row length. Default: {@link defaultAisles}. */
  aislesFor: (seatsInRow: number) => readonly number[];
}

export interface SeatPlacement {
  seatId: number;
  row: string;
  number: number;
  rowIndex: number;
  x: number;
  /** Floor height of the seat's row. */
  y: number;
  z: number;
}

export interface RowPlacement {
  label: string;
  index: number;
  y: number;
  z: number;
  /** Leftmost and rightmost seat centres. */
  minX: number;
  maxX: number;
}

export interface ScreenPlacement {
  width: number;
  height: number;
  /** Centre of the screen. */
  y: number;
  z: number;
}

export interface HallLayout {
  seats: SeatPlacement[];
  rows: RowPlacement[];
  /** x of every aisle centre line (taken from the widest row). */
  aisleXs: number[];
  /** Total width covered by seats. */
  width: number;
  /** z of the back row. */
  backZ: number;
  /** Floor height of the back row. */
  backY: number;
  screen: ScreenPlacement;
  eyeHeight: number;
}

/**
 * Aisles that look like a real cinema:
 * small rows → none, medium → one centre aisle, wide rows → two aisles splitting the row into ¼ · ½ · ¼.
 */
export function defaultAisles(seatsInRow: number): number[] {
  if (seatsInRow < 10) return [];
  if (seatsInRow < 16) return [Math.ceil(seatsInRow / 2)];
  const side = Math.round(seatsInRow / 4);
  return [side, seatsInRow - side];
}

export const DEFAULT_LAYOUT: LayoutOptions = {
  seatWidth: 0.56,
  seatGap: 0.08,
  aisleWidth: 1.1,
  rowDepth: 1.0,
  rowRise: 0.34,
  screenDistance: 5.5,
  eyeHeight: 1.15,
  screenAspect: 2.2,
  aislesFor: defaultAisles,
};

export function buildHallLayout(
  rows: readonly LayoutRowInput[],
  options: Partial<LayoutOptions> = {},
): HallLayout {
  const o: LayoutOptions = { ...DEFAULT_LAYOUT, ...options };
  const pitch = o.seatWidth + o.seatGap;

  const seats: SeatPlacement[] = [];
  const placedRows: RowPlacement[] = [];
  let widest: { count: number; aisleXs: number[] } = { count: 0, aisleXs: [] };

  rows.forEach((row, rowIndex) => {
    const ordered = [...row.seats].sort((a, b) => a.number - b.number);
    const aisles = new Set(o.aislesFor(ordered.length));

    // Lay seats left to right, inserting aisle gaps, then centre the row on x = 0.
    const xs: number[] = [];
    const aisleXs: number[] = [];
    let cursor = 0;
    ordered.forEach((seat, i) => {
      xs.push(cursor);
      cursor += pitch;
      if (aisles.has(i + 1) && i < ordered.length - 1) {
        aisleXs.push(cursor - pitch / 2 + o.aisleWidth / 2);
        cursor += o.aisleWidth;
      }
    });
    const rowWidth = xs.length ? xs[xs.length - 1] : 0;
    const shift = -rowWidth / 2;

    const y = rowIndex * o.rowRise;
    const z = rowIndex * o.rowDepth;

    ordered.forEach((seat, i) =>
      seats.push({
        seatId: seat.id,
        row: row.label,
        number: seat.number,
        rowIndex,
        x: xs[i] + shift,
        y,
        z,
      }),
    );

    placedRows.push({
      label: row.label,
      index: rowIndex,
      y,
      z,
      minX: (xs[0] ?? 0) + shift,
      maxX: rowWidth + shift,
    });

    if (ordered.length > widest.count)
      widest = { count: ordered.length, aisleXs: aisleXs.map((x) => x + shift) };
  });

  const width = Math.max(0, ...placedRows.map((r) => r.maxX - r.minX)) + o.seatWidth;
  const screenWidth = Math.max(6, width * 0.95);
  const screenHeight = screenWidth / o.screenAspect;
  const lastRow = placedRows.at(-1);

  return {
    seats,
    rows: placedRows,
    aisleXs: widest.aisleXs,
    width,
    backZ: lastRow?.z ?? 0,
    backY: lastRow?.y ?? 0,
    // Bottom edge of the screen a little above the front-row eye line, so heads don't block it.
    screen: {
      width: screenWidth,
      height: screenHeight,
      y: 1.6 + screenHeight / 2,
      z: -o.screenDistance,
    },
    eyeHeight: o.eyeHeight,
  };
}
