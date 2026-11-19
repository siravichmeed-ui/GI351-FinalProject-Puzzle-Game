
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewItem",
    menuName = "Cursed Shop/Item Data"
)]
public class ItemData : ScriptableObject
{
    [Header("Item Information")]
    public string itemID;
    public string itemName;

    [Header("Item Prefab")]
    public GameObject itemPrefab;

    [Header("Item Appearance")]
    public Sprite cursedSprite;
    public Sprite cleanedSprite;
}
