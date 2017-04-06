using HoloToolkit.Sharing;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;
using HoloToolkit.Sharing.Tests;
using HoloToolkit.Sharing.Spawning;
using HoloToolkit.Unity;

#if UNITY_UWP && !UNITY_EDITOR
using Windows.Networking.Connectivity;
#endif

public class Coordinator : MonoBehaviour
{
  public GameObject modelParent;

  enum CurrentStatus
  {
    Start,
    WaitingForModelToLoad,
    WaitingToConnectToStage,
    WaitingForRoomApiToStabilise,
    WaitingForModelPositioning,
    WaitingForWorldAnchorExport,
    WaitingForWorldAnchorImport
  }
  void Start()
  {
    StatusTextDisplay.Instance.SetStatusText("waiting for model to load");
  }
  void Update()
  {
    switch (this.currentStatus)
    {
      case CurrentStatus.Start:
        this.MoveToStatus(CurrentStatus.WaitingForModelToLoad);

        Debug.Log("Coordinator: starting to load model from web server");
        StatusTextDisplay.Instance.SetStatusText("loading model from web server");

        this.GetComponent<BundleDownloader>().Downloaded += this.OnModelDownloaded;
        this.GetComponent<BundleDownloader>().StartAsyncDownload();
        break;
      case CurrentStatus.WaitingToConnectToStage:

        if (SharingStage.Instance.IsConnected)
        {
          Debug.Log("Coordinator: moving to connection stage");
          StatusTextDisplay.Instance.SetStatusText("network connected");

          this.GetWiFiNetworkName();
          this.roomApiStartTime = DateTime.Now;
          this.MoveToStatus(CurrentStatus.WaitingForRoomApiToStabilise);
        }
        break;
      case CurrentStatus.WaitingForRoomApiToStabilise:

        // Note - with a room created, I find that the room API can return 0 rooms
        // and yet call it just one frame later and it changes it mind. Hence...
        // here we give it a little time.
        var doneWaitingForRoomApi = this.WaitForRoomCountToStabilise(ROOM_API_STABILISATION_TIME);

        if (doneWaitingForRoomApi)
        {
          this.CreateOrJoinRoomBasedonWifiNetworkName();
        }
        break;
      default:
        break;
    }
  }
  void OnModelDownloaded(object sender, BundleDownloadedEventArgs e)
  {
    var bundleDownloader = this.GetComponent<BundleDownloader>();

    bundleDownloader.Downloaded -= this.OnModelDownloaded;

    Debug.Log(
      string.Format(
        "Coordinator: download of model from web server has completed and {0}",
        e.DownloadSucceeded ? "succeeded" : "failed or wasn't tried"));

    StatusTextDisplay.Instance.SetStatusText(
      string.Format(
        "{0} model from web server",
        e.DownloadSucceeded ? "loaded" : "failed to load"));

    // Create the model and parent it off this object.
    this.model = Instantiate(bundleDownloader.LoadedPrefab);
    this.model.transform.parent = this.modelParent.transform;

    Debug.Log(
      string.Format(
        "Coordinator: waiting for network connection",
        e.DownloadSucceeded ? "succeeded" : "failed or wasn't tried"));

    StatusTextDisplay.Instance.SetStatusText("connecting to room server");

    this.MoveToStatus(CurrentStatus.WaitingToConnectToStage);
  }
  bool WaitForRoomCountToStabilise(TimeSpan timeSpan)
  {
    bool expired = false;

    if (this.roomApiStartTime == null)
    {
      this.roomApiStartTime = DateTime.Now;
    }
    if (DateTime.Now - this.roomApiStartTime > timeSpan)
    {
      expired = true;
    }
    else
    {
      var roomManager = SharingStage.Instance.Manager.GetRoomManager();
      expired = (roomManager.GetRoomCount() > 0);
    }
    return (expired);
  }
  void CreateOrJoinRoomBasedonWifiNetworkName()
  {
    StatusTextDisplay.Instance.SetStatusText(
      string.Format("using wifi name of {0}", wifiName));

    Debug.Log(String.Format("Coordinator: identified WiFi as {0}", wifiName));

    var roomManager = SharingStage.Instance.Manager.GetRoomManager();
    this.currentRoom = null;
    var roomCount = roomManager.GetRoomCount();

    Debug.Log(String.Format("Coordinator: discovered {0} rooms", roomCount));

    for (int i = 0; i < roomCount; i++)
    {
      var room = roomManager.GetRoom(i);

      if (room.GetName() == wifiName)
      {
        this.currentRoom = room;
        break;
      }
    }

    if (this.currentRoom == null)
    {
      StatusTextDisplay.Instance.SetStatusText("setting up new room");

      this.currentRoom = roomManager.CreateRoom(new XString(wifiName), roomCount + 1, true);
      Debug.Log("Coordinator: created a new room for this WiFi network");

      this.modelParent.GetComponent<UserMoveable>().enabled = true;

      this.MoveToStatus(CurrentStatus.WaitingForModelPositioning);
      StatusTextDisplay.Instance.SetStatusText("waiting for user to position model");

      Debug.Log("Coordinator: waiting for user to position model");
      this.modelParent.GetComponent<UserMoveable>().Locked += OnPositionLocked;
    }
    else
    {
      StatusTextDisplay.Instance.SetStatusText("joining existing room");

      roomManager.JoinRoom(this.currentRoom);

      var manager = this.modelParent.AddComponent<ImportAnchorManager>() as ICompleted;
      manager.Completed += this.OnImportOrExportCompleted;

      this.MoveToStatus(CurrentStatus.WaitingForWorldAnchorImport);

      StatusTextDisplay.Instance.SetStatusText("waiting for room sync");

      Debug.Log("Coordinator: joined an existing room for this WiFi network");
    }
    this.modelParent.SetActive(true);
  }
  void OnPositionLocked(object sender, EventArgs e)
  {
    Debug.Log("Coordinator: position has been locked by user");
    this.modelParent.GetComponent<UserMoveable>().Locked -= OnPositionLocked;

    StatusTextDisplay.Instance.SetStatusText("creating room sync");

    var manager = this.modelParent.AddComponent<ExportAnchorManager>() as ICompleted;
    manager.Completed += OnImportOrExportCompleted;

    this.MoveToStatus(CurrentStatus.WaitingForWorldAnchorExport);
  }
  void OnImportOrExportCompleted(bool succeeded)
  {
    StatusTextDisplay.Instance.SetStatusText("room in sync");

    // Allow the child to be moved now that it is positioned in
    // the right place.
    this.model.AddComponent<UserMoveable>();

    var dataModel = SharingStage.Instance.Root.ModelObject;
    dataModel.GameObject = this.model.gameObject;
    dataModel.Initialize(this.model.gameObject.name, this.model.transform.GetFullPath());
    dataModel.Transform.Position.Value = this.model.transform.localPosition;
    dataModel.Transform.Rotation.Value = this.model.transform.localRotation;
    dataModel.Transform.Scale.Value = this.model.transform.localScale;

    var synchronizer = this.model.EnsureComponent<TransformSynchronizer>();
    synchronizer.TransformDataModel = dataModel.Transform;

    // Switch on the remote head management.
    this.modelParent.GetComponent<RemoteHeadManager>().enabled = true;
  }
  void GetWiFiNetworkName()
  {
    if (this.wifiName == null)
    {
#if UNITY_UWP && !UNITY_EDITOR
      var interfaces = NetworkInformation.GetConnectionProfiles();

      var wifi = interfaces.Where(
        i => (i.GetNetworkConnectivityLevel() != NetworkConnectivityLevel.None) &&
             (i.IsWlanConnectionProfile)).FirstOrDefault();

      if (wifi != null)
      {
        this.wifiName = wifi.WlanConnectionProfileDetails.GetConnectedSsid();
      }
      else
      {
        this.wifiName = DEFAULT_WIFI_NAME;
      }
#endif
    }
  }
  void MoveToStatus(CurrentStatus newStatus)
  {
    // This is here to add logging etc. at a later point...
    this.currentStatus = newStatus;
  }
  GameObject model;
  string wifiName;
  Room currentRoom;
  CurrentStatus currentStatus;
  DateTime roomApiStartTime;
  static readonly string DEFAULT_WIFI_NAME = "DefaultWifi";
  static readonly TimeSpan ROOM_API_STABILISATION_TIME = TimeSpan.FromSeconds(3);
}