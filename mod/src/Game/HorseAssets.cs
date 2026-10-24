#if YX_GAME
using System;
using UnityEngine;
using Yx.ModSdk;

namespace YxPrediction
{
    public sealed class HorseAssets
    {
        readonly Sprite[] _sprites = new Sprite[8];
        readonly Texture2D[] _textures = new Texture2D[8];
        readonly bool[] _attempted = new bool[8];
        static int Index(string title)
        {
            switch (title)
            {
                case "特等马": return 0;
                case "上等马": return 1;
                case "中等马": return 2;
                case "牛马": return 3;
                case "海马": return 4;
                case "黑马": return 5;
                case "机械马": return 6;
                default: return -1;
            }
        }
        public Sprite Get(string title, ModContext ctx)
        {
            int index = Index(title);
            if (index < 0) return null;
            return Load(index, ctx);
        }
        public Sprite CircleMask(ModContext ctx) { return Load(7, ctx); }
        Sprite Load(int index, ModContext ctx)
        {
            if (_attempted[index]) return _sprites[index];
            _attempted[index] = true;
            Texture2D texture = null;
            try
            {
                byte[] data = Convert.FromBase64String(HorseImageData.Get(index));
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, data, true)) throw new FormatException("马图无法解码");
                // 构建前已将素材缩至128px；不在ILRuntime主线程逐像素处理，不改游戏头像。
                int width = texture.width, height = texture.height;
                _sprites[index] = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
                _textures[index] = texture;
            }
            catch (Exception ex)
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                ctx.Log.Warn("马图加载失败，保留文字称号：" + ex.Message);
            }
            return _sprites[index];
        }
        public void Destroy()
        {
            for (int i = 0; i < _sprites.Length; i++)
            {
                if (_sprites[i] != null) UnityEngine.Object.Destroy(_sprites[i]);
                if (_textures[i] != null) UnityEngine.Object.Destroy(_textures[i]);
                _sprites[i] = null; _textures[i] = null; _attempted[i] = false;
            }
        }
    }
}
#endif
