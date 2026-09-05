using Game.Gameplay.Dev;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    /// <summary>Dev HUD on FacelessReview: three buttons switch Stand / Walk / Attack on every unit.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class FacelessReviewHudController : MonoBehaviour
    {
        [SerializeField] UIDocument _uiDocument;

        VisualElement _root;
        Button _idleButton;
        Button _runButton;
        Button _attackButton;
        IVisualElementScheduledItem _bindSchedule;

        void Awake()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }
        }

        void OnEnable()
        {
            Bind();
            _bindSchedule = _root?.schedule.Execute(Bind).StartingIn(0);
        }

        void OnDisable()
        {
            _bindSchedule?.Pause();
            _bindSchedule = null;
            Unbind();
        }

        void Bind()
        {
            Unbind();
            _root = _uiDocument != null ? _uiDocument.rootVisualElement : null;
            if (_root == null)
            {
                return;
            }

            _idleButton = _root.Q<Button>("IdleButton");
            _runButton = _root.Q<Button>("RunButton");
            _attackButton = _root.Q<Button>("AttackButton");
            if (_idleButton != null)
            {
                _idleButton.clicked += OnIdle;
                _idleButton.pickingMode = PickingMode.Position;
            }

            if (_runButton != null)
            {
                _runButton.clicked += OnRun;
                _runButton.pickingMode = PickingMode.Position;
            }

            if (_attackButton != null)
            {
                _attackButton.clicked += OnAttack;
                _attackButton.pickingMode = PickingMode.Position;
            }

            _root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            Refresh();
        }

        void Update() => Refresh();

        void Unbind()
        {
            if (_idleButton != null) _idleButton.clicked -= OnIdle;
            if (_runButton != null) _runButton.clicked -= OnRun;
            if (_attackButton != null) _attackButton.clicked -= OnAttack;
            _root?.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            _idleButton = null;
            _runButton = null;
            _attackButton = null;
        }

        void OnIdle() => SetPose(FacelessReviewPlayback.PoseStand);

        void OnRun() => SetPose(FacelessReviewPlayback.PoseWalk);

        void OnAttack() => SetPose(FacelessReviewPlayback.PoseAttack);

        void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Alpha1 || evt.keyCode == KeyCode.Keypad1)
            {
                SetPose(FacelessReviewPlayback.PoseStand);
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Alpha2 || evt.keyCode == KeyCode.Keypad2)
            {
                SetPose(FacelessReviewPlayback.PoseWalk);
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Alpha3 || evt.keyCode == KeyCode.Keypad3)
            {
                SetPose(FacelessReviewPlayback.PoseAttack);
                evt.StopPropagation();
            }
        }

        void SetPose(int pose)
        {
            var playback = FacelessReviewPlayback.Current;
            if (playback == null)
            {
                playback = FindAnyObjectByType<FacelessReviewPlayback>();
            }

            playback?.SetPose(pose);
            Refresh();
        }

        void Refresh()
        {
            var pose = FacelessReviewPlayback.Current != null
                ? FacelessReviewPlayback.Current.Pose
                : FacelessReviewPlayback.PoseStand;
            Toggle(_idleButton, pose == FacelessReviewPlayback.PoseStand);
            Toggle(_runButton, pose == FacelessReviewPlayback.PoseWalk);
            Toggle(_attackButton, pose == FacelessReviewPlayback.PoseAttack);
        }

        static void Toggle(Button button, bool on)
        {
            if (button == null)
            {
                return;
            }

            button.EnableInClassList("ui-btn--primary", on);
        }
    }
}
