/** True if this browser/GPU can create a WebGL context. Otherwise the page falls back to the 2D seat grid. */
export function supportsWebGL(): boolean {
  try {
    const canvas = document.createElement('canvas');
    return !!(canvas.getContext('webgl2') ?? canvas.getContext('webgl'));
  } catch {
    return false;
  }
}
