using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Zombie : Enemy
{

    protected override void Start()
    {
        base.Start();
        rb.gravityScale = 12f;
    }

    protected override void Update()
    {
        base.Update();
        if (!isRecoiling)
        {
            transform.position = Vector2.MoveTowards
            (transform.position, new Vector2(PlayerController.Instance.transform.position.x, transform.position.y), speed * Time.deltaTime);
        }
    }

}
