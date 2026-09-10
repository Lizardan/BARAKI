using UnityEngine;

namespace Game.Gameplay.Vfx
{
    /// <summary>
    /// Гасит встроенную тряску камеры у CFXR-эффектов (Cartoon FX Remaster).
    ///
    /// <c>CFXR_Effect.cameraShake</c> смещает <c>Camera.main</c> в <c>OnPreRenderCamera</c>
    /// и возвращает назад в <c>OnPostRenderCamera</c> — то есть дёргает камеру в обход
    /// Cinemachine. Для RTS это лишнее, поэтому каждый заспавненный FX проходит через
    /// <see cref="Strip"/>, а BARAKI Studio использует тот же гард в превью.
    ///
    /// Поля читаются рефлексией: пак сторонний, ссылаться на его типы напрямую нельзя.
    /// </summary>
    public static class AbilityFxCameraShakeGuard
    {
        const string CfxrEffectTypeName = "CFXR_Effect";
        const string CameraShakeFieldName = "cameraShake";
        const string EnabledFieldName = "enabled";

        /// <summary>Погасить cameraShake у эффекта и всех его детей. Безопасно, если CFXR нет.</summary>
        public static void Strip(GameObject fx)
        {
            if (fx == null)
            {
                return;
            }

            var behaviours = fx.GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null || behaviour.GetType().Name != CfxrEffectTypeName)
                {
                    continue;
                }

                DisableCameraShake(behaviour);
            }
        }

        /// <summary>Погасить cameraShake у конкретного <c>CFXR_Effect</c>.</summary>
        public static void DisableCameraShake(MonoBehaviour cfxrEffect)
        {
            if (cfxrEffect == null)
            {
                return;
            }

            var type = cfxrEffect.GetType();
            var shake = type.GetField(CameraShakeFieldName)?.GetValue(cfxrEffect);
            shake?.GetType().GetField(EnabledFieldName)?.SetValue(shake, false);
        }
    }
}
