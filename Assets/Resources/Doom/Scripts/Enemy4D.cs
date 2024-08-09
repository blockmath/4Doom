using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy4D : Object4D
{
    protected bool destroyOnDeath = true;
    [SerializeField] public float healthMax = 1.0f;
    public float health { get; private set; }

    public float mass = 1.0f;

    public Vector4 velocity = Vector4.zero;


    // Damages the enemy. Returns the amount of damage applied.
    public float Damage(float damageValue) {
        Debug.Log("ouch");
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
        if (destroyOnDeath) {
            Destroy(gameObject);
        }
    }

    public void AddForceFrom(Vector4 origin, float force) {
        Vector4 ds = worldPosition4D - origin;
        Vector4 dir = ds.normalized;
        velocity += dir * force / mass;
    }

    // Start is called before the first frame update
    void Start()
    {
        health = healthMax;
    }

    // Update is called once per frame
    void Update()
    {
        localPosition4D += velocity * Time.deltaTime;
    }
}
