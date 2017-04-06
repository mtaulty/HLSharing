using AssetBundles;
using HoloToolkit.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BundleDownloadedEventArgs : EventArgs
{
  public bool DownloadSucceeded { get; set; }
}

public class BundleDownloader : Singleton<BundleDownloader>
{
  [SerializeField]
  string downloadUrl;

  [SerializeField]
  string bundleName;

  [SerializeField]
  string prefabName;

  [SerializeField]
  GameObject fallbackPrefab;

  [SerializeField]
  bool isActive = true;

  public GameObject LoadedPrefab
  {
    get; set;
  }

  public event EventHandler<BundleDownloadedEventArgs> Downloaded;

  public void StartAsyncDownload()
  {
    StartCoroutine(this.DownloadAsync());
  }
  IEnumerator DownloadAsync()
  {
    var prefabObject = this.fallbackPrefab;
    var succeeded = false;

#if !UNITY_EDITOR

    if (this.isActive &&
      !string.IsNullOrEmpty(this.downloadUrl) &&
      !string.IsNullOrEmpty(this.bundleName) &&
      !string.IsNullOrEmpty(this.prefabName))
    {
      AssetBundleManager.SetSourceAssetBundleURL(this.downloadUrl);

      var initializeOperation = AssetBundleManager.Initialize();

      if (initializeOperation != null)
      {
        yield return StartCoroutine(initializeOperation);

        AssetBundleLoadAssetOperation loadOperation = null;

        try
        {
          loadOperation = AssetBundleManager.LoadAssetAsync(
            this.bundleName, this.prefabName, typeof(GameObject));
        }
        catch
        {

        }
        if (loadOperation != null)
        {
          yield return StartCoroutine(loadOperation);

          var loadedPrefab = loadOperation.GetAsset<GameObject>();

          if (loadedPrefab != null)
          {
            prefabObject = loadedPrefab;
            succeeded = true;
          }
        }
      }
    }
#else
    succeeded = true;
#endif

    this.LoadedPrefab = prefabObject;

    if (this.Downloaded != null)
    {
      this.Downloaded(
        this, new global::BundleDownloadedEventArgs()
        {
          DownloadSucceeded = succeeded
        }
      );
    }
    yield break;
  }
}