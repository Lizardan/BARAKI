using Game.Core;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class HeroParkRulesTests
    {
        [Test]
        public void GetParkWorldPosition_IsBehindMainOppositeCenterBarracks()
        {
            var layout = MatchArenaGenerator.Generate(2);
            var main = layout.Slots[0].GetBuildingWorldPosition(GameIds.Buildings.Main);
            var center = layout.Slots[0].GetBuildingWorldPosition(GameIds.Buildings.BarracksCenter);
            var park = HeroParkRules.GetParkWorldPosition(layout, 0, 1, layout.MainToTowerDistance);

            var toCenter = center - main;
            var toPark = park - main;
            toCenter.y = 0f;
            toPark.y = 0f;
            Assert.Less(Vector3.Dot(toCenter.normalized, toPark.normalized), 0f);
        }
    }
}
