#if UNITY_EDITOR
using Game.UI.Controllers;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Editor
{
    [CustomEditor(typeof(MainMenuController))]
    public sealed class MainMenuControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var controller = (MainMenuController)target;
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Предпросмотр UI", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!TryGetDocumentRoot(controller, out _)))
            {
                if (GUILayout.Button("Применить предпросмотр загрузки"))
                {
                    controller.ApplyEditorHubLoadingPreview();
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("Применить предпросмотр друзей"))
                {
                    controller.ApplyEditorFriendsHubPreview();
                    SceneView.RepaintAll();
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "В Edit Mode откройте Game View (сцена MainMenu), включите Preview Friends Hub " +
                    "и нажмите «Применить предпросмотр друзей». Переключайте вкладку Friends/Invites в Inspector.",
                    MessageType.Info);
            }
        }

        private static bool TryGetDocumentRoot(MainMenuController controller, out VisualElement root)
        {
            root = null;
            if (controller == null)
            {
                return false;
            }

            var document = controller.GetComponent<UIDocument>();
            if (document == null)
            {
                return false;
            }

            root = document.rootVisualElement;
            return root != null;
        }
    }
}
#endif
