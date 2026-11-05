using UnityEngine;

/// <summary>
/// First-person horror controller designed for heavy, deliberate movement,
/// conditional sprinting during chases, procedural head-bobbing, dynamo flashlight integration,
/// and right-hand object pickup/interaction.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class HorrorPlayerController : MonoBehaviour
{
    public enum MovementState
    {
        Idle,
        Walking,
        Running,
        Crouching
    }

    [Header("Current State (Read-Only)")]
    [Tooltip("Tracks the player's current movement behavior.")]
    public MovementState currentState = MovementState.Idle;

    [Tooltip("ONLY allows the player to sprint when true (e.g., triggered by a monster chase manager).")]
    public bool estaPersecucio = false;

    [Header("Movement Speeds & Weight")]
    [Tooltip("Slow, deliberate base walking speed.")]
    public float walkSpeed = 2.4f;

    [Tooltip("Adrenaline sprint speed, accessible only when estaPersecucio is true.")]
    public float runSpeed = 5.0f;

    [Tooltip("Stealth speed when crouched.")]
    public float crouchSpeed = 1.2f;

    [Tooltip("Higher values make starting and stopping feel heavier and less arcade-y.")]
    [Range(0.05f, 0.5f)]
    public float movementSmoothTime = 0.18f;

    [Tooltip("Downward pull. Stronger gravity stops the player from feeling 'floaty' on steps.")]
    public float gravity = -19.62f;

    [Header("Mouse Look & Camera")]
    [Tooltip("Reference to the Transform that holds the main Camera.")]
    public Transform cameraPivot;

    [Tooltip("Base mouse sensitivity.")]
    public float mouseSensitivity = 1.8f;

    [Tooltip("Dampens mouse input so looking around feels weighty rather than twitchy.")]
    [Range(0.01f, 0.2f)]
    public float cameraSmoothTime = 0.06f;

    [Tooltip("Maximum upward look angle.")]
    public float maxLookUpAngle = 75f;

    [Tooltip("Maximum downward look angle.")]
    public float maxLookDownAngle = -80f;

    [Header("Crouch Settings")]
    [Tooltip("If true, pressing the crouch key toggles crouch. If false, player must hold the key.")]
    public bool toggleCrouch = true;

    public KeyCode crouchKey = KeyCode.LeftControl;
    public float standingHeight = 1.8f;
    public float crouchHeight = 1.0f;

    [Tooltip("Camera Y local position when standing.")]
    public float standingCameraY = 1.6f;

    [Tooltip("Camera Y local position when crouched.")]
    public float crouchingCameraY = 0.85f;

    [Tooltip("Speed at which the player lowers/raises into a crouch.")]
    public float crouchTransitionSpeed = 7f;

    [Tooltip("Layers detected above the player's head to prevent standing up inside vents/low ceilings.")]
    public LayerMask ceilingObstacleMask = ~0;

    [Header("Procedural Head Bob")]
    public bool enableHeadBob = true;

    [Tooltip("Subtle breathing sway when standing completely still.")]
    public float idleBobFrequency = 1.2f;
    public float idleBobAmplitude = 0.012f;

    [Tooltip("Slow, heavy footstep rhythm while walking.")]
    public float walkBobFrequency = 6.5f;
    public float walkBobAmplitude = 0.04f;

    [Tooltip("Frantic, heavy shaking while sprinting during a chase.")]
    public float runBobFrequency = 11.5f;
    public float runBobAmplitude = 0.075f;

    [Tooltip("Muted, careful steps while crouched.")]
    public float crouchBobFrequency = 4.5f;
    public float crouchBobAmplitude = 0.022f;

    [Header("Dynamo Flashlight Integration")]
    [Tooltip("Reference to the external DynamoFlashlight script.")]
    public DynamoFlashlight flashlight;

    [Tooltip("Key toggled to turn the flashlight on/off.")]
    public KeyCode toggleLightKey = KeyCode.F;

    [Tooltip("Key held down to crank and recharge the flashlight.")]
    public KeyCode rechargeKey = KeyCode.R;

    [Tooltip("Also allow Left Mouse Button to crank the flashlight.")]
    public bool allowLeftClickRecharge = true;

    [Header("Object Interaction (Right Hand)")]
    [Tooltip("Camera used for casting the interaction ray. Defaults to Camera.main if left empty.")]
    public Camera playerCamera;

    [Tooltip("Empty Transform on the right side of the camera where picked-up items are held.")]
    public Transform rightHandHoldPoint;

    [Tooltip("Maximum distance from which the player can pick up objects.")]
    public float interactionDistance = 3.0f;

    [Tooltip("Layers that the interaction raycast can hit.")]
    public LayerMask interactionLayerMask = ~0;

    [Tooltip("Key pressed to pick up or drop an item.")]
    public KeyCode interactKey = KeyCode.E;

    [Tooltip("Read-only reference to the currently held item.")]
    public PickupItem currentHeldItem;

    // --- Internal Private Variables ---
    private CharacterController controller;

    // Movement smoothing
    private Vector2 currentInputVector;
    private Vector2 smoothInputVelocity;
    private float verticalVelocity;

    // Camera look smoothing
    private float targetYaw;
    private float targetPitch;
    private float currentYaw;
    private float currentPitch;
    private float yawVelocity;
    private float pitchVelocity;

    // Crouch tracking
    private bool wantsToCrouch = false;
    private bool isCrouching = false;
    private float currentCameraBaseY;

    // Headbob tracking
    private float bobTimer = 0f;
    private Vector3 headBobOffset = Vector3.zero;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        // Lock and hide the cursor for first-person immersion
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Initialize dimensions
        controller.height = standingHeight;
        controller.center = new Vector3(0f, standingHeight * 0.5f, 0f);
        currentCameraBaseY = standingCameraY;

        // Initialize rotation targets to match starting rotation
        targetYaw = currentYaw = transform.eulerAngles.y;
        if (cameraPivot != null)
        {
            targetPitch = currentPitch = cameraPivot.localEulerAngles.x;
        }
    }

    private void Update()
    {
        HandleCameraLook();
        HandleCrouchState();
        HandleMovement();
        HandleHeadBob();
        HandleItemInteraction();
        HandleDynamoInput();
    }

    /// <summary>
    /// Reads mouse input, clamps vertical look, and applies smooth damping for a heavy camera feel.
    /// </summary>
    private void HandleCameraLook()
    {
        if (cameraPivot == null) return;

        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        targetYaw += mouseX;
        targetPitch -= mouseY;

        targetPitch = Mathf.Clamp(targetPitch, maxLookDownAngle, maxLookUpAngle);

        currentYaw = Mathf.SmoothDamp(currentYaw, targetYaw, ref yawVelocity, cameraSmoothTime);
        currentPitch = Mathf.SmoothDamp(currentPitch, targetPitch, ref pitchVelocity, cameraSmoothTime);

        transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);
        cameraPivot.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
    }

    /// <summary>
    /// Handles crouch input, checks for low ceilings before standing, and smoothly resizes the collider.
    /// </summary>
    private void HandleCrouchState()
    {
        if (toggleCrouch)
        {
            if (Input.GetKeyDown(crouchKey))
            {
                wantsToCrouch = !wantsToCrouch;
            }
        }
        else
        {
            wantsToCrouch = Input.GetKey(crouchKey);
        }

        if (!wantsToCrouch && isCrouching)
        {
            if (!CanStandUp())
            {
                isCrouching = true;
            }
            else
            {
                isCrouching = false;
            }
        }
        else
        {
            isCrouching = wantsToCrouch;
        }

        float targetHeight = isCrouching ? crouchHeight : standingHeight;

        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
        controller.center = new Vector3(0f, controller.height * 0.5f, 0f);

        float targetCamY = isCrouching ? crouchingCameraY : standingCameraY;
        currentCameraBaseY = Mathf.Lerp(currentCameraBaseY, targetCamY, Time.deltaTime * crouchTransitionSpeed);
    }

    /// <summary>
    /// Casts a sphere upwards to check if an obstacle prevents the player from standing up.
    /// </summary>
    private bool CanStandUp()
    {
        float radius = controller.radius * 0.9f;
        Vector3 origin = transform.position + Vector3.up * (crouchHeight - radius);
        float checkDistance = standingHeight - crouchHeight;

        return !Physics.SphereCast(origin, radius, Vector3.up, out _, checkDistance, ceilingObstacleMask, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// Evaluates movement states (Idle, Walk, Crouch, Run) and moves the CharacterController with inertia.
    /// </summary>
    private void HandleMovement()
    {
        Vector2 rawInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (rawInput.sqrMagnitude > 1f)
        {
            rawInput.Normalize();
        }

        bool holdingSprint = Input.GetKey(KeyCode.LeftShift);
        bool isMovingForward = rawInput.y > 0.1f;
        bool canSprint = estaPersecucio && holdingSprint && !isCrouching && isMovingForward;

        float targetSpeed = walkSpeed;
        if (isCrouching)
        {
            currentState = MovementState.Crouching;
            targetSpeed = crouchSpeed;
        }
        else if (canSprint)
        {
            currentState = MovementState.Running;
            targetSpeed = runSpeed;
        }
        else if (rawInput.sqrMagnitude > 0.01f)
        {
            currentState = MovementState.Walking;
            targetSpeed = walkSpeed;
        }
        else
        {
            currentState = MovementState.Idle;
            targetSpeed = 0f;
        }

        Vector2 targetVelocity = rawInput * targetSpeed;
        currentInputVector = Vector2.SmoothDamp(currentInputVector, targetVelocity, ref smoothInputVelocity, movementSmoothTime);

        Vector3 moveDirection = (transform.right * currentInputVector.x) + (transform.forward * currentInputVector.y);

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2.5f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        moveDirection.y = verticalVelocity;

        controller.Move(moveDirection * Time.deltaTime);
    }

    /// <summary>
    /// Applies procedural sine/cosine wave offsets to the camera to simulate footsteps and breathing.
    /// </summary>
    private void HandleHeadBob()
    {
        if (cameraPivot == null) return;

        if (!enableHeadBob || !controller.isGrounded)
        {
            headBobOffset = Vector3.Lerp(headBobOffset, Vector3.zero, Time.deltaTime * 8f);
            cameraPivot.localPosition = new Vector3(0f, currentCameraBaseY, 0f) + headBobOffset;
            return;
        }

        float frequency = idleBobFrequency;
        float amplitude = idleBobAmplitude;

        float horizontalSpeed = new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude;

        if (horizontalSpeed > 0.2f)
        {
            switch (currentState)
            {
                case MovementState.Crouching:
                    frequency = crouchBobFrequency;
                    amplitude = crouchBobAmplitude;
                    break;
                case MovementState.Running:
                    frequency = runBobFrequency;
                    amplitude = runBobAmplitude;
                    break;
                case MovementState.Walking:
                default:
                    frequency = walkBobFrequency;
                    amplitude = walkBobAmplitude;
                    break;
            }
        }

        bobTimer += Time.deltaTime * frequency;

        float bobY = Mathf.Sin(bobTimer) * amplitude;
        float bobX = Mathf.Cos(bobTimer * 0.5f) * (amplitude * 0.6f);

        Vector3 targetOffset = new Vector3(bobX, bobY, 0f);
        headBobOffset = Vector3.Lerp(headBobOffset, targetOffset, Time.deltaTime * 12f);

        cameraPivot.localPosition = new Vector3(0f, currentCameraBaseY, 0f) + headBobOffset;
    }

    /// <summary>
    /// Handles raycasting to pick up items into the right hand, or dropping the currently held item.
    /// </summary>
    private void HandleItemInteraction()
    {
        if (Input.GetKeyDown(interactKey))
        {
            if (currentHeldItem != null)
            {
                DropHeldItem();
            }
            else
            {
                TryPickupItem();
            }
        }
    }

    private void TryPickupItem()
    {
        Transform rayOrigin = playerCamera != null ? playerCamera.transform : cameraPivot;
        if (rayOrigin == null) return;

        if (Physics.Raycast(rayOrigin.position, rayOrigin.forward, out RaycastHit hit, interactionDistance, interactionLayerMask, QueryTriggerInteraction.Ignore))
        {
            PickupItem item = hit.collider.GetComponentInParent<PickupItem>();
            if (item != null && !item.IsHeld && rightHandHoldPoint != null)
            {
                currentHeldItem = item;
                currentHeldItem.OnPickup(rightHandHoldPoint);
            }
        }
    }

    private void DropHeldItem()
    {
        if (currentHeldItem == null) return;

        Transform rayOrigin = playerCamera != null ? playerCamera.transform : cameraPivot;
        Vector3 dropDir = rayOrigin != null ? rayOrigin.forward : transform.forward;

        currentHeldItem.OnDrop(dropDir);
        currentHeldItem = null;
    }

    /// <summary>
    /// Controls the dynamo flashlight. Prevents recharging if the right hand is holding an item.
    /// </summary>
    private void HandleDynamoInput()
    {
        if (flashlight == null) return;

        // 1. Si tenim un objecte a la mà, bloquegem la recàrrega
        if (currentHeldItem != null)
        {
            flashlight.inputRechargeHeld = false;

            if (Input.GetKeyDown(toggleLightKey))
            {
                flashlight.ToggleLightInput();
            }
            return;
        }

        // 2. Comprovem si s'està mantenint R o Clic Esquerre per recarregar
        bool pressingRecharge = Input.GetKey(rechargeKey);
        bool pressingLeftClick = allowLeftClickRecharge && Input.GetMouseButton(0);

        flashlight.inputRechargeHeld = (pressingRecharge || pressingLeftClick);

        // 3. Només permetem encendre/apagar amb F si NO s'està recarregant
        if (!flashlight.inputRechargeHeld && Input.GetKeyDown(toggleLightKey))
        {
            flashlight.ToggleLightInput();
        }
    }
}