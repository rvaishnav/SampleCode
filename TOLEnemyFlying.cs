using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TOLEnemyFlying : TOLEnemyMovement
{
	[System.Serializable]
	public class FlyingEnemyAttackVariable{
		//Time for aiming
		public float aimingTime;
		//Speed for Aiming
		public float aimingSpeed;
		//Speed for Attack
		public float attackSpeed;
		//Speed for Hoover
		public float hooverSpeed;
		//Time for Hoover
		public float hooverTime;
		//Time that force enemy to move to next stage even if it doesn't reach the destination point 
		public float bailOutTime;
	}

    public enum FlyingMotionTypes { Sine, Circular, Circle,StraightLine , Swarm, None};

    [System.Serializable]
    public class BoidData
    {
        public float m_coherence = 1;           // velocity co-efficient towards the flock center
        public float m_seperation = 5;          // velocity co-efficient away from other Boid (swarming flock member of this group) 
        public float m_alignment = 1;           // velocity co-efficient along the flocks avg velocity
        public float m_threshold = 5;           // distance from group leader
        public float m_followLeader = 2;        // velocity co-efficient towards the leader
        public int m_groupId = -1;              // group id of this boid member
        public bool m_isGroupLeader = false;            // true if this is the group leader    
        public bool isSwarmingEnemy;            // true if this is flock follower
        [HideInInspector]
        public bool setUpBoid;                  // true if boid initial setup done   
        public float randomness = 100;          // random velocity component in flock member for realistic swarm
		public float maxBoidRadius = 5.0f;		// Radius that the flock will maintain
    }

    #region Public Variables

    public FlyingMotionTypes m_currentFlyingMotion = FlyingMotionTypes.Sine;

    public bool followSemiCircularPath;
	
	public FlyingEnemyAttackVariable flyingVariable = new FlyingEnemyAttackVariable();

    public BoidData BoidInfo = new BoidData();
    [HideInInspector]
    // reference to group leader of this flock
    public TOLEnemyFlying GroupLeader;
    #endregion

    #region Private Variables
    //previous and current patrol points
    protected     Transform           m_startPatrolPoint;
    protected     Transform           m_endPatrolPoint;

    //starting and destination position for the specified motion
    protected     Vector3             m_srcPosition;
    protected     Vector3             m_destPosition;

    //vel for the enemy for current frame
    protected     Vector3             m_Vel                   =   Vector3.zero;

    protected float amplitudeY = 1.0f;

    // patrol direction
    protected     int                 m_patrolDir;

    //projected distance on the straight line to destination
    protected     float               m_projectedDistance;

    // radius for circular or spiral motion
    protected     float               m_circlingRadius        =   5.0f;
    
    // how fast do we wanna reach the destination
    protected     float               m_actualVelocity       =   1.0f;
    
    // Flying motion before switching to straight line pursuit
    protected     FlyingMotionTypes   m_lastFlyingMotion    = FlyingMotionTypes.None;
    
    //min and max velocity if this is a flock member
    public  float     minVelocity     = 5;
    public  float     maxVelocity     = 20;
    protected  Vector3   flockCenter;
    protected  Vector3   flockVelocity;
    
    //list of boids in this group ,used only by group leader
    protected List<TOLEnemyFlying> flyingBoids = new List<TOLEnemyFlying>();


	//Flag for collision detection
	protected bool 				m_colActive	= false;

    protected bool              m_colAvoidance = false;

    //simple collision avoidance for boid
    protected bool flyParallel;

    protected int frameLastCollided = 0;

    protected int maxFrameToWait = 5;

    protected Vector3 contactNormal = Vector3.zero;

    protected Transform chaseTriggerTarget = null;

    protected bool moveToNewTarget;
    protected Transform targetBeforeStuck;
    #endregion

    #region Unity Overrides
    protected override void Start()
    {
        base.Start();
        m_currentSpeed = 1;
        m_lastFlyingMotion = m_currentFlyingMotion;
    }

	//-------------------------------------------------------------------------

	protected override void Update ()
	{
		base.Update ();
		
		if (game.IsPaused())
		{
			return;
		}

		//Check if you have been winded
		if(m_windAffected)
		{
			//Update wind timer
			m_windTimer += Time.deltaTime;
			
			if(m_windTimer >= windAffectedTime)
			{
				//Reset wind flag
				m_windAffected = false;

				//Turn off collision detection
				m_colActive = false;

				StartRotation();
			}
		}
        //if set up is done and this is the leader then update flock data
        if (BoidInfo.setUpBoid)
        {
            if (BoidInfo.m_isGroupLeader)
            {
                UpdateFlockData();
            }
        }
        //If we are a swarming enemy, we will teleport if we are offscreen.
        if (BoidInfo.isSwarmingEnemy)
        {
			if(m_dest != null)
			{
	            TOLEnemyController m_enemyController = ((TOLEnemyController)m_owner);
	            float sqDistanceToPlayer = Vector3.SqrMagnitude(this.transform.position - m_dest.position);
	            if (sqDistanceToPlayer > TOLCommon.MAX_SWARM_DISTANCE)
	            {
	                // Too far away, so teleport.
	                TOLEnemySwarmPoint[] swarmPoints = m_enemyController.swarmPoints;
	                if (swarmPoints != null && swarmPoints.Length > 0)
	                {
		                int swarmPointIndex = m_enemyController.m_currentSwarmPoint;
						while(swarmPoints[swarmPointIndex].IsVisible(m_dest.position) && swarmPointIndex > 0)
		                {
		                    swarmPointIndex--;
		                }
		                TeleportEnemy(swarmPoints[swarmPointIndex].transform.position);
	                }
	            }
			}
			if(m_dest != null)
			Debug.DrawLine(transform.position,m_dest.position,Color.green,0.001f);
        }

	}

	//-------------------------------------------------------------------------
	
	void OnCollisionEnter(Collision hit)
	{
		if(m_colActive)
		{
			if(hit.collider.GetComponent<TOLTerrain>() != null)
			{
				//Check for collision with ground terrain
				if(Vector3.Dot(Vector3.up, hit.contacts[0].normal) > .5f)
				{
					//Enemy has collided with terrain, transition to downed state
					m_owner.StateTransition(game.states.GetState(eStateName.ENEMY_DOWNED));

					//Reset Flags
					m_colActive = false;
					m_windAffected = false;

					//Turn gravity on
					rigidbody.useGravity = true;
                    return;
				}
			}
		}

        if (BoidInfo.isSwarmingEnemy && 
            ((TOLEnemyController)m_owner).GetCurrentState() != game.states.GetState(eStateName.ENEMY_DOWNED))
	    {

	        /*if (hit.collider.GetComponent<TOLTerrain>() != null)
	        {
	            contactNormal = hit.contacts[0].normal;
              //Debug.DrawRay(transform.position, contactNormal, Color.red, 0.01f);
	            frameLastCollided = maxFrameToWait;
	            if (Vector3.Angle(contactNormal, (GroupLeader.flockCenter - transform.position)) > 90)
	                flyParallel = true;
                
                / *if (!moveToNewTarget)
                {
                    CheckIfStuck();
                }* /
	        }*/
	    }
        
        //avoiding sync hazards for swarming enemy
        if (BoidInfo.isSwarmingEnemy)
        {
            if (hit.collider.CompareTag("FlyingEnemyObstacle"))
            {
                contactNormal = hit.contacts[0].normal;
                frameLastCollided = maxFrameToWait;
                if (Vector3.Angle(contactNormal, (GroupLeader.flockCenter - transform.position)) > 90)
                    flyParallel = true;
            }
        }
	}
    #endregion

    #region Internal Functions

    /// <summary>
    /// maps circular path between 2 points using the separation
    /// as the diameter
    /// </summary>
    /// <param name="startPoint"></param>
    /// <param name="endPoint"></param>
    /// <returns></returns>
    protected Vector3 CircularMotion(Vector3 startPoint, Vector3 endPoint)
    {
        Vector3 center;
        float radius = Vector3.Distance(endPoint, startPoint) / 2;
        center.x = (endPoint.x + startPoint.x) / 2;
        center.y = (endPoint.y + startPoint.y) / 2;
        center.z = (endPoint.z + startPoint.z) / 2;
        Vector3 tangentVector = Vector3.Cross(center - transform.position, Vector3.forward).normalized;
        Vector3 centri = center - transform.position;
        centri.Normalize();
        centri = centri * ((tangentVector.magnitude * tangentVector.magnitude) / radius);
        centri *= Time.deltaTime;
        return (tangentVector + centri)*m_actualVelocity;
    }

    // -----------------------------------------------------------------------

    protected Vector3 Circle(Vector3 center)
    {
        float radius = Vector3.Distance(center, transform.position);
        Vector3 tangentVector = Vector3.Cross(center - transform.position, Vector3.forward).normalized;
        Vector3 centerVel = center - transform.position;
        centerVel.Normalize();
        centerVel = centerVel * ((tangentVector.magnitude * tangentVector.magnitude) / radius);
        centerVel *= Time.deltaTime;
        return tangentVector + centerVel;
    }

    // -----------------------------------------------------------------------

    /// <summary>
    /// maps a sine wave between given 2 points
    /// projected distance is theta
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    protected Vector3 SineWave(Vector3 start, Vector3 end)
    {
        int noofcycles = 2;
        Vector3 dir = end - start;
        Vector3 vel;
        float projectedDistance = Vector3.Magnitude(Vector3.Project(transform.position - start, dir.normalized));
        int maxx = (int)Vector3.Distance(start, end) / noofcycles;
        float newPosY = amplitudeY * Mathf.Cos((2 * Mathf.PI * projectedDistance) / maxx);
        vel = new Vector3(0, newPosY, m_actualVelocity);
        m_projectedDistance = projectedDistance;
        return vel;
    }

    // -----------------------------------------------------------------------

    /// <summary>
    /// maps a sine wave with very low amplitude
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    protected Vector3 StraightLine(Vector3 start, Vector3 end)
    {
        int noofcycles = 2;
        Vector3 dir = end - start;
        Vector3 vel;
        float projectedDistance = Vector3.Magnitude(Vector3.Project(transform.position - start, dir.normalized));
        int maxx = (int)Vector3.Distance(start, end) / noofcycles;
        float newPosY = 0.1f * Mathf.Cos((2 * Mathf.PI * projectedDistance) / maxx);
        vel = new Vector3(0, newPosY, m_actualVelocity);
        m_projectedDistance = projectedDistance;
        return vel;
    }

    //-------------------------------------------------------------------------

    /// <summary>
    /// initial Boid setup
    /// </summary>
    protected void DoBoidSetup()
    {

        TOLEnemyFlying[] AllFlyingEnemies = GameObject.FindObjectsOfType<TOLEnemyFlying>();
        foreach (TOLEnemyFlying enemy in AllFlyingEnemies)
        {
            if (enemy.BoidInfo.m_groupId == BoidInfo.m_groupId)
            {
                if (!enemy.BoidInfo.m_isGroupLeader)
                {
                    flyingBoids.Add(enemy);
                    enemy.BoidInfo.isSwarmingEnemy = true;
                }
                else
                    GroupLeader = enemy;
            }
        }
        if (BoidInfo.m_isGroupLeader)
        {
            GroupLeader = this;
            //set destination for each boid as group leader
            foreach (TOLEnemyFlying enemy in flyingBoids)
            {
                enemy.SetDestination(GroupLeader.transform);
            }
        }

    }

    //-------------------------------------------------------------------------

    /// <summary>
    /// updates flocks average position and velocity 
    /// only group leader should do this, he can share it with all
    /// </summary>
    protected void UpdateFlockData()
    {
        //compute current flock center and flock velocity
        Vector3 center = Vector3.zero;
        Vector3 velocity = Vector3.zero;
        foreach (TOLEnemyFlying boid in flyingBoids)
        {
            center += boid.transform.position;
            velocity += boid.rigidbody.velocity;
        }
        flockCenter = center / flyingBoids.Count;
        flockVelocity = velocity / flyingBoids.Count;
    }

    //-------------------------------------------------------------------------

    /// <summary>
    /// computes velocity component for separation of Boid from each other
    /// </summary>
    /// <returns> returns velocity component</returns>
    protected Vector3 BoidSeparation()
    {
        float desiredseparation = 2;
        Vector3 steer = new Vector3(0, 0, 0);
        int count = 0;
        // For every boid in the system, check if it's too close
        foreach (TOLEnemyFlying boid in flyingBoids)
        {
            float d = Vector3.Distance(transform.position, boid.transform.position);

            // If the distance is greater than 0 and less than an arbitrary amount (0 when you are yourself)
            if ((d > 0) && (d < desiredseparation))
            {
                // Calculate vector pointing away from neighbor
                Vector3 diff = transform.position - boid.transform.position;
                diff.Normalize();
                diff /= d;        // Weight by distance
                steer += diff;
                count++;            // Keep track of how many
            }
        }
        // Average -- divide by how many
        if (count > 0)
        {
            steer /= ((float)count);
        }

        // As long as the vector is greater than 0
        if (steer.magnitude > 0)
        {
            // Implement Reynolds: Steering = Desired - Velocity
            steer.Normalize();
            steer *= maxVelocity;
            //steer -= rigidbody.velocity;
            //steer.limit(maxforce);
            steer = Vector3.ClampMagnitude(steer, maxVelocity);
        }
        return steer;
    }

    //-------------------------------------------------------------------------

    /// <summary>
    /// computes velocity component for separation of Boid from Leader
    /// </summary>
    /// <returns> returns velocity component</returns>
    protected Vector3 BoidSeparationFromLeader()
    {
        Vector3 toLeader = transform.position - GroupLeader.transform.position;
        float distanced = toLeader.magnitude;
        toLeader.Normalize();
        toLeader /= distanced;
        toLeader *= maxVelocity;
        return toLeader;
    }

    //-------------------------------------------------------------------------

    /// <summary>
    /// updates the velocity of this Boid using all the Boid components
    /// and clamp it
    /// </summary>
    protected void UpdateBoidVelocity()
    {
        Vector3 awayFromLeader = transform.position - GroupLeader.transform.position;
        float distanced = awayFromLeader.magnitude;
        if (distanced < GroupLeader.BoidInfo.m_threshold)
        {
            rigidbody.velocity += BoidSeparationFromLeader();
        }

        rigidbody.velocity += BoidSteering() * Time.deltaTime;
        float speed = rigidbody.velocity.magnitude;
        if (speed > maxVelocity)
        {
            rigidbody.velocity = rigidbody.velocity.normalized * maxVelocity;
        }
        else if (speed < minVelocity)
        {
            rigidbody.velocity = rigidbody.velocity.normalized * minVelocity;
        }
        return;
    }

    //-------------------------------------------------------------------------

    /// <summary>
    /// scale Boid component according to each Boid Coefficient
    /// </summary>
    /// <returns>Velocity vector</returns>
    public Vector3 BoidSteering()
    {
        Vector3 randomize = new Vector3((Random.value * 2) - 1, (Random.value * 2) - 1, (Random.value * 2) - 1);
        randomize.Normalize();
        randomize *= GroupLeader.BoidInfo.randomness;
        Vector3 centerVel = GroupLeader.flockCenter - transform.position;
        Vector3 alignVelocity = GroupLeader.flockVelocity - rigidbody.velocity;
        Vector3 followVel = m_dest.position - transform.position;
        if (centerVel.sqrMagnitude > followVel.sqrMagnitude)
        {
            centerVel.Normalize();
            followVel.Normalize();
        }
        if(m_colAvoidance || moveToNewTarget)
        {
            return (followVel * GroupLeader.BoidInfo.m_followLeader);
        }

        else
        {
            return (centerVel * GroupLeader.BoidInfo.m_coherence + BoidSeparation() * GroupLeader.BoidInfo.m_seperation + alignVelocity * GroupLeader.BoidInfo.m_alignment +
                followVel * GroupLeader.BoidInfo.m_followLeader + randomize);
        }
    }

    //-------------------------------------------------------------------------

    /// <summary>
    /// simple collision avoidance for Boid 
    /// </summary>
    /// <returns></returns>
    public Vector3 BoidsCollisionCompensation()
    {
        if (flyParallel)
        {
            Vector3 newVel = Vector3.zero;
            newVel = Vector3.Cross(Vector3.forward, contactNormal);
            Vector3 along = Vector3.Project(m_dest.position - transform.position, newVel.normalized);
            along.Normalize();
            along *= maxVelocity;
            frameLastCollided--;
            //Debug.DrawRay(transform.position, (along + contactNormal).normalized*3, Color.black, 0.01f);
            return (along + contactNormal);
        }
        else
        {
            Vector3 followVel = GroupLeader.flockCenter - transform.position;
            followVel.Normalize();
            followVel *= maxVelocity/2;
            frameLastCollided = 0;
            //Debug.DrawRay(transform.position, followVel, Color.red, 0.01f);
            return (followVel);
        }
    }

    //-------------------------------------------------------------------------

    /// <summary>
    /// Flock member facing leader's dir
    /// </summary>
    public void FacetheLeader()
    {
        // this one for facing the leader
        /*float angleDiff = Vector3.Angle(transform.forward, (GroupLeader.transform.forward));
        if (angleDiff > 1)
        {
            Vector3 lookVector = GroupLeader.transform.forward;
            Quaternion targetRotation = Quaternion.LookRotation(lookVector);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.time * 0.01f);
        }*/
        // this one to face towards the dest
        if(m_dest!=null )
        {
            Vector3 lookVector = m_dest.position - transform.position;
            if (lookVector.sqrMagnitude < 1)
            {
                lookVector.Normalize();
            }
            
            float angleDiff = Vector3.Angle(transform.forward, (m_dest.position - transform.position));
            if (angleDiff > 1)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookVector);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.time * 0.01f);
            }
        }
    }

    //-------------------------------------------------------------------------
    
    /// <summary>
    /// if stuck then move to new destination
    /// </summary>
    public void CheckIfStuck()
    {
        float length;
        int layerMask = 1 << 9;
        Vector3 startPos,dir;
        RaycastHit hit;
        startPos = transform.position;
        dir = (m_dest.position - startPos);
        length = dir.magnitude;
        dir.Normalize();
        if (Physics.Raycast(startPos, dir, out hit, length, layerMask))
        {
            moveToNewTarget = true;
            targetBeforeStuck = m_dest;
            SetDestination(((TOLEnemyController)m_owner).GetCurrentSwarmPoint());
        }
        
    }

    //-------------------------------------------------------------------------

    /// <summary>
    /// check if reached new target point
    /// can resume swarming after reaching
    /// </summary>
    /// <returns></returns>
    public bool ReachedNewTarget()
    {
        if (Vector3.Distance(m_dest.position, transform.position) < 1)
        {
            return true;
        }
        return false;
    }

    //-------------------------------------------------------------------------

    /// <summary>
    /// reached to new target
    /// resume swarming
    /// </summary>
    public void ResumeSwarming()
    {
        moveToNewTarget = false;
        SetDestination(targetBeforeStuck);
    }

    #endregion

    #region Public Interface

    public override void UpdateMovement()
    {
		if(!m_init)
		{
			return;
		}

        if (m_owner.IsLocal() || m_owner.IsMine())
        {
            Vector3 Step = Vector3.zero;

            switch (m_currentFlyingMotion)
            {
                case FlyingMotionTypes.Sine:
                    Step = SineWave(m_srcPosition, m_destPosition);
                    m_Vel = transform.forward * Step.z + transform.up * Step.y;
                    break;

                case FlyingMotionTypes.Circular:
                    /*if (m_endPatrolPoint == null)
                    {
                        return;
                    }*/
                    Step = CircularMotion(m_srcPosition, m_destPosition);
                    if (followSemiCircularPath)
                        m_Vel = Step * 2 * m_patrolDir;
                    else
                        m_Vel = Step * 2;
                    break;
                case FlyingMotionTypes.Circle:
                    if (m_endPatrolPoint == null)
                    {
                        return;
                    }
                    m_Vel = Circle(m_endPatrolPoint.position);
                    break;
                case FlyingMotionTypes.StraightLine:
                    if (m_startPatrolPoint == null)
                    {
                        m_startPatrolPoint = transform;
                    }
                    Step = StraightLine(m_startPatrolPoint.position, m_endPatrolPoint.position);
                    m_Vel = transform.forward * Step.z + transform.up * Step.y;
                    break;
                    //
                case FlyingMotionTypes.Swarm:
                    if (!BoidInfo.setUpBoid)
                    {
                        //DoBoidSetup();
                        //BoidInfo.setUpBoid = true;
                        return;
                    }
                    else
                    {
                        if (GroupLeader != null)
                        {
                            if (!BoidInfo.m_isGroupLeader)
                            {
                                
                                if (frameLastCollided > 0)
                                {
                                    rigidbody.velocity = BoidsCollisionCompensation();
                                }
                                else
                                {
                                    UpdateBoidVelocity();
                                    FacetheLeader();
                                    flyParallel = false;
                                }

                                /*if (moveToNewTarget)
                                {
                                    if (ReachedNewTarget())
                                    {
                                        ResumeSwarming();
                                    }
                                }*/

                            }
                        }
                    }
                    break;
            }
                
            if(m_currentFlyingMotion != FlyingMotionTypes.Swarm)
            {
                if (!float.IsNaN(m_Vel.x) && !float.IsNaN(m_Vel.y) && !float.IsNaN(m_Vel.z))
                {
                    rigidbody.velocity = m_Vel * m_currentSpeed;
                }
                if (m_currentSpeed < 1)
                {
                    rigidbody.velocity = Vector3.zero;
                }
            }

        }
    }

	// -----------------------------------------------------------------------

	public override void UpdateWind ()
	{
		if(m_windAffected)
		{
			//Apply wind to velocity
			rigidbody.velocity += (m_windVect * windScalePercentage);

			float speed = rigidbody.velocity.magnitude;
			if (speed > maxVelocity)
			{
				rigidbody.velocity = rigidbody.velocity.normalized * maxVelocity;
			}
		}
	}

    // -----------------------------------------------------------------------

    public override void StopMovement()
    {
        m_currentSpeed = 0.0f;
        UpdateMovement();
    }

    // -----------------------------------------------------------------------

    public override void ResumeMovement()
    {
        m_currentSpeed = 1;
    }

    // -----------------------------------------------------------------------

    public override void SetDestination(Transform dest)
    {
       	if (m_owner.IsLocal() || m_owner.IsMine())
        {
            if (m_dest == dest)
            {
                return;
            }
            m_init = true;
            m_dest = dest;
            m_endPatrolPoint = dest;
            m_startPatrolPoint = ((TOLEnemyController)m_owner).GetPrevPatrolPoint();
            if (m_startPatrolPoint == null)
            {
                m_srcPosition  = transform.position;
            }
            else
            {
                m_srcPosition = m_startPatrolPoint.position;
            }
            m_destPosition = dest.position;
            m_projectedDistance = 0;
            m_patrolDir = ((TOLEnemyController)m_owner).GetPatrolDirection();
        }
    }

    //-------------------------------------------------------------------------

    public override void SetSpeed(TOLEnemyStateGeneric state)
    {
        if (m_init)
        {
            // m_current speed is merely used for stop movement or resume (0/1) 
            //modify m_actualVelocity for differences
            if (state is TOLEnemyStatePatrol 
			    || state is TOLEnemyStatePatrolReturn
			    || state is  TOLEnemyStateSwarmLead)
            {
                m_currentSpeed = 1;
                m_currentFlyingMotion = m_lastFlyingMotion;
                m_actualVelocity = patrolSpeed;
            }
			else if(state is TOLEnemyStateFlyingPatrol)
			{
				m_currentSpeed = 1;
				m_currentFlyingMotion = m_lastFlyingMotion;
				m_actualVelocity = patrolSpeed;
			}
            else if (state is TOLEnemyStatePursuit)
            {
                m_currentSpeed = 1;
                m_currentFlyingMotion = FlyingMotionTypes.StraightLine;
				m_actualVelocity = pursuitSpeed;
			}else if(state is TOLEnemyStateFlyingPursuit)
			{
				m_currentSpeed = 1;
				m_currentFlyingMotion = FlyingMotionTypes.StraightLine;
				m_actualVelocity = pursuitSpeed;
			}
            else if (state is TOLEnemyStateSearch)
            {
                m_currentSpeed = 1;
				m_actualVelocity = searchSpeed;
            }
			else if (state is TOLEnemyStateFlyingSearch)
			{
				m_currentFlyingMotion = FlyingMotionTypes.StraightLine;
				m_currentSpeed = 1;
				m_actualVelocity = 2;
			}
			else if(state is TOLEnemyStateFlyingAttack)
			{
				if(((TOLEnemyController)m_owner).GetFlyingEnemyAttackState() == TOLEnemyController.FlyingEnemyAttackState.Aim)
				{
					m_currentSpeed = 3;
					m_actualVelocity = 3;
				}
				else if (((TOLEnemyController)m_owner).GetFlyingEnemyAttackState() == TOLEnemyController.FlyingEnemyAttackState.Hoover)
				{
					m_currentSpeed = 5;
					m_actualVelocity = 5;
				}
				else
				{
					m_currentSpeed = 1;
					m_actualVelocity = 1;
				}
			}
			else if(state is TOLEnemyStateFlyingSearch)
			{
				m_currentSpeed = 1;
				m_actualVelocity = searchSpeed;
			}
        }
    }

    //-------------------------------------------------------------------------

    public override void SlowEnemy()
    {
        //Set current speed to slow speed
        m_currentSpeed = 1;
        m_actualVelocity = slowSpeed;
    }

    //-------------------------------------------------------------------------

    public override Vector3 GetVelocity()
    {

        //Return enemies current velocity
        if (m_init)
        {
            return m_Vel;
        }
        else
        {
            return Vector3.zero;
        }
    }

    //-------------------------------------------------------------------------

    public override void StartRotation(bool turnOffViewfield = false)
    {
		if(m_dest == null)
		{
			return; 
		}

        if (m_needsRot)
        {
            return;
        }
        if (m_startPatrolPoint == null)
        {
            m_startPatrolPoint = transform;
        }
        float angleDiff = Vector3.Angle(transform.forward, (m_dest.position - transform.position));
        if (angleDiff > 0.1f)
        {
            m_needsRot = true;
        }
    }

    //-------------------------------------------------------------------------

    public override void UpdateRotation()
    {
        //Check if enemy needs rotation
        if (!m_init || !m_needsRot)
        {
            return;
        }

        if (m_owner.IsLocal() || m_owner.IsMine())
        {
            if (m_needsRot)
            {
                Vector3 lookVector = m_dest.position - transform.position;
                Quaternion targetRotation = Quaternion.LookRotation(lookVector);
                float angleCoeff = Quaternion.Angle(transform.rotation, targetRotation)/10;
                angleCoeff = Mathf.Clamp(angleCoeff, 1.0f, 3.0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.time * 0.002f*angleCoeff);
                if (Quaternion.Angle(transform.rotation, targetRotation) < 0.01f)
                {
                    m_needsRot = false;
                }
            }
        }
    }

    //-------------------------------------------------------------------------

    public override void PatrolReturn(Transform patrolPt)
    {
        if (m_owner.IsLocal() || m_owner.IsMine())
        {
            m_init = true;
            m_srcPosition = transform.position;
            if (BoidInfo.isSwarmingEnemy)
            {
                m_currentFlyingMotion = FlyingMotionTypes.Swarm;
            }
            if (chaseTriggerTarget != null)
            {
                m_dest = chaseTriggerTarget;
                m_destPosition = chaseTriggerTarget.position;
            }
            else
            {
                m_dest = patrolPt;
                m_destPosition = patrolPt.position;
            }
        } 
    }

    //-------------------------------------------------------------------------

	public override void ApplyWind (Vector3 windVect)
	{
		//Set wind vector
		base.ApplyWind (windVect);

		//Start allowing collisions to be detected
		m_colActive = true;
	}

    //-------------------------------------------------------------------------

    /// <summary>
    /// for removing randomness during collision avoidance
    /// </summary>
    /// <param name="isCollisionAvoidance"></param>
    public void AvoidObstacle(bool isCollisionAvoidance)
    {
        m_colAvoidance = isCollisionAvoidance;
    }
    
    //-------------------------------------------------------------------------

	public void FacetheTarget(Transform target)
	{
		float angleDiff = Vector3.Angle(transform.forward, (target.position - transform.position));
		if(angleDiff > 1)
		{
			Vector3 lookVector = target.position - transform.position;
			Quaternion targetRotation = Quaternion.LookRotation(lookVector);
			transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.time * 0.01f);
		}
	}

    //-------------------------------------------------------------------------

	
	public void SetAttackTarget(Transform target, int attackStage)
	{
		float flyingSpeed;
		switch(attackStage){
			case 1:
				flyingSpeed = flyingVariable.aimingSpeed;
				break;
			case 2:
				flyingSpeed = flyingVariable.attackSpeed;
				break;
			case 3:
				flyingSpeed = flyingVariable.hooverSpeed;
				break;
			default:
				flyingSpeed = 2.0f;
				break;
		}
		m_currentSpeed = 1.0f;
		FacetheTarget(target);
		//Calculate the Normalized vector from enemy to target
		Vector3 moveForwardVector =  target.position - gameObject.transform.position;
		moveForwardVector.Normalize();
		moveForwardVector *= flyingSpeed;
		gameObject.rigidbody.velocity = moveForwardVector;
	}

    //-------------------------------------------------------------------------

	public bool isHitTarger()
	{
		if(Vector3.Distance(m_dest.position, gameObject.transform.position) <= 1){
			return true;
		}
		else{
			return false;
		}
	}

    //-------------------------------------------------------------------------

	public void SetMovingWay(FlyingMotionTypes motionType){
		m_currentFlyingMotion = motionType;
	}

    //-------------------------------------------------------------------------

	public FlyingMotionTypes GetCurrentMotion()
	{
		return m_currentFlyingMotion;
	}

    //-------------------------------------------------------------------------
    
    /// <summary>
    ///removes the dead flying enemy from the flying boids list
    ///since all share reference its removed from all
    /// </summary>
    public void chooseNewLeader()
    {
        if (BoidInfo.m_isGroupLeader)
        {
            // NO NEED SINCE LEADER IS NOT KILLED ANYMORE 
            /*BoidInfo.m_groupId = -1;
            if (flyingBoids.Count != 0)
            {
                TOLEnemyFlying newLeader = flyingBoids[0];
                newLeader.m_currentFlyingMotion = m_currentFlyingMotion;
                newLeader.m_lastFlyingMotion = m_lastFlyingMotion;
                newLeader.BoidInfo.m_isGroupLeader = true;
                newLeader.GroupLeader = null;
                newLeader.BoidInfo.isSwarmingEnemy = false;

                ((TOLEnemyController)newLeader.m_owner).patrolPoints =
                    new Transform[((TOLEnemyController)m_owner).patrolPoints.Length];

                ((TOLEnemyController)newLeader.m_owner).patrolPoints =
                    ((TOLEnemyController)m_owner).patrolPoints;
                ((TOLEnemyController)newLeader.m_owner).patrolType = ((TOLEnemyController)m_owner).patrolType;
                ((TOLEnemyController)newLeader.m_owner).SetCurrentPatrolPoint(((TOLEnemyController)m_owner).GetCurrentPatrolPoint());
                ((TOLEnemyController)newLeader.m_owner).SetPatrolDirection(((TOLEnemyController)m_owner).GetPatrolDirection());
                ((TOLEnemyController)newLeader.m_owner).FaceCurrentPatrolPoint();
                foreach (TOLEnemyFlying enemy in flyingBoids)
                {
                    enemy.flyingBoids.Remove(newLeader);
                    enemy.GroupLeader = newLeader;
                }
            }*/
        }
        else
        {
            /*foreach (TOLEnemyFlying enemy in flyingBoids)
            {
                enemy.flyingBoids.Remove(this);
            }*/
            
            // just remove this enemy from the leader's list as all have the same reference
            GroupLeader.flyingBoids.Remove(this);
        }
    }

    //-------------------------------------------------------------------------

    /// <summary>
    /// sets up the leader
    /// </summary>
    /// <param name="msg"></param>
    public void SetUpSwarmForLeader(TOLMessage msg)
    {
        //this is the leader , it sets up the group for the leader
        List<TOLEnemyController> fb = (List<TOLEnemyController>)msg.data;
        int count = fb.Count;
        for (int i = 0; i < count; i++)
        {
            flyingBoids.Add(fb[i].GetComponent<TOLEnemyFlying>());
            
        }
        GroupLeader = this;
        BoidInfo.setUpBoid = true;
        int flockSize = flyingBoids.Count;
        for (int i = 0; i < flockSize; i++)
        {
            flyingBoids[i].SetUpSwarmFollowers(flyingBoids,this);
        }

    }

    //-------------------------------------------------------------------------

    /// <summary>
    /// sets up the swarm followers
    /// called by the leader
    /// </summary>
    /// <param name="swarm"></param>
    /// <param name="Leader"></param>
    public void SetUpSwarmFollowers(List<TOLEnemyFlying> swarm , TOLEnemyFlying Leader)
    {
        flyingBoids = swarm;
        BoidInfo.isSwarmingEnemy = true;
        GroupLeader = Leader;
        maxVelocity = GroupLeader.maxVelocity;
        minVelocity = GroupLeader.minVelocity;
        SetDestination(Leader.transform);
        BoidInfo.setUpBoid = true;
    }

    //-------------------------------------------------------------------------

    /// <summary>
    /// this is leader
    /// will signal followers to follow leader
    /// </summary>
    /// <param name="player"></param>
    public void SwarmFollowTarget(Transform swarmPoint)
    {
        int count = flyingBoids.Count;
        for (int i = 0; i < count; i++)
        {
            flyingBoids[i].FollowThisTarget(swarmPoint);
        }
    }

    //-------------------------------------------------------------------------

    /// <summary>
    /// called by leader on follower
    /// to follow player
    /// </summary>
    /// <param name="player"></param>
    public void FollowThisTarget(Transform swarmPoint)
    {
        SetDestination(swarmPoint);
        chaseTriggerTarget = swarmPoint;
    }

    //-------------------------------------------------------------------------

    public override void TeleportEnemy(Vector3 destination)
    {
        base.TeleportEnemy(destination);
        transform.position = destination;
    }

    public void SpeedUpSwarm(float new_minVelocity, float new_maxVelocity)
    {
        maxVelocity = new_maxVelocity;
        minVelocity = new_minVelocity;
    }
    #endregion
}
