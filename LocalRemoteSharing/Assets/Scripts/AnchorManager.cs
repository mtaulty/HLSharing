using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;
using HoloToolkit.Sharing;

#if UNITY_WSA && !UNITY_EDITOR
using HoloToolkit.Sharing;
#endif // UNITY_WSA

public interface ICompleted
{
  event Action<bool> Completed;
}
public class AnchorManager<T> : Singleton<T>, ICompleted where T : Singleton<T>
{
  public event Action<bool> Completed;

  /// <summary>
  /// The room manager API for the sharing service.
  /// </summary>
  protected RoomManager roomManager;

  /// <summary>
  /// Keeps track of the current room we are connected to.  Anchors
  /// are kept in rooms.
  /// </summary>
  protected Room currentRoom;

  /// <summary>
  /// Provides updates when anchor data is uploaded/downloaded.
  /// </summary>
  protected RoomManagerAdapter roomManagerListener;

  protected void FireCompleted(bool succeeded)
  {
    if (this.Completed != null)
    {
      this.Completed(succeeded);
    }
  }
  protected virtual void AddRoomManagerHandlers()
  {

  }
  protected void ConnectToRoom()
  {
    if (SharingStage.Instance.ShowDetailedLogs)
    {
      Debug.Log("Import Anchor Manager: Starting...");
    }

    // Setup the room manager callbacks.
    roomManager = SharingStage.Instance.Manager.GetRoomManager();
    roomManagerListener = new RoomManagerAdapter();
    this.AddRoomManagerHandlers();

    roomManager.AddListener(roomManagerListener);

    currentRoom = roomManager.GetCurrentRoom();
  }
  protected override void OnDestroy()
  {
    if (roomManagerListener != null)
    {
      if (roomManager != null)
      {
        roomManager.RemoveListener(roomManagerListener);
      }
      roomManagerListener.Dispose();
      roomManagerListener = null;
    }

    if (roomManager != null)
    {
      roomManager.Dispose();
      roomManager = null;
    }
    base.OnDestroy();
  }
}
