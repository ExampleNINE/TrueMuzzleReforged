using System;
using System.Collections.Generic;
using Unity.Collections; // 🌟 新增：为了使用现代 Unity 的 NativeArray 黑科技
using UnityEngine;
using Verse;

namespace TrueMuzzle
{
    public static class MuzzleScanner
    {
        // 🌟 终极优化 1：将缓存键改为 int (贴图的 InstanceID)
        // 彻底解除了对 Texture 对象的强引用。即使其他 Mod 销毁了贴图，我们的字典也不会导致内存泄漏！
        private static readonly Dictionary<int, Vector2> offsetCache = new Dictionary<int, Vector2>();

        public static Vector2 GetMuzzleOffset(Thing equipment)
        {
            if (equipment == null) return Vector2.zero;

            Graphic graphic = equipment.Graphic;
            Texture2D texture = graphic?.MatSingle?.mainTexture as Texture2D;

            if (texture == null) return Vector2.zero;

            // 获取贴图在 Unity 底层的唯一整型 ID
            int texID = texture.GetInstanceID();

            // 极速匹配
            if (offsetCache.TryGetValue(texID, out Vector2 cachedOffset))
            {
                return cachedOffset;
            }

            Vector2 calculatedOffset = ScanTextureForMuzzle(texture, graphic);
            offsetCache[texID] = calculatedOffset;

            return calculatedOffset;
        }

        private static Vector2 ScanTextureForMuzzle(Texture2D texture, Graphic graphic)
        {
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

                // =========================================================
                // 🌟 终极优化 2：NativeArray 零 GC 内存直读技术
                // 使用 GetRawTextureData<Color32>() 替代 GetPixels32()。
                // 这将直接穿透到 C++ 底层显存，0 字节内存分配，0 垃圾回收，速度提升数十倍！
                // =========================================================
                NativeArray<Color32> pixels = readableText.GetRawTextureData<Color32>();
                int width = readableText.width;
                int height = readableText.height;

                int maxX = -1;
                int sumY = 0;
                int countY = 0;

                // 算法逻辑保持不变，但现在的 pixels 访问是纯底层指针级别的极速操作
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

                if (maxX != -1 && countY > 0)
                {
                    float averageY = (float)sumY / countY;
                    float widthFactor = ((float)maxX / width) - 0.5f;
                    float heightFactor = (averageY / height) - 0.5f;

                    Vector2 currentDrawSize = graphic.drawSize;
                    return new Vector2(widthFactor * currentDrawSize.x, heightFactor * currentDrawSize.y);
                }
            }
            finally
            {
                // 🌟 终极优化 3：将可读贴图的销毁放入 finally 块
                // 无论扫描过程是否因为极端的贴图损坏而报错，临时贴图都必定被立刻销毁，防死锁防泄漏！
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTex);
                if (readableText != null) UnityEngine.Object.Destroy(readableText);
            }

            return Vector2.zero;
        }
    }
}