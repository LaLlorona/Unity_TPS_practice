using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class Enemy : LivingEntity
{
    private enum State
    {
        Patrol,
        Tracking,
        AttackBegin,
        Attacking
    }

    private State state;

    private NavMeshAgent agent;
    private Animator animator;

    public Transform attackRoot;
    public Transform eyeTransform;

    private AudioSource audioPlayer;
    public AudioClip hitClip;
    public AudioClip deathClip;

    private Renderer skinRenderer;

    public float runSpeed = 10f;
    [Range(0.01f, 2f)] public float turnSmoothTime = 0.1f;
    private float turnSmoothVelocity;

    public float damage = 30f;
    public float attackRadius = 2f;
    private float attackDistance;

    public float fieldOfView = 50f;
    public float viewDistance = 10f;
    public float patrolSpeed = 3f;

    public LivingEntity targetEntity;
    public LayerMask whatIsTarget;


    private RaycastHit[] hits = new RaycastHit[10];
    private List<LivingEntity> lastAttackedTargets = new List<LivingEntity>();

    private bool hasTarget => targetEntity != null && !targetEntity.dead;


#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        if (attackRoot != null)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawSphere(attackRoot.position, attackRadius);
        }

        if (eyeTransform != null)
        {
            Quaternion leftEyeRotation = Quaternion.AngleAxis(-fieldOfView * 0.5f, Vector3.up);

            Vector3 leftRayDirection = leftEyeRotation * transform.forward;

            Handles.color = new Color(1f, 1f, 1f, 0.2f);
            Handles.DrawSolidArc(eyeTransform.position, Vector3.up, leftRayDirection, fieldOfView, viewDistance);
        }


    }

#endif

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        audioPlayer = GetComponent<AudioSource>();
        skinRenderer = GetComponentInChildren<Renderer>();

        Vector3 attackPivot = attackRoot.position;
        attackPivot.y = transform.position.y;

        attackDistance = Vector3.Distance(transform.position, attackPivot) + attackRadius;

        agent.stoppingDistance = attackDistance;
        agent.speed = patrolSpeed;
    }

    public void Setup(float health, float damage,
        float runSpeed, float patrolSpeed, Color skinColor)
    {
        this.health = health;
        this.startingHealth = health;
        this.damage = damage;
        this.runSpeed = runSpeed;
        this.patrolSpeed = patrolSpeed;
        skinRenderer.material.color = skinColor;

        agent.speed = patrolSpeed;
    }

    private void Start()
    {
        StartCoroutine(UpdatePath());
    }

    private void Update()
    {
        if (dead)
        {
            return;
        }
        if (state == State.Tracking && Vector3.Distance(targetEntity.transform.position, transform.position) <= attackDistance)
        {
            //when the enemy is tracking, and distance between the target and the enemy is less or equal than the attack distance, begin attacking
            BeginAttack();
        }

        animator.SetFloat("Speed", agent.desiredVelocity.magnitude);
    }

    private void FixedUpdate()
    {
        if (dead) return;

        if (state == State.AttackBegin || state == State.Attacking)
        {
            //change direction of the enemy towards the target. 
            var lookRotation = Quaternion.LookRotation(targetEntity.transform.position - transform.position);
            var targetAngleY = lookRotation.eulerAngles.y;

            targetAngleY = Mathf.SmoothDamp(transform.eulerAngles.y, targetAngleY, ref turnSmoothVelocity, turnSmoothTime);
            transform.eulerAngles = Vector3.up * targetAngleY;
        }

        if (state == State.Attacking)
        {
            //when the enemy can actually attack. state will be changed to State.Attacking by EnableAttack(), and this method will be called in the animation 
            Vector3 direction = transform.forward;
            float deltaDistance = agent.velocity.magnitude * Time.deltaTime;

            int size = Physics.SphereCastNonAlloc(attackRoot.position, attackRadius, direction, hits, deltaDistance, whatIsTarget);
            //by SphereCastNonAlloc, update 'private RaycastHit[] hits = new RaycastHit[10]' array. It will save gameObjects that is in the path where the
            //sphere moves 

            for (int i = 0; i < size; i++)
            {
                var attackTargetEntity = hits[i].collider.GetComponent<LivingEntity>();

                if (attackTargetEntity != null & !lastAttackedTargets.Contains(attackTargetEntity))
                {
                    var message = new DamageMessage();
                    message.amount = damage;
                    message.damager = gameObject;

                    if (hits[i].distance <= 0f)
                    {//when the gameObject was in the sphere in the begining of the scan.
                        message.hitPoint = attackRoot.position;
                    }

                    else
                    {
                        message.hitPoint = hits[i].point;
                    }
                    message.hitNormal = hits[i].normal;

                    attackTargetEntity.ApplyDamage(message);
                    lastAttackedTargets.Add(attackTargetEntity);
                    //to avoid multiple attack 
                    break;

                }
            }
        }
    }

    private IEnumerator UpdatePath()
    {
        while (!dead)
        {
            if (hasTarget)
            {
                //when targetEntity is not null, and not dead
                if (state == State.Patrol)
                {
                    //change speed of the enemy to running speed
                    state = State.Tracking;
                    agent.speed = runSpeed;
                }

                //keep track the target 
                agent.SetDestination(targetEntity.transform.position);
            }
            else
            {//when the enemy is in the Patrol mode

                if (targetEntity != null) targetEntity = null;

                if (state != State.Patrol)
                {
                    state = State.Patrol;
                    agent.speed = patrolSpeed;

                }

                if (agent.remainingDistance <= 1f)
                {
                    //in the patrol mode, if the enemy almost reaches the destination, set a new target position on the map
                    var patrolTargetPosition =
                        Utility.GetRandomPointOnNavMesh(transform.position, 20f, NavMesh.AllAreas);
                    agent.SetDestination(patrolTargetPosition);
                }



                Collider[] colliders = Physics.OverlapSphere(eyeTransform.position, viewDistance, whatIsTarget);
                //check every target object in the view Distance. 

                foreach (var collider in colliders)
                {
                    if (!IsTargetOnSight(collider.transform))
                    {
                        //check whether there are blocking objects between the enemy and the target object
                        continue;
                    }
                    var livingEntity = collider.GetComponent<LivingEntity>();
                    if (livingEntity != null && !livingEntity.dead) //when the target object has 'LivingEntity' and not dead, begin tracking.
                    {
                        targetEntity = livingEntity;
                        break;
                    }
                }


            }

            yield return new WaitForSeconds(0.2f);
        }
    }

    public override bool ApplyDamage(DamageMessage damageMessage)
    {
        if (!base.ApplyDamage(damageMessage)) return false;

        if (targetEntity == null)
        {
            targetEntity = damageMessage.damager.GetComponent<LivingEntity>();
        }

        EffectManager.Instance.PlayHitEffect(damageMessage.hitPoint, damageMessage.hitNormal, transform, EffectManager.EffectType.Flesh);

        audioPlayer.PlayOneShot(hitClip);

        return true;
    }

    public void BeginAttack()
    {
        state = State.AttackBegin;

        agent.isStopped = true;
        animator.SetTrigger("Attack");
    }

    public void EnableAttack()
    {
        state = State.Attacking;

        lastAttackedTargets.Clear();
    }

    public void DisableAttack()
    {
        if (hasTarget)
        {
            state = State.Tracking;
        }
        else
        {
            state = State.Patrol;
        }


        agent.isStopped = false;
    }

    private bool IsTargetOnSight(Transform target)
    {


        Vector3 direction = target.position - eyeTransform.position;
        direction.y = eyeTransform.forward.y;

        if (Vector3.Angle(direction, eyeTransform.forward) > fieldOfView * 0.5f)
        {
            return false;
        }

        direction = target.position - eyeTransform.position;

        RaycastHit hit;
        if (Physics.Raycast(eyeTransform.position, direction, out hit, viewDistance, whatIsTarget))
        {
            if (hit.transform == target)
            {
                return true;
            }
        }
        return false;
    }

    public override void Die()
    {
        base.Die();
        GetComponent<Collider>().enabled = false;

        agent.enabled = false;
        animator.applyRootMotion = true;
        animator.SetTrigger("Die");

        audioPlayer.PlayOneShot(deathClip);
    }
}