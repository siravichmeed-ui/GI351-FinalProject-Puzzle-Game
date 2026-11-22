using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// มินิเกมที่ใช้กับ PuzzleManager ต้องมี 2 ฟังก์ชันนี้
/// (MagicCirclePuzzle และ LightBeamPuzzle ทำไว้แล้ว ตัวใหม่แค่เพิ่ม ", IPuzzle" ต่อท้ายชื่อคลาส)
/// </summary>
public interface IPuzzle
{
    void Open(Action<bool> callback);
    void GiveUp();
}

/// <summary>
/// ตัวทดสอบมินิเกม: ลาก window ของมินิเกมจากซีนใส่ลิสต์ → เลือกใน dropdown ว่าจะรันตัวไหน → กด Play
/// วางบน object ที่เปิดอยู่เสมอ (ห้ามอยู่ใต้ window ของมินิเกม)
/// </summary>
public class PuzzleManager : MonoBehaviour
{
    [SerializeField] private List<GameObject> windows = new List<GameObject>(); // ลาก window ของแต่ละมินิเกมใส่
    [SerializeField] private int selectedIndex = 0;                             // เลือกจาก dropdown
    [SerializeField] private bool runOnStart = true;                            // กด Play แล้วรันตัวที่เลือกเลย
    [SerializeField] private bool showButtons = true;                           // ปุ่มเลือก/เล่นอีกรอบ มุมซ้ายบนตอนเล่น

    [Header("ถ้าลาก Prefab จาก Project ใส่ลิสต์: Canvas ที่จะสร้างสำเนาหน้าต่าง (ไม่ใส่ = Canvas ที่ object นี้อยู่ใต้)")]
    [SerializeField] private Transform canvas;

    private bool running;

    private void Start()
    {
        // ปิดหน้าต่างมินิเกมทุกตัวก่อน เปิดเฉพาะตัวที่สั่งรัน (ไม่ต้องไปปิดเองในซีน)
        foreach (var w in windows) if (w != null && w.scene.IsValid()) w.SetActive(false); // ไม่แตะไฟล์ Prefab
        if (runOnStart) Run(selectedIndex);
    }

    private void OnValidate()
    {
        selectedIndex = windows != null && windows.Count > 0 ? Mathf.Clamp(selectedIndex, 0, windows.Count - 1) : 0;
    }

    /// <summary>รันตามลำดับในลิสต์ (0 = ตัวแรก) ผูกกับปุ่มได้</summary>
    public void Run(int index)
    {
        if (running) return;
        if (index < 0 || index >= windows.Count || windows[index] == null)
        {
            Debug.LogError("PuzzleManager: ช่องที่ " + index + " ว่าง ให้ลาก window ของมินิเกมใส่ลิสต์ Windows");
            return;
        }

        GameObject target = windows[index];
        GameObject spawned = null;
        if (!target.scene.IsValid())
        {
            // เป็นไฟล์ Prefab ใน Project -> สร้างสำเนาใต้ Canvas แล้วเล่นตัวสำเนา (ลบทิ้งตอนจบ)
            Transform parent = canvas;
            if (parent == null)
            {
                Canvas c = GetComponentInParent<Canvas>();
                if (c != null) parent = c.transform;
            }
            if (parent == null)
            {
                Debug.LogError("PuzzleManager: ลาก Prefab ใส่ลิสต์ไว้ แต่ไม่พบ Canvas ให้ลาก Canvas ใส่ช่อง Canvas");
                return;
            }
            spawned = Instantiate(target, parent, false);
            target = spawned;
        }

        IPuzzle puzzle = Find(target);
        if (puzzle == null)
        {
            Debug.LogError("PuzzleManager: ไม่พบสคริปต์มินิเกมใต้ '" + windows[index].name + "' ต้องลาก window ที่มีมินิเกมอยู่ข้างใน");
            if (spawned != null) Destroy(spawned);
            return;
        }

        running = true;
        string label = Describe(windows[index]);
        puzzle.Open(success =>
        {
            running = false;
            if (spawned != null) Destroy(spawned);
            Debug.Log("PuzzleManager: " + label + " → " + (success ? "ชนะ" : "ยอมแพ้/ปิด"));
        });
    }

    /// <summary>รันตัวที่เลือกไว้ใน dropdown</summary>
    public void RunSelected() => Run(selectedIndex);

    // ---------- ช่วย ----------
    private static IPuzzle Find(GameObject go)
    {
        foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb is IPuzzle p) return p;
        return null;
    }

    public static string Describe(GameObject go)
    {
        if (go == null) return "(ว่าง)";
        IPuzzle p = Find(go);
        return p == null ? go.name + " (ไม่พบมินิเกม)" : go.name + " [" + p.GetType().Name + "]";
    }

    private void OnGUI()
    {
        if (!showButtons || running) return;

        const float w = 300f, h = 36f, pad = 6f;
        GUI.Box(new Rect(10f, 10f, w + 20f, 34f + windows.Count * (h + pad)), "Puzzle Manager");
        float y = 38f;
        for (int i = 0; i < windows.Count; i++)
        {
            if (GUI.Button(new Rect(20f, y, w, h), Describe(windows[i]))) Run(i);
            y += h + pad;
        }
    }
}

#if UNITY_EDITOR
// ทำให้ช่องเลือกมินิเกมใน Inspector เป็น dropdown แสดงชื่อ
[CustomEditor(typeof(PuzzleManager))]
public class PuzzleManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty list = serializedObject.FindProperty("windows");
        SerializedProperty index = serializedObject.FindProperty("selectedIndex");
        int n = list.arraySize;

        if (n == 0)
        {
            EditorGUILayout.HelpBox("ลาก window ของมินิเกมจาก Hierarchy ใส่ลิสต์ Windows ข้างล่างก่อน แล้วจะเลือกจาก dropdown ได้", MessageType.Info);
        }
        else
        {
            string[] names = new string[n];
            for (int i = 0; i < n; i++)
            {
                GameObject go = list.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                names[i] = i + ": " + PuzzleManager.Describe(go);
            }
            index.intValue = EditorGUILayout.Popup("เลือกมินิเกมที่จะรัน", Mathf.Clamp(index.intValue, 0, n - 1), names);
        }

        DrawPropertiesExcluding(serializedObject, "m_Script", "selectedIndex");
        serializedObject.ApplyModifiedProperties();
    }
}
#endif