using Game.Gameplay.Match.Selection;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Ruins keep the TT construction-stage foundation mesh (<c>Foundation</c> child) and hide
    /// the upper <c>Model</c>. Same path for combat kills and main smite — no procedural pad.
    /// </summary>
    public static class BuildingRuinsVisual
    {
        public const string FoundationName = "Foundation";
        public const string ModelName = "Model";

        /// <summary>
        /// For greybox/primitive markers only: flat box under the model (not a cylinder).
        /// TT prefabs already ship an inactive mesh Foundation from construction *_0.
        /// </summary>
        public static void EnsurePrimitiveFoundation(Transform buildingRoot, string buildingId)
        {
            if (buildingRoot == null || buildingRoot.Find(FoundationName) != null)
            {
                return;
            }

            var diameter = MatchPickFootprint.GetBuildingDiameter(buildingId, margin: 1f);
            var size = Mathf.Max(1.5f, diameter * 0.72f);
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = FoundationName;
            pad.transform.SetParent(buildingRoot, false);
            pad.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            pad.transform.localRotation = Quaternion.identity;
            pad.transform.localScale = new Vector3(size, 0.08f, size);
            pad.transform.SetAsFirstSibling();
            pad.SetActive(false);

            var collider = pad.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }
        }

        /// <summary>
        /// Inverse of <see cref="ApplyRuins"/>: show the upper Model, hide Foundation.
        /// Used by BARAKI Studio to loop the building-smite preview.
        /// </summary>
        public static void RestoreIntact(Transform buildingRoot)
        {
            if (buildingRoot == null)
            {
                return;
            }

            var model = buildingRoot.Find(ModelName);
            if (model != null)
            {
                model.gameObject.SetActive(true);
            }

            var foundation = buildingRoot.Find(FoundationName);
            if (foundation != null)
            {
                foundation.gameObject.SetActive(false);
            }

            if (model != null)
            {
                return;
            }

            var renderers = buildingRoot.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = true;
                }
            }
        }

        /// <summary>
        /// Hides the upper structure, shows the mesh foundation. Root stays active for FX.
        /// Destroys leftover procedural cylinder pads named Foundation (legacy).
        /// </summary>
        public static void ApplyRuins(Transform buildingRoot, string buildingId)
        {
            if (buildingRoot == null)
            {
                return;
            }

            RemoveLegacyCylinderFoundation(buildingRoot);

            var model = buildingRoot.Find(ModelName);
            if (model != null)
            {
                model.gameObject.SetActive(false);
            }

            var foundation = buildingRoot.Find(FoundationName);
            if (foundation != null)
            {
                foundation.gameObject.SetActive(true);
                return;
            }

            if (model != null)
            {
                return;
            }

            // Legacy markers without Model/Foundation: hide everything.
            var renderers = buildingRoot.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = false;
                }
            }
        }

        static void RemoveLegacyCylinderFoundation(Transform buildingRoot)
        {
            var foundation = buildingRoot.Find(FoundationName);
            if (foundation == null)
            {
                return;
            }

            var filter = foundation.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                return;
            }

            // Unity cylinder primitive mesh is named "Cylinder".
            if (!string.Equals(filter.sharedMesh.name, "Cylinder", System.StringComparison.Ordinal))
            {
                return;
            }

            // Detach/rename first so the same-frame Find(Foundation) won't revive a dying pad.
            foundation.name = "__LegacyCylinderPad";
            foundation.SetParent(null, true);
            if (Application.isPlaying)
            {
                Object.Destroy(foundation.gameObject);
            }
            else
            {
                Object.DestroyImmediate(foundation.gameObject);
            }
        }
    }
}
