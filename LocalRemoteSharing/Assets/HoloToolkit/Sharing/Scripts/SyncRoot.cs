#define MIKET_CHANGE
//
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
//

using HoloToolkit.Sharing.Spawning;
using HoloToolkit.Sharing.SyncModel;

namespace HoloToolkit.Sharing
{
  /// <summary>
  /// Root of the synchronization data model used by this application.
  /// </summary>
  public class SyncRoot : SyncObject
  {
#if MIKET_CHANGE
    [SyncData]
    public SyncArray<SyncSpawnedObject> ModelObjects;

    [SyncData]
    public GlobalDataModel Model = new GlobalDataModel("Model");

#endif

    /// <summary>
    /// Children of the root.
    /// </summary>
    [SyncData]
    public SyncArray<SyncSpawnedObject> InstantiatedPrefabs;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="rootElement">Root Element from Sharing Stage</param>
    public SyncRoot(ObjectElement rootElement)
    {
      Element = rootElement;
      FieldName = Element.GetName().GetString();
      InitializeSyncSettings();
      InitializeDataModel();
    }

    private void InitializeSyncSettings()
    {
      SyncSettings.Instance.Initialize();
    }

    /// <summary>
    /// Initializes any data models that need to have a local state.
    /// </summary>
    private void InitializeDataModel()
    {
      InstantiatedPrefabs.InitializeLocal(Element);

#if MIKET_CHANGE
      this.ModelObjects.InitializeLocal(Element);
      this.Model.InitializeLocal(Element);
#endif
    }
  }
}
