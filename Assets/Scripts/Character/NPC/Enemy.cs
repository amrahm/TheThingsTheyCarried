﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{

    public Transform DeathExplosionPrefab;
    private bool exploded = false;

    [System.Serializable]
    public class EnemyStats
    {
        public int maxHealth = 100;

        private int _curHealth;
        public int curHealth
        {
            get { return _curHealth; }
            set { _curHealth = (int)Mathf.Clamp(value, 0f, maxHealth); }
        }

        public void Init()
        {
            curHealth = maxHealth;
        }
    }

    public EnemyStats stats = new EnemyStats();

    [Header("Optional")] // Writes text in Unity editor above the field in inspector
    [SerializeField]
    private StatusIndicator statusIndicator;

    void Start()
    {
        stats.Init();

        if (statusIndicator != null)
        {
            statusIndicator.SetHealth(stats.curHealth, stats.maxHealth);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            // exploded flag prevents multiple damaging collisions to player
            if (!exploded)
            {
                // Enemy self-destructs and causes damage to player
                // Comment out line below if you do not want enemy to self-destruct
                DamageEnemy(100);
                Player player = collision.gameObject.GetComponent<Player>();
                player.DamagePlayer(34);
                exploded = true;
            }
        }
    }

    void Update()
    {
        
    }

    void DeathEffect(Vector3 deathPos)
    {
        // Audio to play on death
        //GetComponent<AudioSource>().Play();

        Transform deathParticle = (Transform)Instantiate(DeathExplosionPrefab, deathPos, Quaternion.identity);
        // Destroy particle system after 1 second
        Destroy(deathParticle.gameObject, 1f);

        // TODO: Shake the camera (need to implement CameraShake script)
        //camShake.Shake(camShakeAmt, camShakeLength);
    }

    public void DamageEnemy(int damage)
    {
        stats.curHealth -= damage;
        if (stats.curHealth <= 0)
        {
            DeathEffect(this.transform.position);
            GameMaster.KillEnemy(this);
        }

        if (statusIndicator != null)
        {
            statusIndicator.SetHealth(stats.curHealth, stats.maxHealth);
        }
        
        //TODO: damage particle effects
    }

    public void HealEnemy(int healing)
    {
        stats.curHealth += healing;

        if (statusIndicator != null)
        {
            statusIndicator.SetHealth(stats.curHealth, stats.maxHealth);
        }
    }
}
