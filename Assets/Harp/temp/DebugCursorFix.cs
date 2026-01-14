using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugCursorFix : MonoBehaviour
{
    [Header("Cursor Settings")]
    public bool lockCursor = true;

    void Start()
    {
        UpdateCursorLock();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            lockCursor = !lockCursor;
            UpdateCursorLock();
        }
    }

    private void UpdateCursorLock()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
