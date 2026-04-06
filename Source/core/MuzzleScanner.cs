using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace TrueMuzzle
{
    public static class MuzzleScanner
    {
        // 🌟 核心改进：将缓存键改为 Texture。这样同一把枪的不同特化贴图会分别拥有自己的偏移量。
        private static readonly Dictionary<Texture, Vector2> offsetCache = new Dictionary<Texture, Vector2>();

        public static Vector2 GetMuzzleOffset(Thing equipment)
        {
            if (equipment == null) return Vector2.zero;

            // 🌟 获取武器实例当前正在显示的真实 Graphic 和 Texture
            Graphic graphic = equipment.Graphic;
            Texture2D texture = graphic?.MatSingle?.mainTexture as Texture2D;

            if (texture == null) return Vector2.zero;

            // 优先从贴图缓存读取
            if (offsetCache.TryGetValue(texture, out Vector2 cachedOffset))
            {
                return cachedOffset;
            }

            // 如果没有缓存，则进行单次精准扫描
            Vector2 calculatedOffset = ScanTextureForMuzzle(texture, graphic);
            offsetCache[texture] = calculatedOffset;

            return calculatedOffset;
        }

        private static Vector2 ScanTextureForMuzzle(Texture2D texture, Graphic graphic)
        {
            // 1. 安全地将贴图复制到内存
            RenderTexture renderTex = RenderTexture.GetTemporary(
                texture.width, texture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);

            RenderTexture previous = RenderTexture.active;
            Texture2D readableText = null;

            try
            {
                Graphics.Blit(texture, renderTex);
                RenderTexture.active = renderTex;

                readableText = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
                readableText.Apply();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTex);
            }

            // 2. 获取所有像素并扫描最右侧边缘
            Color32[] pixels = readableText.GetPixels32();
            int width = readableText.width;
            int height = readableText.height;

            int maxX = -1;
            int sumY = 0;
            int countY = 0;

            for (int x = width - 1; x >= 0; x--)
            {
                for (int y = 0; y < height; y++)
                {
                    if (pixels[y * width + x].a > 10)
                    {
                        if (maxX == -1) maxX = x;
                        if (x == maxX)
                        {
                            sumY += y;
                            countY++;
                        }
                    }
                }
                if (maxX != -1) break;
            }

            if (readableText != null) UnityEngine.Object.Destroy(readableText);

            if (maxX != -1 && countY > 0)
            {
                float averageY = (float)sumY / countY;
                float widthFactor = ((float)maxX / width) - 0.5f;
                float heightFactor = (averageY / height) - 0.5f;

                // 🌟 使用当前 Graphic 实例的真实 drawSize（特化武器可能会改变大小）
                Vector2 currentDrawSize = graphic.drawSize;
                return new Vector2(widthFactor * currentDrawSize.x, heightFactor * currentDrawSize.y);
            }

            return Vector2.zero;
        }
    }
}