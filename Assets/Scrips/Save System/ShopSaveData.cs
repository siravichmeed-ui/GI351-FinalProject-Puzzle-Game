using System;
using System.Collections.Generic;

[Serializable]
public class ShopSaveData
{
    public int version = 2;

    public List<ItemSaveData> items = new List<ItemSaveData>();

    // ลูกค้าที่กำลังมาฝากของ (คนใหม่)
    public CustomerSaveData customer;
    public CustomerMovementSaveData customerMovement;

    // ออเดอร์ทั้งหมดที่ฝากของไว้และยังรอรับคืน
    public List<OrderSaveData> orders = new List<OrderSaveData>();

    // เจ้าของสินค้าที่กำลังเดินมารับของ
    public string returningOrderInstanceID;
    public CustomerMovementSaveData returnCustomerMovement;

    // ระบบวัน-เวลา-ค่าเช่า
    public DaySaveData day = new DaySaveData();
}

[Serializable]
public class DaySaveData
{
    public int currentDay;
    public float elapsedGameMinutes;
    public int unpaidRent;
    public int debtDays;
    public int todayEarnings;
}

[Serializable]
public class OrderSaveData
{
    public string instanceID;
    public string customerID;
    public string itemID;

    public bool rewardPaid;
    public bool itemPickedUp;
}

[Serializable]
public class CustomerSaveData
{
    public string customerID;
    public string itemID;

    public bool orderAccepted;
    public bool hasDelivered;
    public string deliveredItemInstanceID;

    // ---- Legacy (version 1) : ใช้สำหรับ migrate เท่านั้น ----
    public bool hasPendingReturn;
    public bool isReturning;
    public string pendingCustomerID;
    public string pendingItemID;
    public string pendingItemInstanceID;
    public bool returnRewardPaid;
    public bool returnItemPickedUp;
}

[Serializable]
public class CustomerMovementSaveData
{
    public float posX;
    public float posY;
    public float posZ;

    public float currentPosX;
    public float currentPosY;
    public float currentPosZ;

    public float rotationX;
    public float rotationY;
    public float rotationZ;
    public float rotationW;

    public float scaleX;
    public float scaleY;
    public float scaleZ;

    public bool isMoving;
    public bool hasArrived;
    public bool isLeaving;
    public bool isReturning;

    public float walkTimer;
    public float currentSpeed;
    public int lastStepIndex;

    public float settleTimer;
    public float settleAmount;
    public float hopTimer;
    public float talkSquash;
}

[Serializable]
public class ItemSaveData
{
    public string customerID;
    public string itemID;
    public string instanceID;

    public float posX;
    public float posY;
    public float posZ;

    public float rotX;
    public float rotY;
    public float rotZ;
    public float rotW;

    public float scaleX;
    public float scaleY;
    public float scaleZ;

    public bool wasOnWorkArea;
    public bool curseFound;
    public bool puzzleSolved;

    public float velocityX;
    public float velocityY;
    public float angularVelocity;
    public float gravityScale;
    public int bodyType;
}
