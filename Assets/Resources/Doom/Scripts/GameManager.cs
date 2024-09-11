using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour {
    public static GameManager singleton;

    void Awake() {
        if (singleton is null) {
            DontDestroyOnLoad(this);
            singleton = this;
        } else {
            Destroy(this);
        }
    }

    private static LevelData? levelData;

    public static void SetLevelData(LevelData lev) {
        levelData = lev;
    }

    void Update() {
        if (levelData is not null && WorldManager.instance is not null) {
            WorldManager.SetLevelData(levelData.Value);
            WorldManager.BuildLevelFromData();
            levelData = null;
        }
    }
}