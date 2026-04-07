using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using UnityEngine;
using Verse;

namespace MuzzleFlash
{
    public class AssetsManager
    {
        private static AssetsManager _instance;
        public static AssetsManager Default
        {
            get
            {
                if (_instance == null) _instance = new AssetsManager();
                return _instance;
            }
        }

        private readonly Dictionary<string, Shader> _shaders = new Dictionary<string, Shader>();
        private readonly List<Material> _materials = new List<Material>();

        private Shader _animatedInstanced;
        private AssetBundle _assets;
        private bool _initialized = false;

        // =================================================================
        // 这就是全新升级的 Initialize 方法！
        // 它现在的逻辑是：直接去 DLL 肚子里寻找嵌入的 shaders，找不到才报错。
        // =================================================================
        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            Assembly currentAssembly = Assembly.GetExecutingAssembly();

            // 动态构建我们需要寻找的后缀名 (例如 "-windows")
            string targetPostfix = "shaders" + GetPlatformPostfix();

            // 核心修复：动态在整个 DLL 内部搜索匹配的嵌入资源
            string resourceName = currentAssembly.GetManifestResourceNames().FirstOrDefault(name => name.EndsWith(targetPostfix));

            if (resourceName == null)
            {
                Log.Error($"[TrueMuzzle] 致命错误：未能在 DLL 中找到嵌入的着色器资源 ({targetPostfix})！请返回 Visual Studio 检查那三个 shaders 文件是否已将'生成操作'设置为'嵌入的资源'。");
                return;
            }

            Log.Message($"[TrueMuzzle] 成功读取嵌入的渲染核心: {resourceName}");
            using (Stream stream = currentAssembly.GetManifestResourceStream(resourceName))
            {
                LoadAssetBundle(AssetBundle.LoadFromStream(stream));
            }
        }

        public void LoadAssetBundle(AssetBundle assets)
        {
            UnloadAssetBundle();
            _shaders.Clear();
            this._assets = assets;
            foreach (var shader in assets.LoadAllAssets<Shader>())
            {
                Log.Message($"Loaded shader {shader.name}");
                _shaders.Add(shader.name, shader);
            }
        }

        public void UnloadAssetBundle()
        {
            if (_assets == null) return;
            _shaders.Clear();
            _materials.Clear();
            _assets.Unload(true);
            _assets = null;
        }

        public Shader GetShader(string key)
        {
            if (!_initialized) Initialize();
            if (!_shaders.TryGetValue(key, out Shader shader))
            {
                return null;
            }
            return shader;
        }

        public IEnumerable<string> GetShaderNames()
        {
            foreach (var shader in _shaders)
            {
                yield return shader.Value.name;
            }
        }

        public Material GetMaterial(Shader shader, Texture2D texture, Vector4 splits, float lightIntensity = 1f)
        {
            if (shader == null) throw ExceptionRendering.ShaderNotFound(" Null shader");

            for (int i = 0; i < _materials.Count; i++)
            {
                if (_materials[i].shader != shader) continue;
                if (_materials[i].mainTexture != texture) continue;
                if (_materials[i].GetVector(ShaderPropertyID.splits) != splits) continue;
                if (_materials[i].GetFloat(ShaderPropertyID.lightIntensity) != lightIntensity) continue;
                return _materials[i];
            }

            Material result = UtilsMaterial.CreateMaterial(shader, true);
            result.mainTexture = texture;
            result.SetVector(ShaderPropertyID.splits, splits);
            result.SetFloat(ShaderPropertyID.lightIntensity, lightIntensity);
            return result;
        }

        public Shader ShaderAnimatedAdditiveInstanced
        {
            get
            {
                if (_animatedInstanced == null) _animatedInstanced = GetShader("Unlit/AnimatedAdditiveInstanced");
                if (_animatedInstanced == null) ExceptionRendering.ShaderNotFound("Unlit/AnimatedAdditiveInstanced");
                return _animatedInstanced;
            }
        }

        private string GetPlatformPostfix()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    return "-windows";
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                    return "-macos";
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                    return "-linux";
                default:
                    return "-unknown";
            }
        }
    }
}