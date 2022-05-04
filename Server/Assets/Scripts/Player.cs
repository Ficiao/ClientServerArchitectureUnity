using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public int id;
    public string username;
    public CharacterController controller;
    public float gravity = -9.81f;
    public float moveSpeed = 5f;
    public float jumpSpeed = 5f;
    public int itemAmount = 0;
    public int maxItemAmount = 3;
    public float thorwForce = 600f;
    public Transform shootOrigin;
    public float health;
    public float maxHealth = 100f;

    private bool[] inputs;
    private float yVelocity = 0;

    private void Start()
    {
        gravity *= Time.fixedDeltaTime * Time.fixedDeltaTime;
        moveSpeed *= Time.fixedDeltaTime;
        jumpSpeed *= Time.fixedDeltaTime;
    }

    public void Initialize(int _id, string _username, Vector3 _spawnPosition)
    {
        id = _id;
        username = _username;
        health = maxHealth;

        inputs = new bool[5];
    }

    public void ThrowItem(Vector3 _viewDirection)
    {
        if (health <= 0) return;

        if (itemAmount > 0)
        {
            itemAmount--;
            NetworkManager.instance.InstantiateProjectile(shootOrigin).Initialize(_viewDirection, thorwForce, id);
        }
    }

    internal bool AttemptPickupItem()
    {
        if (itemAmount < maxItemAmount)
        {
            itemAmount++;
            return true;
        }
        return false;
    }

    public void FixedUpdate()
    {
        if (health > 0)
        {
            Vector2 _inputDirection = Vector2.zero;
            if (inputs[0])
            {
                _inputDirection.y += 1;
            }
            if (inputs[1])
            {
                _inputDirection.y -= 1;
            }
            if (inputs[2])
            {
                _inputDirection.x -= 1;
            }
            if (inputs[3])
            {
                _inputDirection.x += 1;
            }

            Move(_inputDirection);
        }
    }

    private void Move(Vector2 _inputDirection)
    {
        

        Vector3 _moveDirection = transform.right * _inputDirection.x + transform.forward * _inputDirection.y;
        _moveDirection *= moveSpeed;

        if (controller.isGrounded)
        {
            yVelocity = 0f;
            if (inputs[4])
            {
                yVelocity = jumpSpeed;
            }
        }
        yVelocity += gravity;

        _moveDirection.y = yVelocity;
        controller.Move(_moveDirection);

        /*if (_inputDirection.x != 0 && _inputDirection.y != 0)
        {
            transform.position += _moveDirection * (float)((moveSpeed / Math.Sqrt(2)));
        }
        else
        {
            transform.position += _moveDirection * moveSpeed;
        }*/

        ServerSend.PlayerPosition(this);
        ServerSend.PlayerRotation(this);
    }

    internal void SetInput(bool[] _inputs, Quaternion _rotation)
    {
        inputs = _inputs;
        transform.rotation = _rotation;
    }

    public void Shoot(Vector3 _shootDirection)
    {
        if (health <= 0)
        {
            return;
        }
        if(Physics.Raycast(shootOrigin.position,_shootDirection, out RaycastHit _hit, 25f))
        {
            if (_hit.transform.CompareTag("Player"))
            {
                _hit.transform.GetComponent<Player>().TakeDamage(50f);
            }
            else if (_hit.transform.CompareTag("Enemy"))
            {
                _hit.transform.GetComponent<Enemy>().TakeDamage(50f);
            }
        }
    }

    public void TakeDamage(float damage)
    {
        if (health > 0)
        {
            health -= damage;
            if (health <= 0f)
            {
                health = 0f;
                controller.enabled = false;
                transform.position = new Vector3(0f, 25f, 0f);
                ServerSend.PlayerPosition(this);
                StartCoroutine(Respawn());
            }
        }

        ServerSend.PlayerHealth(this);
    }

    private IEnumerator Respawn()
    {
        yield return new WaitForSeconds(5f);

        health = maxHealth;
        controller.enabled = true;
        ServerSend.PlayerRespawned(this);
    }
}
