using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace AutomateFleet.Operations
{
    // Operation icons load from "operations/ICO_<ClassName>_on/_off" in the game's asset bundles.
    // Ours aren't there, so serve those paths from PNGs embedded in the DLL.
    static class Icons
    {
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        public static Sprite Get(string path)
        {
            if (path == null || !path.StartsWith("operations/ICO_"))
                return null;
            if (sprites.TryGetValue(path, out var cached))
                return cached;
            var name = "Icons." + path.Substring("operations/".Length) + ".png";
            Sprite sprite = null;
            using (var stream = typeof(Icons).Assembly.GetManifestResourceStream(name))
            {
                if (stream != null)
                {
                    var bytes = new byte[stream.Length];
                    stream.Read(bytes, 0, bytes.Length);
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = name };
                    if (texture.LoadImage(bytes))
                        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                }
            }
            sprites[path] = sprite;
            return sprite;
        }
    }

    [HarmonyPatch(typeof(AssetLoader), nameof(AssetLoader.LoadAssetForImageAssignment))]
    static class ImageIconPatch
    {
        static bool Prefix(string asset, Image imageToAssign)
        {
            var sprite = Icons.Get(asset);
            if (sprite == null)
                return true;
            imageToAssign.sprite = sprite;
            return false;
        }
    }

    [HarmonyPatch(typeof(AssetLoader), nameof(AssetLoader.LoadAssetForSpriteAssignment))]
    static class SpriteIconPatch
    {
        static bool Prefix(string asset, ref Sprite __result)
        {
            var sprite = Icons.Get(asset);
            if (sprite == null)
                return true;
            __result = sprite;
            return false;
        }
    }
}
