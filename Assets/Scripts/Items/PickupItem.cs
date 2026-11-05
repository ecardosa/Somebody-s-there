using UnityEngine;
using UnityEngine.Rendering;

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

    [Header("Outline Settings")]
    [Tooltip("Color del contorn quan mires l'objecte.")]
    public Color outlineColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Tooltip("Gruix del contorn (0.03 = 3% més gran).")]
    [Range(0.01f, 0.15f)]
    public float outlineScale = 0.04f;

    protected Rigidbody rb;
    protected Collider col;
    private GameObject outlineObject;

    public bool IsHeld { get; private set; } = false;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        CreateOutlineObject();
    }

    private void CreateOutlineObject()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        MeshRenderer mr = GetComponent<MeshRenderer>();

        if (mf == null || mr == null) return;

        // Creem un objecte fill que farà de contorn
        outlineObject = new GameObject("Outline");
        outlineObject.transform.SetParent(transform, false);
        outlineObject.transform.localPosition = Vector3.zero;
        outlineObject.transform.localRotation = Quaternion.identity;
        outlineObject.transform.localScale = Vector3.one * (1f + outlineScale);

        MeshFilter outlineMf = outlineObject.AddComponent<MeshFilter>();
        outlineMf.sharedMesh = mf.sharedMesh;

        MeshRenderer outlineMr = outlineObject.AddComponent<MeshRenderer>();
        outlineMr.shadowCastingMode = ShadowCastingMode.Off;

        // Creem un material Unlit per al contorn compatible amb URP
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null) unlitShader = Shader.Find("Unlit/Color");

        Material outlineMat = new Material(unlitShader);
        outlineMat.SetColor("_BaseColor", outlineColor);
        outlineMat.color = outlineColor;
        // Renderitzem només les cares del darrere (Front Culling = 1) perquè faci d'aurèola/contorn
        outlineMat.SetInt("_Cull", (int)CullMode.Front);

        outlineMr.material = outlineMat;
        outlineObject.SetActive(false);
    }

    /// <summary>
    /// Activa o desactiva el contorn visual de l'objecte.
    /// </summary>
    public void SetHighlight(bool highlight)
    {
        if (outlineObject != null)
        {
            outlineObject.SetActive(highlight && !IsHeld);
        }
    }

    /// <summary>
    /// Disables physics/collisions and parents the object to the player's hand.
    /// </summary>
    public virtual void OnPickup(Transform handSlot)
    {
        IsHeld = true;
        SetHighlight(false); // Apaguem el contorn en agafar-lo

        rb.isKinematic = true;
        rb.useGravity = false;
        col.enabled = false;

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

        transform.SetParent(null);

        rb.isKinematic = false;
        rb.useGravity = true;
        col.enabled = true;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(dropDirection * dropForwardForce, ForceMode.Impulse);
    }
}