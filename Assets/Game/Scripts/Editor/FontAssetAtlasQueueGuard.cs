#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine.TextCore.Text;

namespace Game.Editor
{
    /// <summary>
    /// UITK/ATG can leave destroyed atlas textures in FontAsset's static apply-queue
    /// after Play Mode or Dynamic atlas churn — that yields MissingReferenceException
    /// on Texture2D.Apply and blank UI text. Clear the queues when leaving Play Mode.
    /// </summary>
    [InitializeOnLoad]
    public static class FontAssetAtlasQueueGuard
    {
        static FontAssetAtlasQueueGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            ClearAtlasQueues();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                ClearAtlasQueues();
            }
        }

        static void ClearAtlasQueues()
        {
            var type = typeof(FontAsset);
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;

            ClearListAndLookup(type, "k_FontAssets_AtlasTexturesUpdateQueue",
                "k_FontAssets_AtlasTexturesUpdateQueueLookup", flags);
            ClearListAndLookup(type, "k_FontAssets_FontFeaturesUpdateQueue",
                "k_FontAssets_FontFeaturesUpdateQueueLookup", flags);
            ClearListAndLookup(type, "k_FontAssets_KerningUpdateQueue",
                "k_FontAssets_KerningUpdateQueueLookup", flags);
        }

        static void ClearListAndLookup(System.Type type, string listName, string lookupName, BindingFlags flags)
        {
            var listField = type.GetField(listName, flags);
            var lookupField = type.GetField(lookupName, flags);
            if (listField?.GetValue(null) is IList list)
            {
                list.Clear();
            }

            lookupField?.GetValue(null)?.GetType().GetMethod("Clear")?.Invoke(lookupField.GetValue(null), null);
        }
    }
}
#endif
