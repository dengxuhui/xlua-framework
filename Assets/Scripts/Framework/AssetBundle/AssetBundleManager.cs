using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XLua;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.SceneManagement;

#endif

namespace AssetBundles
{
    /// <summary>
    /// 资源管理器：
    /// 1.Additive类型场景资源，通过LoadAdditiveSceneAsync接口加载
    /// 2.Sprite，GameObject等类型资源，通过LoadAssetAsync接口加载
    /// 3.Lua资源，Lua资源使用强引用管理，直到被XLua托管后，释放引用
    /// 4.AB资源，加载资源之前会自动加载AB包
    /// </summary>
    [Hotfix]
    [LuaCallCSharp]
    public class AssetBundleManager : MonoSingleton<AssetBundleManager>
    {
        // 最大同时进行的ab创建数量
        const int MAX_ASSETBUNDLE_CREATE_NUM = 5;

        // manifest：提供依赖关系查找以及hash值比对
        Manifest manifest = null;

        // 资源路径相关的映射表
        private AssetsPathMapping _assetsPathMapping = null;

        // ab缓存包：所有目前已经加载的ab包，包括临时ab包与公共ab包
        private readonly Dictionary<string, AssetBundle> _assetbundleCaching = new Dictionary<string, AssetBundle>();

        // asset缓存：给非公共ab包的asset提供逻辑层的复用
        private readonly Dictionary<string, WeakReference> _assetsCaching = new Dictionary<string, WeakReference>();

        // lua asset资源缓存，在lua逻辑require之前的缓存
        private readonly Dictionary<string, Object> _luaAssetsCaching = new Dictionary<string, Object>();

        // 加载数据请求：正在processing或者等待processing的资源请求
        private readonly Dictionary<string, ResourceWebRequester>
            _webRequesting = new Dictionary<string, ResourceWebRequester>();

        // 等待处理的资源请求
        private readonly Queue<ResourceWebRequester> _webRequesterQueue = new Queue<ResourceWebRequester>();

        // 正在处理的资源请求
        private readonly List<ResourceWebRequester> _processingWebRequester = new List<ResourceWebRequester>();

        // 逻辑层正在等待的ab加载异步句柄
        private readonly List<AssetBundleAsyncLoader> _processingAssetBundleAsyncLoader =
            new List<AssetBundleAsyncLoader>();

        // 逻辑层正在等待的asset加载异步句柄
        private readonly List<AssetAsyncLoader> _processingAssetAsyncLoader = new List<AssetAsyncLoader>();

        // 为了消除GC
        private readonly List<string> _tmpStringList = new List<string>(128);

        public static string ManifestBundleName => BuildUtils.ManifestBundleName;
        
        // Hotfix测试---用于侧测试资源模块的热修复
        public void TestHotfix()
        {
#if UNITY_EDITOR || CLIENT_DEBUG
            Logger.Log("********** AssetBundleManager : Call TestHotfix in cs...");
#endif
        }

        public IEnumerator Initialize()
        {
#if UNITY_EDITOR
            if (AssetBundleConfig.IsEditorMode)
            {
                yield break;
            }
#endif

            manifest = new Manifest();
            _assetsPathMapping = new AssetsPathMapping();
            // 说明：同时请求资源可以提高加载速度
            var manifestRequest = RequestAssetBundleAsync(manifest.AssetbundleName);
            var pathMapRequest = RequestAssetBundleAsync(_assetsPathMapping.AssetbundleName);

            yield return manifestRequest;
            var assetbundle = manifestRequest.assetbundle;
            manifest.LoadFromAssetbundle(assetbundle);
            assetbundle.Unload(false);
            manifestRequest.Dispose();

            yield return pathMapRequest;
            assetbundle = pathMapRequest.assetbundle;
            var mapContent = assetbundle.LoadAsset<TextAsset>(_assetsPathMapping.AssetName);
            if (mapContent != null)
            {
                _assetsPathMapping.Initialize(mapContent.text);
            }

            assetbundle.Unload(true);
            pathMapRequest.Dispose();
        }

        public IEnumerator Cleanup()
        {
#if UNITY_EDITOR
            if (AssetBundleConfig.IsEditorMode)
            {
                yield break;
            }
#endif

            // 等待所有请求完成
            // 要是不等待Unity很多版本都有各种Bug
            yield return new WaitUntil(() => !IsProcessRunning);

            ClearAssetsCache(true);
            foreach (var assetBundle in _assetbundleCaching.Values)
            {
                if (assetBundle != null)
                {
                    assetBundle.Unload(false);
                }
            }

            _luaAssetsCaching.Clear();
            _assetbundleCaching.Clear();
        }

        public Manifest curManifest => manifest;

        public string DownloadUrl => URLSetting.SERVER_RESOURCE_URL;

        public bool IsProcessRunning =>
            _processingWebRequester.Count != 0 || _processingAssetBundleAsyncLoader.Count != 0 ||
            _processingAssetAsyncLoader.Count != 0;

        public bool IsAssetBundleLoaded(string assetbundleName)
        {
            return _assetbundleCaching.ContainsKey(assetbundleName);
        }

        public AssetBundle GetAssetBundleCache(string assetbundleName)
        {
            _assetbundleCaching.TryGetValue(assetbundleName, out var target);
            return target;
        }

        protected void AddAssetBundleCache(string assetbundleName, AssetBundle assetbundle)
        {
            _assetbundleCaching[assetbundleName] = assetbundle;
        }

        public Object GetAssetCache(string assetName)
        {
            _assetsCaching.TryGetValue(assetName, out var reference);
            if (reference == null || reference.Target as Object == null)
            {
                var abName = _assetsPathMapping.GetAssetBundleName(assetName);
                var abAsset = GetAssetBundleCache(abName);
                if (abAsset.isStreamedSceneAssetBundle)
                {
                    return null;
                }

                if (abAsset != null)
                {
                    var assetPath = AssetBundleUtility.PackagePathToAssetsPath(assetName);
                    var asset = abAsset.LoadAsset<Object>(assetPath);
                    reference = WeakReferenceUtility.Get(asset);
                    _assetsCaching[assetName] = reference;
                }
                else
                {
                    Logger.LogError("asset bundle not loaded??ab name>{0}", abName);
                }
            }

            return reference?.Target as Object;
        }

        public void ClearAssetsCache(bool clearAll = false)
        {
            if (clearAll)
            {
                foreach (var kv in _assetsCaching)
                {
                    WeakReferenceUtility.Recover(kv.Value);
                }

                _assetsCaching.Clear();
            }
            else
            {
                _tmpStringList.Clear();
                foreach (var keyValuePair in _assetsCaching)
                {
                    var asset = keyValuePair.Value.Target as Object;
                    if (asset == null)
                    {
                        _tmpStringList.Add(keyValuePair.Key);
                        WeakReferenceUtility.Recover(keyValuePair.Value);
                    }
                }

                for (var i = 0; i < _tmpStringList.Count; i++)
                {
                    _assetsCaching.Remove(_tmpStringList[i]);
                }

                _tmpStringList.Clear();
            }
        }

        public TextAsset GetLuaAssetCache(string assetName)
        {
            _luaAssetsCaching.TryGetValue(assetName, out var asset);
            _luaAssetsCaching.Remove(assetName);
            return asset as TextAsset;
        }

        public void AddAssetbundleAssetsCache(string assetbundleName)
        {
#if UNITY_EDITOR
            if (AssetBundleConfig.IsEditorMode)
            {
                return;
            }
#endif
            var isLuaAB = assetbundleName == XLuaManager.Instance.AssetbundleName;
            if (!isLuaAB)
            {
                return;
            }

            if (!IsAssetBundleLoaded(assetbundleName))
            {
                Logger.LogError("Try to add assets cache from unloaded assetbundle : " + assetbundleName);
                return;
            }

            var curAssetbundle = GetAssetBundleCache(assetbundleName);
            var allAssetNames = _assetsPathMapping.GetAllAssetNames(assetbundleName);
            for (int i = 0; i < allAssetNames.Count; i++)
            {
                var assetName = allAssetNames[i];
                var assetPath = AssetBundleUtility.PackagePathToAssetsPath(assetName);
                var asset = curAssetbundle == null ? null : curAssetbundle.LoadAsset(assetPath);
                _luaAssetsCaching[assetName] = asset;
            }
        }

        public ResourceWebRequester GetAssetBundleAsyncCreator(string assetbundleName)
        {
            _webRequesting.TryGetValue(assetbundleName, out var creator);
            return creator;
        }

        private bool CreateAssetBundleAsync(string assetbundleName, bool onlyHeader)
        {
            if (IsAssetBundleLoaded(assetbundleName) || _webRequesting.ContainsKey(assetbundleName))
            {
                return false;
            }

            var creator = ResourceWebRequester.Get();
            var url = AssetBundleUtility.GetAssetBundleFileUrl(assetbundleName, !onlyHeader);
            creator.Init(assetbundleName, url, false, true, onlyHeader);
            _webRequesting.Add(assetbundleName, creator);
            _webRequesterQueue.Enqueue(creator);
            return true;
        }

        // 从服务器下载网页内容，需提供完整url，非AB（不计引用计数、不缓存），Creater使用后记得回收
        public ResourceWebRequester DownloadWebResourceAsync(string url, int timeout = 0)
        {
            var creator = ResourceWebRequester.Get();
            creator.Init(url, url, true, false, false, timeout);
            _webRequesting.Add(url, creator);
            _webRequesterQueue.Enqueue(creator);
            return creator;
        }

        // 从资源服务器下载非Assetbundle资源，非AB（不计引用计数、不缓存），Creater使用后记得回收
        public ResourceWebRequester DownloadAssetFileAsync(string filePath, bool ab = false, int timeout = 0)
        {
            if (string.IsNullOrEmpty(DownloadUrl))
            {
                Logger.LogError("You should set download url first!!!");
                return null;
            }

            var creator = ResourceWebRequester.Get();
            var url = DownloadUrl + filePath;
            creator.Init(filePath, url, true, ab);
            if (_webRequesting.ContainsKey(filePath))
            {
                _webRequesting.Remove(filePath);
            }

            _webRequesting.Add(filePath, creator);
            _webRequesterQueue.Enqueue(creator);
            return creator;
        }

        /// <summary>
        /// 从资源服务器下载Assetbundle资源，非AB（不计引用计数、不缓存），Creater使用后记得回收
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="timeout">超时时间，默认20秒</param>
        /// <returns></returns>
        public ResourceWebRequester DownloadAssetBundleAsync(string filePath, int timeout = 20)
        {
            // 如果ResourceWebRequester升级到使用UnityWebRequester，那么下载AB和下载普通资源需要两个不同的DownLoadHandler
            // 兼容升级的可能性，这里也做一下区分
            return DownloadAssetFileAsync(filePath, false, timeout);
        }

        // 本地异步请求非Assetbundle资源，非AB（不计引用计数、不缓存），Creater使用后记得回收
        public ResourceWebRequester RequestAssetFileAsync(string filePath, bool streamingAssetsOnly = true)
        {
            var creator = ResourceWebRequester.Get();
            string url = null;
            if (streamingAssetsOnly)
            {
                url = AssetBundleUtility.GetStreamingAssetsFilePath(filePath);
            }
            else
            {
                url = AssetBundleUtility.GetAssetBundleFileUrl(filePath);
            }

            creator.Init(filePath, url, true, false);
            _webRequesting.Add(filePath, creator);
            _webRequesterQueue.Enqueue(creator);
            return creator;
        }

        // 本地异步请求Assetbundle资源，不计引用计数、不缓存，Creater使用后记得回收
        public ResourceWebRequester RequestAssetBundleAsync(string assetbundleName)
        {
            var creator = ResourceWebRequester.Get();
            var url = AssetBundleUtility.GetAssetBundleFileUrl(assetbundleName);
            creator.Init(assetbundleName, url, true, true);
            _webRequesting.Add(assetbundleName, creator);
            _webRequesterQueue.Enqueue(creator);
            return creator;
        }

        public bool MapAssetPath(string assetPath, out string assetbundleName, out string assetName)
        {
            return _assetsPathMapping.MapAssetPath(assetPath, out assetbundleName, out assetName);
        }

        public bool MapAssetPath(string assetPath)
        {
            return _assetsPathMapping.MapAssetPath(assetPath);
        }

        #region load api

        public BaseAssetAsyncLoader LoadAssetAsync(string assetPath, Type assetType)
        {
#if UNITY_EDITOR
            if (AssetBundleConfig.IsEditorMode)
            {
                string path = AssetBundleUtility.PackagePathToAssetsPath(assetPath);
                Object target = AssetDatabase.LoadAssetAtPath(path, assetType);
                return new EditorAssetAsyncLoader(target);
            }
#endif

            bool status = MapAssetPath(assetPath, out var assetbundleName, out var assetName);
            if (!status)
            {
                Logger.LogError("No assetbundle at asset path :" + assetPath);
                return null;
            }

            var loader = AssetAsyncLoader.Get();
            _processingAssetAsyncLoader.Add(loader);
            //对应的AB包是否加载
            if (IsAssetBundleLoaded(assetbundleName))
            {
                loader.Init(assetName, GetAssetCache(assetName));
                return loader;
            }
            else
            {
                var assetbundleLoader = LoadAssetBundleAsync(assetbundleName, true);
                loader.Init(assetName, assetbundleLoader);
                return loader;
            }
        }

        public Object LoadAssetSync(string assetPath, Type assetType)
        {
#if UNITY_EDITOR
            if (AssetBundleConfig.IsEditorMode)
            {
                string path = AssetBundleUtility.PackagePathToAssetsPath(assetPath);
                Object target = AssetDatabase.LoadAssetAtPath(path, assetType);
                return target;
            }
#endif
            bool status = MapAssetPath(assetPath, out var assetbundleName, out var assetName);
            if (!status)
            {
                Logger.LogError("No assetbundle at asset path :" + assetPath);
                return null;
            }

            //对应的AB包是否加载
            if (IsAssetBundleLoaded(assetbundleName))
            {
                return GetAssetCache(assetName);
            }
            else
            {
                Logger.LogError("Synchronous loading please load asset bundle first=>{0}", assetbundleName);
                return null;
            }
        }

        public BaseAssetAsyncLoader LoadAdditiveSceneAsync(string assetPath)
        {
#if UNITY_EDITOR
            if (AssetBundleConfig.IsEditorMode)
            {
                string path = AssetBundleUtility.PackagePathToAssetsPath(assetPath);
                Object target = AssetDatabase.LoadAssetAtPath(path, typeof(Scene));
                return new EditorAssetAsyncLoader(target);
            }
#endif
            bool status = MapAssetPath(assetPath, out var assetbundleName, out var assetName);
            if (!status)
            {
                Logger.LogError("No assetbundle at asset path :" + assetPath);
                return null;
            }

            if (IsAssetBundleLoaded(assetbundleName))
            {
                return null;
            }
            else
            {
                var loader = AssetAsyncLoader.Get();
                _processingAssetAsyncLoader.Add(loader);
                var assetbundleLoader = LoadAssetBundleAsync(assetbundleName, true);
                loader.Init(assetName, assetbundleLoader);
                return loader;
            }
        }

        /// <summary>
        /// 加载ab包资源
        /// </summary>
        /// <param name="assetbundleName"></param>
        /// <param name="onlyHeader">是否只加载AB header【如果只加载Header使用.LoadFromFile,如果不是则使用WebRequest进行全量加载】</param>
        /// <returns></returns>
        public BaseAssetBundleAsyncLoader LoadAssetBundleAsync(string assetbundleName, bool onlyHeader)
        {
#if UNITY_EDITOR
            if (AssetBundleConfig.IsEditorMode)
            {
                return new EditorAssetBundleAsyncLoader(assetbundleName);
            }
#endif

            var loader = AssetBundleAsyncLoader.Get();
            _processingAssetBundleAsyncLoader.Add(loader);
            if (manifest != null)
            {
                string[] dependencies = manifest.GetAllDependencies(assetbundleName);
                for (int i = 0; i < dependencies.Length; i++)
                {
                    var dependency = dependencies[i];
                    if (!string.IsNullOrEmpty(dependency) && dependency != assetbundleName)
                    {
                        CreateAssetBundleAsync(dependency, onlyHeader);
                    }
                }

                loader.Init(assetbundleName, dependencies);
            }
            else
            {
                loader.Init(assetbundleName, null);
            }

            CreateAssetBundleAsync(assetbundleName, onlyHeader);
            return loader;
        }

        #endregion

        #region frame updater

        void Update()
        {
            OnProcessingWebRequester();
            OnProcessingAssetBundleAsyncLoader();
            OnProcessingAssetAsyncLoader();
        }

        void OnProcessingWebRequester()
        {
            for (int i = _processingWebRequester.Count - 1; i >= 0; i--)
            {
                var creator = _processingWebRequester[i];
                creator.Update();
                if (creator.IsDone())
                {
                    _processingWebRequester.RemoveAt(i);
                    _webRequesting.Remove(creator.assetbundleName);
                    if (!creator.noCache)
                    {
                        // AB缓存
                        // 说明：有错误也缓存下来，只不过资源为空
                        // 1、避免再次错误加载
                        // 2、如果不存下来加载器将无法判断什么时候结束
                        AddAssetBundleCache(creator.assetbundleName, creator.assetbundle);
                        creator.Dispose();
                    }
                }
            }

            int slotCount = _processingWebRequester.Count;
            while (slotCount < MAX_ASSETBUNDLE_CREATE_NUM && _webRequesterQueue.Count > 0)
            {
                var creator = _webRequesterQueue.Dequeue();
                creator.Start();
                _processingWebRequester.Add(creator);
                slotCount++;
            }
        }

        void OnProcessingAssetBundleAsyncLoader()
        {
            for (int i = _processingAssetBundleAsyncLoader.Count - 1; i >= 0; i--)
            {
                var loader = _processingAssetBundleAsyncLoader[i];
                loader.Update();
                if (loader.IsDone())
                {
                    _processingAssetBundleAsyncLoader.RemoveAt(i);
                }
            }
        }

        void OnProcessingAssetAsyncLoader()
        {
            for (int i = _processingAssetAsyncLoader.Count - 1; i >= 0; i--)
            {
                var loader = _processingAssetAsyncLoader[i];
                loader.Update();
                if (loader.IsDone())
                {
                    _processingAssetAsyncLoader.RemoveAt(i);
                }
            }
        }

        #endregion

        #region editor api

#if UNITY_EDITOR
        [BlackList]
        public HashSet<string> GetAssetbundleResident()
        {
            //todo 编辑器接口需要重写。等待游戏逻辑跑通再进行修改
            return new HashSet<string>();
        }

        [BlackList]
        public ICollection<string> GetAssetbundleCaching()
        {
            return _assetbundleCaching.Keys;
        }

        [BlackList]
        public Dictionary<string, ResourceWebRequester> GetWebRequesting()
        {
            return _webRequesting;
        }

        [BlackList]
        public Queue<ResourceWebRequester> GetWebRequestQueue()
        {
            return _webRequesterQueue;
        }

        [BlackList]
        public List<ResourceWebRequester> GetProsessingWebRequester()
        {
            return _processingWebRequester;
        }

        [BlackList]
        public List<AssetBundleAsyncLoader> GetProsessingAssetBundleAsyncLoader()
        {
            return _processingAssetBundleAsyncLoader;
        }

        [BlackList]
        public List<AssetAsyncLoader> GetProsessingAssetAsyncLoader()
        {
            return _processingAssetAsyncLoader;
        }

        [BlackList]
        public string GetAssetBundleName(string assetName)
        {
            return _assetsPathMapping.GetAssetBundleName(assetName);
        }

        [BlackList]
        public int GetAssetCachingCount()
        {
            return _assetsCaching.Count;
        }

        [BlackList]
        public Dictionary<string, List<string>> GetAssetCaching()
        {
            var assetbundleDic = new Dictionary<string, List<string>>();
            List<string> assetNameList = null;

            var iter = _assetsCaching.GetEnumerator();
            while (iter.MoveNext())
            {
                var assetName = iter.Current.Key;
                var assetbundleName = _assetsPathMapping.GetAssetBundleName(assetName);
                assetbundleDic.TryGetValue(assetbundleName, out assetNameList);
                if (assetNameList == null)
                {
                    assetNameList = new List<string>();
                }

                assetNameList.Add(assetName);
                assetbundleDic[assetbundleName] = assetNameList;
            }

            return assetbundleDic;
        }

        [BlackList]
        public int GetAssetbundleReferenceCount(string assetbundleName)
        {
            //todo 编辑器接口需要重写。等待游戏逻辑跑通再进行修改
            return 0;
        }

        [BlackList]
        public int GetAssetbundleDependenciesCount(string assetbundleName)
        {
            string[] dependancies = manifest.GetAllDependencies(assetbundleName);
            int count = 0;
            for (int i = 0; i < dependancies.Length; i++)
            {
                var cur = dependancies[i];
                if (!string.IsNullOrEmpty(cur) && cur != assetbundleName)
                {
                    count++;
                }
            }

            return count;
        }

        [BlackList]
        public List<string> GetAssetBundleRefrences(string assetbundleName)
        {
            List<string> refrences = new List<string>();
            var cachingIter = _assetbundleCaching.GetEnumerator();
            while (cachingIter.MoveNext())
            {
                var curAssetbundleName = cachingIter.Current.Key;
                if (curAssetbundleName == assetbundleName)
                {
                    continue;
                }

                string[] dependencies = manifest.GetAllDependencies(curAssetbundleName);
                for (int i = 0; i < dependencies.Length; i++)
                {
                    var dependency = dependencies[i];
                    if (dependency == assetbundleName)
                    {
                        refrences.Add(curAssetbundleName);
                    }
                }
            }

            var requestingIter = _webRequesting.GetEnumerator();
            while (requestingIter.MoveNext())
            {
                var curAssetbundleName = requestingIter.Current.Key;
                if (curAssetbundleName == assetbundleName)
                {
                    continue;
                }

                string[] dependencies = manifest.GetAllDependencies(curAssetbundleName);
                for (int i = 0; i < dependencies.Length; i++)
                {
                    var dependency = dependencies[i];
                    if (dependency == assetbundleName)
                    {
                        refrences.Add(curAssetbundleName);
                    }
                }
            }

            return refrences;
        }

        [BlackList]
        public List<string> GetWebRequesterReferences(string assetbundleName)
        {
            List<string> references = new List<string>();
            var iter = _webRequesting.GetEnumerator();
            while (iter.MoveNext())
            {
                var curAssetbundleName = iter.Current.Key;
                var webRequester = iter.Current.Value;
                if (curAssetbundleName == assetbundleName)
                {
                    references.Add(webRequester.Sequence.ToString());
                    continue;
                }
            }

            return references;
        }

        [BlackList]
        public List<string> GetAssetBundleLoaderReferences(string assetbundleName)
        {
            List<string> references = new List<string>();
            var iter = _processingAssetBundleAsyncLoader.GetEnumerator();
            while (iter.MoveNext())
            {
                var curAssetbundleName = iter.Current.assetbundleName;
                var curLoader = iter.Current;
                if (curAssetbundleName == assetbundleName)
                {
                    references.Add(curLoader.Sequence.ToString());
                }
            }

            return references;
        }
#endif

        #endregion
    }
}