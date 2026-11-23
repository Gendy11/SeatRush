import {
  AdditiveBlending,
  BoxGeometry,
  BufferAttribute,
  BufferGeometry,
  CanvasTexture,
  Color,
  DoubleSide,
  InstancedMesh,
  Mesh,
  MeshBasicMaterial,
  Object3D,
  PlaneGeometry,
  PointLight,
  Points,
  PointsMaterial,
  SpotLight,
  Vector3,
} from 'three';
import { HallLayout } from './seat-layout';

const BEAM_OPACITY = 0.055;

export interface RoomBox {
  width: number;
  front: number;
  back: number;
  ceiling: number;
}

export function roomBox(layout: HallLayout): RoomBox {
  return {
    width: layout.width + 5,
    front: layout.screen.z - 0.5,
    back: layout.backZ + 3,
    ceiling: layout.screen.y + layout.screen.height / 2 + 2.5,
  };
}

export function lensPosition(room: RoomBox): Vector3 {
  return new Vector3(0, room.ceiling - 0.35, room.back - 0.15);
}

export class Atmosphere {
  readonly objects: Object3D[] = [];

  private readonly screenLight: PointLight;
  private readonly frontWash: SpotLight;
  private readonly halo: Mesh<PlaneGeometry, MeshBasicMaterial>;
  private readonly beam: Mesh<BufferGeometry, MeshBasicMaterial>;
  private readonly dust: Points<BufferGeometry, PointsMaterial>;
  private readonly dustBase: Float32Array;
  private readonly dustPhase: Float32Array;
  private readonly mineGlow: InstancedMesh<PlaneGeometry, MeshBasicMaterial>;
  private readonly glowTexture = radialTexture();
  private readonly helper = new Object3D();

  private beamStrength = 1;
  private glowStrength = 1;

  constructor(
    private readonly layout: HallLayout,
    room: RoomBox,
    color: Color,
  ) {
    const { screen } = layout;

    this.halo = new Mesh(
      new PlaneGeometry(screen.width * 1.7, screen.height * 2.1),
      new MeshBasicMaterial({
        map: this.glowTexture,
        color,
        transparent: true,
        opacity: 0.35,
        blending: AdditiveBlending,
        depthWrite: false,
      }),
    );
    this.halo.position.set(0, screen.y, screen.z - 0.25);
    this.objects.push(this.halo);

    this.screenLight = new PointLight(color, 9, 22, 1.5);
    this.screenLight.position.set(0, screen.y, screen.z + 2.5);
    this.objects.push(this.screenLight);

    this.frontWash = new SpotLight(color, 14, 30, Math.PI / 4, 0.8, 1.4);
    this.frontWash.position.set(0, screen.y + screen.height / 2, screen.z + 0.5);
    this.frontWash.target.position.set(
      0,
      0,
      layout.rows[Math.min(2, layout.rows.length - 1)]?.z ?? 2,
    );
    this.objects.push(this.frontWash, this.frontWash.target);

    const lens = lensPosition(room);
    this.beam = beamMesh(lens, layout, color);
    this.objects.push(this.beam, projectorBox(lens));

    const count = 260;
    this.dustBase = new Float32Array(count * 3);
    this.dustPhase = new Float32Array(count);
    for (let i = 0; i < count; i++) {
      const t = 0.12 + Math.random() * 0.85;
      const sx = (Math.random() - 0.5) * screen.width * 0.9;
      const sy = screen.y + (Math.random() - 0.5) * screen.height * 0.9;
      this.dustBase.set(
        [lens.x + (sx - lens.x) * t, lens.y + (sy - lens.y) * t, lens.z + (screen.z - lens.z) * t],
        i * 3,
      );
      this.dustPhase[i] = Math.random() * Math.PI * 2;
    }
    const dustGeo = new BufferGeometry();
    dustGeo.setAttribute('position', new BufferAttribute(this.dustBase.slice(), 3));
    this.dust = new Points(
      dustGeo,
      new PointsMaterial({
        color: color.clone().lerp(new Color('#ffffff'), 0.6),
        size: 0.035,
        map: this.glowTexture,
        transparent: true,
        opacity: 0.55,
        blending: AdditiveBlending,
        depthWrite: false,
      }),
    );
    this.objects.push(this.dust);

    this.objects.push(aisleLights(layout));

    this.mineGlow = new InstancedMesh(
      new PlaneGeometry(1, 1).rotateX(-Math.PI / 2),
      new MeshBasicMaterial({
        map: this.glowTexture,
        color: '#f5c518',
        transparent: true,
        opacity: 0.75,
        blending: AdditiveBlending,
        depthWrite: false,
      }),
      Math.max(1, layout.seats.length),
    );
    this.mineGlow.count = 0;
    this.objects.push(this.mineGlow);
  }

  setBeamStrength(value: number): void {
    this.beamStrength = value;
  }

  setGlowStrength(value: number): void {
    this.glowStrength = value;
  }

  setMine(positions: readonly { x: number; y: number; z: number }[]): void {
    positions.forEach((p, i) => {
      this.helper.position.set(p.x, p.y + 0.015, p.z);
      this.helper.updateMatrix();
      this.mineGlow.setMatrixAt(i, this.helper.matrix);
    });
    this.mineGlow.count = positions.length;
    this.mineGlow.instanceMatrix.needsUpdate = true;
  }

  update(t: number): void {
    const breath = 1 + Math.sin(t * 0.6) * 0.08;
    const glow = this.glowStrength * breath;
    this.screenLight.intensity = 9 * glow;
    this.frontWash.intensity = 14 * glow;
    this.halo.material.opacity = 0.35 * glow;

    this.beam.material.opacity = BEAM_OPACITY * this.beamStrength * (0.9 + Math.sin(t * 1.3) * 0.1);
    this.dust.material.opacity = 0.55 * this.beamStrength;

    const positions = this.dust.geometry.attributes['position'] as BufferAttribute;
    const arr = positions.array as Float32Array;
    for (let i = 0; i < this.dustPhase.length; i++) {
      const ph = this.dustPhase[i];
      arr[i * 3] = this.dustBase[i * 3] + Math.sin(t * 0.21 + ph) * 0.12;
      arr[i * 3 + 1] = this.dustBase[i * 3 + 1] + Math.sin(t * 0.17 + ph * 1.7) * 0.18;
    }
    positions.needsUpdate = true;

    this.mineGlow.material.opacity = 0.6 + Math.sin(t * 2.4) * 0.15;
  }

  dispose(): void {
    this.glowTexture.dispose();
  }
}

function radialTexture(): CanvasTexture {
  const size = 128;
  const canvas = document.createElement('canvas');
  canvas.width = canvas.height = size;
  const ctx = canvas.getContext('2d')!;
  const g = ctx.createRadialGradient(size / 2, size / 2, 0, size / 2, size / 2, size / 2);
  g.addColorStop(0, 'rgba(255,255,255,1)');
  g.addColorStop(0.35, 'rgba(255,255,255,0.45)');
  g.addColorStop(1, 'rgba(255,255,255,0)');
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, size, size);
  return new CanvasTexture(canvas);
}

function beamMesh(
  lens: Vector3,
  layout: HallLayout,
  color: Color,
): Mesh<BufferGeometry, MeshBasicMaterial> {
  const { screen } = layout;
  const hw = screen.width * 0.48;
  const hh = screen.height * 0.48;
  const z = screen.z + 0.05;
  const corners = [
    [-hw, screen.y - hh],
    [hw, screen.y - hh],
    [hw, screen.y + hh],
    [-hw, screen.y + hh],
  ];

  const positions: number[] = [];
  const colors: number[] = [];
  const near = color.clone().lerp(new Color('#ffffff'), 0.5);
  const far = color.clone().multiplyScalar(0.12);
  for (let i = 0; i < 4; i++) {
    const a = corners[i];
    const b = corners[(i + 1) % 4];
    positions.push(lens.x, lens.y, lens.z, a[0], a[1], z, b[0], b[1], z);
    colors.push(near.r, near.g, near.b, far.r, far.g, far.b, far.r, far.g, far.b);
  }
  const geometry = new BufferGeometry();
  geometry.setAttribute('position', new BufferAttribute(new Float32Array(positions), 3));
  geometry.setAttribute('color', new BufferAttribute(new Float32Array(colors), 3));

  return new Mesh(
    geometry,
    new MeshBasicMaterial({
      vertexColors: true,
      transparent: true,
      opacity: BEAM_OPACITY,
      blending: AdditiveBlending,
      depthWrite: false,
      side: DoubleSide,
      fog: false,
    }),
  );
}

function projectorBox(lens: Vector3): Object3D {
  const glass = new Mesh(new PlaneGeometry(0.16, 0.12), new MeshBasicMaterial({ color: '#fff6dd' }));
  glass.rotation.y = Math.PI;
  glass.position.copy(lens);
  return glass;
}

function aisleLights(layout: HallLayout): InstancedMesh {
  const spots: { x: number; y: number; z: number }[] = [];
  for (const row of layout.rows) {
    for (const x of layout.aisleXs) spots.push({ x, y: row.y, z: row.z - 0.45 });
  }

  const mesh = new InstancedMesh(
    new BoxGeometry(0.22, 0.03, 0.05),
    new MeshBasicMaterial({ color: '#c98a3c' }),
    Math.max(1, spots.length),
  );
  const helper = new Object3D();
  spots.forEach((s, i) => {
    helper.position.set(s.x, s.y + 0.02, s.z);
    helper.updateMatrix();
    mesh.setMatrixAt(i, helper.matrix);
  });
  mesh.count = spots.length;
  return mesh;
}
