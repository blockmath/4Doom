using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Missile4D : MonoBehaviour
{
    
    [SerializeField] private float startVelocity = 1.0f;
    [SerializeField] private float acceleration = 0.5f;
    [SerializeField] private float limitVelocity = 10.0f;
    [SerializeField] public float speed = 0.0f;
    [SerializeField] private float lifetime = 10.0f;
    [SerializeField] private float timeActive = 0.0f;
    
    
    [SerializeField] public bool shouldExplode = false;
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
        timeActive += Time.deltaTime;
        if (timeActive >= lifetime) {
            Destroy(gameObject);
        }
        speed = Mathf.Clamp(speed, startVelocity, limitVelocity + inheritedVelocity.magnitude);
        speed += acceleration * Time.deltaTime;
        objectRef.localPosition4D += (inheritedVelocity * Time.deltaTime) + (objectRef._localRotation4D * new Vector4(0.0f, 0.0f, speed * Time.deltaTime, 0.0f));
    }
    
}
