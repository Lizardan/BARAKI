#if UNITY_EDITOR
using Game.UI.Controllers;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Editor
{
    [CustomEditor(typeof(BootstrapLoadingController))]
    public sealed class BootstrapLoadingControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var controller = (BootstrapLoadingController)target;
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Предпросмотр UI", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!TryGetDocumentRoot(controller, out _)))
            {
                if (GUILayout.Button("Применить предпросмотр"))
                {
                    controller.ApplyEditorPreview();
                    SceneView.RepaintAll();
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Откройте сцену Bootstrap и Game View. Выберите Preview Mode " +
                    "(Loading / UpdateAvailable / Downloading) и нажмите «Применить предпросмотр».",
                    MessageType.Info);
            }
        }

        private static bool TryGetDocumentRoot(BootstrapLoadingController controller, out VisualElement root)
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
