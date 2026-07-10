using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Central game lifecycle coordinator. Persists across scenes.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private static GameManager s_instance;

        [SerializeField] private bool _persistAcrossScenes = true;

        public static GameManager Instance => s_instance;

        public static event Action OnInitialized;

        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;

            if (_persistAcrossScenes)
            {
                if (transform.parent != null)
                {
                    transform.SetParent(null);
                }

                DontDestroyOnLoad(gameObject);
            }

            Initialize();
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        private void Initialize()
        {
            IsInitialized = true;
            OnInitialized?.Invoke();
        }
    }
}
