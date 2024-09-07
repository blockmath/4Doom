using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Need to remove dependency on AssetDatabase for any sort of release build!
using UnityEditor;

public class SectorMesh : MonoBehaviour {
    public Sector SectorRef;
    public Mesh4D mesh;

    public Object4D obj;
    public MeshFilter meshFilter;
    public MeshRenderer meshRenderer;
    public ShadowFilter shadowFilter;
    public MeshCollider4D meshCollider;

    public void RecalculateSector() {
        Mesh mesh3D = new Mesh();
        Mesh shadowMesh3D = new Mesh();
        Mesh wireMesh3D = new Mesh();

        mesh.GenerateMesh(mesh3D);
        mesh.GenerateShadowMesh(shadowMesh3D);
        mesh.GenerateWireMesh(wireMesh3D);

        meshFilter.mesh = mesh3D;
        shadowFilter.shadowMesh = shadowMesh3D;
        shadowFilter.wireMesh = wireMesh3D;
        meshCollider.mesh = mesh3D;

        // TODO: dynamic texture loading.
        meshRenderer.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Default.mat");
    }

    void Start() {
        obj = GetComponent<Object4D>();
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        shadowFilter = GetComponent<ShadowFilter>();
        meshCollider = GetComponent<MeshCollider4D>();
    }

    void Update() {

    }
}