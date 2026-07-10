namespace Game.Gameplay.Match.Selection
{
    public static class MatchPickLayers
    {
        public const string PickableLayerName = "MatchPickable";

        public static int PickableLayer { get; private set; } = -1;

        public static int PickableLayerMask =>
            PickableLayer >= 0 ? 1 << PickableLayer : UnityEngine.Physics.DefaultRaycastLayers;

        public static void SetPickableLayer(int layer)
        {
            PickableLayer = layer;
        }

        public static void InitializeFromName()
        {
            SetPickableLayer(UnityEngine.LayerMask.NameToLayer(PickableLayerName));
        }
    }
}
