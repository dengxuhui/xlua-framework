using UnityEngine;
using XLua;
using System.IO;

/// <summary>
/// added by wsh @ 2017.12.25
/// 功能： Assetbundle相关的通用静态函数，提供运行时，或者Editor中使用到的有关Assetbundle操作和路径处理的函数
/// TODO：
/// 1、做路径处理时是否考虑引入BetterStringBuilder消除GC问题
/// 2、目前所有路径处理不支持variant，后续考虑是否支持
/// </summary>

namespace AssetBundles
{
    [Hotfix]
    [LuaCallCSharp]
    public class AssetBundleUtility
    {
        private static string GetPlatformName(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.Android:
                    return "Android";
                case RuntimePlatform.IPhonePlayer:
                    return "iOS";
                default:
                    Logger.LogError("Error platform!!!");
                    return null;
            }
        }

        public static string GetStreamingAssetsFilePath(string assetPath = null, bool wwwUrl = true)
        {
            string outputPath;
#if UNITY_EDITOR || UNITY_IPHONE || UNITY_IOS
            if (wwwUrl)
            {
                outputPath = Path.Combine("file://" + Application.streamingAssetsPath,
                    AssetBundleConfig.AssetBundlesFolderName);
            }
            else
            {
                outputPath = Path.Combine(Application.streamingAssetsPath,
                    AssetBundleConfig.AssetBundlesFolderName);
            }
#elif UNITY_ANDROID
            outputPath = Path.Combine(Application.streamingAssetsPath, AssetBundleConfig.AssetBundlesFolderName);
#else
            Logger.LogError("Unsupported platform!!!");
#endif
            if (!string.IsNullOrEmpty(assetPath))
            {
                outputPath = Path.Combine(outputPath, assetPath);
            }

            return outputPath;
        }

        public static string GetStreamingAssetsDataPath(string assetPath = null)
        {
            string outputPath = Path.Combine(Application.streamingAssetsPath, AssetBundleConfig.AssetBundlesFolderName);
            if (!string.IsNullOrEmpty(assetPath))
            {
                outputPath = Path.Combine(outputPath, assetPath);
            }

            return outputPath;
        }

        public static string GetPersistentFilePath(string assetPath = null, bool wwwUrl = true)
        {
            if (wwwUrl)
            {
                return "file://" + GetPersistentDataPath(assetPath);
            }
            else
            {
                return GetPersistentDataPath(assetPath);
            }
        }

        public static string GetPersistentDataPath(string assetPath = null)
        {
            string outputPath = Path.Combine(Application.persistentDataPath, AssetBundleConfig.AssetBundlesFolderName);
            if (!string.IsNullOrEmpty(assetPath))
            {
                outputPath = Path.Combine(outputPath, assetPath);
            }
#if UNITY_EDITOR
            return GameUtility.FormatToSysFilePath(outputPath);
#else
            return outputPath;
#endif
        }

        public static bool CheckPersistentFileExsits(string filePath)
        {
            var path = GetPersistentDataPath(filePath);
            return File.Exists(path);
        }

        // 注意：这个路径是给WWW读文件使用的url，如果要直接磁盘写persistentDataPath，使用GetPlatformPersistentDataPath
        public static string GetAssetBundleFileUrl(string filePath, bool wwwUrl = true)
        {
            if (CheckPersistentFileExsits(filePath))
            {
                return GetPersistentFilePath(filePath, wwwUrl);
            }
            else
            {
                return GetStreamingAssetsFilePath(filePath, wwwUrl);
            }
        }

        public static string AssetBundlePathToAssetBundleName(string assetPath)
        {
            if (!string.IsNullOrEmpty(assetPath))
            {
                if (assetPath.StartsWith("Assets/"))
                {
                    assetPath = AssetsPathToPackagePath(assetPath);
                }

                //no " "
                assetPath = assetPath.Replace(" ", "");
                //there should not be any '.' in the assetbundle name
                //otherwise the variant handling in client may go wrong
                assetPath = assetPath.Replace(".", "_");
                //add after suffix ".assetbundle" to the end
                assetPath = assetPath + AssetBundleConfig.AssetBundleSuffix;
                return assetPath.ToLower();
            }

            return null;
        }

        public static string PackagePathToAssetsPath(string assetPath)
        {
            return AssetBundleConfig.AssetsFolderPath + assetPath;
        }

        public static bool IsPackagePath(string assetPath)
        {
            return assetPath.StartsWith(AssetBundleConfig.AssetsFolderPath);
        }

        public static string AssetsPathToPackagePath(string assetPath)
        {
            string path = AssetBundleConfig.AssetsFolderPath;
            if (assetPath.StartsWith(path))
            {
                return assetPath.Substring(path.Length);
            }
            else
            {
                Debug.LogError("Asset path is not a package path! " + assetPath);
                return assetPath;
            }
        }
    }
}