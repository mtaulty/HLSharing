#define MIKET_CHANGE
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;
using HoloToolkit.Unity.InputModule;

namespace HoloToolkit.Sharing.Tests
{
  /// <summary>
  /// Broadcasts the head transform of the local user to other users in the session,
  /// and adds and updates the head transforms of remote users.
  /// Head transforms are sent and received in the local coordinate space of the GameObject this component is on.
  /// </summary>
  public class RemoteHeadManager : Singleton<RemoteHeadManager>
  {
    public class RemoteHeadInfo
    {
      public long UserID;
      public GameObject HeadObject;
#if MIKET_CHANGE
      public GameObject BodyObject;
#endif     
    }

#if MIKET_CHANGE
    public GameObject remoteHeadPrefab;
    public GameObject remoteBodyPrefab;
#endif

    /// <summary>
    /// Keep a list of the remote heads, indexed by XTools userID
    /// </summary>
    private Dictionary<long, RemoteHeadInfo> remoteHeads = new Dictionary<long, RemoteHeadInfo>();

#if MIKET_CHANGE
    private void OnEnable()
    {
      this.roomId = -1;

      CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.HeadTransform] =
        UpdateHeadTransform;

      SharingStage.Instance.SessionUsersTracker.UserJoined += UserJoinedSession;
      SharingStage.Instance.SessionUsersTracker.UserLeft += UserLeftSession;
    }
#else
    private void Start()
    {
      CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.HeadTransform] = UpdateHeadTransform;

      // SharingStage should be valid at this point, but we may not be connected.
      if (SharingStage.Instance.IsConnected)
      {
        Connected();
      }
      else
      {
        SharingStage.Instance.SharingManagerConnected += Connected;
      }
    }
    private void Connected(object sender = null, EventArgs e = null)
    {
      SharingStage.Instance.SharingManagerConnected -= Connected;

      SharingStage.Instance.SessionUsersTracker.UserJoined += UserJoinedSession;
      SharingStage.Instance.SessionUsersTracker.UserLeft += UserLeftSession;
    }
#endif

    private void Update()
    {
#if MIKET_CHANGE
      this.DetermineCurrentRoom();
#endif
      // Grab the current head transform and broadcast it to all the other users in the session
      Transform headTransform = Camera.main.transform;

      // Transform the head position and rotation from world space into local space
      Vector3 headPosition = transform.InverseTransformPoint(headTransform.position);

      Quaternion headRotation = Quaternion.Inverse(transform.rotation) * headTransform.rotation;

#if MIKET_CHANGE
      CustomMessages.Instance.SendHeadTransform(headPosition, headRotation,
         this.roomId);
#endif
    }
#if MIKET_CHANGE
    void DetermineCurrentRoom()
    {
      if (this.roomId == -1)
      {
        var roomManager = SharingStage.Instance.Manager.GetRoomManager();

        if (roomManager != null)
        {
          var room = roomManager.GetCurrentRoom();
          this.roomId = room.GetID();
        }
      }
    }
#endif

    protected override void OnDestroy()
    {
      if (SharingStage.Instance != null)
      {
        if (SharingStage.Instance.SessionUsersTracker != null)
        {
          SharingStage.Instance.SessionUsersTracker.UserJoined -= UserJoinedSession;
          SharingStage.Instance.SessionUsersTracker.UserLeft -= UserLeftSession;
        }
      }

      base.OnDestroy();
    }

    /// <summary>
    /// Called when a new user is leaving the current session.
    /// </summary>
    /// <param name="user">User that left the current session.</param>
    private void UserLeftSession(User user)
    {
      int userId = user.GetID();
      if (userId != SharingStage.Instance.Manager.GetLocalUser().GetID())
      {
        RemoveRemoteHead(remoteHeads[userId].HeadObject);
        remoteHeads.Remove(userId);
      }
    }

    /// <summary>
    /// Called when a user is joining the current session.
    /// </summary>
    /// <param name="user">User that joined the current session.</param>
    private void UserJoinedSession(User user)
    {
      if (user.GetID() != SharingStage.Instance.Manager.GetLocalUser().GetID())
      {
        GetRemoteHeadInfo(user.GetID());
      }
    }

    /// <summary>
    /// Gets the data structure for the remote users' head position.
    /// </summary>
    /// <param name="userId">User ID for which the remote head info should be obtained.</param>
    /// <returns>RemoteHeadInfo for the specified user.</returns>
    public RemoteHeadInfo GetRemoteHeadInfo(long userId)
    {
      RemoteHeadInfo headInfo;

      // Get the head info if its already in the list, otherwise add it
      if (!remoteHeads.TryGetValue(userId, out headInfo))
      {
        headInfo = new RemoteHeadInfo();
        headInfo.UserID = userId;
        headInfo.HeadObject = CreateRemoteHead();

#if MIKET_CHANGE
        headInfo.BodyObject = Instantiate(this.remoteBodyPrefab);
        headInfo.BodyObject.transform.parent = this.gameObject.transform;
#endif
        remoteHeads.Add(userId, headInfo);
      }

      return headInfo;
    }

    /// <summary>
    /// Called when a remote user sends a head transform.
    /// </summary>
    /// <param name="msg"></param>
    private void UpdateHeadTransform(NetworkInMessage msg)
    {
      // Parse the message
      long userID = msg.ReadInt64();

      Vector3 headPos = CustomMessages.Instance.ReadVector3(msg);

      Quaternion headRot = CustomMessages.Instance.ReadQuaternion(msg);

#if MIKET_CHANGE
      long remoteRoomId = msg.ReadInt64();
#endif

      RemoteHeadInfo headInfo = GetRemoteHeadInfo(userID);
      headInfo.HeadObject.transform.localPosition = headPos;
      headInfo.HeadObject.transform.localRotation = headRot;

#if MIKET_CHANGE
      var rayLength = maxRayDistance;

      RaycastHit hitInfo;

      if (Physics.Raycast(
        headInfo.HeadObject.transform.position,
        headInfo.HeadObject.transform.forward,
        out hitInfo))
      {
        rayLength = hitInfo.distance;
      }
      var lineRenderer = headInfo.HeadObject.GetComponent<LineRenderer>();
      lineRenderer.SetPosition(1, Vector3.forward * rayLength);

      if ((remoteRoomId == -1) || (this.roomId == -1) ||
        (remoteRoomId != this.roomId))
      {
        headInfo.BodyObject.SetActive(true);
        headInfo.BodyObject.transform.localPosition = headPos;
        headInfo.BodyObject.transform.localRotation = headRot;
      }
      else
      {
        headInfo.BodyObject.SetActive(false);
      }
#endif
    }

    /// <summary>
    /// Creates a new game object to represent the user's head.
    /// </summary>
    /// <returns></returns>
    private GameObject CreateRemoteHead()
    {
      GameObject newHeadObj = Instantiate(this.remoteHeadPrefab);
      newHeadObj.transform.parent = gameObject.transform;

#if MIKET_CHANGE
      this.AddLineRenderer(newHeadObj);
#endif
      return newHeadObj;
    }
#if MIKET_CHANGE
    void AddLineRenderer(GameObject headObject)
    {
      var lineRenderer = headObject.AddComponent<LineRenderer>();
      lineRenderer.useWorldSpace = false;
      lineRenderer.startWidth = 0.01f;
      lineRenderer.endWidth = 0.05f;
      lineRenderer.numPositions = 2;
      lineRenderer.SetPosition(0, Vector3.forward * 0.1f);
      var material = new Material(Shader.Find("Diffuse"));
      material.color = colors[this.colorIndex++ % colors.Length];

      lineRenderer.material = material;
    }
#endif

    /// <summary>
    /// When a user has left the session this will cleanup their
    /// head data.
    /// </summary>
    /// <param name="remoteHeadObject"></param>
    private void RemoveRemoteHead(GameObject remoteHeadObject)
    {
      DestroyImmediate(remoteHeadObject);
    }
#if MIKET_CHANGE
    long roomId;
    const float maxRayDistance = 5.0f;
    int colorIndex;
    static Color[] colors =
    {
      Color.red,
      Color.green,
      Color.blue,
      Color.cyan,
      Color.magenta,
      Color.yellow
    };
#endif
  }
}