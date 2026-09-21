import os
import math

models_dir = r"c:\Users\srira\AR_Virtual_Lab\Assets\Models"
os.makedirs(models_dir, exist_ok=True)

class ObjBuilder:
    def __init__(self):
        self.verts = []
        self.norms = []
        self.uvs = []
        self.faces = []

    def add_vertex(self, v, n, uv):
        self.verts.append(v)
        self.norms.append(n)
        self.uvs.append(uv)

    def add_quad(self, i0, i1, i2, i3):
        self.faces.append([i0 + 1, i1 + 1, i2 + 1, i3 + 1])

    def add_tri(self, i0, i1, i2):
        self.faces.append([i0 + 1, i1 + 1, i2 + 1])

    def add_cylinder(self, bottom_center, top_center, r_bottom, r_top, segments=18, cap_bottom=True, cap_top=True):
        base_idx = len(self.verts)
        dx = top_center[0] - bottom_center[0]
        dy = top_center[1] - bottom_center[1]
        dz = top_center[2] - bottom_center[2]
        length = math.sqrt(dx*dx + dy*dy + dz*dz)
        if length == 0:
            return
        axis = (dx/length, dy/length, dz/length)

        for s in range(segments + 1):
            angle = (s / segments) * math.pi * 2.0
            cos_a = math.cos(angle)
            sin_a = math.sin(angle)

            rad_x, rad_z = cos_a, sin_a
            nx, ny, nz = rad_x, 0.0, rad_z

            vb = (bottom_center[0] + rad_x * r_bottom, bottom_center[1], bottom_center[2] + rad_z * r_bottom)
            vt = (top_center[0] + rad_x * r_top, top_center[1], top_center[2] + rad_z * r_top)

            self.verts.append(vb)
            self.norms.append((nx, ny, nz))
            self.uvs.append((s / segments, 0.0))

            self.verts.append(vt)
            self.norms.append((nx, ny, nz))
            self.uvs.append((s / segments, 1.0))

        for s in range(segments):
            b0 = base_idx + s * 2
            t0 = b0 + 1
            b1 = b0 + 2
            t1 = b0 + 3
            self.add_quad(b0, b1, t1, t0)

        if cap_bottom:
            c_idx = len(self.verts)
            self.verts.append(bottom_center)
            self.norms.append((-axis[0], -axis[1], -axis[2]))
            self.uvs.append((0.5, 0.5))

            for s in range(segments):
                a0 = (s / segments) * math.pi * 2.0
                a1 = ((s + 1) / segments) * math.pi * 2.0
                idx0 = len(self.verts)
                idx1 = idx0 + 1

                self.verts.append((bottom_center[0] + math.cos(a0) * r_bottom, bottom_center[1], bottom_center[2] + math.sin(a0) * r_bottom))
                self.norms.append((-axis[0], -axis[1], -axis[2]))
                self.uvs.append((math.cos(a0)*0.5 + 0.5, math.sin(a0)*0.5 + 0.5))

                self.verts.append((bottom_center[0] + math.cos(a1) * r_bottom, bottom_center[1], bottom_center[2] + math.sin(a1) * r_bottom))
                self.norms.append((-axis[0], -axis[1], -axis[2]))
                self.uvs.append((math.cos(a1)*0.5 + 0.5, math.sin(a1)*0.5 + 0.5))

                self.add_tri(c_idx, idx1, idx0)

        if cap_top:
            c_idx = len(self.verts)
            self.verts.append(top_center)
            self.norms.append((axis[0], axis[1], axis[2]))
            self.uvs.append((0.5, 0.5))

            for s in range(segments):
                a0 = (s / segments) * math.pi * 2.0
                a1 = ((s + 1) / segments) * math.pi * 2.0
                idx0 = len(self.verts)
                idx1 = idx0 + 1

                self.verts.append((top_center[0] + math.cos(a0) * r_top, top_center[1], top_center[2] + math.sin(a0) * r_top))
                self.norms.append((axis[0], axis[1], axis[2]))
                self.uvs.append((math.cos(a0)*0.5 + 0.5, math.sin(a0)*0.5 + 0.5))

                self.verts.append((top_center[0] + math.cos(a1) * r_top, top_center[1], top_center[2] + math.sin(a1) * r_top))
                self.norms.append((axis[0], axis[1], axis[2]))
                self.uvs.append((math.cos(a1)*0.5 + 0.5, math.sin(a1)*0.5 + 0.5))

                self.add_tri(c_idx, idx0, idx1)

    def add_box(self, center, size):
        hx = size[0] * 0.5
        hy = size[1] * 0.5
        hz = size[2] * 0.5
        cx, cy, cz = center

        corners = [
            (cx - hx, cy - hy, cz - hz), # 0
            (cx + hx, cy - hy, cz - hz), # 1
            (cx + hx, cy + hy, cz - hz), # 2
            (cx - hx, cy + hy, cz - hz), # 3
            (cx - hx, cy - hy, cz + hz), # 4
            (cx + hx, cy - hy, cz + hz), # 5
            (cx + hx, cy + hy, cz + hz), # 6
            (cx - hx, cy + hy, cz + hz), # 7
        ]

        def add_face(p0, p1, p2, p3, n):
            b = len(self.verts)
            self.verts.extend([p0, p1, p2, p3])
            self.norms.extend([n, n, n, n])
            self.uvs.extend([(0,0), (1,0), (1,1), (0,1)])
            self.add_quad(b, b+1, b+2, b+3)

        add_face(corners[0], corners[1], corners[2], corners[3], (0, 0, -1))
        add_face(corners[5], corners[4], corners[7], corners[6], (0, 0, 1))
        add_face(corners[4], corners[0], corners[3], corners[7], (-1, 0, 0))
        add_face(corners[1], corners[5], corners[6], corners[2], (1, 0, 0))
        add_face(corners[3], corners[2], corners[6], corners[7], (0, 1, 0))
        add_face(corners[4], corners[5], corners[1], corners[0], (0, -1, 0))

    def save(self, filepath):
        with open(filepath, "w", encoding="utf-8") as f:
            f.write("# Procedural Chemistry Lab Model\n")
            for v in self.verts:
                f.write(f"v {v[0]:.5f} {v[1]:.5f} {v[2]:.5f}\n")
            for n in self.norms:
                f.write(f"vn {n[0]:.5f} {n[1]:.5f} {n[2]:.5f}\n")
            for u in self.uvs:
                f.write(f"vt {u[0]:.5f} {u[1]:.5f}\n")
            for face in self.faces:
                if len(face) == 3:
                    f.write(f"f {face[0]}/{face[0]}/{face[0]} {face[1]}/{face[1]}/{face[1]} {face[2]}/{face[2]}/{face[2]}\n")
                elif len(face) == 4:
                    f.write(f"f {face[0]}/{face[0]}/{face[0]} {face[1]}/{face[1]}/{face[1]} {face[2]}/{face[2]}/{face[2]} {face[3]}/{face[3]}/{face[3]}\n")

# 1. Bunsen Burner
b1 = ObjBuilder()
b1.add_cylinder((0, 0, 0), (0, 0.008, 0), 0.028, 0.026, 18, True, True)
b1.add_cylinder((0, 0.008, 0), (0, 0.016, 0), 0.026, 0.012, 18, False, True)
b1.add_cylinder((0.008, 0.012, 0), (0.030, 0.012, 0), 0.004, 0.0035, 12, False, True)
b1.add_cylinder((0.030, 0.012, 0), (0.040, 0.010, 0), 0.0045, 0.0045, 12, True, True)
b1.add_cylinder((0, 0.016, 0), (0, 0.032, 0), 0.0085, 0.0085, 16, True, True)
b1.add_cylinder((0, 0.032, 0), (0, 0.102, 0), 0.0065, 0.0065, 16, False, False)
b1.add_cylinder((0, 0.102, 0), (0, 0.108, 0), 0.0075, 0.0080, 16, False, True)
b1.save(os.path.join(models_dir, "BunsenBurner.obj"))

# 2. Crucible Tongs
b2 = ObjBuilder()
b2.add_cylinder((0, -0.003, 0), (0, 0.003, 0), 0.003, 0.003, 12, True, True)
b2.add_cylinder((-0.016, 0, -0.075), (-0.016, 0.003, -0.075), 0.014, 0.014, 14, True, True)
b2.add_box((-0.008, 0, -0.038), (0.0035, 0.003, 0.075))
b2.add_box((-0.010, 0, 0.022), (0.0035, 0.003, 0.038))
b2.add_box((-0.002, 0, 0.052), (0.003, 0.003, 0.028))
b2.add_cylinder((0.016, 0, -0.075), (0.016, 0.003, -0.075), 0.014, 0.014, 14, True, True)
b2.add_box((0.008, 0, -0.038), (0.0035, 0.003, 0.075))
b2.add_box((0.010, 0, 0.022), (0.0035, 0.003, 0.038))
b2.add_box((0.002, 0, 0.052), (0.003, 0.003, 0.028))
b2.save(os.path.join(models_dir, "CrucibleTongs.obj"))

# 3. Watch Glass
b3 = ObjBuilder()
rings = 8
segments = 24
max_r = 0.035
depth = 0.007
for r in range(rings + 1):
    frac = r / rings
    cur_r = math.sin(frac * math.pi * 0.5) * max_r
    cur_y = (1.0 - math.cos(frac * math.pi * 0.5)) * depth
    for s in range(segments + 1):
        ang = (s / segments) * math.pi * 2.0
        x = math.cos(ang) * cur_r
        z = math.sin(ang) * cur_r
        pos = (x, cur_y, z)
        length = math.sqrt(x*x*0.25 + 1.0 + z*z*0.25)
        norm = (-x*0.5/length, 1.0/length, -z*0.5/length)
        b3.add_vertex(pos, norm, (s/segments, frac))

for r in range(rings):
    for s in range(segments):
        i0 = r * (segments + 1) + s
        i1 = i0 + 1
        i2 = (r + 1) * (segments + 1) + s + 1
        i3 = (r + 1) * (segments + 1) + s
        b3.add_quad(i0, i1, i2, i3)

b3.add_cylinder((0, depth - 0.001, 0), (0, depth + 0.001, 0), max_r - 0.001, max_r, segments, False, True)
b3.save(os.path.join(models_dir, "WatchGlass.obj"))

# 4. Laboratory Table
b4 = ObjBuilder()
b4.add_box((0, -0.010, 0), (0.52, 0.020, 0.36))
lx, lz, leg_h = 0.23, 0.15, 0.28
for px, pz in [(-lx, -lz), (lx, -lz), (-lx, lz), (lx, lz)]:
    b4.add_cylinder((px, -0.02 - leg_h, pz), (px, -0.02, pz), 0.010, 0.010, 12, True, True)
b4.add_box((0, -0.15, -lz), (lx * 2.0, 0.010, 0.010))
b4.add_box((0, -0.15,  lz), (lx * 2.0, 0.010, 0.010))
b4.add_box((-lx, -0.15, 0), (0.010, 0.010, lz * 2.0))
b4.add_box(( lx, -0.15, 0), (0.010, 0.010, lz * 2.0))
b4.save(os.path.join(models_dir, "LaboratoryTable.obj"))

# 5. Sandpaper
b5 = ObjBuilder()
b5.add_box((0, 0.001, 0), (0.065, 0.002, 0.065))
b5.save(os.path.join(models_dir, "Sandpaper.obj"))

# 6. Magnesium Ribbon Strip
b6 = ObjBuilder()
segs = 12
length = 0.055
width = 0.0045
thick = 0.0006
for i in range(segs + 1):
    t = i / segs
    z = (t - 0.5) * length
    y = math.sin(t * math.pi * 2.0) * 0.0012 + thick * 0.5
    b6.add_vertex((-width * 0.5, y, z), (0, 1, 0), (0, t))
    b6.add_vertex(( width * 0.5, y, z), (0, 1, 0), (1, t))
for i in range(segs):
    b0 = i * 2
    b1 = b0 + 1
    b2 = b0 + 3
    b3 = b0 + 2
    b6.add_quad(b0, b1, b2, b3)
    b6.add_quad(b3, b2, b1, b0)
b6.save(os.path.join(models_dir, "MagnesiumRibbon.obj"))

# 7. MgO Powder Heap (in watch glass)
b7 = ObjBuilder()
rings, segs = 6, 16
base_r, h = 0.018, 0.006
for r in range(rings + 1):
    frac = r / rings
    cur_r = (1.0 - frac) * base_r
    cur_y = ((1.0 - frac) ** 0.6) * h
    for s in range(segs + 1):
        ang = (s / segs) * math.pi * 2.0
        x = math.cos(ang) * cur_r
        z = math.sin(ang) * cur_r
        pos = (x, h - cur_y, z)
        ln = math.sqrt(x*x + 0.64 + z*z)
        norm = (x/ln, 0.8/ln, z/ln)
        b7.add_vertex(pos, norm, (s/segs, frac))
for r in range(rings):
    for s in range(segs):
        i0 = r * (segs + 1) + s
        i1 = i0 + 1
        i2 = (r + 1) * (segs + 1) + s + 1
        i3 = (r + 1) * (segs + 1) + s
        b7.add_quad(i0, i1, i2, i3)
b7.save(os.path.join(models_dir, "MgOPowder.obj"))

# 8. MgO Residue (on tongs)
b8 = ObjBuilder()
b8.add_box((0, 0, 0), (0.006, 0.008, 0.016))
b8.add_box((0.001, 0.002, 0.004), (0.005, 0.006, 0.010))
b8.save(os.path.join(models_dir, "MgOResidue.obj"))

print("All 3D OBJ models generated successfully in Assets/Models.")
