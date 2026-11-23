using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>ตัวรับการลากของเศษแต่ละชิ้น ส่งต่อให้ตัวเกมหลัก</summary>
public class MirrorShardPiece : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [NonSerialized] public ShatteredMirrorPuzzle owner;
    [NonSerialized] public RectTransform rt;
    [NonSerialized] public MirrorShardGraphic fill;
    [NonSerialized] public MirrorShardGraphic outline;
    [NonSerialized] public MirrorShardGraphic guide;
    [NonSerialized] public Vector2 home;      // ตำแหน่งที่ถูกต้องบนพื้นที่ต่อภาพ
    [NonSerialized] public bool placed;
    [NonSerialized] public float baseShade;

    public void OnPointerDown(PointerEventData e) { if (owner != null) owner.PieceDown(this, e); }
    public void OnDrag(PointerEventData e) { if (owner != null) owner.PieceDrag(this, e); }
    public void OnPointerUp(PointerEventData e) { if (owner != null) owner.PieceUp(this, e); }
}
