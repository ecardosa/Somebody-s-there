using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PickupItem : MonoBehaviour
{
    public static readonly List<PickupItem> AllItems = new List<PickupItem>();

    [Header("Hold Settings")]
    [Tooltip("Offset position relative to the player's right hand hold point.")]
    public Vector3 holdPositionOffset = Vector3.zero;
    
    [Tooltip("Offset rotation relative to the player's right hand hold point.")]
    public Vector3 holdRotationOffset = Vector3.zero;

    [Tooltip("Force applied forward when dropping the item.")]
    public float dropForwardForce = 1.5f;

    [Header("Puzzle / Rat Settings")]
    [Tooltip("Distància màxima per considerar que l'objecte encara és al seu lloc original.")]
    public float originalPositionTolerance = 0.6f;

    [Tooltip("Marca-ho a true per codi si l'objecte ja s'ha col·locat i bloquejat al puzle.")]
    public bool isLockedInPuzzle = false;

    [Header("Outline Settings")]
    public Color outlineColor = new Color(1f, 0.85f, 0.2f, 1f);
    [Range(0.01f, 0.15f)]
    public float outlineScale = 0.04f;

    protected Rigidbody rb;
    protected Collider col;
    private GameObject outlineObject;

    public bool IsHeld { get; private set; } = false;
    public bool IsBeingCarriedByRat { get; private set; } = false;
    public Vector3 InitialPosition { get; private set; }
    public Quaternion InitialRotation { get; private set; }

    public bool IsAtOriginalPosition =>
        !IsHeld &&
        !IsBeingCarriedByRat &&
        Vector3.Distance(transform.position, InitialPosition) <= originalPositionTolerance;

    private void OnEnable()
    {
        if (!AllItems.Contains(this)) AllItems.Add(this);
    }

    private void OnDisable()
    {
        AllItems.Remove(this);
    }

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        InitialPosition = transform.position;
        InitialRotation = transform.rotation;

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

    /// <summary>
    /// La rata agafa l'objecte amb la boca per tornar-lo al seu lloc.
    /// </summary>
    public void OnRatPickup(Transform ratMouth)
    {
        IsHeld = false;
        IsBeingCarriedByRat = true;
        SetHighlight(false);

        rb.isKinematic = true;
        rb.useGravity = false;
        col.enabled = false;

        transform.SetParent(ratMouth);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// La rata deixa caure l'objecte si el jugador l'espanta mentre el transporta.
    /// </summary>
    public void OnRatDrop()
    {
        IsBeingCarriedByRat = false;
        transform.SetParent(null);

        rb.isKinematic = false;
        rb.useGravity = true;
        col.enabled = true;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    /// <summary>
    /// La rata deixa l'objecte exactament al seu lloc d'origen.
    /// </summary>
    public void ReturnToOriginalPosition()
    {
        IsBeingCarriedByRat = false;
        IsHeld = false;
        transform.SetParent(null);

        transform.position = InitialPosition;
        transform.rotation = InitialRotation;

        rb.isKinematic = false;
        rb.useGravity = true;
        col.enabled = true;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}