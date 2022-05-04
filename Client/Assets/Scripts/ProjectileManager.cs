using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileManager : MonoBehaviour
{
    public int projectileId;
    public int byPlayer;
    public GameObject explosionPrefab;

    internal void Initialize(int _projectileId,int _byplayer)
    {
        projectileId = _projectileId;
        byPlayer = _byplayer;
    }

    public void Explode(Vector3 _position)
    {
        transform.position = _position;
        Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        GameManager.projectiles.Remove(projectileId);
        Destroy(gameObject);
    }
}
