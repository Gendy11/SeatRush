import {
  AmbientLight,
  BoxGeometry,
  BufferGeometry,
  Color,
  DirectionalLight,
  Fog,
  Material,
  Mesh,
  MeshBasicMaterial,
  MeshStandardMaterial,
  Object3D,
  PerspectiveCamera,
  Spherical,
  PlaneGeometry,
  PointLight,
  Raycaster,
  Scene,
  SRGBColorSpace,
  Texture,
  Vector2,
  Vector3,
  WebGLRenderer,
  InstancedMesh,
} from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { RoundedBoxGeometry } from 'three/examples/jsm/geometries/RoundedBoxGeometry.js';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';
import { Atmosphere, lensPosition, RoomBox, roomBox } from './atmosphere';
import { CameraFlight, CameraPose } from './camera-flight';
import { createScreenTexture } from './poster-texture';
import { SEAT_COLORS, SeatVisualState } from './seat-colors';
import { HallLayout, SeatPlacement } from './seat-layout';

const BACKGROUND = new Color('#07070a');

const TILT_PER_METRE = 0.013;

export interface HallSceneEvents {
  hover: (seatId: number | null, x: number, y: number) => void;

  select: (seatId: number) => void;

  intro: (running: boolean) => void;

  posterColor: (css: string) => void;
}

const INTRO = {
  beamOn: 700,
  posterFrom: 500,
  posterTo: 1500,
  moveFrom: 1300,
  moveMs: 2000,
} as const;

const HELD_DIM = new Color('#6b4f1f');
const HELD_BRIGHT = new Color('#c18a2c');

const clamp01 = (v: number): number => Math.min(1, Math.max(0, v));
const smooth = (from: number, to: number, v: number): number => {
  const t = clamp01((v - from) / (to - from));
  return t * t * (3 - 2 * t);
};
const easeOutCubic = (t: number): number => 1 - Math.pow(1 - t, 3);

const HOVER_LIFT = 0.09;
const FLIGHT_MS = 1400;

const CLICK_SLOP_PX = 6;

export class HallScene {
  private readonly renderer: WebGLRenderer;
  private readonly scene = new Scene();
  private readonly camera = new PerspectiveCamera(50, 1, 0.1, 200);
  private readonly controls: OrbitControls;
  private readonly resizeObserver: ResizeObserver;

  private seats: InstancedMesh | null = null;

  private readonly seatIndex = new Map<number, number>();
  private readonly hallObjects: Object3D[] = [];
  private disposed = false;

  private layout: HallLayout | null = null;
  private readonly placements = new Map<number, SeatPlacement>();
  private seatIdByIndex: number[] = [];
  private lift = new Float32Array(0);
  private liftTarget = new Float32Array(0);
  private readonly lifting = new Set<number>();
  private readonly matrixHelper = new Object3D();

  private readonly raycaster = new Raycaster();
  private readonly ndc = new Vector2();
  private pendingHover: { x: number; y: number } | null = null;
  private hoveredIndex = -1;
  private down: { x: number; y: number; time: number } | null = null;

  private flight: CameraFlight | null = null;
  private returnPose: CameraPose | null = null;
  private seatView = false;
  private readonly reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  private room: RoomBox | null = null;
  private atmosphere: Atmosphere | null = null;
  private screenMaterial: MeshBasicMaterial | null = null;
  private intro: { start: number; from: CameraPose; to: CameraPose } | null = null;
  private heldIndices: number[] = [];
  private readonly pulseColor = new Color();
  private readonly t0 = performance.now();

  private lastDistance = 0;
  private readonly spherical = new Spherical();
  private readonly offset = new Vector3();

  constructor(
    private readonly host: HTMLElement,
    private readonly events: HallSceneEvents,
  ) {
    this.renderer = new WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
    this.renderer.outputColorSpace = SRGBColorSpace;

    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.setClearColor(BACKGROUND);
    host.appendChild(this.renderer.domElement);

    this.scene.background = BACKGROUND;
    this.scene.fog = new Fog(BACKGROUND, 22, 70);

    this.scene.add(new AmbientLight('#ffffff', 0.25));
    const key = new DirectionalLight('#ffffff', 0.6);
    key.position.set(0, 10, 6);
    this.scene.add(key);

    this.controls = new OrbitControls(this.camera, this.renderer.domElement);
    this.controls.enableDamping = true;
    this.controls.enablePan = false;
    this.controls.zoomSpeed = 0.6;

    this.resizeObserver = new ResizeObserver(() => this.resize());
    this.resizeObserver.observe(host);
    this.resize();

    const canvas = this.renderer.domElement;
    canvas.addEventListener('pointerdown', this.onPointerDown);
    canvas.addEventListener('pointerup', this.onPointerUp);
    canvas.addEventListener('pointermove', this.onPointerMove);
    canvas.addEventListener('pointerleave', this.onPointerLeave);

    this.renderer.setAnimationLoop(() => this.frame());
    document.addEventListener('visibilitychange', this.onVisibility);
  }

  get inSeatView(): boolean {
    return this.seatView;
  }

  get introRunning(): boolean {
    return this.intro !== null;
  }

  skipIntro(): void {
    if (this.intro) this.finishIntro();
  }

  setCursor(cursor: 'pointer' | 'default'): void {
    this.renderer.domElement.style.cursor = cursor;
  }

  viewFromSeat(seatId: number): Promise<void> {
    const seat = this.placements.get(seatId);
    if (!seat || !this.layout) return Promise.resolve();

    if (!this.seatView) this.returnPose = this.currentPose();
    this.seatView = true;
    this.controls.enabled = false;
    this.clearHover();

    const { screen, eyeHeight } = this.layout;
    return this.flyTo({
      position: new Vector3(seat.x, seat.y + eyeHeight, seat.z - 0.05),
      target: new Vector3(0, screen.y, screen.z),
    });
  }

  async backToHall(): Promise<void> {
    if (!this.seatView || !this.returnPose) return;
    await this.flyTo(this.returnPose);
    this.seatView = false;
    this.controls.enabled = true;
    this.lastDistance = this.camera.position.distanceTo(this.controls.target);
  }

  async build(layout: HallLayout, posterUrl: string, title: string): Promise<void> {
    this.clearHall();
    this.layout = layout;
    this.room = roomBox(layout);

    this.addRoom(layout);
    this.addSeats(layout);
    const color = await this.addScreen(layout, posterUrl, title);
    if (this.disposed || !color) return;

    this.atmosphere = new Atmosphere(layout, this.room, color);
    for (const object of this.atmosphere.objects) this.add(object);
    this.events.posterColor(color.getStyle());

    const overview = this.frameOverview(layout);
    if (!this.reducedMotion) this.startIntro(overview);
  }

  updateSeats(states: ReadonlyMap<number, SeatVisualState>): void {
    if (!this.seats) return;
    const held: number[] = [];
    const mine: SeatPlacement[] = [];
    for (const [seatId, state] of states) {
      const index = this.seatIndex.get(seatId);
      if (index === undefined) continue;
      this.seats.setColorAt(index, SEAT_COLORS[state]);
      if (state === 'held') held.push(index);
      if (state === 'mine') {
        const seat = this.placements.get(seatId);
        if (seat) mine.push(seat);
      }
    }
    if (this.seats.instanceColor) this.seats.instanceColor.needsUpdate = true;

    this.heldIndices = held;
    this.atmosphere?.setMine(mine);
  }

  dispose(): void {
    this.disposed = true;
    document.removeEventListener('visibilitychange', this.onVisibility);
    const canvas = this.renderer.domElement;
    canvas.removeEventListener('pointerdown', this.onPointerDown);
    canvas.removeEventListener('pointerup', this.onPointerUp);
    canvas.removeEventListener('pointermove', this.onPointerMove);
    canvas.removeEventListener('pointerleave', this.onPointerLeave);
    this.renderer.setAnimationLoop(null);
    this.resizeObserver.disconnect();
    this.controls.dispose();
    this.clearHall();
    this.renderer.dispose();
    this.renderer.forceContextLoss();
    this.renderer.domElement.remove();
  }

  private addRoom(layout: HallLayout): void {
    const { width, front, back, ceiling } = roomBox(layout);

    const floorMat = new MeshStandardMaterial({ color: '#15151b', roughness: 0.95 });

    const wallMat = new MeshStandardMaterial({ color: '#1c1a20', roughness: 0.9 });

    const floor = new Mesh(new PlaneGeometry(width, back - front), floorMat);
    floor.rotation.x = -Math.PI / 2;
    floor.position.set(0, 0, (front + back) / 2);
    this.add(floor);

    for (const row of layout.rows) {
      if (row.index === 0) continue;
      const step = new Mesh(new BoxGeometry(width, row.y, 1), floorMat);
      step.position.set(0, row.y / 2, row.z);
      this.add(step);
    }

    const platform = new Mesh(new BoxGeometry(width, layout.backY, back - layout.backZ), floorMat);
    platform.position.set(0, layout.backY / 2, (layout.backZ + back) / 2 + 0.5);
    this.add(platform);

    const depth = back - front;
    for (const side of [-1, 1]) {
      const wall = new Mesh(new PlaneGeometry(depth, ceiling), wallMat);
      wall.rotation.y = (side * -Math.PI) / 2;
      wall.position.set((side * width) / 2, ceiling / 2, (front + back) / 2);
      this.add(wall);
    }
    const backWall = new Mesh(new PlaneGeometry(width, ceiling), wallMat);
    backWall.rotation.y = Math.PI;
    backWall.position.set(0, ceiling / 2, back);
    this.add(backWall);

    const roof = new Mesh(new PlaneGeometry(width, depth), wallMat);
    roof.rotation.x = Math.PI / 2;
    roof.position.set(0, ceiling, (front + back) / 2);
    this.add(roof);

    this.addWallSconces(width, front, back, ceiling);
  }

  private addWallSconces(width: number, front: number, back: number, ceiling: number): void {
    const count = Math.min(4, Math.max(2, Math.round((back - front) / 7)));
    const fixtureMat = new MeshStandardMaterial({
      color: '#2a2218',
      emissive: '#ffb366',
      emissiveIntensity: 2.2,
    });
    const fixtureGeo = new BoxGeometry(0.08, 0.5, 0.25);

    for (const side of [-1, 1]) {
      const x = side * (width / 2 - 0.06);
      for (let i = 0; i < count; i++) {
        const z = front + ((back - front) * (i + 0.75)) / (count + 0.5);
        const y = ceiling * 0.5;

        const fixture = new Mesh(fixtureGeo, fixtureMat);
        fixture.position.set(x, y, z);
        this.add(fixture);

        const light = new PointLight('#ffb070', 5, 10, 1.8);
        light.position.set(x - side * 0.4, y, z);
        this.add(light);
      }
    }
  }

  private addSeats(layout: HallLayout): void {
    const geometry = seatGeometry();
    const material = new MeshStandardMaterial({
      color: '#ffffff',
      roughness: 0.75,
      metalness: 0.05,
    });
    const mesh = new InstancedMesh(geometry, material, layout.seats.length);

    const dummy = new Object3D();
    this.seatIdByIndex = layout.seats.map((s) => s.seatId);
    this.lift = new Float32Array(layout.seats.length);
    this.liftTarget = new Float32Array(layout.seats.length);
    layout.seats.forEach((seat, i) => {
      dummy.position.set(seat.x, seat.y, seat.z);
      dummy.updateMatrix();
      mesh.setMatrixAt(i, dummy.matrix);
      mesh.setColorAt(i, SEAT_COLORS.available);
      this.seatIndex.set(seat.seatId, i);
      this.placements.set(seat.seatId, seat);
    });
    mesh.instanceMatrix.needsUpdate = true;

    this.seats = mesh;
    this.add(mesh);
  }

  private async addScreen(
    layout: HallLayout,
    posterUrl: string,
    title: string,
  ): Promise<Color | null> {
    const { width, height, y, z } = layout.screen;

    const geometry = new PlaneGeometry(width, height, 48, 1);
    const position = geometry.attributes['position'];
    const curve = width * 0.05;
    for (let i = 0; i < position.count; i++) {
      const nx = position.getX(i) / (width / 2);
      position.setZ(i, curve * nx * nx);
    }
    geometry.computeVertexNormals();

    const { texture, color } = await createScreenTexture(posterUrl, title, width / height);
    if (this.disposed) {
      texture.dispose();
      geometry.dispose();
      return null;
    }

    this.screenMaterial = new MeshBasicMaterial({ map: texture, toneMapped: false, fog: false });
    const screen = new Mesh(geometry, this.screenMaterial);
    screen.position.set(0, y, z);
    this.add(screen);
    return color;
  }

  private frameOverview(layout: HallLayout): CameraPose {
    const target = new Vector3(0, layout.screen.y * 0.75, (layout.screen.z + layout.backZ) * 0.3);

    const vfov = (this.camera.fov * Math.PI) / 180;
    const hfov = 2 * Math.atan(Math.tan(vfov / 2) * this.camera.aspect);
    const fitWidth = (layout.width / 2 + 1.5) / Math.tan(hfov / 2);

    const distance = Math.max(layout.backZ - target.z + 9, fitWidth * 1.08) * 1.22;

    const direction = new Vector3(0, 0.7, 1).normalize();
    this.camera.position.copy(target).addScaledVector(direction, distance);
    this.controls.target.copy(target);

    this.controls.minDistance = 5;
    this.controls.maxDistance = distance * 1.3;
    this.controls.minPolarAngle = Math.PI * 0.15;
    this.controls.maxPolarAngle = Math.PI * 0.46;
    this.controls.minAzimuthAngle = -Math.PI * 0.35;
    this.controls.maxAzimuthAngle = Math.PI * 0.35;
    this.controls.update();
    this.lastDistance = distance;
    return this.currentPose();
  }

  private startIntro(to: CameraPose): void {
    if (!this.room || !this.layout) return;
    const lens = lensPosition(this.room);
    const from: CameraPose = {
      position: new Vector3(0, lens.y + 0.55, lens.z + 0.3),
      target: new Vector3(0, this.layout.screen.y, this.layout.screen.z),
    };

    this.camera.position.copy(from.position);
    this.camera.lookAt(from.target);
    this.controls.enabled = false;
    this.setIntroLevels(0, 0);

    this.intro = { start: performance.now(), from, to };
    this.events.intro(true);
  }

  private updateIntro(now: number): void {
    if (!this.intro) return;
    const e = now - this.intro.start;

    const flicker = e < INTRO.beamOn ? 0.55 + 0.45 * Math.sin(e * 0.09) * Math.sin(e * 0.023) : 1;
    const beam = smooth(0, INTRO.beamOn, e) * flicker;
    const poster = smooth(INTRO.posterFrom, INTRO.posterTo, e);
    this.setIntroLevels(beam, poster);

    const move = easeOutCubic(clamp01((e - INTRO.moveFrom) / INTRO.moveMs));
    const { from, to } = this.intro;
    this.camera.position.lerpVectors(from.position, to.position, move);
    this.offset.lerpVectors(from.target, to.target, move);
    this.camera.lookAt(this.offset);

    if (e >= INTRO.moveFrom + INTRO.moveMs) this.finishIntro();
  }

  private finishIntro(): void {
    const to = this.intro?.to;
    this.intro = null;
    this.setIntroLevels(1, 1);
    if (to) {
      this.camera.position.copy(to.position);
      this.controls.target.copy(to.target);
      this.camera.lookAt(to.target);
    }
    this.controls.enabled = !this.seatView;
    this.controls.update();
    this.lastDistance = this.camera.position.distanceTo(this.controls.target);
    this.events.intro(false);
  }

  private setIntroLevels(beam: number, poster: number): void {
    this.atmosphere?.setBeamStrength(beam);
    this.atmosphere?.setGlowStrength(poster);
    this.screenMaterial?.color.setScalar(poster);
  }

  private pulseHeld(t: number): void {
    if (!this.seats || this.heldIndices.length === 0) return;
    for (const index of this.heldIndices) {
      const s = this.reducedMotion ? 0.5 : (Math.sin(t * 2.2 + index * 0.7) + 1) / 2;
      this.pulseColor.lerpColors(HELD_DIM, HELD_BRIGHT, s);
      this.seats.setColorAt(index, this.pulseColor);
    }
    if (this.seats.instanceColor) this.seats.instanceColor.needsUpdate = true;
  }

  private frame(): void {
    const now = performance.now();
    const t = (now - this.t0) / 1000;

    if (this.intro) {
      this.updateIntro(now);
    } else if (this.flight) {
      if (this.flight.update(now)) this.flight = null;
    } else if (this.controls.enabled) {
      this.controls.update();
      this.tiltWithZoom();
    }

    this.atmosphere?.update(this.reducedMotion ? 0 : t);
    this.pulseHeld(t);
    this.updateHover();
    this.animateLift();
    this.renderer.render(this.scene, this.camera);
  }

  private readonly onPointerDown = (e: PointerEvent) => {
    this.down = { x: e.clientX, y: e.clientY, time: performance.now() };
  };

  private readonly onPointerUp = (e: PointerEvent) => {
    const down = this.down;
    this.down = null;
    if (!down) return;
    const moved = Math.hypot(e.clientX - down.x, e.clientY - down.y);
    if (moved > CLICK_SLOP_PX || performance.now() - down.time > 600) return;
    if (this.intro) {
      this.finishIntro();
      return;
    }

    const index = this.pick(e.clientX, e.clientY);
    if (index >= 0) this.events.select(this.seatIdByIndex[index]);
  };

  private readonly onPointerMove = (e: PointerEvent) => {
    if (e.pointerType !== 'mouse') return;
    this.pendingHover = { x: e.clientX, y: e.clientY };
  };

  private readonly onPointerLeave = () => this.clearHover();

  private updateHover(): void {
    const p = this.pendingHover;
    if (!p) return;
    this.pendingHover = null;

    const index = this.seatView || this.intro ? -1 : this.pick(p.x, p.y);
    if (index !== this.hoveredIndex) {
      if (this.hoveredIndex >= 0) this.setLiftTarget(this.hoveredIndex, 0);
      if (index >= 0) this.setLiftTarget(index, HOVER_LIFT);
      this.hoveredIndex = index;
    }

    const rect = this.host.getBoundingClientRect();
    this.events.hover(
      index >= 0 ? this.seatIdByIndex[index] : null,
      p.x - rect.left,
      p.y - rect.top,
    );
  }

  private clearHover(): void {
    this.pendingHover = null;
    if (this.hoveredIndex >= 0) this.setLiftTarget(this.hoveredIndex, 0);
    this.hoveredIndex = -1;
    this.events.hover(null, 0, 0);
  }

  private pick(clientX: number, clientY: number): number {
    if (!this.seats) return -1;
    const rect = this.renderer.domElement.getBoundingClientRect();
    this.ndc.set(
      ((clientX - rect.left) / rect.width) * 2 - 1,
      -((clientY - rect.top) / rect.height) * 2 + 1,
    );
    this.raycaster.setFromCamera(this.ndc, this.camera);
    const hit = this.raycaster.intersectObject(this.seats, false)[0];
    return hit?.instanceId ?? -1;
  }

  private setLiftTarget(index: number, value: number): void {
    this.liftTarget[index] = value;
    this.lifting.add(index);
  }

  private animateLift(): void {
    if (!this.seats || this.lifting.size === 0) return;
    for (const index of this.lifting) {
      const target = this.liftTarget[index];
      const next = this.reducedMotion
        ? target
        : this.lift[index] + (target - this.lift[index]) * 0.25;
      this.lift[index] = Math.abs(next - target) < 0.001 ? target : next;
      if (this.lift[index] === target) this.lifting.delete(index);

      const seat = this.placements.get(this.seatIdByIndex[index]);
      if (!seat) continue;
      this.matrixHelper.position.set(seat.x, seat.y + this.lift[index], seat.z);
      this.matrixHelper.updateMatrix();
      this.seats.setMatrixAt(index, this.matrixHelper.matrix);
    }
    this.seats.instanceMatrix.needsUpdate = true;
  }

  private currentPose(): CameraPose {
    return { position: this.camera.position.clone(), target: this.controls.target.clone() };
  }

  private flyTo(to: CameraPose): Promise<void> {
    return new Promise((resolve) => {
      const duration = this.reducedMotion ? 0 : FLIGHT_MS;
      this.flight = new CameraFlight(this.camera, this.currentPose(), to, duration, () => {
        this.controls.target.copy(to.target);
        resolve();
      });
    });
  }

  private tiltWithZoom(): void {
    this.offset.copy(this.camera.position).sub(this.controls.target);
    const distance = this.offset.length();
    const delta = this.lastDistance - distance;
    this.lastDistance = distance;
    if (Math.abs(delta) < 1e-4 || Math.abs(delta) > 5) return;

    this.spherical.setFromVector3(this.offset);
    this.spherical.phi = Math.min(
      this.controls.maxPolarAngle,
      Math.max(this.controls.minPolarAngle, this.spherical.phi + delta * TILT_PER_METRE),
    );
    this.camera.position
      .copy(this.controls.target)
      .add(this.offset.setFromSpherical(this.spherical));
  }

  private readonly onVisibility = () => {
    if (this.disposed) return;
    this.renderer.setAnimationLoop(document.hidden ? null : () => this.frame());
  };

  private resize(): void {
    const { clientWidth: w, clientHeight: h } = this.host;
    if (!w || !h) return;
    this.renderer.setSize(w, h, false);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
  }

  private add(object: Object3D): void {
    this.hallObjects.push(object);
    this.scene.add(object);
  }

  private clearHall(): void {
    for (const object of this.hallObjects) {
      this.scene.remove(object);
      disposeObject(object);
    }
    this.hallObjects.length = 0;
    this.seats = null;
    this.seatIndex.clear();
    this.placements.clear();
    this.lifting.clear();
    this.hoveredIndex = -1;
    this.layout = null;
    this.atmosphere?.dispose();
    this.atmosphere = null;
    this.screenMaterial = null;
    this.heldIndices = [];
    this.intro = null;
  }
}

function seatGeometry(): BufferGeometry {
  const cushion = new RoundedBoxGeometry(0.52, 0.14, 0.5, 2, 0.05);
  cushion.translate(0, 0.42, 0);
  const back = new RoundedBoxGeometry(0.52, 0.6, 0.1, 2, 0.04);
  back.translate(0, 0.68, 0.22);
  const merged = mergeGeometries([cushion, back]);
  cushion.dispose();
  back.dispose();
  return merged;
}

function disposeObject(object: Object3D): void {
  object.traverse((child) => {
    const mesh = child as Mesh;
    mesh.geometry?.dispose();
    const materials: Material[] = Array.isArray(mesh.material)
      ? mesh.material
      : mesh.material
        ? [mesh.material]
        : [];
    for (const material of materials) {
      for (const value of Object.values(material)) {
        if (value instanceof Texture) value.dispose();
      }
      material.dispose();
    }
  });
}
