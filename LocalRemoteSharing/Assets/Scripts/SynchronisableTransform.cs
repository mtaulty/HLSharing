using HoloToolkit.Sharing.SyncModel;

/// <summary>
/// This only exists because I don't *think* I can simply have a SyncArray<SyncTransform>
/// whereas I do seem to be able to have a SyncArray of a class which simply contains
/// a SyncTransform.
/// </summary>
[SyncDataClass]
public class SynchronisableTransform : SyncObject
{
  [SyncData]
  public SyncTransform Transform = new SyncTransform("Transform");
}