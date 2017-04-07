#define DEBUG_EMULATOR_SINGLE_ROOM_ONLY
using HoloToolkit.Sharing;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;
using HoloToolkit.Sharing.Tests;
using HoloToolkit.Sharing.Spawning;
using HoloToolkit.Unity;
using HoloToolkit.Unity.InputModule;

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
  public void OnSplit()
  {
    this.GetComponent<KeywordManager>().enabled = false;
    this.model.GetComponent<Collider>().enabled = false;
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

    // Move the world locked parent so that it's in a 'reasonable'
    // place to start with
    this.modelParent.transform.SetPositionAndRotation(
      WORLD_LOCKED_STARTING_POSITION, Quaternion.identity);

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
    StatusTextDisplay.Instance.SetStatusText(
      string.Format("room import/export {0}", succeeded ? "succeeded" : "failed"));

    if (succeeded)
    {
      var isExporter = this.currentStatus == CurrentStatus.WaitingForWorldAnchorExport;

      StatusTextDisplay.Instance.SetStatusText("room is now in sync");

      // First, make sure the model itself is set up to be moveable in a trackable way.
      // NB: We use index 0 for the model itself.
      this.MakeModelPartMoveableAndTrackable(this.model, 0, isExporter);

      // And all of its children are also moveable.
      var childCount = this.model.transform.childCount;

      // NB: 1 to N because the have the model in slot 0.
      for (int i = 1; i <= childCount; i++)
      {
        // NB: We add 1 because the model itself is in slot 1.
        var child = this.model.transform.GetChild(i - 1);
        this.MakeModelPartMoveableAndTrackable(child.gameObject, i, isExporter);
      }
      // Switch on the remote head management.
      this.modelParent.GetComponent<RemoteHeadManager>().enabled = true;

      // Switch on the keyword recognizer listening for 'join' and 'split'
      this.gameObject.GetComponent<KeywordManager>().StartKeywordRecognizer();
    }
  }
  SyncSpawnedObject MakeModelPartMoveableAndTrackable(
    GameObject objectInstance, int indexIntoRootSyncStore, bool isExporter)
  {
    SyncSpawnedObject dataModel = null;

    if (isExporter)
    {
      dataModel = new SyncSpawnedObject();
      dataModel.GameObject = objectInstance; ;
      dataModel.Initialize(objectInstance.name, objectInstance.transform.GetFullPath());
      dataModel.Transform.Position.Value = objectInstance.transform.localPosition;
      dataModel.Transform.Rotation.Value = objectInstance.transform.localRotation;
      dataModel.Transform.Scale.Value = objectInstance.transform.localScale;

      SharingStage.Instance.Root.ModelObjects.AddObject(dataModel);
    }
    else
    {
      dataModel = 
        SharingStage.Instance.Root.ModelObjects.GetDataArray()[indexIntoRootSyncStore];
    }
    objectInstance.EnsureComponent<UserMoveable>();
    var synchronizer = objectInstance.EnsureComponent<TransformSynchronizer>();
    synchronizer.TransformDataModel = dataModel.Transform;

    return (dataModel);
  }
  void GetWiFiNetworkName()
  {
    if (this.wifiName == null)
    {
#if DEBUG_EMULATOR_SINGLE_ROOM_ONLY
      this.wifiName = DEFAULT_WIFI_NAME;
#else
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

  static readonly Vector3 WORLD_LOCKED_STARTING_POSITION = new Vector3(0, 0, 3.0f);
  static readonly string DEFAULT_WIFI_NAME = "DefaultWifi";
  static readonly TimeSpan ROOM_API_STABILISATION_TIME = TimeSpan.FromSeconds(3);
}