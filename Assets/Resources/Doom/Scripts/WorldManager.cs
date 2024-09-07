using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.CSharp;
using UnityEngine;

public class PlaneDefBehaviour {
    public List<Vector3> Vertices;
    public int SectorId;
    public Autofield Tags;

    public Vector3 playerPrevPos;

    public PlaneDefBehaviour(List<Vector3> list, int index, Autofield tags) {
        Vertices = list;
        SectorId = index;
        Tags = tags;
        playerPrevPos = new Vector3(float.NaN, float.NaN, float.NaN);
    }

    private bool PlayerDidCrossTri(Vector3 p1, Vector3 p2, Vector3[] t) {
        // Initial cheap dot product check: did the player cross the plane?
        Vector3 tn = Vector3.Cross(t[1] - t[0], t[2] - t[0]);
        int s1 = (int)Mathf.Sign(Vector3.Dot(p1 - t[0], tn));
        int s2 = (int)Mathf.Sign(Vector3.Dot(p2 - t[0], tn));
        if (s1 == 0) s1 = 1;
        if (s2 == 0) s2 = 1;

        if (s1 == s2) return false; // Plane was not broken
        return true; // TODO: ACTUAL CALCULATION
    }

    public void Update(Vector3 playerNowPos) {

    }
}

public class WorldManager : MonoBehaviour {
    public static WorldManager instance;

    public GameObject _SectorPrefab;

    public LevelData level;
    public List<SectorMesh> SectorMeshes;
    public Dictionary<int, PlaneDefBehaviour> PlaneDefs;


    public static Vector4 ExtendY(Vector3 v, float y) {
        return new Vector4(v.x, y, v.z, v.y);
    }

    void WallFromTriBound(Mesh4D m_, Vector3[] tri, float lo, float hi) {
        m_.AddHalfCell(ExtendY(tri[0], lo), ExtendY(tri[0], hi), ExtendY(tri[1], lo), ExtendY(tri[1], hi), ExtendY(tri[2], lo), ExtendY(tri[2], hi));
    }

    void SectorFloorFromBoundary(Mesh4D m_, SectorBoundary bound, float y) {
        foreach (Autofield tri in bound.Triangles) {
            Vector3Int vi = (Vector3Int)tri.Fields[typeof(Vector3Int)];
            m_.AddTetrahedron(ExtendY(bound.Vertices[vi.x], y), ExtendY(bound.Vertices[vi.y], y), ExtendY(bound.Vertices[vi.z], y), ExtendY(bound.Kernel, y));
        }
    }

    void SectorCeilingFromBoundary(Mesh4D m_, SectorBoundary bound, float y) {
        foreach (Autofield tri in bound.Triangles) {
            Vector3Int vi = (Vector3Int)tri.Fields[typeof(Vector3Int)];
            m_.AddTetrahedron(ExtendY(bound.Kernel, y), ExtendY(bound.Vertices[vi.x], y), ExtendY(bound.Vertices[vi.y], y), ExtendY(bound.Vertices[vi.z], y));
        }
    }

    SectorMesh SectorGenerateMesh(Sector sector) {
        SectorMesh sectorMesh = Instantiate(_SectorPrefab).GetComponent<SectorMesh>();
        sector.ApplyTransform();

        Mesh4D mesh = new Mesh4D(3);
        SectorFloorFromBoundary(mesh, sector.Boundary, sector.CellFloor);
        mesh.NextSubmesh();
        SectorCeilingFromBoundary(mesh, sector.Boundary, sector.CellHeight + sector.CellFloor);
        mesh.NextSubmesh();
        foreach (Autofield tri in sector.Boundary.Triangles) {
            Vector3[] vts = new Vector3[] {sector.Boundary.Vertices[((Vector3Int)tri.Fields[typeof(Vector3Int)]).x], sector.Boundary.Vertices[((Vector3Int)tri.Fields[typeof(Vector3Int)]).y], sector.Boundary.Vertices[((Vector3Int)tri.Fields[typeof(Vector3Int)]).z]};
            if (tri.Tags.Contains(FieldTag.NoWall)) {
                break;
            } else if (tri.Tags.Contains(FieldTag.HoleWall)) {
                dynamic hts = tri.Fields[typeof(List<>).MakeGenericType(typeof(float))];
                WallFromTriBound(mesh, vts, sector.CellFloor, hts[0]);
                WallFromTriBound(mesh, vts, hts[1], sector.CellHeight + sector.CellFloor);
            } else if (tri.Tags.Contains(FieldTag.BottomWall)) {
                WallFromTriBound(mesh, vts, sector.CellFloor, (float)tri.Fields[typeof(float)]);
            } else if (tri.Tags.Contains(FieldTag.TopWall)) {
                WallFromTriBound(mesh, vts, (float)tri.Fields[typeof(float)], sector.CellHeight + sector.CellFloor);
            } else {
                WallFromTriBound(mesh, vts, sector.CellFloor, sector.CellHeight + sector.CellFloor);
            }
        }

        sectorMesh.SectorRef = sector;
        sectorMesh.mesh = mesh;
        sectorMesh.RecalculateSector();
        return sectorMesh;
    }

    public static void SetLevelData(LevelData lev) {
        instance.level = lev;
    }

    public static void BuildLevelFromData() {
        instance.SectorMeshes = new List<SectorMesh>();
        instance.PlaneDefs = new Dictionary<int, PlaneDefBehaviour>();

        foreach (Sector sector in instance.level.Sectors) {
            instance.SectorMeshes.Add(instance.SectorGenerateMesh(sector));
        }
        foreach ((int i, PlaneDef planeDef) in instance.level.PlaneDefs) {
            instance.PlaneDefs.Add(i, new PlaneDefBehaviour(planeDef.Vertices, planeDef.SectorId, planeDef.Tags));
        }
    }

    void Awake() {
        instance = this;
        SectorMeshes = new List<SectorMesh>();
        PlaneDefs = new Dictionary<int, PlaneDefBehaviour>();
    }


    void Start() {

    }

    void Update() {
        foreach ((int i, PlaneDefBehaviour planeDef) in PlaneDefs) {
            // TODO: pass player position to PlaneDefBehaviour to check for interaction
        }
    }
}