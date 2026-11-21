import { PerspectiveCamera, Vector3 } from 'three';

export interface CameraPose {
  position: Vector3;
  target: Vector3;
}

/** Smooth start and end. */
export const easeInOutCubic = (t: number): number =>
  t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;

/**
 * A camera move from one pose to another over time, driven by the render loop (no animation library).
 * Position and look-at target are interpolated together, so the camera never "snaps" its aim.
 */
export class CameraFlight {
  private readonly start = performance.now();
  private readonly lookAt = new Vector3();

  constructor(
    private readonly camera: PerspectiveCamera,
    private readonly from: CameraPose,
    readonly to: CameraPose,
    private readonly durationMs: number,
    private readonly onDone: () => void,
  ) {}

  /** Moves the camera for this frame. Returns true when the flight has finished. */
  update(now: number): boolean {
    const t = this.durationMs <= 0 ? 1 : Math.min(1, (now - this.start) / this.durationMs);
    const e = easeInOutCubic(t);

    this.camera.position.lerpVectors(this.from.position, this.to.position, e);
    this.lookAt.lerpVectors(this.from.target, this.to.target, e);
    this.camera.lookAt(this.lookAt);

    if (t >= 1) {
      this.onDone();
      return true;
    }
    return false;
  }
}
