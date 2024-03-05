using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    [Header("Horizontal Movement Settings:")]
    [SerializeField] private float walkSpeed = 1; //Kecepatan player saat jalan
    [Space(5)]

    [Header("Vertical Movement Settings")]
    [SerializeField] private float jumpForce = 45f; //Ketinggian loncat player

    private int jumpBufferCounter = 0; //stores the jump button input
    [SerializeField] private int jumpBufferFrames; //sets the max amount of frames the jump buffer input is stored

    private float coyoteTimeCounter = 0; //stores the Grounded() bool
    [SerializeField] private float coyoteTime; ////sets the max amount of frames the Grounded() bool is stored

    private int airJumpCounter = 0; //keeps track of how many times the player has jumped in the air
    [SerializeField] private int maxAirJumps; //Max loncat

    private float gravity; //Skala gravitasi
    [Space(5)]



    [Header("Ground Check Settings:")]
    [SerializeField] private Transform groundCheckPoint; //point at which ground check happens
    [SerializeField] private float groundCheckY = 0.2f; //how far down from ground chekc point is Grounded() checked
    [SerializeField] private float groundCheckX = 0.5f; //how far horizontally from ground chekc point to the edge of the player is
    [SerializeField] private LayerMask whatIsGround; //layer untuk Ground(daratan)
    [Space(5)]



    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed; //Kecepatan Dash
    [SerializeField] private float dashTime; //Waktu yang dibutuhkan untuk Dash
    [SerializeField] private float dashCooldown; //CD Dash
    [SerializeField] GameObject dashEffect;
    private bool canDash = true, dashed;
    [Space(5)]



    [Header("Attack Settings:")]
    [SerializeField] private Transform SideAttackTransform; //bagian tengah dari area attack bagian samping
    [SerializeField] private Vector2 SideAttackArea; //Seberapa besar area attack kesamping

    [SerializeField] private Transform UpAttackTransform; //bagian tengah dari area attack bagian atas
    [SerializeField] private Vector2 UpAttackArea; //Seberapa besar area attack keatas

    [SerializeField] private Transform DownAttackTransform; //bagian tengah dari area attack bagian bawah
    [SerializeField] private Vector2 DownAttackArea; //Seberapa besar area attack kebawah

    [SerializeField] private LayerMask attackableLayer; //the layer the player can attack and recoil off of

    private float timeBetweenAttack, timeSinceAttck;

    [SerializeField] private float damage; //damage yang diberikan player ke musuh

    [SerializeField] private GameObject slashEffect; //effect dari slashs

    bool restoreTime;
    float restoreTimeSpeed;
    [Space(5)]



    [Header("Recoil Settings:")]
    [SerializeField] private int recoilXSteps = 5; //Berapa banyak FixedUpdates() sang player recoils horizontally butuhkan
    [SerializeField] private int recoilYSteps = 5; //Berapa banyak FixedUpdates() sang player recoils vertically butuhkan

    [SerializeField] private float recoilXSpeed = 100; //Kecepatan dari horizontal recoil
    [SerializeField] private float recoilYSpeed = 100; //Kecepatan dari vertical recoil

    private int stepsXRecoiled, stepsYRecoiled; //the no. of steps recoiled horizontally and verticall
    [Space(5)]

    [Header("Health Settings")]
    public int health;
    public int maxHealth;
    [SerializeField] GameObject bloodSpurt;
    [SerializeField] float hitFlashSpeed;
    public delegate void OnHealthChangedDelegate();
    [HideInInspector] public OnHealthChangedDelegate onHealthChangedCallback;

    float healTimer;
    [SerializeField] float timeToHeal;
    [Space(5)]

    [Header("Mana Settings")]
    [SerializeField] UnityEngine.UI.Image manaStorage;

    [SerializeField] float mana;
    [SerializeField] float manaDrainSpeed;
    [SerializeField] float manaGain;
    [Space(5)]

    [Header("Spell Settings")]
    //spell stats
    [SerializeField] float manaSpellCost = 0.3f;
    [SerializeField] float timeBetweenCast = 0.5f;
    [SerializeField] float spellDamage; //upspellexplosion and downspellfireball
    [SerializeField] float downSpellForce; // desolate dive only
    //spell cast objects
    [SerializeField] GameObject sideSpellFireball;
    [SerializeField] GameObject upSpellExplosion;
    [SerializeField] GameObject downSpellFireball;
    float timeSinceCast;
    float castOrHealTimer;
    [Space(5)]


    [HideInInspector] public PlayerStateList pState;
    private Animator anim;
    private Rigidbody2D rb;
    private SpriteRenderer sr;

    //Input Variables
    private float xAxis, yAxis;
    private bool attack = false;

    private bool canFlash = true;


    public static PlayerController Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        pState = GetComponent<PlayerStateList>();

        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();

        anim = GetComponent<Animator>();

        gravity = rb.gravityScale;

        Mana = mana;
        manaStorage.fillAmount = Mana;

        Health = maxHealth;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(SideAttackTransform.position, SideAttackArea);
        Gizmos.DrawWireCube(UpAttackTransform.position, UpAttackArea);
        Gizmos.DrawWireCube(DownAttackTransform.position, DownAttackArea);
    }

    void Update()
    {
        if (pState.cutscene) return;

        GetInputs();
        UpdateJumpVariables();
        RestoreTimeScale();

        if (pState.dashing || pState.healing) return;
        Flip();
        Move();
        Jump();
        StartDash();
        Attack();
        RestoreTimeScale();
        FlashWhileInvincible();
        Heal();
        CastSpell();
    }
    private void OnTriggerEnter2D(Collider2D _other) //Cast ke bawah dan keatas
    {
        if(_other.GetComponent<Enemy>() != null && pState.casting)
        {
            _other.GetComponent<Enemy>().EnemyHit(spellDamage, (_other.transform.position - transform.position).normalized, -recoilYSpeed);
        }
    }

    private void FixedUpdate()
    {
        if (pState.cutscene) return;

        if (pState.dashing || pState.healing) return;
        Recoil();
    }

    void GetInputs()
    {
        xAxis = Input.GetAxisRaw("Horizontal");
        yAxis = Input.GetAxisRaw("Vertical");
        attack = Input.GetButtonDown("Attack");

        if (Input.GetButton("Cast/Heal"))
        {
            castOrHealTimer += Time.deltaTime;
        }
        else
        {
            castOrHealTimer = 0;
        }
    }

    void Flip()
    {
        if (xAxis < 0)
        {
            transform.localScale = new Vector2(-1, transform.localScale.y);
            pState.lookingRight = false;
        }
        else if (xAxis > 0)
        {
            transform.localScale = new Vector2(1, transform.localScale.y);
            pState.lookingRight = true;
        }
    }

    private void Move()
    {
        rb.velocity = new Vector2(walkSpeed * xAxis, rb.velocity.y);
        anim.SetBool("Walking", rb.velocity.x != 0 && Grounded());
    }

    void StartDash()
    {
        if (Input.GetButtonDown("Dash") && canDash && !dashed)
        {
            StartCoroutine(Dash());
            dashed = true;
        }

        if (Grounded())
        {
            dashed = false;
        }
    }

    IEnumerator Dash()
    {
        canDash = false;
        pState.dashing = true;
        anim.SetTrigger("Dashing");
        rb.gravityScale = 0;
        int _dir = pState.lookingRight ? 1 : -1;
        rb.velocity = new Vector2(_dir * dashSpeed, 0);
        if (Grounded()) Instantiate(dashEffect, transform);
        yield return new WaitForSeconds(dashTime);
        rb.gravityScale = gravity;
        pState.dashing = false;
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    public IEnumerator WalkIntoNewScene(Vector2 _exitDir, float _delay)
    {
        //Jika ganti scene keatas
        if(_exitDir.y != 0)
        {
            rb.velocity = jumpForce * _exitDir;
        }

        //Jika ganti scene butuh horiz movement
        if(_exitDir.x != 0)
        {
            xAxis = _exitDir.x > 0 ? 1 : -1;

            Move();
        }

        Flip();
        yield return new WaitForSeconds(_delay);
        print("cutscene played");
        pState.cutscene = false;
    }

    void Attack()
    {
        timeSinceAttck += Time.deltaTime;
        if (attack && timeSinceAttck >= timeBetweenAttack)
        {
            timeSinceAttck = 0;
            anim.SetTrigger("Attacking");

            if (yAxis == 0 || yAxis < 0 && Grounded())
            {
                Hit(SideAttackTransform, SideAttackArea, ref pState.recoilingX, recoilXSpeed);
                Instantiate(slashEffect, SideAttackTransform);
            }
            else if (yAxis > 0)
            {
                Hit(UpAttackTransform, UpAttackArea, ref pState.recoilingY, recoilYSpeed);
                SlashEffectAtAngle(slashEffect, 80, UpAttackTransform);
            }
            else if (yAxis < 0 && !Grounded())
            {
                Hit(DownAttackTransform, DownAttackArea, ref pState.recoilingY, recoilYSpeed);
                SlashEffectAtAngle(slashEffect, -90, DownAttackTransform);
            }
        }


    }
    void Hit(Transform _attackTransform, Vector2 _attackArea, ref bool _recoilDir, float _recoilStrength)
    {
        Collider2D[] objectsToHit = Physics2D.OverlapBoxAll(_attackTransform.position, _attackArea, 0, attackableLayer);

        if (objectsToHit.Length > 0)
        {
            _recoilDir = true;
        }
        for (int i = 0; i < objectsToHit.Length; i++)
        {
            if (objectsToHit[i].GetComponent<Enemy>() != null)
            {
                objectsToHit[i].GetComponent<Enemy>().EnemyHit
                    (damage, (transform.position - objectsToHit[i].transform.position).normalized, _recoilStrength);

                if (objectsToHit[i].CompareTag("Enemy"))
                {
                    Mana += manaGain;
                }
            }
        }
    }
    void SlashEffectAtAngle(GameObject _slashEffect, int _effectAngle, Transform _attackTransform)
    {
        _slashEffect = Instantiate(_slashEffect, _attackTransform);
        _slashEffect.transform.eulerAngles = new Vector3(0, 0, _effectAngle);
        _slashEffect.transform.localScale = new Vector2(transform.localScale.x, transform.localScale.y);
    }
    void Recoil()
    {
        if (pState.recoilingX)
        {
            if (pState.lookingRight)
            {
                rb.velocity = new Vector2(-recoilXSpeed, 0);
            }
            else
            {
                rb.velocity = new Vector2(recoilXSpeed, 0);
            }
        }

        if (pState.recoilingY)
        {
            rb.gravityScale = 0;
            if (yAxis < 0)
            {
                rb.velocity = new Vector2(rb.velocity.x, recoilYSpeed);
            }
            else
            {
                rb.velocity = new Vector2(rb.velocity.x, -recoilYSpeed);
            }
            airJumpCounter = 0;
        }
        else
        {
            rb.gravityScale = gravity;
        }

        //stop recoil
        if (pState.recoilingX && stepsXRecoiled < recoilXSteps)
        {
            stepsXRecoiled++;
        }
        else
        {
            StopRecoilX();
        }
        if (pState.recoilingY && stepsYRecoiled < recoilYSteps)
        {
            stepsYRecoiled++;
        }
        else
        {
            StopRecoilY();
        }

        if (Grounded())
        {
            StopRecoilY();
        }
    }
    void StopRecoilX()
    {
        stepsXRecoiled = 0;
        pState.recoilingX = false;
    }
    void StopRecoilY()
    {
        stepsYRecoiled = 0;
        pState.recoilingY = false;
    }
    public void TakeDamage(float _damage)
    {
        Health -= Mathf.RoundToInt(_damage);
        StartCoroutine(StopTakingDamage());
    }
    IEnumerator StopTakingDamage()
    {
        pState.invincible = true;
        GameObject _bloodSpurtParticles = Instantiate(bloodSpurt, transform.position, Quaternion.identity);
        Destroy(_bloodSpurtParticles, 1.5f);
        anim.SetTrigger("TakeDamage");
        yield return new WaitForSeconds(1f);
        pState.invincible = false;
    }

    IEnumerator Flash()
    {
        sr.enabled = !sr.enabled;
        canFlash = false;
        yield return new WaitForSeconds(0.1f);
        canFlash = true;
    }

    void FlashWhileInvincible()
    {
        if (pState.invincible)
        {
            if(Time.timeScale > 0.2 && canFlash)
            {
                StartCoroutine(Flash());
            }
        }
        else
        {
            sr.enabled = true;
        }
    }
    void RestoreTimeScale()
    {
        if (restoreTime)
        {
            if (Time.timeScale < 1)
            {
                Time.timeScale += Time.deltaTime * restoreTimeSpeed;
            }
            else
            {
                Time.timeScale = 1;
                restoreTime = false;
            }
        }
    }
    public void HitStopTime(float _newTimeScale, int _restoreSpeed, float _delay)
    {
        restoreTimeSpeed = _restoreSpeed;
        if (_delay > 0)
        {
            StopCoroutine(StartTimeAgain(_delay));
            StartCoroutine(StartTimeAgain(_delay));
        }
        else
        {
            restoreTime = true;
        }
        Time.timeScale = _newTimeScale;
    }
    IEnumerator StartTimeAgain(float _delay)
    {
        restoreTime = true;
        yield return new WaitForSeconds(_delay);
    }
    public int Health
    {
        get { return health; }
        set
        {
            if (health != value)
            {
                health = Mathf.Clamp(value, 0, maxHealth);

                if (onHealthChangedCallback != null)
                {
                    onHealthChangedCallback.Invoke();
                }
            }
        }
    }
    void Heal()
    {
        if (Input.GetButton("Cast/Heal") && castOrHealTimer > 0.05f && Health < maxHealth && Mana > 0 && !pState.jumping && !pState.dashing)
        {
            pState.healing = true;
            anim.SetBool("Healing", true);

            //healing
            healTimer += Time.deltaTime;
            if (healTimer >= timeToHeal)
            {
                Health++;
                healTimer = 0;
            }

            //drain mana
            Mana -= Time.deltaTime * manaDrainSpeed;
        }
        else
        {
            pState.healing = false;
            anim.SetBool("Healing", false);
            healTimer = 0;
        }
    }
    float Mana
    {
        get { return mana; }
        set
        {
            //if mana stats change
            if (mana != value)
            {
                mana = Mathf.Clamp(value, 0, 1);
                manaStorage.fillAmount = Mana;
            }
        }
    }

    void CastSpell()
    {
        if (Input.GetButtonUp("Cast/Heal") && castOrHealTimer <= 0.05f && timeSinceCast >= timeBetweenCast && Mana >= manaSpellCost)
        {
            pState.casting = true;
            timeSinceCast = 0;
            StartCoroutine(CastCoroutine());
        }
        else
        {
            timeSinceCast += Time.deltaTime;
        }

        if(Grounded())
        {
            //Jika di tanah, spell ke bawah tidak bisa dipakai
            downSpellFireball.SetActive(false);
        }
        //Jika spell ke bawah aktif, maka player akan dipaksa kebawah
        if(downSpellFireball.activeInHierarchy)
        {
            rb.velocity += downSpellForce * Vector2.down;
        }
    }
    IEnumerator CastCoroutine()
    {
        anim.SetBool("Casting", true);
        yield return new WaitForSeconds(0.15f);

        //Cast arah samping
        if (yAxis == 0 || (yAxis < 0 && Grounded()))
        {
            GameObject _fireBall = Instantiate(sideSpellFireball, SideAttackTransform.position, Quaternion.identity);

            //Arah Fireball
            if(pState.lookingRight)
            {
                _fireBall.transform.eulerAngles = Vector3.zero; // Jika fireball menghadap kanan, maka akan tetap normal
            }
            else
            {
                _fireBall.transform.eulerAngles = new Vector2(_fireBall.transform.eulerAngles.x, 180); 
                //Jika fireball tidak menghadap ke kanan, maka rubah arah fireball 180 derajat
            }
            pState.recoilingX = true;
        }

        //Cast arah atas
        else if( yAxis > 0)
        {
            Instantiate(upSpellExplosion, transform);
            rb.velocity = Vector2.zero;
        }

        //Cast arah bawah
        else if(yAxis < 0 && !Grounded())
        {
            downSpellFireball.SetActive(true);
        }

        Mana -= manaSpellCost;
        yield return new WaitForSeconds(0.35f);
        anim.SetBool("Casting", false);
        pState.casting = false;
    }

    public bool Grounded()
    {
        if (Physics2D.Raycast(groundCheckPoint.position, Vector2.down, groundCheckY, whatIsGround) 
            || Physics2D.Raycast(groundCheckPoint.position + new Vector3(groundCheckX, 0, 0), Vector2.down, groundCheckY, whatIsGround) 
            || Physics2D.Raycast(groundCheckPoint.position + new Vector3(-groundCheckX, 0, 0), Vector2.down, groundCheckY, whatIsGround))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    void Jump()
    {
        if (jumpBufferCounter > 0 && coyoteTimeCounter > 0 && !pState.jumping)
        {
            rb.velocity = new Vector3(rb.velocity.x, jumpForce);

            pState.jumping = true;
        }
        
        if (!Grounded() && airJumpCounter < maxAirJumps && Input.GetButtonDown("Jump"))
        {
            pState.jumping = true;

            airJumpCounter++;

            rb.velocity = new Vector3(rb.velocity.x, jumpForce);
        }

        if (Input.GetButtonUp("Jump") && rb.velocity.y > 3)
        {
            pState.jumping = false;

            rb.velocity = new Vector2(rb.velocity.x, 0);
        }

        anim.SetBool("Jumping", !Grounded());
    }

    void UpdateJumpVariables()
    {
        if (Grounded())
        {
            pState.jumping = false;
            coyoteTimeCounter = coyoteTime;
            airJumpCounter = 0;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferFrames;
        }
        else
        {
            jumpBufferCounter--;
        }
    }
}
