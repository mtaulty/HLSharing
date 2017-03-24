using HoloToolkit.Sharing;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;
using HoloToolkit.Sharing.Tests;

#if UNITY_UWP && !UNITY_EDITOR
using Windows.Networking.Connectivity;
#endif

public class Coordinator : MonoBehaviour
{
  public GameObject modelParent;

  enum CurrentStatus
  {
    WaitingToConnectToStage,
    WaitingForRoomApiToStabilise,
    WaitingForModelPositioning,
    WaitingForWorldAnchorExport,
    WaitingForWorldAnchorImport
  }
  void Start()
  {
    StatusTextDisplay.Instance.SetStatusText("connecting network");

    this.modelParent.SetActive(false);
  }
  void Update()
  {
    if (SharingStage.Instance.IsConnected)
    {
      switch (this.currentStatus)
      {
        case CurrentStatus.WaitingToConnectToStage:

          Debug.Log("Coordinator: moving to connection stage");
          StatusTextDisplay.Instance.SetStatusText("network connected");

          this.GetWiFiNetworkName();
          this.roomApiStartTime = DateTime.Now;
          this.currentStatus = CurrentStatus.WaitingForRoomApiToStabilise;
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

    if (this.currentStatus == CurrentStatus.WaitingForWorldAnchorImport)
    {
      // TBD: we're done importing the world anchor.
    }
    else
    {
      // TBD: we're done exporting the world anchor.
    }
    // Switch on the remote head management.
    this.modelParent.GetComponent<RemoteHeadManager>().enabled = true;
  }
  void GetWiFiNetworkName()
  {
    if (this.wifiName == null)
    {
      var name = string.Empty;

#if UNITY_UWP && !UNITY_EDITOR
      var interfaces = NetworkInformation.GetConnectionProfiles();

      var wifi = interfaces.Where(
        i => (i.GetNetworkConnectivityLevel() != NetworkConnectivityLevel.None) &&
             (i.IsWlanConnectionProfile)).FirstOrDefault();

      this.wifiName = wifi.WlanConnectionProfileDetails.GetConnectedSsid();
#endif
    }
  }
  void MoveToStatus(CurrentStatus newStatus)
  {
    // This is here to add logging etc. at a later point...
    this.currentStatus = newStatus;
  }
  string wifiName;
  Room currentRoom;
  CurrentStatus currentStatus;
  DateTime roomApiStartTime;
  static readonly TimeSpan ROOM_API_STABILISATION_TIME = TimeSpan.FromSeconds(3);
}