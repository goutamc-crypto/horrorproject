using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Rebuilds the Kuldhara village in the open scene. Deterministic and re-runnable: wipes _Kuldhara_Level first.
// All layout coordinates are local (x, z) metres from the map centre C; +z is north.
public static class KuldharaLevelBuilder
{
    const string KitDir = "Assets/MAIN GAME/3D models/KuldharaKit/";
    const string SkDir = "Assets/MAIN GAME/3D models/Sketchfab/";
    const string DataDir = "Assets/MAIN GAME/Terrain/Kuldhara/";
    const string MapPng = "Assets/MAIN GAME/UI/Kuldhara/MapBake.png";
    const float HaveliYaw = 180f;
    static readonly Vector3 C = new Vector3(430, 0, 355); // MapUI covers C +/- 200m
    static readonly Vector2 Baori = new Vector2(85, 38), Shop = new Vector2(58, -48), Cursed = new Vector2(-100, -70), TempleAt = new Vector2(-100, 60);

    static readonly Vector2[][] Lanes =
    {
        new[] { V(0, -165), V(0, -92), V(0, -60), V(0, -18) },
        new[] { V(0, 25), V(-4, 55), V(0, 86) },
        new[] { V(-18, 12), V(-50, 28), V(-82, 42) },
        new[] { V(18, 8), V(60, 2), V(85, 10) },
        new[] { V(0, -70), V(30, -62), V(58, -48), V(88, -20), V(85, 10) },
        new[] { V(-8, -70), V(-40, -45), V(-60, -5), V(-50, 28) },
        new[] { V(-4, 55), V(40, 62), V(75, 68) },
        new[] { V(-4, 55), V(-45, 62), V(-70, 78) },
        new[] { V(-60, -5), V(-73, -38), V(-88, -58) },
        new[] { V(0, -112), V(-35, -113), V(-85, -85) },
    };
    static readonly Vector2[] WestWall = { V(-10, -92), V(-60, -68), V(-100, -25), V(-125, 5), V(-135, 40) };
    static readonly Vector2[] EastWall = { V(10, -92), V(70, -88), V(128, -60), V(128, -5), V(125, 60), V(95, 80) };
    static readonly Vector2[] CursedWall = { V(-125, -40), V(-118, -92), V(-50, -112) };
    static readonly Vector2[] Village = { V(-8, -92), V(-60, -68), V(-100, -25), V(-125, 5), V(-135, 40), V(-120, 90), V(90, 90), V(125, 60), V(128, -5), V(128, -60), V(70, -88), V(8, -92) };

    static System.Random rng;
    static Transform root;
    static readonly List<Vector2> occupied = new List<Vector2>();
    static readonly List<float> occupiedYaw = new List<float>();
    static readonly List<Vector2> alleys = new List<Vector2>();
    static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

    static Vector2 V(float x, float z) => new Vector2(x, z);
    static float F(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    [MenuItem("Tools/Kuldhara/Build Level")]
    public static void Build()
    {
        rng = new System.Random(1337);
        occupied.Clear();
        occupiedYaw.Clear();
        alleys.Clear();
        ClearOld();
        root = new GameObject("_Kuldhara_Level").transform;
        root.position = C;
        BuildTerrain();
        BuildWalls(Group("Walls"));
        BuildGate(Group("Gate"));
        BuildSquare(Group("VillageSquare"));
        BuildHaveli(Group("HauntedHaveli"));
        BuildTemple(Group("AncientTemple"));
        BuildBaori(Group("Baori"));
        BuildShop(Group("AbandonedShop"));
        BuildCursedWell(Group("CursedWell"));
        BuildStreets(Group("Buildings"));
        BuildScatter(Group("Rocks_Rubble"));
        BuildDressing(Group("HorrorDressing"));
        MoveGameplay();
        SetLighting();
        SetStaticFlags();
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
    }

    static void ClearOld()
    {
        foreach (var n in new[] { "_Kuldhara_Level", "Kuldhara_Terrain", "_Village_Houses", "_Village_Ruins", "_Village_Props", "_Boundary", "_Environment_Dressing", "_LevelDesign", "Enviroment", "Haveli" })
        {
            var g = GameObject.Find(n);
            if (g && !g.transform.parent) Object.DestroyImmediate(g);
        }
    }

    static Transform Group(string n, Transform parent = null)
    {
        var t = new GameObject(n).transform;
        t.SetParent(parent ? parent : root, false);
        return t;
    }

    // ---------- placement helpers ----------
    static GameObject Load(string path)
    {
        if (!cache.TryGetValue(path, out var g) || !g)
        {
            g = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!g) throw new FileNotFoundException(path);
            cache[path] = g;
        }
        return g;
    }

    static GameObject P(string kit, Transform parent, Vector3 pos, float yaw = 0, Vector3? scale = null, bool col = true, float cull = 0)
        => PF(KitDir + kit + ".fbx", parent, pos, yaw, scale, col, cull);

    static GameObject PF(string path, Transform parent, Vector3 pos, float yaw, Vector3? scale, bool col, float cull)
    {
        var g = (GameObject)PrefabUtility.InstantiatePrefab(Load(path), parent);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        if (scale.HasValue) g.transform.localScale = scale.Value;
        if (col) foreach (var mf in g.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        if (cull > 0) Cull(g, cull);
        return g;
    }

    // No authored LOD meshes exist for these models, so the LOD group only culls at small screen size.
    static void Cull(GameObject g, float size)
    {
        var lod = g.AddComponent<LODGroup>();
        lod.SetLODs(new[] { new LOD(size, g.GetComponentsInChildren<Renderer>()) });
        lod.RecalculateBounds();
    }

    // Foreign models have off-centre pivots: re-seat on the ground by bounds.
    static GameObject Seat(GameObject g, Vector2 xz, float y = 0)
    {
        var b = new Bounds();
        bool first = true;
        foreach (var r in g.GetComponentsInChildren<Renderer>()) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
        g.transform.position += new Vector3(C.x + xz.x - b.center.x, y - b.min.y, C.z + xz.y - b.center.z);
        return g;
    }

    static void Stair(Transform p, Vector3 top, float yawUp)
    {
        var dir = Quaternion.Euler(0, yawUp, 0) * Vector3.forward;
        bool ascendsPlusZ = StairAscendsPlusZ();
        float yaw = ascendsPlusZ ? yawUp : yawUp + 180f;
        var hi = new Vector3(0, 1, ascendsPlusZ ? 0 : -1.6f);
        P("Kit_Stairs_3m", p, top - Quaternion.Euler(0, yaw, 0) * hi, yaw, null, false);
        // Steps are taller than the CharacterController's stepOffset, so the walkable surface is a hidden ramp.
        var low = top - dir * 1.6f - Vector3.up;
        var ramp = new GameObject("StairRamp").AddComponent<BoxCollider>();
        ramp.size = new Vector3(3.1f, 0.1f, Vector3.Distance(top, low) + 0.2f);
        ramp.transform.SetParent(p, false);
        ramp.transform.localPosition = (top + low) * 0.5f;
        ramp.transform.localRotation = Quaternion.LookRotation(top - low);
    }

    static bool StairAscendsPlusZ()
    {
        var m = Load(KitDir + "Kit_Stairs_3m.fbx").GetComponent<MeshFilter>().sharedMesh;
        float sum = 0; int n = 0;
        foreach (var v in m.vertices) if (v.y > m.bounds.max.y - 0.05f) { sum += v.z; n++; }
        return sum / n > m.bounds.center.z;
    }

    // Modular building: w x d tiles of 4m, facing local +z.
    static GameObject Block(Transform parent, Vector2 at, float yaw, int w, int d, int floors, bool door, bool roofCap = false)
    {
        var t = Group(door ? "House_Modular" : "Tower", parent);
        t.localPosition = new Vector3(at.x, 0, at.y);
        t.localRotation = Quaternion.Euler(0, yaw, 0);
        for (int f = 0; f < floors; f++)
        {
            float y = f * 4.2f;
            bool ground = f == 0;
            for (int s = 0; s < 4; s++)
            {
                int n = s % 2 == 0 ? w : d;
                float off = (s % 2 == 0 ? d : w) * 2f;
                float sYaw = s * 90f;
                var q = Quaternion.Euler(0, sYaw, 0);
                for (int i = 0; i < n; i++)
                {
                    float lx = (i - (n - 1) * 0.5f) * 4f;
                    bool front = s == 0, mid = i == n / 2;
                    string kit = !ground && rng.NextDouble() < 0.5 ? "Kit_Wall_4m_Window" : "Kit_Wall_4m";
                    if (front && mid && door && ground) kit = "Kit_Wall_4m_Doorway";
                    P(kit, t, q * new Vector3(lx, y, off), sYaw, null, ground);
                    if (front && mid && !ground) P(rng.NextDouble() < 0.5 ? "Kit_Jharokha_Domed" : "Kit_Window_Balcony", t, q * new Vector3(lx, y + 0.3f, off + 0.35f), sYaw, null, false);
                }
            }
            foreach (var cx in new[] { -1, 1 })
                foreach (var cz in new[] { -1, 1 })
                    P("Kit_Wall_CornerPost", t, new Vector3(cx * w * 2f, y, cz * d * 2f), 0, null, ground);
        }
        float top = floors * 4.2f;
        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
                P("Kit_Platform_4m", t, new Vector3((x - (w - 1) * 0.5f) * 4f, top - 0.05f, (z - (d - 1) * 0.5f) * 4f), 0, null, false);
        if (roofCap) P("Kit_Jharokha_Domed", t, new Vector3(0, top + 0.6f, -0.5f), 0, null, false);
        Cull(t.gameObject, 0.045f);
        return t.gameObject;
    }

    // ---------- geometry queries ----------
    static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        return Vector2.Distance(p, a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude));
    }

    static float PolyDist(Vector2 p, Vector2[] line)
    {
        float d = 1e9f;
        for (int i = 0; i + 1 < line.Length; i++) d = Mathf.Min(d, SegDist(p, line[i], line[i + 1]));
        return d;
    }

    static float LaneDist(Vector2 p)
    {
        float d = 1e9f;
        foreach (var l in Lanes) d = Mathf.Min(d, PolyDist(p, l));
        return d;
    }

    static bool InVillage(Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = Village.Length - 1; i < Village.Length; j = i++)
            if ((Village[i].y > p.y) != (Village[j].y > p.y) &&
                p.x < (Village[j].x - Village[i].x) * (p.y - Village[i].y) / (Village[j].y - Village[i].y) + Village[i].x)
                inside = !inside;
        return inside;
    }

    static bool InZone(Vector2 p) =>
        Vector2.Distance(p, V(0, 5)) < 27 || Vector2.Distance(p, TempleAt) < 21 || Vector2.Distance(p, Shop) < 13 ||
        (Mathf.Abs(p.x - Baori.x) < 31 && Mathf.Abs(p.y - Baori.y) < 31) || (Mathf.Abs(p.x) < 52 && p.y > 84);

    static bool CanBuild(Vector2 p, float yaw)
    {
        if (!InVillage(p) || InZone(p) || LaneDist(p) < 7.2f) return false;
        if (PolyDist(p, WestWall) < 8.5f || PolyDist(p, EastWall) < 8.5f) return false;
        for (int i = 0; i < occupied.Count; i++)
        {
            // Neighbours along one lane share a yaw and stand 11.5m apart (a 2m alley); other pairs need room for their corners.
            bool sameRow = i < occupiedYaw.Count && Mathf.Abs(Mathf.DeltaAngle(occupiedYaw[i], yaw)) < 5f;
            if (Vector2.Distance(occupied[i], p) < (sameRow ? 11.4f : 14.5f)) return false;
        }
        return true;
    }

    // ---------- terrain ----------
    static float Smooth(float a, float b, float x) { x = Mathf.Clamp01((x - a) / (b - a)); return x * x * (3 - 2 * x); }

    static float Height(float x, float z)
    {
        if (Mathf.Abs(x - Baori.x) < 24.4f && Mathf.Abs(z - Baori.y) < 24.4f) return -5.2f;
        float r = Mathf.Sqrt(x * x + z * z);
        float h = (Mathf.PerlinNoise(x * 0.07f + 50, z * 0.07f + 50) - 0.5f) * 0.25f;
        h += Smooth(135, 160, r) * Mathf.PerlinNoise(x * 0.012f + 9, z * 0.012f + 3) * 4f;
        // The ridge passes the controller's 45 degree slope limit near r=168: it is the map boundary.
        h += Smooth(165, 192, r) * (40f + Mathf.PerlinNoise(x * 0.03f + 20, z * 0.03f) * 10f);
        float a = Mathf.Clamp01(1 - (Mathf.Abs(x) - 24) / 22f), b = Mathf.Clamp01((z - 93.5f) / 0.8f);
        return h + 4f * a * b;
    }

    // TerrainLayer remap tint has no visible effect in this URP terrain, so each layer gets its own baked 512px tile.
    static Texture2D Tinted(string name, string src, Color tint)
    {
        const int s = 512;
        var rt = RenderTexture.GetTemporary(s, s, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(AssetDatabase.LoadAssetAtPath<Texture2D>(src), rt);
        RenderTexture.active = rt;
        var t = new Texture2D(s, s, TextureFormat.RGB24, false);
        t.ReadPixels(new Rect(0, 0, s, s), 0, 0);
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        var px = t.GetPixels();
        for (int i = 0; i < px.Length; i++) px[i] *= tint;
        t.SetPixels(px);
        string path = DataDir + name + ".png";
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static TerrainLayer Layer(string name, string tex, float tile, Color tint)
    {
        var l = new TerrainLayer
        {
            diffuseTexture = Tinted(name + "_Tile", tex, tint),
            tileSize = new Vector2(tile, tile),
        };
        AssetDatabase.CreateAsset(l, DataDir + name + ".terrainlayer");
        Matte(l);
        return l;
    }

    static void BuildTerrain()
    {
        const float size = 400, baseY = -10, range = 60;
        const int hr = 513, ar = 1024;
        Directory.CreateDirectory(DataDir);
        AssetDatabase.DeleteAsset(DataDir.TrimEnd('/'));
        Directory.CreateDirectory(DataDir);
        AssetDatabase.Refresh();

        var data = new TerrainData { heightmapResolution = hr, alphamapResolution = ar, baseMapResolution = 512 };
        data.size = new Vector3(size, range, size);
        AssetDatabase.CreateAsset(data, DataDir + "KuldharaTerrain.asset"); // before painting: creating the asset afterwards drops the alphamap

        const string ground = "Assets/MAIN GAME/ADG_Textures/ground_vol1/ground2/ground2_Diffuse.tga";
        const string stone = KitDir + "Textures/Sandstone_Diffuse.jpg";
        data.terrainLayers = new[]
        {
            Layer("Sand", ground, 14, new Color(0.92f, 0.76f, 0.56f, 1)),
            Layer("Path", ground, 7, new Color(1f, 0.97f, 0.86f, 1)),
            Layer("Rock", stone, 9, new Color(0.82f, 0.66f, 0.52f, 1)),
            Layer("Cursed", ground, 11, new Color(0.36f, 0.27f, 0.23f, 1)),
        };

        // Heights go in after the layer imports above: written earlier they never reach the saved asset.
        var heights = new float[hr, hr];
        for (int zi = 0; zi < hr; zi++)
            for (int xi = 0; xi < hr; xi++)
                heights[zi, xi] = (Height(xi / (hr - 1f) * size - 200, zi / (hr - 1f) * size - 200) - baseY) / range;
        data.SetHeights(0, 0, heights);
        data.SyncHeightmap();

        var map = new float[ar, ar, 4];
        for (int zi = 0; zi < ar; zi++)
            for (int xi = 0; xi < ar; xi++)
            {
                float x = xi / (ar - 1f) * size - 200, z = zi / (ar - 1f) * size - 200;
                var p = V(x, z);
                float n = Mathf.PerlinNoise(x * 0.09f, z * 0.09f) * 2.2f;
                float lane = LaneDist(p);
                float path = 1 - Smooth(2.4f, 4.6f, lane + n);
                path = Mathf.Max(path, 1 - Smooth(19f, 23f, Vector2.Distance(p, V(0, 5)) + n));
                float cursed = (1 - Smooth(22f, 34f, Vector2.Distance(p, Cursed) + n * 2f)) * (1 - path);
                float slope = data.GetSteepness(xi / (ar - 1f), zi / (ar - 1f));
                float rock = Mathf.Max(Smooth(26f, 40f, slope), Smooth(170f, 180f, p.magnitude)) * (1 - path);
                float sand = Mathf.Max(0, 1 - path - cursed - rock);
                float sum = sand + path + cursed + rock;
                map[zi, xi, 0] = sand / sum; map[zi, xi, 1] = path / sum; map[zi, xi, 2] = rock / sum; map[zi, xi, 3] = cursed / sum;
            }
        data.SetAlphamaps(0, 0, map);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);

        var go = Terrain.CreateTerrainGameObject(data);
        go.name = "Kuldhara_Terrain";
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(C.x - 200, baseY, C.z - 200);
        var t = go.GetComponent<Terrain>();
        t.materialTemplate = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/TerrainLit.mat");
        t.drawInstanced = true;
        t.heightmapPixelError = 8;
        t.basemapDistance = 160;
        t.shadowCastingMode = ShadowCastingMode.Off;
        t.drawTreesAndFoliage = false;
    }

    // ---------- zones ----------
    static void WallRun(Transform p, Vector2[] line, float broken, bool towers)
    {
        for (int s = 0; s + 1 < line.Length; s++)
        {
            Vector2 a = line[s], b = line[s + 1];
            float len = Vector2.Distance(a, b);
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 4f));
            var dir = (b - a) / len;
            float yaw = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;
            for (int i = 0; i < n; i++)
            {
                var m = a + dir * ((i + 0.5f) * len / n);
                if (LaneDist(m) < 3.5f) // lanes punch natural breaches through the wall
                {
                    P("Kit_Rubble_Pile", p, new Vector3(m.x, 0, m.y) + new Vector3(dir.x, 0, dir.y) * 3.5f, F(0, 360), Vector3.one * 1.4f, false, 0.02f);
                    continue;
                }
                double r = rng.NextDouble();
                string kit = r < broken ? "Kit_Wall_4m_Broken" : r < broken + 0.08 ? "Kit_Wall_4m_Window" : "Kit_Wall_4m";
                P(kit, p, new Vector3(m.x, 0, m.y), yaw, new Vector3(len / n / 4f, 1, 1));
            }
            if (towers && s > 0 && LaneDist(a) > 7f) Block(p, a, yaw, 1, 1, 2, false, true);
            if (towers && len > 55f && LaneDist((a + b) * 0.5f) > 7f) Block(p, (a + b) * 0.5f, yaw, 1, 1, 2, false, true);
        }
    }

    static void BuildWalls(Transform p)
    {
        WallRun(p, WestWall, 0.12f, true);
        WallRun(p, EastWall, 0.12f, true);
        WallRun(p, CursedWall, 0.45f, false);
    }

    static void BuildGate(Transform p)
    {
        foreach (var s in new[] { -1, 1 }) Block(p, V(s * 8, -92), 0, 1, 1, 2, false, true);
        for (int i = -1; i <= 1; i++) P("Kit_Wall_4m", p, new Vector3(i * 4, 5f, -92), 0, null, false);
        foreach (var s in new[] { -1, 1 })
        {
            P("Kit_Pillar_Carved", p, new Vector3(s * 5.2f, 0, -93), 0);
            P("Kit_Pillar_Carved", p, new Vector3(s * 5.2f, 0, -91), 0);
        }
        P("Kit_Jharokha_Domed", p, new Vector3(0, 8.8f, -92.5f), 180, null, false);
    }

    static void BuildSquare(Transform p)
    {
        foreach (var x in new[] { -2.05f, 2.05f })
            foreach (var z in new[] { -2.05f, 2.05f })
                P("Kit_Platform_4m", p, new Vector3(x, 0, 5 + z));
        Seat(PF(SkDir + "OldWell/OldWell.fbx", p, Vector3.zero, 0, Vector3.one * 1.8f, true, 0.02f), V(0, 5), 0.6f);
        for (int i = 0; i < 12; i++)
        {
            if (i == 3 || i == 6 || i == 9 || i == 0) continue; // openings on the four lanes
            float a = i * 30f * Mathf.Deg2Rad;
            P("Kit_Pillar_Carved", p, new Vector3(Mathf.Sin(a) * 17, 0, 5 + Mathf.Cos(a) * 17), i * 30f, null, true, 0.02f);
        }
        for (int i = 0; i < 6; i++)
            P("Kit_Rubble_Pile", p, new Vector3(F(-14, 14), 0, 5 + F(-14, 14)), F(0, 360), Vector3.one * F(0.7f, 1.3f), false, 0.02f);
    }

    static void BuildHaveli(Transform p)
    {
        PF("Assets/MAIN GAME/3D models/Haveli/Haveli.fbx", p, new Vector3(0, 4, 112), HaveliYaw, Vector3.one * 1.35f, true, 0);
        for (int x = -44; x <= 44; x += 4)
            if (Mathf.Abs(x) > 5) P("Kit_Wall_4m", p, new Vector3(x, 0, 94));
        foreach (var s in new[] { -1, 1 })
        {
            P("Kit_Wall_4m", p, new Vector3(s * 5.1f, 0, 89.8f), 90, new Vector3(1, 0.6f, 1));
            P("Kit_Wall_4m", p, new Vector3(s * 5.1f, 0, 92.8f), 90);
            P("Kit_Pillar_Carved", p, new Vector3(s * 7, 4, 98), 0);
            P("Kit_Pillar_Carved", p, new Vector3(s * 7, 4, 103), 0);
        }
        for (int k = 0; k < 4; k++)
            foreach (var x in new[] { -3f, 0f, 3f })
                Stair(p, new Vector3(x, 4 - k, 94.4f - 1.6f * k), 0);
        foreach (var s in new[] { -1, 1 }) Block(p, V(s * 34, 112), s > 0 ? -90 : 90, 2, 2, 2, true);
    }

    static void BuildTemple(Transform p)
    {
        const float yaw = 135;
        P("Temple", p, new Vector3(TempleAt.x, 0, TempleAt.y), yaw, Vector3.one * 1.8f);
        foreach (var s in new[] { -8f, 8f })
        {
            var offs = Quaternion.Euler(0, yaw, 0) * new Vector3(s, 0, 21);
            P("Kit_Pillar_Carved", p, new Vector3(TempleAt.x, 0, TempleAt.y) + offs, yaw);
        }
    }

    static void BuildBaori(Transform p)
    {
        var at = new Vector3(Baori.x, 0, Baori.y);
        float[] grid = { -20, -12, -4, 4, 12, 20 };
        var wide = new Vector3(2, 1, 1);
        var tile = new Vector3(2, 1, 2);
        foreach (var x in grid)
            foreach (var z in grid)
            {
                float m = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                if (m < 5) continue; // central water tank
                P("Kit_Platform_4m", p, at + new Vector3(x, (m > 15 ? -2 : -4) - 0.65f, z), 0, tile);
            }
        for (int s = 0; s < 4; s++)
        {
            var q = Quaternion.Euler(0, s * 90f, 0);
            foreach (var x in grid)
            {
                P("Kit_Wall_4m", p, at + q * new Vector3(x, -4.2f, 24.4f), s * 90f, wide);
                if (Mathf.Abs(x) < 15) P("Kit_Wall_4m", p, at + q * new Vector3(x, -6.2f, 16.4f), s * 90f, wide);
                if (Mathf.Abs(x) < 5) P("Kit_Wall_4m", p, at + q * new Vector3(x, -8.2f, 8.4f), s * 90f, wide);
            }
            P("Kit_Pillar_Carved", p, at + q * new Vector3(24.5f, 0, 24.5f), s * 90f);
            P("Kit_Jharokha_Domed", p, at + q * new Vector3(24.5f, 4.4f, 24), s * 90f + 45f, null, false, 0.02f);
        }
        foreach (var x in new[] { -3f, 0f, 3f })
            foreach (var top in new[] { new Vector2(0, -23.6f), new Vector2(-1, -22f), new Vector2(-2, -15.6f), new Vector2(-3, -14f), new Vector2(-4, -7.6f) })
                Stair(p, at + new Vector3(x, top.x, top.y), 180);

        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        Object.DestroyImmediate(plane.GetComponent<Collider>());
        plane.name = "BaoriWater";
        plane.transform.SetParent(p, false);
        plane.transform.localPosition = at + Vector3.up * -4.7f;
        plane.transform.localScale = Vector3.one * 1.6f;
        var water = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.05f, 0.2f, 0.19f) };
        water.SetFloat("_Smoothness", 0.85f);
        AssetDatabase.CreateAsset(water, DataDir + "Kuldhara_Water.mat");
        plane.GetComponent<Renderer>().sharedMaterial = water;
        plane.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    static void BuildShop(Transform p)
    {
        // Stalls line both sides of the bazaar lane, facing it.
        Vector2 a = V(30, -62), b = V(58, -48), c = V(88, -20);
        for (int i = 0; i < 7; i++)
        {
            var s = i < 4 ? Vector2.Lerp(a, b, 0.2f + i * 0.26f) : Vector2.Lerp(b, c, (i - 3) * 0.2f);
            var dir = i < 4 ? (b - a).normalized : (c - b).normalized;
            var n = new Vector2(dir.y, -dir.x); // south-east side of the lane
            var pos = s + n * 6f;
            P("Kit_Awning", p, new Vector3(pos.x, 0, pos.y), Mathf.Atan2(-n.x, -n.y) * Mathf.Rad2Deg, Vector3.one * 1.25f, false, 0.02f);
            if (i % 3 == 0)
                Seat(PF(SkDir + "AncientClayPots/AncientClayPots.fbx", p, Vector3.zero, F(0, 360), null, false, 0.03f), pos + n * 1.2f);
            if (i % 2 == 0) P("Kit_Rubble_Pile", p, new Vector3(pos.x + F(-2, 2), 0, pos.y + F(-2, 2)), F(0, 360), Vector3.one * 0.7f, false, 0.03f);
        }
        foreach (var at in new[] { V(62, -66), V(80, -46) })
        {
            P("House_Small", p, new Vector3(at.x, 0, at.y), -45, null, true, 0.045f);
            occupied.Add(at);
        }
        occupied.Add(Shop);
    }

    static void BuildCursedWell(Transform p)
    {
        Seat(PF(SkDir + "OldWell/OldWell.fbx", p, Vector3.zero, 40, Vector3.one * 2.6f, true, 0.02f), Cursed);
        for (int i = 0; i < 8; i++)
        {
            float a = (i * 45f + F(-14, 14)) * Mathf.Deg2Rad, r = F(11, 22);
            var at = Cursed + V(Mathf.Sin(a), Mathf.Cos(a)) * r;
            if (LaneDist(at) < 4f) continue;
            var g = Seat(PF(SkDir + "DeadTree/DeadTree.fbx", p, Vector3.zero, F(0, 360), Vector3.one * F(1.1f, 1.7f), false, 0.06f), at);
            g.AddComponent<CapsuleCollider>().height = 4;
        }
        foreach (var at in new[] { V(-78, -92), V(-112, -56), V(-92, -98) })
            P("House_Ruined", p, new Vector3(at.x, 0, at.y), Mathf.Atan2(Cursed.x - at.x, Cursed.y - at.y) * Mathf.Rad2Deg, null, true, 0.045f);
        for (int i = 0; i < 12; i++)
        {
            var at = Cursed + V(F(-22, 22), F(-22, 22));
            P("Kit_Rubble_Pile", p, new Vector3(at.x, 0, at.y), F(0, 360), Vector3.one * F(0.8f, 1.8f), false, 0.02f);
        }
    }

    // Houses front the lanes on both sides, which gives streets instead of a scatter.
    static void BuildStreets(Transform p)
    {
        int built = 0;
        foreach (var lane in Lanes)
            for (int s = 0; s + 1 < lane.Length; s++)
            {
                Vector2 a = lane[s], b = lane[s + 1];
                float len = Vector2.Distance(a, b);
                var dir = (b - a) / len;
                var n = new Vector2(dir.y, -dir.x);
                for (float t = 6; t < len && built < 70; t += 11.5f)
                    foreach (var side in new[] { 1, -1 })
                    {
                        var pos = a + dir * t + n * side * F(7.4f, 8f); // frontage ~3m from lane centre: a 6m street
                        var face = -n * side;
                        float yaw = Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg;
                        if (!CanBuild(pos, yaw)) continue;
                        occupied.Add(pos);
                        while (occupiedYaw.Count < occupied.Count - 1) occupiedYaw.Add(float.NaN);
                        occupiedYaw.Add(yaw);
                        alleys.Add(pos + dir * 5.75f);
                        built++;
                        double r = rng.NextDouble();
                        if (r < 0.5)
                        {
                            P("House_Small", p, new Vector3(pos.x, 0, pos.y), yaw, null, true, 0.045f);
                        }
                        else if (r < 0.72)
                        {
                            P("House_Ruined", p, new Vector3(pos.x, 0, pos.y), yaw, null, true, 0.045f);
                            for (int i = 0; i < 2; i++)
                                P("Kit_Rubble_Pile", p, new Vector3(pos.x + F(-7, 7), 0, pos.y + F(-7, 7)), F(0, 360), Vector3.one * F(0.8f, 1.5f), false, 0.02f);
                        }
                        else Block(p, pos, yaw, 2, 2, r < 0.9 ? 2 : 1, true);
                    }
            }
    }

    static void BuildScatter(Transform rocks)
    {
        for (int i = 0; i < 90; i++) // rock outcrops up the boundary ridge
        {
            float a = i / 90f * Mathf.PI * 2 + F(-0.03f, 0.03f), r = F(155, 188);
            var at = V(Mathf.Sin(a) * r, Mathf.Cos(a) * r);
            if (LaneDist(at) < 8) continue;
            float s = Mathf.Lerp(3f, 11f, Mathf.InverseLerp(155, 188, r));
            P("Kit_Rubble_Pile", rocks, new Vector3(at.x, Height(at.x, at.y) - s * 0.3f, at.y), F(0, 360), new Vector3(s * F(0.8f, 1.3f), s * F(0.8f, 1.6f), s * F(0.8f, 1.3f)), r < 172, 0.008f);
        }
        for (int i = 0; i < 40; i++)
        {
            var at = V(F(-125, 125), F(-90, 85));
            if (!InVillage(at) || LaneDist(at) < 4f || InZone(at)) continue;
            P("Kit_Rubble_Pile", rocks, new Vector3(at.x, 0, at.y), F(0, 360), Vector3.one * F(0.6f, 1.4f), false, 0.025f);
        }
    }

    // ---------- horror dressing: narrows streets, breaks sightlines; zones (InZone) stay open as relief ----------
    const string PropDir = "Assets/MAIN GAME/3D models/HorrorProps/";
    static Material propMat;

    [MenuItem("Tools/Kuldhara/Rebuild Streets + Dressing")]
    public static void RebuildStreetsAndDressing()
    {
        root = GameObject.Find("_Kuldhara_Level").transform;
        rng = new System.Random(1337);
        foreach (var n in new[] { "Buildings", "Vegetation", "HorrorDressing" })
        {
            var old = root.Find(n);
            if (old) Object.DestroyImmediate(old.gameObject);
        }
        occupied.Clear();
        occupiedYaw.Clear();
        alleys.Clear();
        occupied.Add(Shop);
        BuildStreets(Group("Buildings"));
        BuildDressing(Group("HorrorDressing"));
        SetStaticFlags();
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        CheckNavigation();
    }

    static Material PropMaterial()
    {
        if (propMat) return propMat;
        propMat = MatAsset(PropDir + "HorrorProps.mat", () =>
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PropDir + "HorrorProps_Atlas.png"));
            m.SetFloat("_Smoothness", 0f);
            m.enableInstancing = true;
            return m;
        });
        return propMat;
    }

    enum Col { None, Box, Trunk }

    static GameObject Prop(string name, Transform p, Vector2 at, float yaw, float scale, Col col)
    {
        var g = PF(PropDir + name + ".fbx", p, new Vector3(at.x, Height(at.x, at.y), at.y), yaw, Vector3.one * scale, false, 0);
        Renderer lod0 = null, lod1 = null;
        foreach (var r in g.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterial = PropMaterial();
            if (r.name.EndsWith("_LOD0")) lod0 = r; else lod1 = r;
        }
        var group = g.GetComponent<LODGroup>();
        if (!group) group = g.AddComponent<LODGroup>();
        group.SetLODs(new[] { new LOD(0.12f, new[] { lod0 }), new LOD(0.02f, new[] { lod1 }) });
        group.RecalculateBounds();
        var bounds = lod0.GetComponent<MeshFilter>().sharedMesh.bounds;
        if (col == Col.Box)
        {
            var b = g.AddComponent<BoxCollider>();
            b.center = bounds.center;
            b.size = bounds.size;
        }
        else if (col == Col.Trunk)
        {
            var c = g.AddComponent<CapsuleCollider>();
            c.radius = 0.4f;
            c.height = 4f;
            c.center = Vector3.up * 2f;
        }
        return g;
    }

    // Blocks one side of the lane; the far side keeps a walkable gap of about 2.5m. Tall pieces hide what lies ahead.
    static void Chicane(Transform p, Vector2 at, Vector2 dir, int side)
    {
        var n = new Vector2(dir.y, -dir.x) * side;
        float across = Mathf.Atan2(-n.y, n.x) * Mathf.Rad2Deg;
        var c = at + n * 1.3f;
        switch (rng.Next(4))
        {
            case 0:
                P("Kit_Wall_4m_Broken", p, new Vector3(c.x, 0, c.y), across);
                P("Kit_Rubble_Pile", p, new Vector3(c.x + dir.x * 1.5f, 0, c.y + dir.y * 1.5f), F(0, 360), Vector3.one * 0.9f, false, 0.02f);
                break;
            case 1:
                Prop("WallRuin", p, c, across, 1f, Col.Box);
                Prop("Bush_Dry", p, c + n - dir * 1.4f, F(0, 360), 0.9f, Col.None);
                break;
            case 2:
                Prop(rng.Next(2) == 0 ? "Fence_Broken_A" : "Fence_Broken_B", p, c, across, 1.15f, Col.Box);
                Prop("Bush_Thicket", p, c + n * 1.2f + dir * 1.2f, F(0, 360), 0.65f, Col.Trunk);
                Prop("Wood_Debris", p, at - dir * 2.5f, F(0, 360), 0.9f, Col.None);
                break;
            default:
                Prop("Cart_Abandoned", p, c, across + F(-12, 12), 0.9f, Col.Box);
                Prop("Wood_Debris", p, at + dir * 2.5f, F(0, 360), 0.8f, Col.None);
                break;
        }
    }

    static void BuildDressing(Transform p)
    {
        var veg = Group("Vegetation");

        // Winding route: alternate-side blockers along every lane, clear of junctions and open zones.
        int side = 1;
        foreach (var lane in Lanes)
            for (int s = 0; s + 1 < lane.Length; s++)
            {
                var dir = (lane[s + 1] - lane[s]).normalized;
                float len = Vector2.Distance(lane[s], lane[s + 1]);
                for (float t = 11; t < len - 9; t += 19)
                {
                    var at = lane[s] + dir * t;
                    if (!InVillage(at) || InZone(at) || Junction(at)) continue;
                    Chicane(p, at, dir, side);
                    side = -side;
                }
            }

        // Alleys: most are choked with something; every fifth is a small abandoned scene.
        for (int i = 0; i < alleys.Count; i++)
        {
            var a = alleys[i];
            if (LaneDist(a) < 4.5f || InZone(a)) continue;
            float yaw = F(0, 360);
            switch (i % 5)
            {
                case 0: Prop("Charpoy_Broken", p, a, yaw, 1f, Col.None); StoryPots(p, a + V(0.9f, 0.7f)); break;
                case 1: Prop("Bush_Thicket", veg, a, yaw, 0.55f, Col.Trunk); break;
                case 2: Prop("Wood_Debris", p, a, yaw, 1f, Col.None); break;
                case 3: Prop("Bush_Dry", veg, a, yaw, 1f, Col.None); break;
            }
        }

        // Thickets behind the rows and along the walls hide the open desert; lanes stay clear.
        var used = new List<Vector2>();
        for (int i = 0; i < 400 && used.Count < 85; i++)
        {
            var at = V(F(-135, 130), F(-95, 92));
            if (!InVillage(at) || InZone(at) || LaneDist(at) < 9f) continue;
            bool clear = true;
            foreach (var o in occupied) if (Vector2.Distance(o, at) < 7.5f) { clear = false; break; }
            foreach (var o in used) if (Vector2.Distance(o, at) < 5.5f) { clear = false; break; }
            if (!clear) continue;
            used.Add(at);
            Scatter(veg, at);
        }
        // Approach to the gate: bare trees crowding the road.
        for (int i = 0; i < 26; i++)
        {
            float x = (i % 2 == 0 ? 1 : -1) * F(5.5f, 16f);
            Scatter(veg, V(x, F(-160, -98)));
        }
    }

    static void Scatter(Transform p, Vector2 at)
    {
        double r = rng.NextDouble();
        if (r < 0.28) Prop("DeadTree_A", p, at, F(0, 360), F(0.85f, 1.25f), Col.Trunk);
        else if (r < 0.5) Prop("DeadTree_B", p, at, F(0, 360), F(0.85f, 1.25f), Col.Trunk);
        else if (r < 0.58) Prop("DeadTree_Snag", p, at, F(0, 360), F(0.9f, 1.3f), Col.Trunk);
        else if (r < 0.78) Prop("Bush_Thicket", p, at, F(0, 360), F(0.55f, 0.8f), Col.Trunk);
        else if (r < 0.9) Prop("Bush_Dry", p, at, F(0, 360), F(0.8f, 1.3f), Col.None);
        else Prop(r < 0.95 ? "Rock_Round" : "Rock_Slab", p, at, F(0, 360), F(0.8f, 1.5f), Col.Box);
    }

    static void StoryPots(Transform p, Vector2 at)
    {
        Prop("ClayPots", p, at, F(0, 360), 1f, Col.None);
    }

    static bool Junction(Vector2 at)
    {
        foreach (var l in Lanes) foreach (var v in l) if (Vector2.Distance(v, at) < 8f) return true;
        return false;
    }

    // Flood-fills walkable ground from the spawn with the player's capsule and logs any objective it cannot reach.
    [MenuItem("Tools/Kuldhara/Check Navigation")]
    public static void CheckNavigation()
    {
        const float cell = 0.5f;
        const int half = 400; // +/- 200m around C
        Physics.SyncTransforms();
        var seen = new bool[half * 2, half * 2];
        var ground = new float[half * 2, half * 2];
        var q = new Queue<Vector2Int>();
        var player = GameObject.Find("PlayerMain").transform;
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false; // the player shares the Default layer with the level, so its own capsule must not count
        const int mask = ~0;
        System.Func<Vector3, Vector2Int> toCell = w => new Vector2Int(Mathf.RoundToInt((w.x - C.x) / cell) + half, Mathf.RoundToInt((w.z - C.z) / cell) + half);
        var start = toCell(player.position);
        seen[start.x, start.y] = true;
        ground[start.x, start.y] = player.position.y;
        q.Enqueue(start);
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        int walkable = 0;
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            walkable++;
            foreach (var d in dirs)
            {
                var n = c + d;
                if (n.x < 0 || n.y < 0 || n.x >= half * 2 || n.y >= half * 2 || seen[n.x, n.y]) continue;
                var w = new Vector3((n.x - half) * cell + C.x, ground[c.x, c.y] + 1.2f, (n.y - half) * cell + C.z);
                if (!Physics.SphereCast(w, 0.2f, Vector3.down, out var hit, 4f, mask, QueryTriggerInteraction.Ignore)) continue; // a sphere bridges thin seams like the capsule does
                if (hit.point.y - ground[c.x, c.y] > cc.stepOffset || Vector3.Angle(hit.normal, Vector3.up) > cc.slopeLimit) continue;
                var feet = hit.point + Vector3.up * (cc.radius + 0.15f); // clears 32 degree stair ramps
                if (Physics.CheckCapsule(feet, feet + Vector3.up * (cc.height - cc.radius * 2 - 0.15f), cc.radius, mask, QueryTriggerInteraction.Ignore)) continue;
                seen[n.x, n.y] = true;
                ground[n.x, n.y] = hit.point.y;
                q.Enqueue(n);
            }
        }
        cc.enabled = true;
        int ok = 0, bad = 0;
        foreach (var t in Object.FindObjectsByType<ObjectiveTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var c = toCell(t.transform.position);
            bool reach = false;
            for (int x = -8; x <= 8 && !reach; x++) // triggers have some reach: accept walkable ground within 4m
                for (int z = -8; z <= 8 && !reach; z++)
                    reach = seen[c.x + x, c.y + z];
            if (reach) ok++; else { bad++; Debug.LogError("NAV: unreachable " + t.name + " " + t.transform.position); }
        }
        Debug.Log("NAV: walkable cells " + walkable + ", reachable objectives " + ok + ", unreachable " + bad);
    }

    // ---------- gameplay + look ----------
    static void MoveGameplay()
    {
        var player = GameObject.Find("PlayerMain");
        player.transform.SetPositionAndRotation(C + new Vector3(0, 0.3f, -128), Quaternion.identity);

        var relics = GameObject.Find("Relics").transform;
        var spots = new (string name, Vector3 at)[]
        {
            ("Relic_CursedWell", new Vector3(-94, 1.2f, -63)),
            ("Relic_Shop", new Vector3(66, 1.2f, -42)),
            ("Relic_Temple", new Vector3(-84, 1.2f, 40)),
            ("Relic_Baori", new Vector3(93, -2.8f, 26)),
            ("Relic_Square", new Vector3(11, 1.2f, 16)),
        };
        for (int i = 0; i < spots.Length; i++)
        {
            var r = relics.GetChild(i);
            r.name = spots[i].name;
            r.position = C + spots[i].at;
        }
        var shrine = relics.parent.Find("Shrine_Goal");
        shrine.position = C + new Vector3(0, 5, 100);

        GameObject.Find("Trigger box").transform.SetPositionAndRotation(C + new Vector3(0, 1, -72), Quaternion.Euler(0, 90, 0));
        var ghost = FindRootEvenIfInactive("Ghost1_1");
        if (ghost) ghost.transform.position = C + new Vector3(0, 0.5f, -50);

        var cam = player.GetComponentInChildren<Camera>();
        cam.farClipPlane = 260;
        cam.useOcclusionCulling = true;
        // Lamps only make sense at night; each one is also a per-pixel light on mobile.
        foreach (var l in cam.GetComponentsInChildren<Light>(true)) l.gameObject.SetActive(false);
    }

    static GameObject FindRootEvenIfInactive(string n)
    {
        foreach (var g in root.gameObject.scene.GetRootGameObjects()) if (g.name == n) return g;
        return null;
    }

    static Material MatAsset(string path, System.Func<Material> create)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = create(); AssetDatabase.CreateAsset(m, path); }
        return m;
    }

    // Layers read smoothness from albedo alpha by default, and the baked tiles have alpha 1: that made the terrain glossy.
    static void Matte(TerrainLayer l)
    {
        l.smoothnessSource = TerrainLayerSmoothnessSource.Constant;
        l.smoothness = 0;
        l.metallic = 0;
        EditorUtility.SetDirty(l);
    }

    // Lighting, sky, fog, grading and terrain material only: touches no level objects, safe to re-run.
    [MenuItem("Tools/Kuldhara/Apply Look")]
    public static void SetLighting()
    {
        var sun = GameObject.Find("Directional Light").GetComponent<Light>();
        root = GameObject.Find("_Kuldhara_Level").transform;
        if (rng == null) rng = new System.Random(1337);

        // Cold moon: dim enough that the flashlight and zone lights carry the scene.
        sun.transform.rotation = Quaternion.Euler(24, 150, 0); // low in the north-west: the moon hangs over the gate at spawn
        sun.useColorTemperature = false;
        sun.color = new Color(0.55f, 0.68f, 0.95f);
        sun.intensity = 0.5f;
        sun.shadows = LightShadows.Hard;
        sun.shadowStrength = 0.85f;
        RenderSettings.sun = sun;
        var fogColor = new Color(0.045f, 0.06f, 0.09f);
        var sky = MatAsset("Assets/MAIN GAME/Environments/Kuldhara_Night_Sky.mat", () => new Material(Shader.Find("Kuldhara/NightSkyMobile")));
        sky.SetColor("_Horizon", fogColor);
        sky.SetVector("_MoonDir", -sun.transform.forward);
        sky.SetFloat("_MoonSize", 0.0085f);
        sky.SetFloat("_StarAmount", 0.6f);
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.17f, 0.21f, 0.31f);
        RenderSettings.ambientEquatorColor = new Color(0.12f, 0.15f, 0.21f);
        RenderSettings.ambientGroundColor = new Color(0.06f, 0.07f, 0.09f);
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 0.1f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.024f; // nothing past ~80m is visible, so the far clip can drop to 120m
        RenderSettings.fogColor = fogColor;
        DynamicGI.UpdateEnvironment();

        BuildZoneLights();

        var terrain = GameObject.Find("Kuldhara_Terrain").GetComponent<Terrain>();
        const string lit = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/TerrainLit.mat";
        var tm = MatAsset("Assets/MAIN GAME/Terrain/Kuldhara_TerrainLit.mat", () => new Material(AssetDatabase.LoadAssetAtPath<Material>(lit)));
        for (int i = 0; i < 4; i++)
        {
            tm.SetFloat("_Smoothness" + i, 0);
            tm.SetFloat("_Metallic" + i, 0);
        }
        EditorUtility.SetDirty(tm);
        terrain.materialTemplate = tm;
        foreach (var l in terrain.terrainData.terrainLayers) Matte(l);

        var profile = GameObject.Find("Global Volume").GetComponent<Volume>().sharedProfile;
        if (profile.TryGet(out ColorAdjustments ca))
        {
            ca.postExposure.value = 0f;
            ca.contrast.value = 22f;
            ca.colorFilter.value = new Color(0.9f, 0.95f, 1f);
            ca.saturation.value = -30f;
        }
        if (profile.TryGet(out Vignette vg)) vg.intensity.value = 0.42f;
        // Bloom and tonemapping need HDR; nothing here is emissive, so they cost more than they give on mobile.
        if (profile.TryGet(out Bloom bl)) bl.active = false;
        if (profile.TryGet(out Tonemapping tn)) tn.active = false;
        if (!profile.TryGet(out ShadowsMidtonesHighlights smh)) smh = profile.Add<ShadowsMidtonesHighlights>();
        smh.active = true;
        smh.shadows.Override(new Vector4(0.86f, 0.94f, 1.1f, -0.02f)); // blue-black shadows
        smh.highlights.Override(new Vector4(1.02f, 1f, 0.96f, 0f));
        EditorUtility.SetDirty(profile);

        var rp = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        rp.supportsHDR = false;
        rp.shadowDistance = 45;
        var rpSo = new SerializedObject(rp);
        rpSo.FindProperty("m_MainLightShadowmapResolution").intValue = 1024;
        rpSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rp);

        var cam = GameObject.Find("MainCamera").GetComponent<UniversalAdditionalCameraData>();
        cam.renderPostProcessing = true;
        cam.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        var camera = cam.GetComponent<Camera>();
        camera.farClipPlane = 120;
        camera.fieldOfView = 60; // 40 magnified the sky and made it feel close
        // The Flashlight light now lives on TorchRig in the scene, aligned to the hand-held model; don't re-pose it here.
        EditorSceneManager.MarkSceneDirty(sun.gameObject.scene);
    }

    // Green palms break the mood: same spots, dead trees.
    // Unshadowed and far apart, so at most a couple of these touch any one object.
    static void BuildZoneLights()
    {
        var old = root.Find("ZoneLights");
        if (old) Object.DestroyImmediate(old.gameObject);
        var p = Group("ZoneLights");
        var blood = new Color(0.95f, 0.1f, 0.07f);
        var sick = new Color(0.35f, 0.85f, 0.45f);
        var torch = new Color(1f, 0.5f, 0.18f);
        ZoneLight(p, "Gate_Torch", new Vector3(0, 6, -98), torch, 7f, 18, 5f);
        ZoneLight(p, "Square_Torch", new Vector3(0, 3.5f, 5), torch, 4f, 16, 5f);
        ZoneLight(p, "Haveli_Blood", new Vector3(0, 7, 99), blood, 6f, 24, 1.2f);
        ZoneLight(p, "Temple_Blood", new Vector3(-86, 4, 44), blood, 5f, 18, 1.5f);
        ZoneLight(p, "CursedWell_Sick", new Vector3(-100, 3, -70), sick, 5f, 20, 0.8f);
        ZoneLight(p, "Baori_Sick", new Vector3(85, -2, 38), sick, 4f, 20, 0.8f);
    }

    static void ZoneLight(Transform p, string name, Vector3 at, Color c, float intensity, float range, float flicker)
    {
        var l = new GameObject(name).AddComponent<Light>();
        l.transform.SetParent(p, false);
        l.transform.localPosition = at;
        l.type = LightType.Point;
        l.color = c;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        l.gameObject.AddComponent<LightFlicker>().speed = flicker;
    }

    static void SetStaticFlags()
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            // Props stay out of static batching so their shared material can instance them.
            if (r.name.Contains("_LOD")) { GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.OccludeeStatic); continue; }
            var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
            if (r.bounds.size.magnitude > 5f) flags |= StaticEditorFlags.OccluderStatic;
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags);
        }
        var terrain = root.GetComponentInChildren<Terrain>().gameObject;
        GameObjectUtility.SetStaticEditorFlags(terrain, StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
    }

    // Re-renders the minimap texture from above, matching MapUI's worldMin/worldSize.
    [MenuItem("Tools/Kuldhara/Bake Map Texture")]
    public static void BakeMap()
    {
        var go = new GameObject("MapBakeCam");
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 200;
        cam.nearClipPlane = 1;
        cam.farClipPlane = 300;
        cam.transform.SetPositionAndRotation(C + Vector3.up * 200, Quaternion.Euler(90, 0, 0));
        bool fog = RenderSettings.fog;
        RenderSettings.fog = false;
        float lodBias = QualitySettings.lodBias;
        QualitySettings.lodBias = 100; // keep cull-only LOD groups visible at map scale
        var rt = new RenderTexture(1024, 1024, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
        tex.Apply();
        File.WriteAllBytes(MapPng, tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        RenderSettings.fog = fog;
        QualitySettings.lodBias = lodBias;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(go);
        AssetDatabase.ImportAsset(MapPng);
    }

    [MenuItem("Tools/Kuldhara/Bake Occlusion")]
    public static void BakeOcclusion()
    {
        StaticOcclusionCulling.smallestOccluder = 6f;
        StaticOcclusionCulling.smallestHole = 0.5f;
        StaticOcclusionCulling.GenerateInBackground();
    }
}
