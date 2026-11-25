using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AudioSource))]
public class RatEnemy : MonoBehaviour
{
    public enum RatState
    {
        Idle,
        Observing,
        Telegraphing,
        ChasingPlayer,
        ChasingGroundItem,
        ReturningItem,
        FleeingToBurrow,
        HidingInBurrow
    }

    [Header("Estat actual (Només lectura)")]
    public RatState currentState = RatState.Idle;
    [Range(0f, 1f)] public float lightExposure = 0f;
    public float currentDetectionRadius;

    [Header("Referències")]
    [Tooltip("Si es deixa buit, buscarà automàticament el HorrorPlayerController de l'escena.")]
    public HorrorPlayerController player;

    [Tooltip("SphereCollider en mode Trigger per visualitzar i gestionar el radi de detecció.")]
    public SphereCollider detectionCollider;

    [Tooltip("Transform buit a la boca/cap de la rata on es col·locarà l'objecte robat.")]
    public Transform mouthHoldPoint;

    [Tooltip("Lloc assignat (cau/forat) on fuig la rata quan s'espanta.")]
    public Transform burrowPoint;

    [Tooltip("Posició de guàrdia/espera. Si es deixa buit, utilitza la posició inicial de la rata.")]
    public Transform guardPoint;

    [Header("Detecció per soroll i Dinamo")]
    [Tooltip("Radi base quan el jugador camina dret.")]
    public float baseDetectionRadius = 8f;

    [Tooltip("Radi reduït quan el jugador va ajupit.")]
    public float crouchDetectionRadius = 4.5f;

    [Tooltip("Radi ampliat quan el jugador recarrega la dinamo.")]
    public float dynamoDetectionRadius = 13f;

    [Tooltip("Velocitat amb què s'ajusta el radi del SphereCollider.")]
    public float radiusTransitionSpeed = 5f;

    [Tooltip("Distància a la qual la rata s'atura per observar si el jugador no porta res.")]
    public float observeStopDistance = 3.2f;

    [Header("Velocitats de moviment")]
    public float normalChaseSpeed = 3.8f;
    public float dynamoChaseSpeed = 4.8f;
    public float investigateSpeed = 2.6f;
    public float returnItemSpeed = 3.5f;
    public float fleeSpeed = 6.2f;
    public float patrolReturnSpeed = 2.4f;

    [Header("Atac, Robatori i Flanqueig Nerviós")]
    public float attackRange = 1.6f;
    public float itemPickupRange = 1.2f;
    [Tooltip("Per sota d'aquesta distància, la rata deixa de fer ziga-zaga i es llança recta.")]
    public float directLungeDistance = 2.2f;
    [Tooltip("Desplaçament lateral de flanqueig.")]
    public float flankOffset = 1.4f;
    [Tooltip("Amplitud de la ziga-zaga nerviosa.")]
    public float zigzagAmplitude = 1.1f;
    [Tooltip("Freqüència del moviment nerviós.")]
    public float zigzagFrequency = 4.5f;

    [Header("Resistència a la llum")]
    [Tooltip("Segons d'il·luminació per espantar-la amb la bateria al 100%.")]
    public float timeToScareFullLight = 1.0f;

    [Tooltip("Segons d'il·luminació per espantar-la amb la bateria gairebé buida (sempre possible).")]
    public float timeToScareLowLight = 1.7f;

    [Tooltip("Velocitat a la qual perd la por si deixes d'il·luminar-la.")]
    public float lightDecayRate = 0.45f;

    [Tooltip("Factor de reducció de velocitat mentre rep la llum directa.")]
    [Range(0.2f, 1f)]
    public float speedMultiplierInLight = 0.55f;

    [Tooltip("Alçada des del terra per calcular el punt central de la rata pel raig de llum.")]
    public float ratCenterHeight = 0.25f;

    [Header("Telegrafiat i Cau")]
    [Tooltip("Temps que la rata avisa amb un xiscle abans de llançar-se a atacar.")]
    public float telegraphDuration = 0.6f;

    [Tooltip("Temps que la rata s'espera amagada al cau després de fugir.")]
    public float burrowHideDuration = 6.0f;

    [Tooltip("Opcional: amaga els Renderers de la rata mentre és dins del cau.")]
    public bool hideVisualsInBurrow = false;

    [Header("Àudio (3D Spatial)")]
    [Tooltip("Sorolls fluixos quan la rata espera o és fora de rang.")]
    public AudioClip[] ambientSqueaks;
    [Range(0f, 1f)] public float ambientVolume = 0.35f;
    public float minAmbientInterval = 4f;
    public float maxAmbientInterval = 9f;

    [Tooltip("Soroll especial obligatori abans d'atacar (telegrafiat).")]
    public AudioClip attackTelegraphClip;
    [Range(0f, 1f)] public float alertVolume = 1.0f;

    [Tooltip("Soroll opcional quan s'espanta per la llum.")]
    public AudioClip scaredClip;

    [Tooltip("Soroll opcional quan et roba l'objecte.")]
    public AudioClip stealClip;

    // --- Internes ---
    private NavMeshAgent agent;
    private AudioSource audioSource;
    private Renderer[] ratRenderers;

    private Vector3 initialGuardPos;
    private PickupItem targetGroundItem;
    private PickupItem carriedItem;

    private float stateTimer = 0f;
    private float ambientSoundTimer = 0f;
    private float zigzagSeed;
    private float flankDirection = 1f;
    private bool isIlluminated = false;
    private bool pendingAttackIsPlayer = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        audioSource = GetComponent<AudioSource>();
        ratRenderers = GetComponentsInChildren<Renderer>();

        initialGuardPos = guardPoint != null ? guardPoint.position : transform.position;
        currentDetectionRadius = baseDetectionRadius;
        zigzagSeed = Random.Range(0f, 100f);
        flankDirection = Random.value > 0.5f ? 1f : -1f;

        SetupDetectionCollider();
        SetupAudioSource();
        ResetAmbientTimer();
    }

    private void Start()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<HorrorPlayerController>();
        }
    }

    private void SetupDetectionCollider()
    {
        if (detectionCollider == null)
        {
            detectionCollider = GetComponent<SphereCollider>();
            if (detectionCollider == null)
            {
                detectionCollider = gameObject.AddComponent<SphereCollider>();
            }
        }
        detectionCollider.isTrigger = true;
        ApplyRadiusToCollider(currentDetectionRadius);
    }

    private void SetupAudioSource()
    {
        audioSource.spatialBlend = 1f; // So 3D
        audioSource.playOnAwake = false;
    }

    private void Update()
    {
        if (player == null) return;

        bool isDynamoRunning = IsPlayerUsingDynamo();

        // 1. Actualitzar radi del collider segons soroll (ajupit / normal / dinamo)
        UpdateDetectionRadius(isDynamoRunning);

        // 2. Comprovar il·luminació i resistència a la llum (excepte si ja fuig o s'amaga)
        if (currentState != RatState.FleeingToBurrow && currentState != RatState.HidingInBurrow)
        {
            HandleLightResistance();
            if (currentState == RatState.FleeingToBurrow) return;
        }

        // 3. Màquina d'estats
        switch (currentState)
        {
            case RatState.Idle:
                UpdateIdle(isDynamoRunning);
                break;

            case RatState.Observing:
                UpdateObserving(isDynamoRunning);
                break;

            case RatState.Telegraphing:
                UpdateTelegraphing();
                break;

            case RatState.ChasingPlayer:
                UpdateChasingPlayer(isDynamoRunning);
                break;

            case RatState.ChasingGroundItem:
                UpdateChasingGroundItem(isDynamoRunning);
                break;

            case RatState.ReturningItem:
                UpdateReturningItem(isDynamoRunning);
                break;

            case RatState.FleeingToBurrow:
                UpdateFleeingToBurrow();
                break;

            case RatState.HidingInBurrow:
                UpdateHidingInBurrow();
                break;
        }
    }

    // =========================================================================
    // DETECCIÓ PER SOROLL I DINAMO
    // =========================================================================

    private bool IsPlayerUsingDynamo()
    {
        return player.flashlight != null && player.flashlight.IsRecharging();
    }

    private void UpdateDetectionRadius(bool isDynamoRunning)
    {
        float targetRadius = baseDetectionRadius;

        if (isDynamoRunning)
        {
            targetRadius = dynamoDetectionRadius;
        }
        else if (player.IsCrouching)
        {
            targetRadius = crouchDetectionRadius;
        }

        currentDetectionRadius = Mathf.Lerp(currentDetectionRadius, targetRadius, Time.deltaTime * radiusTransitionSpeed);
        ApplyRadiusToCollider(currentDetectionRadius);
    }

    private void ApplyRadiusToCollider(float worldRadius)
    {
        if (detectionCollider == null) return;

        float maxScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        if (maxScale > 0.001f)
        {
            detectionCollider.radius = worldRadius / maxScale;
        }
    }

    private bool IsPlayerInDetectionZone()
    {
        return Vector3.Distance(transform.position, player.transform.position) <= currentDetectionRadius;
    }

    private PickupItem FindNearestMisplacedItem()
    {
        PickupItem closest = null;
        float closestDist = currentDetectionRadius;

        for (int i = 0; i < PickupItem.AllItems.Count; i++)
        {
            PickupItem item = PickupItem.AllItems[i];
            if (item == null || item.IsHeld || item.IsBeingCarriedByRat || item.isLockedInPuzzle) continue;
            if (item.IsAtOriginalPosition) continue;

            float dist = Vector3.Distance(transform.position, item.transform.position);
            if (dist <= closestDist)
            {
                closestDist = dist;
                closest = item;
            }
        }

        return closest;
    }

    // =========================================================================
    // RESISTÈNCIA A LA LLUM
    // =========================================================================

    private void HandleLightResistance()
    {
        DynamoFlashlight fl = player.flashlight;
        isIlluminated = CheckIfIlluminated(fl);

        if (isIlluminated && fl != null)
        {
            // Com més fluixa la bateria, una mica més triga, però sempre és possible espantar-la
            float requiredTime = Mathf.Lerp(timeToScareLowLight, timeToScareFullLight, fl.BatteryRatio);
            lightExposure += Time.deltaTime / Mathf.Max(0.1f, requiredTime);

            if (lightExposure >= 1f)
            {
                lightExposure = 1f;
                StartFleeingToBurrow();
            }
        }
        else
        {
            lightExposure = Mathf.Max(0f, lightExposure - Time.deltaTime * lightDecayRate);
        }
    }

    private bool CheckIfIlluminated(DynamoFlashlight fl)
    {
        if (fl == null || !fl.IsLightActive || fl.spotlight == null) return false;

        Light spot = fl.spotlight;
        Vector3 lightPos = spot.transform.position;
        Vector3 ratCenter = transform.position + Vector3.up * ratCenterHeight;
        Vector3 toRat = ratCenter - lightPos;
        float distance = toRat.magnitude;

        // Fora d'abast de la llanterna
        if (distance > spot.range) return false;

        // Fora del con de llum
        float angle = Vector3.Angle(spot.transform.forward, toRat.normalized);
        if (angle > spot.spotAngle * 0.5f) return false;

        // Comprovem que no hi hagi cap paret al mig (ignorant el jugador, la rata i l'objecte que porta)
        RaycastHit[] hits = Physics.RaycastAll(lightPos, toRat.normalized, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitT = hits[i].transform;
            if (hitT.IsChildOf(transform)) continue;
            if (hitT.IsChildOf(player.transform)) continue;
            if (carriedItem != null && hitT.IsChildOf(carriedItem.transform)) continue;

            // Algun obstacle sòlid bloqueja la llum
            return false;
        }

        return true;
    }

    // =========================================================================
    // ESTATS DE LA MÀQUINA D'ESTATS
    // =========================================================================

    private void UpdateIdle(bool isDynamoRunning)
    {
        HandleAmbientAudio();

        // Tornem cap al punt de guàrdia si estem lluny
        Vector3 guardPos = guardPoint != null ? guardPoint.position : initialGuardPos;
        if (Vector3.Distance(transform.position, guardPos) > 1f)
        {
            agent.isStopped = false;
            agent.speed = ApplyLightSlowdown(patrolReturnSpeed);
            agent.SetDestination(guardPos);
        }
        else
        {
            agent.isStopped = true;
        }

        // Prioritat 1: Objecte al terra fora de lloc dins del radi
        PickupItem groundItem = FindNearestMisplacedItem();
        if (groundItem != null)
        {
            StartTelegraphForGroundItem(groundItem);
            return;
        }

        // Prioritat 2: Jugador dins del radi
        if (IsPlayerInDetectionZone())
        {
            if (player.currentHeldItem != null)
            {
                StartTelegraphForPlayer();
            }
            else
            {
                currentState = RatState.Observing;
            }
        }
    }

    private void UpdateObserving(bool isDynamoRunning)
    {
        HandleAmbientAudio();

        // Si el jugador surt del radi, tornem a Idle
        if (!IsPlayerInDetectionZone())
        {
            currentState = RatState.Idle;
            return;
        }

        // Si apareix un objecte fora de lloc (ex: el jugador l'acaba de deixar per recarregar), a per ell!
        PickupItem groundItem = FindNearestMisplacedItem();
        if (groundItem != null)
        {
            StartTelegraphForGroundItem(groundItem);
            return;
        }

        // Si el jugador agafa un objecte, passem a l'atac
        if (player.currentHeldItem != null)
        {
            StartTelegraphForPlayer();
            return;
        }

        // Si el jugador està fent soroll amb la dinamo, la rata s'acosta atreta i més ràpida
        float distToPlayer = Vector3.Distance(transform.position, player.transform.position);
        if (isDynamoRunning && distToPlayer > observeStopDistance)
        {
            agent.isStopped = false;
            agent.speed = ApplyLightSlowdown(investigateSpeed * 1.25f);
            SetFlankingDestination(player.transform.position);
        }
        else
        {
            // Es limita a observar el jugador sense atacar
            agent.isStopped = true;
            LookAtTarget(player.transform.position);
        }
    }

    private void StartTelegraphForPlayer()
    {
        pendingAttackIsPlayer = true;
        targetGroundItem = null;
        EnterTelegraphState();
    }

    private void StartTelegraphForGroundItem(PickupItem item)
    {
        pendingAttackIsPlayer = false;
        targetGroundItem = item;
        EnterTelegraphState();
    }

    private void EnterTelegraphState()
    {
        currentState = RatState.Telegraphing;
        stateTimer = telegraphDuration;
        agent.isStopped = true;
        flankDirection = Random.value > 0.5f ? 1f : -1f;

        // So especial obligatori abans d'atacar
        if (attackTelegraphClip != null)
        {
            audioSource.PlayOneShot(attackTelegraphClip, alertVolume);
        }
    }

    private void UpdateTelegraphing()
    {
        Vector3 lookTarget = pendingAttackIsPlayer
            ? player.transform.position
            : (targetGroundItem != null ? targetGroundItem.transform.position : player.transform.position);

        LookAtTarget(lookTarget);

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            agent.isStopped = false;
            if (pendingAttackIsPlayer)
            {
                currentState = RatState.ChasingPlayer;
            }
            else if (targetGroundItem != null && !targetGroundItem.IsHeld && !targetGroundItem.IsBeingCarriedByRat)
            {
                currentState = RatState.ChasingGroundItem;
            }
            else
            {
                currentState = RatState.Idle;
            }
        }
    }

    private void UpdateChasingPlayer(bool isDynamoRunning)
    {
        // Si el jugador deixa caure l'objecte, la rata canvia immediatament a l'objecte del terra
        if (player.currentHeldItem == null)
        {
            PickupItem groundItem = FindNearestMisplacedItem();
            if (groundItem != null)
            {
                targetGroundItem = groundItem;
                currentState = RatState.ChasingGroundItem;
            }
            else
            {
                currentState = RatState.Observing;
            }
            return;
        }

        // Si el jugador s'escapa molt fora del radi, deixa de perseguir
        if (Vector3.Distance(transform.position, player.transform.position) > currentDetectionRadius * 1.35f)
        {
            currentState = RatState.Idle;
            return;
        }

        float baseSpeed = isDynamoRunning ? dynamoChaseSpeed : normalChaseSpeed;
        agent.speed = ApplyLightSlowdown(baseSpeed);
        agent.isStopped = false;

        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist <= attackRange)
        {
            StealFromPlayer();
            return;
        }

        SetFlankingDestination(player.transform.position);
    }

    private void UpdateChasingGroundItem(bool isDynamoRunning)
    {
        if (targetGroundItem == null || targetGroundItem.IsBeingCarriedByRat || targetGroundItem.IsAtOriginalPosition)
        {
            targetGroundItem = null;
            currentState = RatState.Idle;
            return;
        }

        // Si el jugador recull l'objecte abans que hi arribi la rata, la rata persegueix el jugador
        if (targetGroundItem.IsHeld)
        {
            if (player.currentHeldItem == targetGroundItem)
            {
                currentState = RatState.ChasingPlayer;
            }
            else
            {
                currentState = RatState.Idle;
            }
            return;
        }

        float baseSpeed = isDynamoRunning ? dynamoChaseSpeed : normalChaseSpeed;
        agent.speed = ApplyLightSlowdown(baseSpeed);
        agent.isStopped = false;

        float dist = Vector3.Distance(transform.position, targetGroundItem.transform.position);
        if (dist <= itemPickupRange)
        {
            GrabGroundItem(targetGroundItem);
            return;
        }

        SetFlankingDestination(targetGroundItem.transform.position);
    }

    private void StealFromPlayer()
    {
        PickupItem stolen = player.StealHeldItem();
        if (stolen == null)
        {
            currentState = RatState.Observing;
            return;
        }

        if (stealClip != null)
        {
            audioSource.PlayOneShot(stealClip, alertVolume);
        }

        carriedItem = stolen;
        Transform attachPoint = mouthHoldPoint != null ? mouthHoldPoint : transform;
        carriedItem.OnRatPickup(attachPoint);

        currentState = RatState.ReturningItem;
    }

    private void GrabGroundItem(PickupItem item)
    {
        if (stealClip != null)
        {
            audioSource.PlayOneShot(stealClip, alertVolume * 0.8f);
        }

        carriedItem = item;
        targetGroundItem = null;
        Transform attachPoint = mouthHoldPoint != null ? mouthHoldPoint : transform;
        carriedItem.OnRatPickup(attachPoint);

        currentState = RatState.ReturningItem;
    }

    private void UpdateReturningItem(bool isDynamoRunning)
    {
        if (carriedItem == null)
        {
            currentState = RatState.Idle;
            return;
        }

        float baseSpeed = isDynamoRunning ? returnItemSpeed * 1.15f : returnItemSpeed;
        agent.speed = ApplyLightSlowdown(baseSpeed);
        agent.isStopped = false;
        agent.SetDestination(carriedItem.InitialPosition);

        if (Vector3.Distance(transform.position, carriedItem.InitialPosition) <= itemPickupRange)
        {
            carriedItem.ReturnToOriginalPosition();
            carriedItem = null;
            currentState = RatState.Idle;
        }
    }

    private void StartFleeingToBurrow()
    {
        // Si portava un objecte robat, el deixa caure al terra en espantar-se
        if (carriedItem != null)
        {
            carriedItem.OnRatDrop();
            carriedItem = null;
        }

        targetGroundItem = null;
        currentState = RatState.FleeingToBurrow;
        agent.isStopped = false;
        agent.speed = fleeSpeed;

        if (scaredClip != null)
        {
            audioSource.PlayOneShot(scaredClip, alertVolume);
        }

        Vector3 dest = burrowPoint != null ? burrowPoint.position : initialGuardPos;
        agent.SetDestination(dest);
    }

    private void UpdateFleeingToBurrow()
    {
        Vector3 dest = burrowPoint != null ? burrowPoint.position : initialGuardPos;
        agent.SetDestination(dest);

        if (!agent.pathPending && agent.remainingDistance <= 0.7f)
        {
            EnterBurrow();
        }
    }

    private void EnterBurrow()
    {
        currentState = RatState.HidingInBurrow;
        stateTimer = burrowHideDuration;
        lightExposure = 0f;
        agent.isStopped = true;

        if (hideVisualsInBurrow)
        {
            SetVisualsVisible(false);
        }
    }

    private void UpdateHidingInBurrow()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            if (hideVisualsInBurrow)
            {
                SetVisualsVisible(true);
            }
            currentState = RatState.Idle;
        }
    }

    // =========================================================================
    // MOVIMENT NERVIÓS, FLANQUEIG I AUXILIARS
    // =========================================================================

    private void SetFlankingDestination(Vector3 targetPosition)
    {
        Vector3 toTarget = targetPosition - transform.position;
        float distance = toTarget.magnitude;

        // Quan estem molt a prop, anem directes a l'objectiu per assegurar l'atac
        if (distance <= directLungeDistance)
        {
            agent.SetDestination(targetPosition);
            return;
        }

        Vector3 forwardDir = toTarget.normalized;
        Vector3 sideDir = Vector3.Cross(Vector3.up, forwardDir).normalized;

        // Combina un flanqueig lateral + oscil·lació nerviosa (ziga-zaga)
        float zigzag = Mathf.Sin(Time.time * zigzagFrequency + zigzagSeed) * zigzagAmplitude;
        float distanceFactor = Mathf.Clamp01((distance - directLungeDistance) / 4f);
        float lateralShift = (flankDirection * flankOffset + zigzag) * distanceFactor;

        Vector3 desiredPoint = targetPosition + sideDir * lateralShift;

        // Assegurem que el punt calculat existeix sobre el NavMesh
        if (NavMesh.SamplePosition(desiredPoint, out NavMeshHit navHit, 2.0f, NavMesh.AllAreas))
        {
            agent.SetDestination(navHit.position);
        }
        else
        {
            agent.SetDestination(targetPosition);
        }
    }

    private float ApplyLightSlowdown(float speed)
    {
        return isIlluminated ? speed * speedMultiplierInLight : speed;
    }

    private void LookAtTarget(Vector3 targetPos)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
        }
    }

    private void HandleAmbientAudio()
    {
        if (ambientSqueaks == null || ambientSqueaks.Length == 0) return;

        ambientSoundTimer -= Time.deltaTime;
        if (ambientSoundTimer <= 0f)
        {
            AudioClip clip = ambientSqueaks[Random.Range(0, ambientSqueaks.Length)];
            if (clip != null)
            {
                audioSource.PlayOneShot(clip, ambientVolume);
            }
            ResetAmbientTimer();
        }
    }

    private void ResetAmbientTimer()
    {
        ambientSoundTimer = Random.Range(minAmbientInterval, maxAmbientInterval);
    }

    private void SetVisualsVisible(bool visible)
    {
        for (int i = 0; i < ratRenderers.Length; i++)
        {
            if (ratRenderers[i] != null)
            {
                ratRenderers[i].enabled = visible;
            }
        }
    }

    // Visualització de radis a l'editor de Unity
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, crouchDetectionRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, baseDetectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, dynamoDetectionRadius);

        if (burrowPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, burrowPoint.position);
            Gizmos.DrawWireSphere(burrowPoint.position, 0.4f);
        }
    }
}