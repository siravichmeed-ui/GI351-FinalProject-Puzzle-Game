
using System;
using System.Collections.Generic;

[Serializable]
public class ShopSaveData
{
    public int version = 1;
    public List<ItemSaveData> items = new List<ItemSaveData>();
    public CustomerMovementSaveData customerMovement;
    public CustomerSaveData customer;
}
[System.Serializable]
public class CustomerSaveData
{
    public string customerID;
    public string itemID;
    public bool orderAccepted;
    public bool hasDelivered;
    public string deliveredItemInstanceID;
}
[System.Serializable]
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


