﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using XLua;

/// <summary>
/// added by wsh @ 2017.12.22
/// 功能：资源异步请求，本地、远程通杀
/// 注意：
/// 1、Unity5.3官方建议用UnityWebRequest取代WWW：https://unity3d.com/cn/learn/tutorials/topics/best-practices/assetbundle-fundamentals?playlist=30089
/// 2、这里还是采用WWW，因为UnityWebRequest的Bug无数：
///     1）Unity5.3.5：http://blog.csdn.net/st75033562/article/details/52411197
///     2）Unity5.5：https://bitbucket.org/Unity-Technologies/assetbundledemo/pull-requests/25/feature-unitywebrequest/diff#comment-None
///     3）还有各个版本发行说明中关于UnityWebRequest的修复，如Unity5.4.1（5.4全系列版本基本都有修复这个API的Bug）：https://unity3d.com/cn/unity/whats-new/unity-5.4.1
///     4）此外对于LZMA压缩，采用UnityWebRequest好处在于节省内存，性能上并不比WWW优越：https://docs.unity3d.com/530/Documentation/Manual/AssetBundleCompression.html
/// 3、LoadFromFile(Async)在Unity5.4以上支持streamingAsset目录加载资源，5.3.7和5.4.3以后支持LAMZ压缩，但是没法加载非Assetbundle资源
/// 4、另外，虽然LoadFromFile(Async)是加载ab最快的API，但是会延缓Asset加载的时间（读磁盘），如果ab尽量预加载，不考虑内存敏感问题，这个API意义就不大
/// </summary>

namespace AssetBundles
{
    [Hotfix]
    [LuaCallCSharp]
    public class ResourceWebRequester : ResourceAsyncOperation
    {
        static Queue<ResourceWebRequester> pool = new Queue<ResourceWebRequester>();
        static int sequence = 0;
        private UnityWebRequest uwr = null;
        private AssetBundleCreateRequest abRequest = null;
        private bool isOver = false;
        private int timeout = 0;

        public static ResourceWebRequester Get()
        {
            if (pool.Count > 0)
            {
                return pool.Dequeue();
            }
            else
            {
                return new ResourceWebRequester(++sequence);
            }
        }

        public static void Recycle(ResourceWebRequester creater)
        {
            pool.Enqueue(creater);
        }

        public ResourceWebRequester(int sequence)
        {
            Sequence = sequence;
        }

        public void Init(string name, string url, bool noCache = false, bool ab = false, bool onlyHeader = false,
            int timeout = 0)
        {
            this.assetbundleName = name;
            this.ab = ab;
            this.url = url;
            this.noCache = noCache;
            this.timeout = timeout;
            this.onlyHeader = onlyHeader;
            uwr = null;
            isOver = false;
        }

        public int Sequence { get; protected set; }

        public bool noCache { get; protected set; }

        public string assetbundleName { get; protected set; }

        public bool ab { get; protected set; }

        public bool onlyHeader { get; protected set; }

        public string url { get; protected set; }

        public string text => uwr?.downloadHandler.text;
        public byte[] bytes => uwr?.downloadHandler.data;

        public AssetBundle assetbundle
        {
            get
            {
                if (uwr != null)
                {
                    return DownloadHandlerAssetBundle.GetContent(uwr);
                }
                else if (abRequest != null)
                {
                    return abRequest.assetBundle;
                }

                return null;
            }
        }

        // 注意：不能直接判空
        // 详见：https://docs.unity3d.com/530/Documentation/ScriptReference/WWW-error.html
        public string error => (uwr == null || string.IsNullOrEmpty(uwr.error)) ? null : uwr.error;

        public override bool IsDone()
        {
            return isOver;
        }

        public void Start()
        {
            if (ab)
            {
                if (onlyHeader)
                {
                    abRequest = AssetBundle.LoadFromFileAsync(url);
                }
                else
                {
                    uwr = UnityWebRequestAssetBundle.GetAssetBundle(url);
                }
            }
            else
            {
                uwr = UnityWebRequest.Get(url);
            }

            if (uwr == null && abRequest == null)
            {
                isOver = true;
            }
            else
            {
                if (uwr != null)
                {
                    uwr.timeout = this.timeout;
                    uwr.SendWebRequest();
                }
            }
        }

        public override float Progress()
        {
            if (isDone)
            {
                return 1.0f;
            }

            if (uwr != null)
            {
                return uwr.downloadProgress;
            }
            else if (abRequest != null)
            {
                return abRequest.progress;
            }

            return 0.0f;
        }

        public override void Update()
        {
            if (isDone)
            {
                return;
            }

            if (uwr != null)
            {
                isOver = (uwr.isDone || !string.IsNullOrEmpty(uwr.error));
            }
            else if (abRequest != null)
            {
                isOver = abRequest.isDone;
            }

            if (!isOver)
            {
                return;
            }

            if (uwr != null && !string.IsNullOrEmpty(uwr.error))
            {
                Logger.LogError("{0}:{1}", url, uwr.error);
            }
        }

        public override void Dispose()
        {
            if (uwr != null)
            {
                uwr.Dispose();
                uwr = null;
            }

            abRequest = null;
            Recycle(this);
        }
    }
}