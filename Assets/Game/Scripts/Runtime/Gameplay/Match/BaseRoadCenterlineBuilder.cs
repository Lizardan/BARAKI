using System.Collections.Generic;

using Game.Core;

using UnityEngine;



namespace Game.Gameplay.Match

{

    /// <summary>Base T-road centerlines for flank lane paths (+Z = toward map center, −Z = toward map edge).</summary>

    public static class BaseRoadCenterlineBuilder

    {

        const float LocalEpsilon = 0.05f;



        public readonly struct BaseRoadGeometry

        {

            public BaseRoadGeometry(

                float extentX,

                float extentZ,

                float halfWidth,

                float turnRadius,

                float filletZ,

                float centerStripStart)

            {

                ExtentX = extentX;

                ExtentZ = extentZ;

                HalfWidth = halfWidth;

                TurnRadius = turnRadius;

                FilletZ = filletZ;

                CenterStripStart = centerStripStart;

            }



            public float ExtentX { get; }

            public float ExtentZ { get; }

            public float HalfWidth { get; }

            public float TurnRadius { get; }

            public float FilletZ { get; }

            public float CenterStripStart { get; }

        }



        public static bool IsSideBarracks(string barracksId) =>

            barracksId == GameIds.Buildings.BarracksLeft

            || barracksId == GameIds.Buildings.BarracksRight;



        public static BaseRoadGeometry GetBaseRoadGeometry(PlayerSlotLayout slot)

        {

            var offsets = slot.BuildingLocalOffsets;

            var extentX = Mathf.Max(

                Mathf.Abs(offsets[GameIds.Buildings.BarracksLeft].x),

                Mathf.Abs(offsets[GameIds.Buildings.BarracksRight].x));

            var extentZ = offsets[GameIds.Buildings.BarracksCenter].z;

            var halfWidth = MatchArenaGreyboxBuilder.RoadWidth * 0.5f;

            var turnRadius = Mathf.Min(RoadJunctionBuilder.BaseTurnRadius, extentZ * 0.25f);

            var filletZ = turnRadius;

            var centerStripStart = filletZ + turnRadius;

            return new BaseRoadGeometry(extentX, extentZ, halfWidth, turnRadius, filletZ, centerStripStart);

        }



        public static void AppendFlankExitFromSideBarracks(

            List<Vector3> points,

            PlayerSlotLayout slot,

            string barracksId,

            float halfSize)

        {

            if (!TryGetBarracksLocal(slot, barracksId, out var barracksLocal))

            {

                return;

            }



            var barracksWorld = slot.GetBuildingWorldPosition(barracksId);

            var joinWorld = N4RoadCenterlineBuilder.GetStripJoinPoint(barracksWorld, slot.BasePosition, halfSize);

            var joinLocal = WorldToLocalHorizontal(slot, joinWorld);

            AppendOutwardStripExit(points, slot, barracksLocal, joinLocal);



            N4RoadCenterlineBuilder.AppendUnique(points, joinWorld);

        }



        public static void AppendFlankEntryToSideBarracks(

            List<Vector3> points,

            PlayerSlotLayout slot,

            string barracksId,

            float halfSize)

        {

            if (!TryGetBarracksLocal(slot, barracksId, out var barracksLocal))

            {

                return;

            }



            var barracksWorld = slot.GetBuildingWorldPosition(barracksId);

            var joinWorld = N4RoadCenterlineBuilder.GetStripJoinPoint(barracksWorld, slot.BasePosition, halfSize);

            var joinLocal = WorldToLocalHorizontal(slot, joinWorld);



            N4RoadCenterlineBuilder.AppendUnique(points, joinWorld);



            AppendOutwardStripEntry(points, slot, joinLocal, barracksLocal);



            N4RoadCenterlineBuilder.AppendUnique(points, N4RoadCenterlineBuilder.WithHeight(barracksWorld));

        }



        /// <summary>Side barracks → perimeter join along the outer apron, away from base center.</summary>

        static void AppendOutwardStripExit(

            List<Vector3> points,

            PlayerSlotLayout slot,

            Vector3 barracksLocal,

            Vector3 joinLocal)

        {

            barracksLocal.y = 0f;

            joinLocal.y = 0f;

            AppendWorldFromLocal(points, slot, barracksLocal);

            var apron = new Vector3(joinLocal.x, 0f, barracksLocal.z);

            if ((apron - barracksLocal).sqrMagnitude > LocalEpsilon * LocalEpsilon)

            {

                AppendWorldFromLocal(points, slot, apron);

            }

            if (Mathf.Abs(joinLocal.z - barracksLocal.z) > LocalEpsilon)

            {

                AppendWorldFromLocal(points, slot, joinLocal);

            }

        }



        static void AppendOutwardStripEntry(

            List<Vector3> points,

            PlayerSlotLayout slot,

            Vector3 joinLocal,

            Vector3 barracksLocal)

        {

            barracksLocal.y = 0f;

            joinLocal.y = 0f;

            if (Mathf.Abs(joinLocal.z - barracksLocal.z) > LocalEpsilon)

            {

                AppendWorldFromLocal(points, slot, joinLocal);

            }

            var apron = new Vector3(joinLocal.x, 0f, barracksLocal.z);

            if ((apron - barracksLocal).sqrMagnitude > LocalEpsilon * LocalEpsilon)

            {

                AppendWorldFromLocal(points, slot, apron);

            }

            AppendWorldFromLocal(points, slot, barracksLocal);

        }



        static void AppendHorizontalStripExit(

            List<Vector3> points,

            PlayerSlotLayout slot,

            BaseRoadGeometry geometry,

            Vector3 barracksLocal,

            Vector3 joinLocal,

            bool isLeft)

        {

            var filletX = isLeft ? -geometry.HalfWidth : geometry.HalfWidth;

            var stripJoin = new Vector3(joinLocal.x, 0f, 0f);

            var filletCorner = new Vector3(filletX, 0f, geometry.FilletZ);



            if (CrossesFillet(barracksLocal.x, stripJoin.x, filletX, isLeft))

            {

                AppendWorldFromLocal(points, slot, new Vector3(barracksLocal.x, 0f, 0f));

                AppendWorldFromLocal(points, slot, new Vector3(filletX, 0f, 0f));

                AppendFilletTurn(

                    points,

                    slot,

                    filletCorner,

                    isLeft ? Vector3.right : Vector3.left,

                    Vector3.back,

                    geometry.TurnRadius);

                AppendWorldFromLocal(points, slot, new Vector3(filletX, 0f, joinLocal.z));

                if (Mathf.Abs(joinLocal.z) > LocalEpsilon)

                {

                    AppendWorldFromLocal(points, slot, new Vector3(stripJoin.x, 0f, joinLocal.z));

                }

            }

            else

            {

                AppendWorldFromLocal(points, slot, new Vector3(barracksLocal.x, 0f, 0f));

                if (ShouldPassFilletEntry(barracksLocal.x, stripJoin.x, filletX, isLeft))

                {

                    AppendWorldFromLocal(points, slot, new Vector3(filletX, 0f, 0f));

                }



                AppendWorldFromLocal(points, slot, stripJoin);

            }

        }



        static void AppendHorizontalStripEntry(

            List<Vector3> points,

            PlayerSlotLayout slot,

            BaseRoadGeometry geometry,

            Vector3 joinLocal,

            Vector3 barracksLocal,

            bool isLeft)

        {

            var filletX = isLeft ? -geometry.HalfWidth : geometry.HalfWidth;

            var stripJoin = new Vector3(joinLocal.x, 0f, 0f);

            var filletCorner = new Vector3(filletX, 0f, geometry.FilletZ);



            if (CrossesFillet(barracksLocal.x, stripJoin.x, filletX, isLeft))

            {

                if (Mathf.Abs(joinLocal.z) > LocalEpsilon)

                {

                    AppendWorldFromLocal(points, slot, new Vector3(stripJoin.x, 0f, joinLocal.z));

                }



                AppendWorldFromLocal(points, slot, new Vector3(filletX, 0f, joinLocal.z));

                AppendFilletTurn(

                    points,

                    slot,

                    filletCorner,

                    Vector3.forward,

                    isLeft ? Vector3.left : Vector3.right,

                    geometry.TurnRadius);

                AppendWorldFromLocal(points, slot, new Vector3(filletX, 0f, 0f));

                AppendWorldFromLocal(points, slot, new Vector3(barracksLocal.x, 0f, 0f));

            }

            else

            {

                AppendWorldFromLocal(points, slot, stripJoin);

                if (ShouldPassFilletEntry(barracksLocal.x, stripJoin.x, filletX, isLeft))

                {

                    AppendWorldFromLocal(points, slot, new Vector3(filletX, 0f, 0f));

                }



                if (Mathf.Abs(barracksLocal.x - stripJoin.x) > LocalEpsilon)

                {

                    AppendWorldFromLocal(points, slot, new Vector3(barracksLocal.x, 0f, 0f));

                }

            }

        }



        static bool CrossesFillet(float fromX, float toX, float filletX, bool isLeft)

        {

            if (Mathf.Abs(fromX - toX) < LocalEpsilon)

            {

                return false;

            }



            if (isLeft)

            {

                return fromX > filletX && toX < filletX;

            }



            return fromX < filletX && toX > filletX;

        }



        static bool ShouldPassFilletEntry(float fromX, float toX, float filletX, bool isLeft)

        {

            if (isLeft)

            {

                return fromX <= filletX && toX <= filletX && fromX > toX;

            }



            return fromX >= filletX && toX >= filletX && fromX < toX;

        }



        static void AppendFilletTurn(

            List<Vector3> points,

            PlayerSlotLayout slot,

            Vector3 localCorner,

            Vector3 localInDir,

            Vector3 localOutDir,

            float turnRadius)

        {

            var worldCorner = LocalToWorld(slot, localCorner);

            var worldInDir = slot.BaseRotation * localInDir;

            var worldOutDir = slot.BaseRotation * localOutDir;

            RoadFilletArc.AppendPathWaypoints(

                points,

                worldCorner,

                worldInDir,

                worldOutDir,

                turnRadius,

                N4RoadCenterlineBuilder.LaneHeight);

        }



        static void AppendWorldFromLocal(List<Vector3> points, PlayerSlotLayout slot, Vector3 local)

        {

            N4RoadCenterlineBuilder.AppendUnique(points, LocalToWorld(slot, local));

        }



        static Vector3 LocalToWorld(PlayerSlotLayout slot, Vector3 local)

        {

            var world = slot.BasePosition + slot.BaseRotation * local;

            return N4RoadCenterlineBuilder.WithHeight(world);

        }



        static Vector3 WorldToLocalHorizontal(PlayerSlotLayout slot, Vector3 world)

        {

            var local = Quaternion.Inverse(slot.BaseRotation) * (world - slot.BasePosition);

            local.y = 0f;

            return local;

        }



        static bool TryGetBarracksLocal(PlayerSlotLayout slot, string barracksId, out Vector3 local)

        {

            local = Vector3.zero;

            if (!IsSideBarracks(barracksId)

                || !slot.BuildingLocalOffsets.TryGetValue(barracksId, out local))

            {

                return false;

            }



            local.y = 0f;

            return true;

        }

    }

}


