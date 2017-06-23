// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;
using HoloToolkit.Sharing;

#if UNITY_WSA && !UNITY_EDITOR
using UnityEngine.VR.WSA;
using UnityEngine.VR.WSA.Sharing;
#endif

public class ExportAnchorManager : AnchorManager<ExportAnchorManager>
{
  /// <summary>
  /// The anchor this object is attached to.
  /// </summary>
  /// 
#if UNITY_WSA && !UNITY_EDITOR
  WorldAnchor worldAnchor;
#endif // UNITY_WSA

  ExportState currentState = ExportState.Start;

  /// <summary>
  /// Keeps track of the name of the anchor we are exporting.
  /// </summary>
  string exportingAnchorName;

  /// <summary>
  /// The datablob of the anchor.
  /// </summary>
  List<byte> exportingAnchorBytes = new List<byte>();

  /// <summary>
  /// WorldAnchorTransferBatch is the primary object in serializing/deserializing anchors.
  /// <remarks>Only available on device.</remarks>
  /// </summary>

#if UNITY_WSA && !UNITY_EDITOR
  WorldAnchorTransferBatch worldAnchorTransferBatch;
#endif // UNITY_WSA

  /// <summary>
  /// Sometimes we'll see a really small anchor blob get generated.
  /// These tend to not work, so we have a minimum trustable size.
  /// </summary>
  const uint MinTrustworthySerializedAnchorDataSize = 100 * 1024;

  enum ExportState
  {
    // Overall states
    Start,
    WaitingForAnchorLocation,
    ExportingAnchor,
    Failed,
    AnchorUploaded
  }

  void Update()
  {
    if (SharingStage.Instance.IsConnected)
    {
      switch (this.currentState)
      {
        case ExportState.Start:
          this.currentState = ExportState.WaitingForAnchorLocation;
          this.ConnectToRoom();

#if UNITY_WSA && !UNITY_EDITOR
          this.worldAnchor = GetComponent<WorldAnchor>() ?? gameObject.AddComponent<WorldAnchor>();
          StatusTextDisplay.Instance.SetStatusText("waiting for position data");
#endif
          break;
        case ExportState.WaitingForAnchorLocation:
#if UNITY_WSA && !UNITY_EDITOR
          if (this.worldAnchor.isLocated)
          {
            this.currentState = ExportState.ExportingAnchor;
            StatusTextDisplay.Instance.SetStatusText("synchronising...");
            this.ExportWorldAnchor();
          }
#endif // UNITY_WSA
          break;
        default:
          break;
      }
    }
  }
  protected override void AddRoomManagerHandlers()
  {
    base.AddRoomManagerHandlers();
    roomManagerListener.AnchorUploadedEvent += this.OnAnchorUploadCompleted;
  }
  void ExportWorldAnchor()
  {
    string guidString = Guid.NewGuid().ToString();

    exportingAnchorName = guidString;

    // Save the anchor to our local anchor store.
    if (SharingStage.Instance.ShowDetailedLogs)
    {
      Debug.Log("Anchor Manager: Exporting anchor " + exportingAnchorName);
    }

#if UNITY_WSA && !UNITY_EDITOR

    worldAnchorTransferBatch = new WorldAnchorTransferBatch();
    worldAnchorTransferBatch.AddWorldAnchor(guidString, worldAnchor);
    WorldAnchorTransferBatch.ExportAsync(worldAnchorTransferBatch, WriteBuffer, ExportComplete);

#endif // UNITY_WSA
  }
  void WriteBuffer(byte[] data)
  {
    exportingAnchorBytes.AddRange(data);
  }
  protected override void OnDestroy()
  {
    if (roomManagerListener != null)
    {
      roomManagerListener.AnchorUploadedEvent -= this.OnAnchorUploadCompleted;
    }
    base.OnDestroy();
  }
  void OnAnchorUploadCompleted(bool successful, XString failureReason)
  {
    if (successful)
    {
      StatusTextDisplay.Instance.SetStatusText("synchronised");

      if (SharingStage.Instance.ShowDetailedLogs)
      {
        Debug.Log("Anchor Manager: Sucessfully uploaded anchor");
      }
      currentState = ExportState.AnchorUploaded;
    }
    else
    {
      StatusTextDisplay.Instance.SetStatusText("sync data copy failed");

      Debug.LogError("Anchor Manager: Upload failed " + failureReason);
      currentState = ExportState.Failed;
    }
    base.FireCompleted(currentState == ExportState.AnchorUploaded);
  }
#if UNITY_WSA && !UNITY_EDITOR

  void ExportComplete(SerializationCompletionReason status)
  {
    if ((status == SerializationCompletionReason.Succeeded)
      && (exportingAnchorBytes.Count > MinTrustworthySerializedAnchorDataSize))
    {
      StatusTextDisplay.Instance.SetStatusText(
        string.Format(
          "copying {0:N2}MB data", (exportingAnchorBytes.Count / (1024 * 1024))));

      if (SharingStage.Instance.ShowDetailedLogs)
      {
        Debug.Log("Anchor Manager: Uploading anchor: " + exportingAnchorName);
      }

      roomManager.UploadAnchor(
          currentRoom,
          new XString(exportingAnchorName),
          exportingAnchorBytes.ToArray(),
          exportingAnchorBytes.Count);
    }
    else
    {
      StatusTextDisplay.Instance.SetStatusText("retrying export");

      Debug.LogWarning("Anchor Manager: Failed to upload anchor, trying again...");

      currentState = ExportState.WaitingForAnchorLocation;
    }
  }
#endif // UNITY_WSA
}