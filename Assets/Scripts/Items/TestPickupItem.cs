using UnityEngine;

public class TestPickupItem : PickupItem
{
    [Header("Test Item Specifics")]
    public string itemName = "Test Cube";

    public override void OnPickup(Transform handSlot)
    {
        base.OnPickup(handSlot);
        Debug.Log($"Picked up: {itemName}. Right hand occupied (Dynamo recharge blocked).");
    }

    public override void OnDrop(Vector3 dropDirection)
    {
        base.OnDrop(dropDirection);
        Debug.Log($"Dropped: {itemName}. Right hand free (Dynamo recharge available).");
    }
}