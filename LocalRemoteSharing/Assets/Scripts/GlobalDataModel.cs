using HoloToolkit.Sharing.SyncModel;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//[SyncDataClass]
//public class SynchronisableTransform : SyncObject
//{
//  [SyncData]
//  public SyncTransform Transform = new SyncTransform("Transform");
//}

public class GlobalDataModel : SyncObject
{
  public GlobalDataModel(string field) : base(field)
  {
  }
  // Note: this flag is not intended to be synchronised
  public bool IsRoomOwner { get; set; }

  [SyncData]
  public SyncBool AnchorEstablished = new SyncBool("AnchorEstablished");

#if zero
  [SyncData]
  public SyncTransform Transform = new SyncTransform("Transform");

  [SyncData]
  public SyncInteger CurrentState = new SyncInteger("CurrentState");

  [SyncData]
  public SyncArray<SynchronisableTransform> ViewTransforms = new SyncArray<SynchronisableTransform>("ViewTransforms");
#endif
}
