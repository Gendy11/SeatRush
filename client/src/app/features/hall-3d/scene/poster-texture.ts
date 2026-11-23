import { CanvasTexture, Color, SRGBColorSpace } from 'three';

export interface ScreenImage {
  texture: CanvasTexture;

  color: Color;
}

const DEFAULT_LIGHT = new Color('#9fb4ff');

export async function createScreenTexture(
  posterUrl: string,
  title: string,
  aspect: number,
): Promise<ScreenImage> {
  const height = 1024;
  const width = Math.round(height * aspect);
  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  const ctx = canvas.getContext('2d');
  if (!ctx) throw new Error('2D canvas not available');

  let color = DEFAULT_LIGHT.clone();

  try {
    const img = await loadImage(posterUrl);
    color = dominantColor(img) ?? color;

    const cover = Math.max(width / img.width, height / img.height);

    if (img.width / img.height >= 1.3) {
      drawCentered(ctx, img, img.width * cover, img.height * cover, width, height);
    } else {
      ctx.filter = 'blur(40px) brightness(0.35) saturate(1.2)';
      drawCentered(ctx, img, img.width * cover * 1.1, img.height * cover * 1.1, width, height);

      ctx.filter = 'none';
      const fit = Math.min(width / img.width, height / img.height);
      drawCentered(ctx, img, img.width * fit, img.height * fit, width, height);
    }
  } catch {
    drawFallback(ctx, title, width, height);
  }

  const texture = new CanvasTexture(canvas);
  texture.colorSpace = SRGBColorSpace;
  texture.anisotropy = 4;
  return { texture, color };
}

function dominantColor(img: HTMLImageElement): Color | null {
  const size = 24;
  const canvas = document.createElement('canvas');
  canvas.width = canvas.height = size;
  const ctx = canvas.getContext('2d', { willReadFrequently: true });
  if (!ctx) return null;

  let data: Uint8ClampedArray;
  try {
    ctx.drawImage(img, 0, 0, size, size);
    data = ctx.getImageData(0, 0, size, size).data;
  } catch {
    return null;
  }

  let r = 0,
    g = 0,
    b = 0,
    total = 0;
  const pixel = new Color();
  const hsl = { h: 0, s: 0, l: 0 };
  for (let i = 0; i < data.length; i += 4) {
    pixel.setRGB(data[i] / 255, data[i + 1] / 255, data[i + 2] / 255).getHSL(hsl);
    const weight = hsl.s * hsl.s * (1 - Math.abs(hsl.l - 0.5) * 2) + 0.02;
    r += pixel.r * weight;
    g += pixel.g * weight;
    b += pixel.b * weight;
    total += weight;
  }

  new Color(r / total, g / total, b / total).getHSL(hsl);
  return new Color().setHSL(hsl.h, Math.min(0.85, Math.max(0.35, hsl.s * 1.3)), 0.62);
}

function loadImage(url: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const img = new Image();

    img.crossOrigin = 'anonymous';
    img.decoding = 'async';
    img.onload = () => resolve(img);
    img.onerror = () => reject(new Error(`Poster failed to load: ${url}`));
    img.src = url;
  });
}

function drawCentered(
  ctx: CanvasRenderingContext2D,
  img: HTMLImageElement,
  w: number,
  h: number,
  canvasW: number,
  canvasH: number,
): void {
  ctx.drawImage(img, (canvasW - w) / 2, (canvasH - h) / 2, w, h);
}

function drawFallback(
  ctx: CanvasRenderingContext2D,
  title: string,
  width: number,
  height: number,
): void {
  const gradient = ctx.createLinearGradient(0, 0, width, height);
  gradient.addColorStop(0, '#1b1b22');
  gradient.addColorStop(1, '#0b0b0d');
  ctx.fillStyle = gradient;
  ctx.fillRect(0, 0, width, height);

  ctx.fillStyle = '#f4f4f5';
  ctx.font = `800 ${Math.round(height * 0.12)}px Montserrat, system-ui, sans-serif`;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(title.toUpperCase(), width / 2, height / 2, width * 0.85);
}
