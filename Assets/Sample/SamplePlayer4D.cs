using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using UnityEditor;

// Not so "sample" anymore, now is it?
public class SamplePlayer4D : CameraControl4D {
    public static SamplePlayer4D singleton = null;

    [SerializeField] public float JUMP_VELOCITY = 6.0f;
    [SerializeField] public float JUMP_SPEEDUP = 0.05f;
    
    [SerializeField] public float BOB_TIME = 0.5f;
    [SerializeField] public float BOB_STRENGTH = 20.0f;
    [SerializeField] public float BOB_HEIGHT_STRENGTH = 0.15f;
    [SerializeField] public float BOB_HEIGHT_SPEED_FACTOR = 2.0f;
    [SerializeField] public float BOB_ASPECT = 0.7f;
    [SerializeField] public float BOB_CAP = 10.0f;
    
    [SerializeField] public float ROCKET_RECOIL = 5.0f;
    [SerializeField] public float ROCKET_RECOIL_ANIM = 25.0f;
    [SerializeField] public float ROCKET_RECOIL_ANIM_LENGTH = 2.0f;
    [SerializeField] public float RECOIL_POW = 2.0f;
    public float rocketRecoilAnimTimer = 0.0f;
    
    [SerializeField] public float HSPEED;
    
    
    [SerializeField] private UnityEngine.UI.Image gunImage;

    [SerializeField] Sprite[] abilityIconsAtlas;
    
    [SerializeField] private Vector2 gunImageOrigin;
    [SerializeField] private float camHeightOrigin;
    
    [SerializeField] private GameObject missilePrefab;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private GameObject fistsPrefab;

    public float maxHealth = 100.0f;
    public float health;
    
    public float mass = 1.0f;
    private float timer = 0.0f;
    
    public enum DoomWeapon {
        FISTS,
        PISTOL,
        SHOTGUN,
        SUPERSHOTGUN,
        CHAINGUN,
        ROCKETLAUNCHER,
    };
    
    public const DoomWeapon finalWeapon = DoomWeapon.ROCKETLAUNCHER;
    
    public static readonly Dictionary<DoomWeapon, string> weaponSpriteNames = new() {
        {DoomWeapon.FISTS, "hand"},
        {DoomWeapon.PISTOL, "pistol"},
        {DoomWeapon.SHOTGUN, "buck"},
        {DoomWeapon.SUPERSHOTGUN, "ultrabuck"},
        {DoomWeapon.CHAINGUN, "gatling"},
        {DoomWeapon.ROCKETLAUNCHER, "dominion"},
    };
    
    public static readonly Dictionary<DoomWeapon, int> weaponSpriteAnimFrames = new() {
        {DoomWeapon.FISTS, 4},
        {DoomWeapon.PISTOL, 4},
        {DoomWeapon.SHOTGUN, 6},
        {DoomWeapon.SUPERSHOTGUN, 10},
        {DoomWeapon.CHAINGUN, 2},
        {DoomWeapon.ROCKETLAUNCHER, 5},
    };
    
    public static readonly Dictionary<DoomWeapon, float> weaponSpriteAnimSpeed = new() {
        {DoomWeapon.FISTS, 0.1f},
        {DoomWeapon.PISTOL, 0.1f},
        {DoomWeapon.SHOTGUN, 0.09f},
        {DoomWeapon.SUPERSHOTGUN, 0.1f},
        {DoomWeapon.CHAINGUN, 0.05f},
        {DoomWeapon.ROCKETLAUNCHER, 0.1f},
    };
    
    [SerializeField] private DoomWeapon weapSelected = DoomWeapon.FISTS;
    [SerializeField] bool firing = false;
    [SerializeField] int frame = 0;
    [SerializeField] float subframe = 0.0f;
    
    protected override void Awake() {
        if (singleton is null) {
            singleton = this;
            DontDestroyOnLoad(gameObject);
            base.Awake();
        } else {
            Destroy(gameObject);
        }
    }

    protected override void Start() {
        base.Start();
        abilityIconsAtlas = Resources.LoadAll<Sprite>(AssetDatabase.GetAssetPath(gunImage.sprite.GetInstanceID()).Replace(".png", "").Replace("Assets/Resources/", ""));
        gunImageOrigin = gunImage.rectTransform.anchoredPosition;
        camHeightOrigin = CAM_HEIGHT;
        health = maxHealth;
    
        Shader.SetGlobalVector(Shader.PropertyToID("_ShadowColor1"), new Vector4(0.0f, 0.5f, 1.0f, 1.0f));
        Shader.SetGlobalVector(Shader.PropertyToID("_ShadowColor2"), new Vector4(1.0f, 0.5f, 0.0f, 1.0f));
    }
    
    private Sprite getSprite(string name) {
        //Debug.Log(name);
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
        
        // switch weapon (debug showcase version)
        if (InputManager.GetKeyDown(InputManager.KeyBind.LookSpin) && !firing) {
            if (weapSelected == finalWeapon) weapSelected = DoomWeapon.FISTS; else weapSelected++;
        }
        
        // fire weapon
        if (InputManager.GetKey(InputManager.KeyBind.Look5D) && !firing) {
            firing = true;
            switch (weapSelected) {
                case DoomWeapon.FISTS: {
                    Object4D bullet = Instantiate(fistsPrefab).GetComponent<Object4D>();
                    bullet.localPosition4D = position4D + camMatrix * new Vector4(0.0f, 0.0f, 1.33f, 0.0f) + new Vector4(0.0f, CAM_HEIGHT, 0.0f, 0.0f);
                    bullet.GetComponent<Missile4D>().damage = 4;
                    bullet.GetComponent<Missile4D>().inheritedVelocity = velocity;
                    }
                    break;
                case DoomWeapon.PISTOL: {
                    Object4D bullet = Instantiate(bulletPrefab).GetComponent<Object4D>();
                    bullet.localPosition4D = position4D + camMatrix * new Vector4(0.0f, 0.0f, 0.2f, 0.0f) + new Vector4(0.0f, CAM_HEIGHT - 0.1f, 0.0f, 0.0f);
                    bullet.localRotation4D = camMatrix;
                    bullet.GetComponent<Missile4D>().damage = 5;
                    bullet.GetComponent<Missile4D>().inheritedVelocity = velocity;
                    }
                    break;
                case DoomWeapon.SHOTGUN: {
                    Object4D bullet = Instantiate(bulletPrefab).GetComponent<Object4D>();
                    bullet.localPosition4D = position4D + camMatrix * new Vector4(0.0f, 0.0f, 0.2f, 0.0f) + new Vector4(0.0f, CAM_HEIGHT - 0.12f, 0.0f, 0.0f);
                    bullet.localRotation4D = camMatrix;
                    bullet.GetComponent<Missile4D>().damage = 12;
                    bullet.GetComponent<Missile4D>().inheritedVelocity = velocity;
                    
                    bullet = Instantiate(bulletPrefab).GetComponent<Object4D>();
                    bullet.localPosition4D = position4D + camMatrix * new Vector4(0.0f, 0.0f, 0.2f, 0.0f) + new Vector4(0.0f, CAM_HEIGHT - 0.08f, 0.0f, 0.0f);
                    bullet.localRotation4D = camMatrix;
                    bullet.GetComponent<Missile4D>().damage = 12;
                    bullet.GetComponent<Missile4D>().inheritedVelocity = velocity;
                    }
                    break;
                case DoomWeapon.SUPERSHOTGUN: {
                    Object4D bullet = Instantiate(bulletPrefab).GetComponent<Object4D>();
                    bullet.localPosition4D = position4D + camMatrix * new Vector4(-0.02f, 0.0f, 0.2f, 0.0f) + new Vector4(0.0f, CAM_HEIGHT - 0.1f, 0.0f, 0.0f);
                    bullet.localRotation4D = camMatrix;
                    bullet.GetComponent<Missile4D>().damage = 20;
                    bullet.GetComponent<Missile4D>().inheritedVelocity = velocity;
                    
                    bullet = Instantiate(bulletPrefab).GetComponent<Object4D>();
                    bullet.localPosition4D = position4D + camMatrix * new Vector4(0.02f, 0.0f, 0.2f, 0.0f) + new Vector4(0.0f, CAM_HEIGHT - 0.1f, 0.0f, 0.0f);
                    bullet.localRotation4D = camMatrix;
                    bullet.GetComponent<Missile4D>().damage = 20;
                    bullet.GetComponent<Missile4D>().inheritedVelocity = velocity;
                    }
                    break;
                case DoomWeapon.CHAINGUN: {
                    Object4D bullet = Instantiate(bulletPrefab).GetComponent<Object4D>();
                    bullet.localPosition4D = position4D + camMatrix * new Vector4(0.0f, 0.0f, 0.2f, 0.0f) + new Vector4(0.0f, CAM_HEIGHT - 0.1f, 0.0f, 0.0f);
                    bullet.localRotation4D = camMatrix;
                    bullet.GetComponent<Missile4D>().damage = 5;
                    bullet.GetComponent<Missile4D>().inheritedVelocity = velocity;
                    }
                    break;
                case DoomWeapon.ROCKETLAUNCHER:
                    Object4D missile = Instantiate(missilePrefab).GetComponent<Object4D>();
                    missile.localPosition4D = position4D + camMatrix * new Vector4(0.0f, 0.0f, 0.1f, 0.0f) + new Vector4(0.0f, CAM_HEIGHT - 0.3f, 0.0f, 0.0f);
                    missile.localRotation4D = camMatrix;
                    missile.GetComponent<Missile4D>().inheritedVelocity = velocity;
                    rocketRecoilAnimTimer = -0.1f;
                    velocity += (camMatrix * new Vector4(0.0f, 0.0f, -ROCKET_RECOIL, 0.0f));
                    break;
            }
        }
        
        
        
        HSPEED = (Matrix4x4.Inverse(camMatrix) * velocity).z;
        
        if (firing) {
            bool shouldUpdate = false;
            subframe += Time.deltaTime;
            
            if (subframe >= weaponSpriteAnimSpeed[weapSelected]) {
                frame++;
                subframe = 0;
                shouldUpdate = true;
            }
            if (frame >= weaponSpriteAnimFrames[weapSelected]) {
                if ((weapSelected == DoomWeapon.FISTS && frame < weaponSpriteAnimFrames[weapSelected] * 2 - 1) || (weapSelected == DoomWeapon.SHOTGUN && frame < weaponSpriteAnimFrames[weapSelected] * 2 - 2)) {
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
        if (weapSelected == DoomWeapon.ROCKETLAUNCHER) {
            rocketRecoilAnimTimer += Time.deltaTime;
            float recoilFactor = rocketRecoilAnimTimer / ROCKET_RECOIL_ANIM_LENGTH;
            if (0.0f <= recoilFactor && recoilFactor < 1.0f) {
                float correctedRecoilFactor = 1 - Mathf.Pow(1 - recoilFactor, RECOIL_POW);
                float recoilDist = 0.0f;
                if (correctedRecoilFactor < 0.5f) {
                    recoilDist = ROCKET_RECOIL_ANIM * Mathf.Sin(correctedRecoilFactor * Mathf.PI);
                } else {
                    recoilDist = ROCKET_RECOIL_ANIM * Mathf.Sin(recoilFactor * Mathf.PI) * Mathf.Sin(correctedRecoilFactor * Mathf.PI);
                }
                gunImage.rectTransform.anchoredPosition += new Vector2(0.0f, -recoilDist);
            }
        }
        int animFrame = frame;
        if (animFrame >= weaponSpriteAnimFrames[weapSelected]) animFrame = (weaponSpriteAnimFrames[weapSelected] * 2 - animFrame - 1);
        setSpriteTo(weaponSpriteNames[weapSelected] + "_" + animFrame);
        gunImage.SetNativeSize();
    }

    public void AddForceFrom(Vector4 origin, float force) {
        Vector4 ds = position4D - origin;
        Vector4 dir = ds.normalized;
        velocity += dir * force / mass;
    }

    public float Damage(float damageValue) {
        if (damageValue < health) {
            health -= damageValue;
            return damageValue;
        } else {
            float dv = health;
            health = 0.0f;
            Die();
            return dv;
        }
    }

    public void Die() {
        Debug.Log("death");
    }
}
