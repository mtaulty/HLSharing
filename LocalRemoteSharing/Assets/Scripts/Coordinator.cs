using HoloToolkit.Sharing;
using System.Linq;
using UnityEngine;
using System;
using HoloToolkit.Sharing.Tests;
using HoloToolkit.Unity;
using HoloToolkit.Unity.InputModule;
using HoloToolkit.Sharing.SyncModel;

#if UNITY_UWP && !UNITY_EDITOR
using Windows.Networking.Connectivity;
#endif

public class Coordinator : MonoBehaviour
{
  public GameObject modelParent;
  public bool ignoreWifiNetworkName;

  enum CurrentStatus
  {
    Start,
    WaitingForModelToLoad,
    WaitingToConnectToStage,
    WaitingForRoomApiToStabilise,
    WaitingForModelPositioning,
    WaitingForWorldAnchorExport,
    WaitingForAnchorOnServer,
    WaitingForWorldAnchorImport,
    Running
  }
  public void OnSplit()
  {
    if (this.currentStatus == CurrentStatus.Running)
    {
      this.GetComponent<KeywordManager>().enabled = false;

      // Take off the collider on the top level object, leaving its children directly
      // exposed to 'collisions'.
      this.model.GetComponent<Collider>().enabled = false;

      StatusTextDisplay.Instance.SetStatusText(string.Empty);
    }
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

        StatusTextDisplay.Instance.SetStatusText("loading model from web server");

        this.GetComponent<BundleDownloader>().Downloaded += this.OnModelDownloaded;
        this.GetComponent<BundleDownloader>().StartAsyncDownload();
        break;
      case CurrentStatus.WaitingToConnectToStage:

        if (SharingStage.Instance.IsConnected)
        {
          StatusTextDisplay.Instance.SetStatusText("network connected");

          this.GetWiFiNetworkName();
          this.roomApiStartTime = DateTime.Now;
          StatusTextDisplay.Instance.SetStatusText("looking for other users");

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
      case CurrentStatus.WaitingForAnchorOnServer:
        if (SharingStage.Instance.Root.Model.AnchorEstablished.Value)
        {
          var manager = this.modelParent.AddComponent<ImportAnchorManager>() as ICompleted;
          manager.Completed += this.OnImportOrExportCompleted;

          StatusTextDisplay.Instance.SetStatusText("synchronising...");
          this.MoveToStatus(CurrentStatus.WaitingForWorldAnchorImport);
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
    this.model.SetActive(false);

    // Move the world locked parent so that it's in a 'reasonable'
    // place to start with
    this.modelParent.transform.SetPositionAndRotation(
      WORLD_LOCKED_STARTING_POSITION, Quaternion.identity);

    Debug.Log(
      string.Format(
        "Coordinator: waiting for network connection",
        e.DownloadSucceeded ? "succeeded" : "failed or wasn't tried"));

    StatusTextDisplay.Instance.SetStatusText("connecting to server");

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

    SharingStage.Instance.Root.Model.IsRoomOwner = (this.currentRoom == null);

    if (SharingStage.Instance.Root.Model.IsRoomOwner)
    {
      StatusTextDisplay.Instance.SetStatusText("setting up a new room");

      // Note - last false parameter kills the room when people have all left.
      this.currentRoom = roomManager.CreateRoom(new XString(wifiName), 
        roomCount + 1, false);

      Debug.Log("Coordinator: created a new room for this WiFi network");

      this.modelParent.GetComponent<UserMoveable>().enabled = true;

      this.MoveToStatus(CurrentStatus.WaitingForModelPositioning);
      StatusTextDisplay.Instance.SetStatusText("waiting for you to position the model");

      Debug.Log("Coordinator: waiting for user to position model");
      this.modelParent.GetComponent<UserMoveable>().Locked += OnPositionLocked;

      this.model.SetActive(true);
    }
    else
    {
      StatusTextDisplay.Instance.SetStatusText("joining an existing room");

      roomManager.JoinRoom(this.currentRoom);

      this.MoveToStatus(CurrentStatus.WaitingForAnchorOnServer);

      StatusTextDisplay.Instance.SetStatusText("waiting for other user to set up room");

      Debug.Log("Coordinator: joining an existing room for this WiFi network");
    }
  }
  void OnPositionLocked(object sender, EventArgs e)
  {
    Debug.Log("Coordinator: position has been locked by user");
    this.modelParent.GetComponent<UserMoveable>().Locked -= OnPositionLocked;

    StatusTextDisplay.Instance.SetStatusText("synchronising...");

    var manager = this.modelParent.AddComponent<ExportAnchorManager>() as ICompleted;
    manager.Completed += OnImportOrExportCompleted;

    this.MoveToStatus(CurrentStatus.WaitingForWorldAnchorExport);
  }
  void OnImportOrExportCompleted(bool succeeded)
  {
    var dataModel = SharingStage.Instance.Root.Model;

    StatusTextDisplay.Instance.SetStatusText(
      string.Format("room import/export {0}", succeeded ? "succeeded" : "failed"));

    if (succeeded)
    {
      SharingStage.Instance.Root.Model.AnchorEstablished.Value = true;

      if (!dataModel.IsRoomOwner)
      {
        // Time to switch on the model if we aren't the user who positioned it
        // originally.
        this.model.SetActive(true);
      }
      StatusTextDisplay.Instance.SetStatusText("room synchronised");

      // First, make sure the model itself is set up to be moveable in a trackable way.
      // For the room owner, we will copy across the current transform values whereas
      // for non-room owners we'll just let them sync.
      this.SetUpSyncTransform(this.model,
        SharingStage.Instance.Root.Model.ParentTransform, dataModel.IsRoomOwner);

      // And all of its children are also moveable.
      var childCount = this.model.transform.childCount;

      for (int i = 0; i < childCount; i++)
      {
        var syncTransform =
          dataModel.IsRoomOwner ? 
            new SynchronisableTransform() : dataModel.ChildTransforms.GetDataArray()[i];

        var child = this.model.transform.GetChild(i);

        this.SetUpSyncTransform(
          child.gameObject, 
          syncTransform.Transform,
          dataModel.IsRoomOwner);

        if (dataModel.IsRoomOwner)
        {
          dataModel.ChildTransforms.AddObject(syncTransform);
        }
      }
      // Switch on the remote head management.
      this.modelParent.GetComponent<RemoteHeadManager>().enabled = true;

      // Only switching this on for the room owner at the moment to avoid
      // having to add more sync'd data model.
      if (dataModel.IsRoomOwner)
      {
        // Switch on the keyword recognizer listening for 'split'
        this.gameObject.GetComponent<KeywordManager>().StartKeywordRecognizer();

        StatusTextDisplay.Instance.SetStatusText(
          "'split' will treat the model pieces separately");
      }
    }
    else
    {
      StatusTextDisplay.Instance.SetStatusText("room sync failed - given up, sorry!");
    }
    this.MoveToStatus(CurrentStatus.Running);
  }
  void SetUpSyncTransform(GameObject gameObject, SyncTransform syncTransform,
    bool initialiseValues)
  {
    // Initialise scale, position, rotation (NB: scale doesn't change in this
    // code base right now).
    if (initialiseValues)
    {
      syncTransform.Scale.Value = gameObject.transform.localScale;
      syncTransform.Position.Value = gameObject.transform.localPosition;
      syncTransform.Rotation.Value = gameObject.transform.localRotation;
    }
    // Ensure that the object is moveable
    gameObject.EnsureComponent<UserMoveable>();

    // Ensure that the transforms synchronize themselves.
    var synchronizer = gameObject.EnsureComponent<TransformSynchronizer>();
    synchronizer.TransformDataModel = syncTransform;
  }
  void GetWiFiNetworkName()
  {
    if (this.wifiName == null)
    { 
#if UNITY_UWP && !UNITY_EDITOR
      if (!this.ignoreWifiNetworkName)
      {
        var interfaces = NetworkInformation.GetConnectionProfiles();

        var wifi = interfaces.Where(
          i => (i.GetNetworkConnectivityLevel() != NetworkConnectivityLevel.None) &&
               (i.IsWlanConnectionProfile)).FirstOrDefault();

        if (wifi != null)
        {
          this.wifiName = wifi.WlanConnectionProfileDetails.GetConnectedSsid();
        }
      }
#endif
      if (String.IsNullOrEmpty(this.wifiName))
      {
        this.wifiName = DEFAULT_WIFI_NAME;
      }
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