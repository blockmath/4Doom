using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;

public class SelectionMenuItem {
    public string name;
    public string filepath;
    public bool sel;

    public SelectionMenuItem(string _s, bool _b, string _fp = "") {
        name = _s;
        sel = _b;
        filepath = _fp;
    }
}

public class MenuTerminal : MonoBehaviour {
    public int selection = 0;
    public List<SelectionMenuItem> items;

    [SerializeField]TextMeshProUGUI textmesh;
    [SerializeField]List<string> itemTextsGUI;
    private string GenerateMenuText() {
        string text = "<font=\"MajMono\">";
        foreach (SelectionMenuItem item in items) {
            if (item.sel) {
                text += "<mark=#ffffff padding=20,20,0,10><color=\"black\">";
            }
            text += item.name;
            if (item.sel) {
                text += "</mark></color>";
            }
            text += "\n";
        }
        return text;
    }

    void Start() {
        items = new List<SelectionMenuItem>();
        /*foreach (string s in itemTextsGUI) { // Debug
            items.Add(new SelectionMenuItem(s, false));
        }*/
        DirectoryInfo di = new DirectoryInfo("Assets/Resources/Doom/Levels");
        FileInfo[] files = di.GetFiles("*.4dg");
        foreach (FileInfo fi in files) {
            items.Add(new SelectionMenuItem(fi.Name.Replace("_", " "), false, fi.FullName));
        }
        items[selection].sel = true;
        textmesh.text = GenerateMenuText();
    }
    
    private void SelMove(int dir) {
        items[selection].sel = false;
        selection += dir;
        selection = (selection + 2*items.Count) % items.Count;
        items[selection].sel = true;
    }

    private void MoveDown() {
        SelMove(+1);
    }

    private void MoveUp() {
        SelMove(-1);
    }

    private float accumulatedScrollDelta = 0.0f, scrollTick = 0.1f;
    void Update() {
        if (Input.GetButtonDown("MenuUp")) {
            MoveUp();
            InvokeRepeating("MoveUp", 0.25f, 0.075f);
        }
        if (Input.GetButton("MenuUp")) {
            CancelInvoke("MoveUp");
        }
        if (Input.GetButtonDown("MenuDown")) {
            MoveDown();
            InvokeRepeating("MoveDown", 0.25f, 0.075f);
        }
        if (Input.GetButtonUp("MenuDown")) {
            CancelInvoke("MoveDown");
        }
        accumulatedScrollDelta += Input.GetAxis("Mouse ScrollWheel");
        if (accumulatedScrollDelta >= scrollTick) {
            accumulatedScrollDelta -= scrollTick;
            MoveUp();
        }
        if (accumulatedScrollDelta <= -scrollTick) {
            accumulatedScrollDelta += scrollTick;
            MoveDown();
        }

        if (Input.GetButtonDown("Submit")) {
            Debug.Log("Loading level " + items[selection].filepath);
            SceneManager.LoadScene("Assets/Resources/Doom/Scenes/LevelScene.unity");
            string fp = items[selection].filepath;
            Debug.Log("fp get");
            LevelData levelData = MDMFLoader.Load(fp);
            Debug.Log("ld get");
            WorldManager.SetLevelData(levelData);
            Debug.Log("data set");
            WorldManager.BuildLevelFromData();
            Debug.Log("level built");
        }

        textmesh.text = GenerateMenuText();
    }
}
