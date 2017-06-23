using HoloToolkit.Sharing.SyncModel;

public class GlobalDataModel : SyncObject
{
  public GlobalDataModel(string field) : base(field)
  {
  }
  // Note: this flag is not intended to be synchronised across device like the
  // other members are. It's just a piece of global state as it'll default to
  // false on all devices but the room owner (I hope).
  public bool IsRoomOwner { get; set; }

  [SyncData]
  public SyncBool AnchorEstablished = new SyncBool("AnchorEstablished");

  [SyncData]
  public SyncTransform ParentTransform = new SyncTransform("Transform");

  [SyncData]
  public SyncArray<SynchronisableTransform> ChildTransforms = 
    new SyncArray<SynchronisableTransform>("ChildTransforms");
}
