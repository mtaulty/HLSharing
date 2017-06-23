// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using UnityEngine;
using HoloToolkit.Unity;
using HoloToolkit.Sharing;

#if UNITY_WSA && !UNITY_EDITOR
using UnityEngine.VR.WSA;
using UnityEngine.VR.WSA.Sharing;
#endif

public class ImportAnchorManager : AnchorManager<ImportAnchorManager>
{
  enum ImportState
  {
    Start,
    Failed,
    ReadyToImport,
    DataRequested,
    DataDownloadedReadyForImport,
    Importing,
    AnchorImportedAndLocked
  }

  ImportState currentState = ImportState.Start;

  byte[] rawAnchorData;

#if UNITY_WSA && !UNITY_EDITOR

  WorldAnchor worldAnchor;

#endif

  void Update()
  {
#if UNITY_WSA && !UNITY_EDITOR
    if (SharingStage.Instance.IsConnected)
    {
      switch (currentState)
      {
        case ImportState.Start:
          ConnectToRoom();
          this.currentState = ImportState.ReadyToImport;
          break;
        case ImportState.ReadyToImport:
          MakeAnchorDataRequest();
          break;
        case ImportState.DataDownloadedReadyForImport:
          // DataReady is set when the anchor download completes.
          currentState = ImportState.Importing;
          StatusTextDisplay.Instance.SetStatusText("importing synchronisation data");

          WorldAnchorTransferBatch.ImportAsync(rawAnchorData, ImportComplete);
          break;
      }
    }
#endif
  }
  protected override void AddRoomManagerHandlers()
  {
    base.AddRoomManagerHandlers();
    this.roomManagerListener.AnchorsDownloadedEvent += this.OnAnchorDonwloadCompleted;
  }

  protected override void OnDestroy()
  {
    if (roomManagerListener != null)
    {
      roomManagerListener.AnchorsDownloadedEvent -= OnAnchorDonwloadCompleted;
    }
    base.OnDestroy();
  }
  void OnAnchorDonwloadCompleted(
    bool successful,
    AnchorDownloadRequest request,
    XString failureReason)
  {
    // If we downloaded anchor data successfully we should import the data.
    if (successful)
    {
      StatusTextDisplay.Instance.SetStatusText(
        "synchronisation data downloaded");

      int datasize = request.GetDataSize();

      if (SharingStage.Instance.ShowDetailedLogs)
      {
        Debug.LogFormat("Anchor Manager: Anchor size: {0} bytes.", datasize.ToString());
      }

      rawAnchorData = new byte[datasize];

      request.GetData(rawAnchorData, datasize);

      currentState = ImportState.DataDownloadedReadyForImport;
    }
    else
    {
      StatusTextDisplay.Instance.SetStatusText(
        "retrying synchronisation request");

      // If we failed, we can ask for the data again.
      Debug.LogWarning("Anchor Manager: Anchor DL failed " + failureReason);

#if UNITY_WSA && !UNITY_EDITOR
      MakeAnchorDataRequest();
#endif
    }
  }

#if UNITY_WSA && !UNITY_EDITOR

  void MakeAnchorDataRequest()
  {
    StatusTextDisplay.Instance.SetStatusText("requesting data");

    if (roomManager.DownloadAnchor(currentRoom, currentRoom.GetAnchorName(0)))
    {
      currentState = ImportState.DataRequested;
    }
    else
    {
      Debug.LogError("Anchor Manager: Couldn't make the download request.");

      currentState = ImportState.Failed;
    }
  }
  void ImportComplete(SerializationCompletionReason status, WorldAnchorTransferBatch anchorBatch)
  {
    if (status == SerializationCompletionReason.Succeeded)
    {
      if (anchorBatch.GetAllIds().Length > 0)
      {
        string first = anchorBatch.GetAllIds()[0];

        if (SharingStage.Instance.ShowDetailedLogs)
        {
          Debug.Log("Anchor Manager: Sucessfully imported anchor " + first);
        }
        this.worldAnchor = anchorBatch.LockObject(first, gameObject);

        StatusTextDisplay.Instance.SetStatusText("synchronised");
      }

      base.FireCompleted(true);
    }
    else
    {
      StatusTextDisplay.Instance.SetStatusText("retrying synchronisation");

      Debug.LogError("Anchor Manager: Import failed");

      currentState = ImportState.DataDownloadedReadyForImport;
    }
  }
#endif // UNITY_WSA
}
