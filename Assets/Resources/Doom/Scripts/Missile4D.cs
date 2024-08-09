using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Missile4D : MonoBehaviour
{
    [SerializeField] private float missileSize = 0.5f;
    [SerializeField] private float startVelocity = 1.0f;
    [SerializeField] private float acceleration = 0.5f;
    [SerializeField] private float limitVelocity = 10.0f;
    [SerializeField] public float speed = 0.0f;
    [SerializeField] private float lifetime = 10.0f;
    [SerializeField] private float timeActive = 0.0f;
    
    
    [SerializeField] public bool shouldExplode = false;
    [SerializeField] private float explosionRadius = 5.0f;
    [SerializeField] private float explosionForce = 10.0f;
    [SerializeField] public float damage = 10.0f;
    public Vector4 inheritedVelocity = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
    
    private Object4D objectRef;
    
    
    
    // Start is called before the first frame update
    void Start()
    {
        objectRef = GetComponent<Object4D>();
    }

    // Update is called once per frame
    void Update()
    {
        speed = Mathf.Clamp(speed, startVelocity, limitVelocity + inheritedVelocity.magnitude);
        speed += acceleration * Time.deltaTime;
        objectRef.localPosition4D += (inheritedVelocity * Time.deltaTime) + (objectRef._localRotation4D * new Vector4(0.0f, 0.0f, speed * Time.deltaTime, 0.0f));

        // Collision check
        foreach (KeyValuePair<int, ColliderGroup4D> colKV in Collider4D.colliders) {
            ColliderGroup4D col = colKV.Value;
            Object4D _obj = col.colliders[0].GetComponent<Object4D>();
            Collider4D.Hit? hit = Intersect(_obj, col, missileSize);
            if (hit is not null) {
                if (shouldExplode) {
                    Debug.Log("boom");
                    foreach (KeyValuePair<int, ColliderGroup4D> colKV2 in Collider4D.colliders) {
                        ColliderGroup4D col2 = colKV2.Value;
                        Object4D _obj2 = Object4D.InstanceIDToObject(colKV2.Key).GetComponent<Object4D>();
                        Collider4D.Hit? hit2 = Intersect(_obj2, col2, explosionRadius);
                        if (hit2 is not null) {
                            if (_obj2.GetComponent<Enemy4D>() is not null) {
                                _obj2.GetComponent<Enemy4D>().Damage(damage);
                                _obj2.GetComponent<Enemy4D>().AddForceFrom(objectRef.worldPosition4D, explosionForce);
                            } else if (_obj2.GetComponent<SamplePlayer4D>() is not null) {
                                _obj2.GetComponent<SamplePlayer4D>().Damage(damage);
                                _obj2.GetComponent<SamplePlayer4D>().AddForceFrom(objectRef.worldPosition4D, explosionForce);
                            }
                        }
                    }
                } else {
                    if (_obj.GetComponent<Enemy4D>() is not null) {
                        _obj.GetComponent<Enemy4D>().Damage(damage);
                    } else if (_obj.GetComponent<SamplePlayer4D>() is not null) {
                        _obj.GetComponent<SamplePlayer4D>().Damage(damage);
                    }
                    Debug.Log("hit " + _obj.gameObject.name + " at " + objectRef.worldPosition4D);
                }
                Destroy(gameObject);
                //break;
            } else {

            }
        }
    }

    void LateUpdate() {
        timeActive += Time.deltaTime;
        if (timeActive >= lifetime) {
            Destroy(gameObject);
        }
    }

    public Collider4D.Hit? Intersect(Object4D _obj, ColliderGroup4D colGroup, float size) {
        Vector4 pos = objectRef.localPosition4D;
        
        if (_obj is null) {
            Debug.LogError("Collider does not have associated Object4D");
            return null;
        }
        Transform4D _transf = _obj.WorldTransform4D();
        if (colGroup.IntersectsAABB(_transf, _transf.inverse, pos, size)) {
            foreach (Collider4D col in colGroup.colliders) {
                Collider4D.Hit hit = Collider4D.Hit.Empty;
                if (col.Collide(pos, size, ref hit)) {
                    return hit;
                }
            }
        }
        return null;
    }
    
}
