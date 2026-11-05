using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PickupItem : MonoBehaviour
{
    [Header("Hold Settings")]
    [Tooltip("Offset position relative to the player's right hand hold point.")]
    public Vector3 holdPositionOffset = Vector3.zero;
    
    [Tooltip("Offset rotation relative to the player's right hand hold point.")]
    public Vector3 holdRotationOffset = Vector3.zero;

    [Tooltip("Force applied forward when dropping the item.")]
    public float dropForwardForce = 1.5f;

    protected Rigidbody rb;
    protected Collider col;
    public bool IsHeld { get; private set; } = false;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    /// <summary>
    /// Disables physics/collisions and parents the object to the player's hand.
    /// </summary>
    public virtual void OnPickup(Transform handSlot)
    {
        IsHeld = true;

        // Disable physics and collisions so it doesn't glitch with the player or walls
        rb.isKinematic = true;
        rb.useGravity = false;
        col.enabled = false;

        // Attach to right hand
        transform.SetParent(handSlot);
        transform.localPosition = holdPositionOffset;
        transform.localRotation = Quaternion.Euler(holdRotationOffset);
    }

    /// <summary>
    /// Restores physics/collisions and detaches the object from the player's hand.
    /// </summary>
    public virtual void OnDrop(Vector3 dropDirection)
    {
        IsHeld = false;

        // Detach from hand
        transform.SetParent(null);

        // Re-enable physics and collisions
        rb.isKinematic = false;
        rb.useGravity = true;
        col.enabled = true;

        // Clear residual velocity and apply a gentle forward toss
        rb.linearVelocity = Vector3.zero; // Note: Use rb.velocity if on Unity 2022 or older
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(dropDirection * dropForwardForce, ForceMode.Impulse);
    }
}