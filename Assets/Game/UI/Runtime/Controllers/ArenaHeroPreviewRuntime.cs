using System;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    /// <summary>
    /// Idle 3D previews of the local player's arena fighters, for the arena pick overlay. Mirrors
    /// <see cref="HeroOrderPreviewRuntime"/> (one render-to-texture camera per hero, Stand idle
    /// clip each frame) but without drag/order — each preview is a static idle hero the player
    /// clicks to pick. The titan arena shows the single titan instead of the three heroes.
    /// Only renders while the pick overlay is visible; tears everything down on disable/destroy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArenaHeroPreviewRuntime : MonoBehaviour
    {
        private const int HeroPreviewLayer = 9;
        private const int PreviewResolution = 256;
        private const float PreviewSlotSpacingFactor = 6f;

        private sealed class HeroPreview
        {
            public int HeroSlot;
            public bool IsTitan;
            public RenderTexture RenderTexture;
            public GameObject CameraObject;
            public Camera Camera;
            public GameObject Hero;
            public Animator Animator;
            public UnitCombatAnimatorPlayback Playback;
        }

        private HeroPreview[] _previews = Array.Empty<HeroPreview>();
        private VisualElement[] _targets = Array.Empty<VisualElement>();
        private string _raceId;
        private int _pick1;
        private int _pick2;
        private bool _configured;
        private bool _visible;

        /// <summary>
        /// Binds render targets one-to-one with the hero cards. <paramref name="heroSlots"/> carries
        /// the actual hero slot per card; a value &lt;= 0 means the titan. Reconfigures the whole
        /// set only when race/bonus or the slot count changes.
        /// </summary>
        public void Configure(VisualElement[] targets, int[] heroSlots, string raceId, int pick1, int pick2)
        {
            _targets = targets ?? Array.Empty<VisualElement>();
            var targetCount = Mathf.Min(_targets.Length, heroSlots?.Length ?? 0);
            var configChanged = !_configured
                || _raceId != raceId
                || _pick1 != pick1
                || _pick2 != pick2
                || _previews.Length != targetCount;

            if (configChanged)
            {
                Teardown();
                _raceId = raceId;
                _pick1 = pick1;
                _pick2 = pick2;
                _previews = new HeroPreview[targetCount];
                for (var i = 0; i < targetCount; i++)
                {
                    _previews[i] = CreatePreview(_targets[i], heroSlots[i], raceId, i);
                }

                _configured = true;
            }

            ApplyTargets();
        }

        public void SetVisible(bool visible)
        {
            if (!_configured || _visible == visible)
            {
                return;
            }

            _visible = visible;
            foreach (var preview in _previews)
            {
                if (preview?.CameraObject != null)
                {
                    preview.CameraObject.SetActive(visible);
                }
            }
        }

        private void Update()
        {
            if (!_visible || _previews.Length == 0)
            {
                return;
            }

            foreach (var preview in _previews)
            {
                if (preview == null || preview.Animator == null)
                {
                    continue;
                }

                UnitCombatAnimatorDriver.TickStand(preview.Animator, preview.Playback);
            }
        }

        private void OnDisable()
        {
            if (_visible)
            {
                _visible = false;
                foreach (var preview in _previews)
                {
                    if (preview?.CameraObject != null)
                    {
                        preview.CameraObject.SetActive(false);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            Teardown();
        }

        private HeroPreview CreatePreview(VisualElement target, int heroSlot, string raceId, int index)
        {
            var isTitan = heroSlot <= 0;
            var preview = new HeroPreview { HeroSlot = heroSlot, IsTitan = isTitan };

            preview.RenderTexture = new RenderTexture(
                PreviewResolution,
                PreviewResolution,
                24,
                RenderTextureFormat.ARGB32);
            preview.RenderTexture.Create();

            if (target != null)
            {
                target.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(preview.RenderTexture));
            }

            preview.CameraObject = new GameObject($"ArenaPickPreviewCam{heroSlot}");
            preview.Camera = preview.CameraObject.AddComponent<Camera>();
            preview.Camera.orthographic = true;
            preview.Camera.nearClipPlane = 0.05f;
            preview.Camera.farClipPlane = 200f;
            preview.Camera.cullingMask = 1 << HeroPreviewLayer;
            preview.Camera.clearFlags = CameraClearFlags.SolidColor;
            preview.Camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            preview.Camera.targetTexture = preview.RenderTexture;
            preview.CameraObject.transform.SetParent(transform, worldPositionStays: false);
            preview.CameraObject.transform.localPosition = Vector3.zero;
            preview.CameraObject.SetActive(false);

            if (TrySpawnHero(raceId, heroSlot, out preview.Hero) && preview.Hero != null)
            {
                preview.Animator = preview.Hero.GetComponentInChildren<Animator>();
                preview.Playback = new UnitCombatAnimatorPlayback();
                SetLayerRecursively(preview.Hero.transform, HeroPreviewLayer);

                var extent = GetHeroExtent(preview.Hero);
                preview.Hero.transform.localPosition =
                    new Vector3(index * extent * PreviewSlotSpacingFactor, 0f, 0f);

                FrameHero(preview.Camera, preview.Hero);
                OrientHeroToCamera(preview.Hero, preview.Camera);
                FrameHero(preview.Camera, preview.Hero);
                if (preview.Animator != null)
                {
                    UnitCombatAnimatorDriver.TickStand(preview.Animator, preview.Playback);
                }
            }

            return preview;
        }

        private bool TrySpawnHero(string raceId, int heroSlot, out GameObject hero)
        {
            hero = null;
            var catalog = ResolveCatalog();
            if (catalog == null)
            {
                return false;
            }

            UnitRole role;
            int prefabSlot;
            int bonusSlot;
            if (heroSlot <= 0)
            {
                role = UnitRole.Titan;
                prefabSlot = 0;
                bonusSlot = BonusKitRules.EffectiveBonusSlotForTitan(raceId, _pick1, _pick2);
            }
            else
            {
                role = UnitRole.Hero;
                prefabSlot = heroSlot;
                bonusSlot = BonusKitRules.EffectiveBonusSlotForHero(raceId, _pick1, _pick2, heroSlot);
            }

            if (!catalog.TryGetPrefab(raceId, role, prefabSlot, bonusSlot, out var prefab) || prefab == null)
            {
                return false;
            }

            hero = Instantiate(prefab);
            hero.name = $"ArenaPickPreviewInst{heroSlot}";
            hero.transform.SetParent(transform, worldPositionStays: false);
            hero.transform.localPosition = Vector3.zero;
            return true;
        }

        /// <summary>
        /// Turns the spawned hero so its visual face points at the preview camera. Heroes carry an
        /// authored base yaw in their prefab root (Human: identity, Faceless: 270°), so the
        /// camera-facing turn is composed on top of that base rotation. Rerun <see cref="FrameHero"/>
        /// afterwards to reframe the rotated bounds.
        /// </summary>
        private static void OrientHeroToCamera(GameObject hero, Camera camera)
        {
            if (hero == null || camera == null)
            {
                return;
            }

            var baseRotation = hero.transform.localRotation;
            var bounds = GetHeroBounds(hero);
            var toCamera = camera.transform.position - bounds.center;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.0001f)
            {
                hero.transform.rotation =
                    Quaternion.LookRotation(toCamera.normalized, Vector3.up) * baseRotation;
            }
        }

        private static void FrameHero(Camera camera, GameObject hero)
        {
            if (camera == null || hero == null)
            {
                return;
            }

            var bounds = GetHeroBounds(hero);
            var center = bounds.center;
            var extent = GetHeroExtent(bounds);

            camera.transform.position = center + new Vector3(0f, extent * 0.2f, -extent * 2.4f);
            camera.transform.LookAt(center);
            camera.orthographicSize = extent * 1.15f;
        }

        private static float GetHeroExtent(GameObject hero)
        {
            var bounds = GetHeroBounds(hero);
            return Mathf.Max(bounds.extents.y, bounds.extents.x * 0.75f, 1f);
        }

        private static float GetHeroExtent(Bounds bounds)
        {
            return Mathf.Max(bounds.extents.y, bounds.extents.x * 0.75f, 1f);
        }

        /// <summary>
        /// Combined world AABB across every renderer of the hero (humans split the body into many
        /// pieces — quiver, shields, head… — so a single renderer's bounds would frame an
        /// off-center fragment).
        /// </summary>
        private static Bounds GetHeroBounds(GameObject hero)
        {
            Bounds result = new Bounds(hero.transform.position, Vector3.zero);
            var hasRenderer = false;
            foreach (var renderer in hero.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                if (!hasRenderer)
                {
                    result = new Bounds(renderer.bounds.center, renderer.bounds.size);
                    hasRenderer = true;
                }
                else
                {
                    result.Encapsulate(renderer.bounds);
                }
            }

            return result;
        }

        private void ApplyTargets()
        {
            for (var i = 0; i < _previews.Length; i++)
            {
                if (_previews[i] == null || i >= _targets.Length)
                {
                    continue;
                }

                if (_previews[i].RenderTexture != null)
                {
                    _targets[i].style.backgroundImage =
                        new StyleBackground(Background.FromRenderTexture(_previews[i].RenderTexture));
                }
            }
        }

        private UnitVisualCatalog ResolveCatalog()
        {
            var presenter = MatchCombatPresenter.Current;
            if (presenter != null && presenter.VisualCatalog != null)
            {
                return presenter.VisualCatalog;
            }

            if (MatchRuntime.Current != null && MatchRuntime.Current.Controller != null
                && MatchRuntime.Current.Controller.UnitVisualCatalog != null)
            {
                return MatchRuntime.Current.Controller.UnitVisualCatalog;
            }

            return null;
        }

        private void Teardown()
        {
            foreach (var preview in _previews)
            {
                if (preview == null)
                {
                    continue;
                }

                if (preview.RenderTexture != null)
                {
                    preview.RenderTexture.Release();
                    Destroy(preview.RenderTexture);
                }

                if (preview.CameraObject != null)
                {
                    Destroy(preview.CameraObject);
                }

                if (preview.Hero != null)
                {
                    Destroy(preview.Hero);
                }
            }

            _previews = Array.Empty<HeroPreview>();
            _configured = false;
            _visible = false;
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (var i = 0; i < root.childCount; i++)
            {
                SetLayerRecursively(root.GetChild(i), layer);
            }
        }
    }
}
