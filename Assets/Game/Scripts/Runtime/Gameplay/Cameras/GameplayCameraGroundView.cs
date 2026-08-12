using Unity.Cinemachine;
using UnityEngine;

namespace Game.Gameplay.Cameras
{
    /// <summary>
    /// Projects the camera view onto the ground plane (Y=0).
    /// Uses the active Cinemachine pose when available so rays match the rendered view.
    /// </summary>
    public static class GameplayCameraGroundView
    {
        static readonly Vector2[] s_viewportCorners =
        {
            new(0f, 0f),
            new(1f, 0f),
            new(1f, 1f),
            new(0f, 1f),
        };

        static readonly Vector2[] s_viewportEdgeMids =
        {
            new(0f, 0.5f),
            new(1f, 0.5f),
            new(0.5f, 0f),
            new(0.5f, 1f),
        };

        static readonly Plane s_groundPlane = new(Vector3.up, 0f);

        /// <summary>
        /// Fills <paramref name="corners"/> with world positions where viewport corners hit Y=0.
        /// Order: bottom-left, bottom-right, top-right, top-left (viewport space).
        /// </summary>
        public static bool TryGetGroundFrustumCorners(Camera camera, Vector3[] corners)
        {
            if (camera == null || corners == null || corners.Length < 4)
            {
                return false;
            }

            SyncOutputCameraFromCinemachine(camera);

            for (var i = 0; i < 4; i++)
            {
                if (!TryRayToGround(camera.ViewportPointToRay(s_viewportCorners[i]), out corners[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Axis-aligned minimap rectangle from screen <b>edge midpoints</b> (not frustum corners).
        /// Corner AABB overestimates left/right because the far edge of a pitched frustum is wider.
        /// Order: south-west, south-east, north-east, north-west.
        /// </summary>
        public static bool TryGetGroundViewRectangleCorners(Camera camera, Vector3[] corners)
        {
            if (camera == null || corners == null || corners.Length < 4)
            {
                return false;
            }

            SyncOutputCameraFromCinemachine(camera);

            if (!TryRayToGround(camera.ViewportPointToRay(s_viewportEdgeMids[0]), out var left)
                || !TryRayToGround(camera.ViewportPointToRay(s_viewportEdgeMids[1]), out var right)
                || !TryRayToGround(camera.ViewportPointToRay(s_viewportEdgeMids[2]), out var bottom)
                || !TryRayToGround(camera.ViewportPointToRay(s_viewportEdgeMids[3]), out var top))
            {
                return false;
            }

            var minX = Mathf.Min(Mathf.Min(left.x, right.x), Mathf.Min(bottom.x, top.x));
            var maxX = Mathf.Max(Mathf.Max(left.x, right.x), Mathf.Max(bottom.x, top.x));
            var minZ = Mathf.Min(Mathf.Min(left.z, right.z), Mathf.Min(bottom.z, top.z));
            var maxZ = Mathf.Max(Mathf.Max(left.z, right.z), Mathf.Max(bottom.z, top.z));

            corners[0] = new Vector3(minX, 0f, minZ);
            corners[1] = new Vector3(maxX, 0f, minZ);
            corners[2] = new Vector3(maxX, 0f, maxZ);
            corners[3] = new Vector3(minX, 0f, maxZ);
            return true;
        }

        public static void BuildAxisAlignedRectangle(Vector3[] sourceCorners, Vector3[] rectangleCorners)
        {
            var minX = sourceCorners[0].x;
            var maxX = sourceCorners[0].x;
            var minZ = sourceCorners[0].z;
            var maxZ = sourceCorners[0].z;

            for (var i = 1; i < 4; i++)
            {
                minX = Mathf.Min(minX, sourceCorners[i].x);
                maxX = Mathf.Max(maxX, sourceCorners[i].x);
                minZ = Mathf.Min(minZ, sourceCorners[i].z);
                maxZ = Mathf.Max(maxZ, sourceCorners[i].z);
            }

            rectangleCorners[0] = new Vector3(minX, 0f, minZ);
            rectangleCorners[1] = new Vector3(maxX, 0f, minZ);
            rectangleCorners[2] = new Vector3(maxX, 0f, maxZ);
            rectangleCorners[3] = new Vector3(minX, 0f, maxZ);
        }

        public static bool TryProjectViewportToGround(
            Vector3 cameraPosition,
            Quaternion cameraRotation,
            float verticalFieldOfViewDegrees,
            float aspect,
            Vector3[] corners)
        {
            if (corners == null || corners.Length < 4 || aspect < 0.01f)
            {
                return false;
            }

            for (var i = 0; i < 4; i++)
            {
                if (!TryViewportPointToGround(
                        cameraPosition,
                        cameraRotation,
                        verticalFieldOfViewDegrees,
                        aspect,
                        s_viewportCorners[i],
                        out corners[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool TryViewportPointToGround(
            Vector3 cameraPosition,
            Quaternion cameraRotation,
            float verticalFieldOfViewDegrees,
            float aspect,
            Vector2 viewportPoint,
            out Vector3 hit)
        {
            var halfFovRad = Mathf.Deg2Rad * Mathf.Max(1f, verticalFieldOfViewDegrees) * 0.5f;
            var verticalTan = Mathf.Tan(halfFovRad);
            var horizontalTan = verticalTan * Mathf.Max(0.01f, aspect);
            var localDirection = new Vector3(
                (viewportPoint.x * 2f - 1f) * horizontalTan,
                (viewportPoint.y * 2f - 1f) * verticalTan,
                1f);
            var worldDirection = cameraRotation * localDirection.normalized;
            return TryRayToGround(new Ray(cameraPosition, worldDirection), out hit);
        }

        public static bool TryRayToGround(Ray ray, out Vector3 hit)
        {
            if (!s_groundPlane.Raycast(ray, out var distance) || distance < 0f)
            {
                hit = default;
                return false;
            }

            hit = ray.GetPoint(distance);
            return true;
        }

        static void SyncOutputCameraFromCinemachine(Camera camera)
        {
            if (!camera.TryGetComponent<CinemachineBrain>(out var brain))
            {
                return;
            }

            var vcam = brain.ActiveVirtualCamera;
            if (vcam == null)
            {
                return;
            }

            var state = vcam.State;
            camera.transform.SetPositionAndRotation(state.GetFinalPosition(), state.GetFinalOrientation());
            camera.fieldOfView = state.Lens.FieldOfView;
        }
    }
}
