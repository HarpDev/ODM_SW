using SwiftKraft.Gameplay.Motors;
using SwiftKraft.Utils;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SwiftKraft.Gameplay.Common.FPS.Demo
{
    public class MenuOpen : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        public static event Action<bool> UpdateOpen;

        public GameObject MenuObject;

        BooleanLock.Lock cursor;

        private void Awake() => cursor = CursorManager.Unlocked.AddLock();

        private void Start() => SetMenu(false);

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SetMenu(!MenuObject.activeSelf);
            }
        }

        private void OnDestroy() => cursor.Dispose();

        public void SetMenu(bool active)
        {
            MenuObject.SetActive(active);
            cursor.Active = !CursorManager.DefaultUnlocked && active;
            IsOpen = active;
            UpdateOpen?.Invoke(IsOpen);

            // Pause or resume the game based on menu state
            Time.timeScale = active ? 0f : 1f;
        }

        public void QuitGame()
        {
            Application.Quit();
        }

        public void ResetScene()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void BackToMain()
        {
            SceneManager.LoadScene(0);
        }
    }
}