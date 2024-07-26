using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using UnityEditor;

// Not so "sample" anymore, now is it?
public class SamplePlayer4D : CameraControl4D {
    [SerializeField] public float JUMP_VELOCITY = 6.0f;
    [SerializeField] public float JUMP_SPEEDUP = 0.05f;
    [SerializeField] public float FRAME_TIME = 0.1f;
    
    [SerializeField] public float BOB_TIME = 0.5f;
    [SerializeField] public float BOB_STRENGTH = 20.0f;
    [SerializeField] public float BOB_HEIGHT_STRENGTH = 0.15f;
    [SerializeField] public float BOB_HEIGHT_SPEED_FACTOR = 2.0f;
    [SerializeField] public float BOB_ASPECT = 0.7f;
    [SerializeField] public float BOB_CAP = 10.0f;
    
    [SerializeField] public float HSPEED;
    [SerializeField] private UnityEngine.UI.Image gunImage;

    [SerializeField] Sprite[] abilityIconsAtlas;
    
    [SerializeField] private Vector2 gunImageOrigin;
    [SerializeField] private float camHeightOrigin;
    private float timer = 0.0f;
    
    public enum DoomWeapon {
        FISTS,
        PISTOL,
        SHOTGUN,
    };
    
    public static readonly Dictionary<DoomWeapon, string> weaponSpriteNames = new() {
        {DoomWeapon.FISTS, "hand"},
        {DoomWeapon.PISTOL, "pistol"},
    };
    
    public static readonly Dictionary<DoomWeapon, int> weaponSpriteAnimFrames = new() {
        {DoomWeapon.FISTS, 4},
        {DoomWeapon.PISTOL, 5},
    };
    
    [SerializeField] private DoomWeapon weapSelected = DoomWeapon.FISTS;
    [SerializeField] bool firing = false;
    [SerializeField] int frame = 0;
    [SerializeField] float subframe = 0.0f;
    
    protected override void Start() {
        base.Start();
        abilityIconsAtlas = Resources.LoadAll<Sprite>(AssetDatabase.GetAssetPath(gunImage.sprite.GetInstanceID()).Replace(".png", "").Replace("Assets/Resources/", ""));
        gunImageOrigin = gunImage.rectTransform.anchoredPosition;
        camHeightOrigin = CAM_HEIGHT;
    }
    
    private Sprite getSprite(string name) {
        Debug.Log(name);
        foreach (Sprite s in abilityIconsAtlas) {
            if (s.name == "doom-weap_" + name) {
                return s;
            }
        }
        return null;
    }
    
    private void setSpriteTo(string name) {
        gunImage.sprite = getSprite(name);
        //Debug.Log(gunImage.sprite.pivot.x + ", " + gunImage.sprite.rect.width + ", " + gunImage.sprite.pivot.x / gunImage.sprite.rect.width);
        gunImage.rectTransform.pivot = new Vector2(gunImage.sprite.pivot.x / gunImage.sprite.rect.width, 0.0f);
    }
    
    protected override void Update() {
        //Base update
        base.Update();
        timer += Time.deltaTime / BOB_TIME;
        
        // View bobbing
        float velocityFactor = Mathf.Clamp(velocity.magnitude / BOB_CAP, 0.0f, 1.0f);
        float bobFactor = Mathf.SmoothStep(0.0f, BOB_STRENGTH, velocityFactor);
        float bobAngle = Mathf.Sin(timer * Mathf.PI) * Mathf.Sin(timer * Mathf.PI) * Mathf.PI;
        Vector2 bobNormOffset = new Vector2(Mathf.Cos(bobAngle), -Mathf.Abs(Mathf.Sin(bobAngle)) * BOB_ASPECT);
        gunImage.rectTransform.anchoredPosition = gunImageOrigin + bobNormOffset * bobFactor;
        bobAngle = Mathf.Sin(BOB_HEIGHT_SPEED_FACTOR * timer * Mathf.PI) * Mathf.Sin(BOB_HEIGHT_SPEED_FACTOR * timer * Mathf.PI);
        bobFactor = Mathf.SmoothStep(0.0f, BOB_HEIGHT_STRENGTH, bobAngle * velocityFactor);
        CAM_HEIGHT = camHeightOrigin - bobFactor;
        
        

        //Find the nearest colliders
        if (isGrounded && InputManager.GetKeyDown(InputManager.KeyBind.Putt)) {
            velocity += gravityDirection * JUMP_VELOCITY;
            velocity += velocity * JUMP_SPEEDUP;
            position4D += velocity * Time.deltaTime;
            isGrounded = false;
        }
        
        // fire weapon
        if (InputManager.GetKey(InputManager.KeyBind.Look5D) && !firing) {
            firing = true;
        }
        
        // switch weapon (debug showcase version)
        if (InputManager.GetKeyDown(InputManager.KeyBind.LookSpin) && !firing) {
            if (weapSelected == DoomWeapon.FISTS) weapSelected = DoomWeapon.PISTOL; else weapSelected = DoomWeapon.FISTS;
        }
        
        HSPEED = (Matrix4x4.Inverse(camMatrix) * velocity).z;
        
        if (firing) {
            bool shouldUpdate = false;
            subframe += Time.deltaTime;
            if (subframe >= FRAME_TIME) {
                frame++;
                subframe = 0;
                shouldUpdate = true;
            }
            if (frame >= weaponSpriteAnimFrames[weapSelected]) {
                if (weapSelected == DoomWeapon.FISTS && frame < weaponSpriteAnimFrames[weapSelected] * 2 - 1) {
                    ;
                } else {
                    firing = false;
                    frame = 0;
                }
            }
            /*if (shouldUpdate) {
                int animFrame = frame;
                if (animFrame >= weaponSpriteAnimFrames[weapSelected]) animFrame = (weaponSpriteAnimFrames[weapSelected] * 2 - animFrame - 1);
                setSpriteTo(weaponSpriteNames[weapSelected] + "_" + animFrame);
                gunImage.SetNativeSize();
            }*/
        }
        int animFrame = frame;
        if (animFrame >= weaponSpriteAnimFrames[weapSelected]) animFrame = (weaponSpriteAnimFrames[weapSelected] * 2 - animFrame - 1);
        setSpriteTo(weaponSpriteNames[weapSelected] + "_" + animFrame);
        gunImage.SetNativeSize();
    }
}
