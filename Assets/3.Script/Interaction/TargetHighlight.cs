using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeGame.Interaction
{
    // 공유 Material을 수정하지 않고, 원래의 renderer별 PropertyBlock을 복원합니다.
    public sealed class TargetHighlight : IDisposable
    {
        private readonly List<(Renderer renderer, MaterialPropertyBlock saved)> entries = new List<(Renderer, MaterialPropertyBlock)>();
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), Color = Shader.PropertyToID("_Color");
        public TargetHighlight(WorldInteractionTarget target)
        {
            foreach (var renderer in target.GetComponentsInChildren<Renderer>())
            {
                var saved = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(saved);
                var tinted = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(tinted);
                tinted.SetColor(BaseColor, target.HighlightColor);
                tinted.SetColor(Color, target.HighlightColor);
                entries.Add((renderer, saved));
                renderer.SetPropertyBlock(tinted);
            }
        }
        public void Dispose()
        {
            foreach (var entry in entries)
                if (entry.renderer != null)
                    entry.renderer.SetPropertyBlock(entry.saved);
            entries.Clear();
        }
    }
    internal static class InteractionObjectLifetime
    {
        internal static void Destroy(UnityEngine.Object value)
        {
            if (value == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(value);
            else
                UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
